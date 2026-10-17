// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/tracing/__init__.py

using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nooa.Runtime;

namespace Nooa.Tracing;

/// <summary>
/// Turns tracing on and off. Spans come from the <see cref="ActivitySource"/> named <see cref="SourceName"/>, so an application
/// that uses the OpenTelemetry SDK can also listen to it with <c>AddSource("Nooa")</c> and skip the exporters here.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Enable(ISpanExporter[])"/> installs <see cref="OpenInferenceHooks"/> as <see cref="InstrumentationHooks.Current"/> and
/// starts an <see cref="ActivityListener"/> that scrubs each finished span and passes it to the exporters.
/// Deviation: it throws when other hooks are installed, because the C# hook slot has no composition.
/// The LiteLLM instrumentor, the journal exporters, Langfuse and Phoenix are not ported (T19 scope).
/// To trace model calls, wrap the client with <see cref="TracingLlmClient"/> or <see cref="TracingLlmClientResolver"/>.
/// </para>
/// </remarks>
public static class NooaTracing
{
    /// <summary>The name of the activity source.</summary>
    public const string SourceName = "Nooa";

    private static readonly Lock _gate = new();
    private static ActivityListener? _listener;
    private static OpenInferenceHooks? _hooks;
    private static ISpanExporter[] _exporters = [];
    private static Dictionary<string, object?> _resource = [];
    private static ILogger _logger = NullLogger.Instance;

    /// <summary>The activity source of all NOOA spans.</summary>
    public static ActivitySource Source { get; } = new(
        SourceName, typeof(NooaTracing).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);

    /// <summary>True between <see cref="Enable(ISpanExporter[])"/> and <see cref="Shutdown"/>.</summary>
    public static bool IsEnabled
    {
        get
        {
            lock (_gate)
            {
                return _listener is not null;
            }
        }
    }

    /// <summary>Turns tracing on with the default exporter, JSONL files in <c>TRACE_DIR</c> or <c>./traces</c>.</summary>
    public static void Enable() => Enable(new JsonlSpanExporter());

    /// <summary>
    /// Turns tracing on, or, when it is on, replaces the exporters. The old exporters are flushed and shut down.
    /// </summary>
    /// <param name="exporters">The destinations of the spans.</param>
    /// <exception cref="InvalidOperationException">Other instrumentation hooks are installed.</exception>
    public static void Enable(params ISpanExporter[] exporters) => Enable(exporters, null, null, null);

    /// <summary>Turns tracing on with the full options.</summary>
    /// <param name="exporters">The destinations of the spans.</param>
    /// <param name="experiment">An experiment name that groups traces. Null reads <c>TRACE_EXPERIMENT</c>.</param>
    /// <param name="extraResourceAttributes">More attributes for the resource. They are scrubbed like span attributes.</param>
    /// <param name="logger">Receives exporter failures. Null discards them.</param>
    public static void Enable(
        IEnumerable<ISpanExporter> exporters,
        string? experiment,
        IReadOnlyDictionary<string, object?>? extraResourceAttributes,
        ILogger? logger)
    {
        ArgumentNullException.ThrowIfNull(exporters);
        var next = exporters.ToArray();
        ISpanExporter[] previous;
        lock (_gate)
        {
            previous = _exporters;
            if (_listener is null)
            {
                if (InstrumentationHooks.Current is not null)
                {
                    throw new InvalidOperationException("Other instrumentation hooks are installed; tracing cannot compose with them.");
                }

                _hooks = new OpenInferenceHooks();
                _listener = new ActivityListener
                {
                    ShouldListenTo = source => source.Name == SourceName,
                    Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                    ActivityStopped = OnStopped,
                };
                ActivitySource.AddActivityListener(_listener);
                InstrumentationHooks.Current = _hooks;
                TraceSession.SetDefault(TraceSession.NewId());
            }

            _logger = logger ?? NullLogger.Instance;
            _resource = BuildResource(experiment ?? Environment.GetEnvironmentVariable("TRACE_EXPERIMENT"), extraResourceAttributes);
            _exporters = next;
        }

        foreach (var old in previous)
        {
            ShutdownQuietly(old);
        }

        Console.WriteLine($"OTel tracing enabled: {string.Join(", ", next.Select(e => e.Describe()))}");
    }

