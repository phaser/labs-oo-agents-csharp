// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/tracing/_otlp_http_exporter.py and _viewer_auth.py

using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Nooa.Tracing;

/// <summary>
/// Sends spans as OTLP JSON over HTTP POST (the <c>ExportTraceServiceRequest</c> schema), from a background worker, so the agent
/// never waits for the network. The spans of one session go in one request, with <c>session.id</c> on the resource.
/// </summary>
/// <remarks>
/// <para>
/// Deviation: upstream uses the OpenTelemetry Python exporter for third-party collectors. This class needs no package.
/// An application that wants the official OpenTelemetry .NET exporter calls <c>AddSource("Nooa")</c> on its tracer provider instead.
/// </para>
/// <para>
/// The queue is bounded (<see cref="QueueCapacity"/>). A span that does not fit is dropped and counted, and a failed batch is counted
/// and logged, so a loss is never silent. The endpoint must be an absolute http or https URL without user info. Redirects are not followed.
/// The bearer token from <c>NOOA_VIEWER_AUTH_TOKEN</c> goes only over https or to a loopback host, never over plain http to another host.
/// </para>
/// </remarks>
public sealed class OtlpHttpSpanExporter : ISpanExporter
{
    /// <summary>Environment variable with the default endpoint.</summary>
    public const string EndpointEnvVar = "OTLP_ENDPOINT";

    /// <summary>Environment variable with the bearer token for a remote viewer.</summary>
    public const string AuthTokenEnvVar = "NOOA_VIEWER_AUTH_TOKEN";

    /// <summary>The default endpoint, the local viewer.</summary>
    public const string DefaultEndpoint = "http://localhost:5001/v1/traces";

    /// <summary>The largest number of spans that wait for delivery.</summary>
    public const int QueueCapacity = 8192;

    private const int _maxBatch = 512;
    private static readonly TimeSpan _batchDelay = TimeSpan.FromSeconds(1);