    /// <summary>Waits until the exporters have delivered every span that finished so far. Returns false on timeout.</summary>
    public static bool Flush(TimeSpan? timeout = null)
    {
        ISpanExporter[] exporters;
        lock (_gate)
        {
            exporters = _exporters;
        }

        return exporters.All(e => e.Flush(timeout ?? TimeSpan.FromSeconds(30)));
    }

    /// <summary>Ends every span that is still open, with an error status. Returns how many it ended.</summary>
    public static int EndActiveSpans(string reason = "timeout") => _hooks?.EndActiveSpans(reason) ?? 0;

    /// <summary>Turns tracing off: removes the hooks and the listener, then flushes and shuts down the exporters.</summary>
    public static void Shutdown()
    {
        ISpanExporter[] exporters;
        lock (_gate)
        {
            if (_listener is null)
            {
                return;
            }

            if (ReferenceEquals(InstrumentationHooks.Current, _hooks))
            {
                InstrumentationHooks.Current = null;
            }

            _listener.Dispose();
            _listener = null;
            _hooks = null;
            exporters = _exporters;
            _exporters = [];
            TraceSession.SetDefault(null);
        }

        foreach (var exporter in exporters)
        {
            ShutdownQuietly(exporter);
        }
    }

    private static void OnStopped(Activity span)
    {
        ISpanExporter[] exporters;
        IReadOnlyDictionary<string, object?> resource;
        ILogger logger;
        lock (_gate)
        {
            exporters = _exporters;
            resource = _resource;
            logger = _logger;
        }

        ScrubTags(span);
        foreach (var exporter in exporters)
        {
            try
            {
                exporter.Export([span], resource);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Span exporter {Exporter} failed", exporter.Describe());
            }
        }
    }

    // Defense in depth: the hooks scrub when they set a tag, and this pass catches tags that other code adds.
    private static void ScrubTags(Activity span)
    {
        var redacted = 0;
        foreach (var (key, value) in span.TagObjects.ToList())
        {
            if (key == OpenInferenceAttributes.SecretsRedactedCount)
            {
                continue;
            }

            if (SecretScrubber.RedactKey(key) is not null)
            {
                if (value is not string text || text != SecretScrubber.Redacted)
                {
                    span.SetTag(key, SecretScrubber.Redacted);
                    redacted++;
                }

                continue;
            }

            var (scrubbed, count) = SecretScrubber.ScrubValue(value);
            if (count > 0)
            {
                span.SetTag(key, scrubbed);
                redacted += count;
            }
        }

        if (redacted > 0)
        {
            var previous = span.GetTagItem(OpenInferenceAttributes.SecretsRedactedCount) is int n ? n : 0;
            span.SetTag(OpenInferenceAttributes.SecretsRedactedCount, previous + redacted);
        }
    }

    private static Dictionary<string, object?> BuildResource(string? experiment, IReadOnlyDictionary<string, object?>? extra)
    {
        var resource = new Dictionary<string, object?>
        {
            ["service.name"] = "nooa",
            ["nooa.version"] = Source.Version,
            ["dotnet.version"] = Environment.Version.ToString(),
            ["hostname"] = Environment.MachineName,
        };
        if (!string.IsNullOrEmpty(experiment))
        {
            resource["experiment"] = experiment;
        }

        foreach (var (key, value) in extra ?? new Dictionary<string, object?>())
        {
            if (value is not null)
            {
                resource[key] = SecretScrubber.RedactKey(key) is null ? SecretScrubber.ScrubValue(value).Value : SecretScrubber.Redacted;
            }
        }

        return resource;
    }

    private static void ShutdownQuietly(ISpanExporter exporter)
    {
        try
        {
            exporter.Shutdown();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Span exporter {Exporter} failed to shut down", exporter.Describe());
        }
    }
}