    private readonly Uri _endpoint;
    private readonly HttpClient _client;
    private readonly bool _ownsClient;
    private readonly ILogger _logger;
    private readonly Channel<object> _queue = Channel.CreateBounded<object>(new BoundedChannelOptions(QueueCapacity)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = true,
    });

    private readonly Task _worker;
    private IReadOnlyDictionary<string, object?> _resource = new Dictionary<string, object?>();
    private long _dropped;
    private long _failedBatches;

    /// <summary>Creates the exporter and starts its worker.</summary>
    /// <param name="endpoint">The traces endpoint, or null for <c>OTLP_ENDPOINT</c> or the local viewer.</param>
    /// <param name="client">The HTTP client, or null for a new one without redirects and with a 10 second timeout. Tests pass a stub handler.</param>
    /// <param name="logger">Receives failures. Null discards them.</param>
    /// <exception cref="ArgumentException">The endpoint is not an absolute http or https URL, or has user info.</exception>
    public OtlpHttpSpanExporter(string? endpoint = null, HttpClient? client = null, ILogger? logger = null)
    {
        var text = endpoint ?? Environment.GetEnvironmentVariable(EndpointEnvVar) ?? DefaultEndpoint;
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || uri.UserInfo.Length > 0)
        {
            throw new ArgumentException("The OTLP endpoint must be an absolute http or https URL without user info", nameof(endpoint));
        }

        _endpoint = uri;
        _ownsClient = client is null;
        _client = client ?? new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(10) };
        _logger = logger ?? NullLogger.Instance;
        _worker = Task.Run(RunAsync);
    }

    /// <summary>The number of spans that were dropped because the queue was full or a request failed.</summary>
    public long DroppedSpans => Interlocked.Read(ref _dropped);

    /// <inheritdoc />
    public string Describe() => $"otlp:{_endpoint.GetLeftPart(UriPartial.Path)}";

    /// <inheritdoc />
    public bool Export(IReadOnlyList<Activity> spans, IReadOnlyDictionary<string, object?> resource)
    {
        _resource = resource;
        var ok = true;
        foreach (var span in spans)
        {
            if (!_queue.Writer.TryWrite(span))
            {
                Interlocked.Increment(ref _dropped);
                ok = false;
            }
        }

        return ok;
    }

    /// <inheritdoc />
    public bool Flush(TimeSpan timeout)
    {
        var marker = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        return _queue.Writer.TryWrite(marker) && marker.Task.Wait(timeout);
    }

    /// <inheritdoc />
    public void Shutdown()
    {
        _queue.Writer.TryComplete();
        _worker.Wait(TimeSpan.FromSeconds(30));
        if (_ownsClient)
        {
            _client.Dispose();
        }

        if (Interlocked.Read(ref _dropped) > 0)
        {
            _logger.LogWarning(
                "{Dropped} span(s) were NOT delivered to {Endpoint} ({Failed} failed batch(es)). Traces may be incomplete.",
                DroppedSpans, Describe(), Interlocked.Read(ref _failedBatches));
        }
    }

    private async Task RunAsync()
    {
        var batch = new List<Activity>();
        var reader = _queue.Reader;
        while (true)
        {
            bool more;
            using (var delay = new CancellationTokenSource(_batchDelay))
            {
                try
                {
                    more = await reader.WaitToReadAsync(batch.Count == 0 ? CancellationToken.None : delay.Token);
                }
                catch (OperationCanceledException)
                {
                    await SendAsync(batch);
                    continue;
                }
            }

            if (!more)
            {
                await SendAsync(batch);
                return;
            }

            while (reader.TryRead(out var item))
            {
                if (item is Activity span)
                {
                    batch.Add(span);
                    if (batch.Count >= _maxBatch)
                    {
                        await SendAsync(batch);
                    }
                }
                else if (item is TaskCompletionSource marker)
                {
                    await SendAsync(batch);
                    marker.TrySetResult();
                }
            }
        }
    }

    private async Task SendAsync(List<Activity> batch)
    {
        if (batch.Count == 0)
        {
            return;
        }

        var spans = batch.ToList();
        batch.Clear();
        foreach (var group in spans.GroupBy(s => s.GetTagItem(OpenInferenceAttributes.SessionId) as string))
        {
            var resource = new Dictionary<string, object?>(_resource);
            if (group.Key is not null)
            {
                resource[OpenInferenceAttributes.SessionId] = group.Key;
            }

            var spansOfSession = group.ToList();
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint)
                {
                    Content = new ByteArrayContent(OtlpJson.Serialize(spansOfSession, resource)),
                };
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                if (AuthHeader() is { } header)
                {
                    request.Headers.Authorization = header;
                }

                using var response = await _client.SendAsync(request);
                if (response.StatusCode is >= HttpStatusCode.Ambiguous)
                {
                    Fail(spansOfSession.Count, $"HTTP {(int)response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                // Any failure drops this batch only. The worker must stay alive, or every later span and Flush is lost.
                Fail(spansOfSession.Count, ex.Message);
            }
        }
    }

    private AuthenticationHeaderValue? AuthHeader()
    {
        var token = Environment.GetEnvironmentVariable(AuthTokenEnvVar)?.Trim();
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        if (_endpoint.Scheme == Uri.UriSchemeHttps || _endpoint.IsLoopback)
        {
            return new AuthenticationHeaderValue("Bearer", token);
        }

        _logger.LogWarning("{Variable} is set, but it is not sent over plain http to {Host}; use https", AuthTokenEnvVar, _endpoint.Host);
        return null;
    }

    private void Fail(int spanCount, string reason)
    {
        Interlocked.Add(ref _dropped, spanCount);
        Interlocked.Increment(ref _failedBatches);
        _logger.LogError("DROP: failed to export {Count} span(s) to {Endpoint}: {Reason}", spanCount, Describe(), reason);
    }
}
