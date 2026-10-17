// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics;
using Nooa.Runtime;
using Nooa.Tracing;

namespace Nooa.Tests.Tracing;

/// <summary>Test helper: installs <see cref="OpenInferenceHooks"/> and collects the finished spans. Restores the hooks on dispose.</summary>
public sealed class SpanCapture : IDisposable
{
    private readonly ConcurrentQueue<Activity> _spans = new();
    private readonly ActivityListener _listener;
    private readonly IInstrumentationHooks? _previous;

    public SpanCapture()
    {
        _previous = InstrumentationHooks.Current;
        Hooks = new OpenInferenceHooks();
        InstrumentationHooks.Current = Hooks;
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == NooaTracing.SourceName,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = _spans.Enqueue,
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public OpenInferenceHooks Hooks { get; }

    public IReadOnlyList<Activity> Spans => [.. _spans];

    public Activity Single(string name) => Spans.Single(s => s.DisplayName == name);

    public static string Tag(Activity span, string key) => span.GetTagItem(key)?.ToString() ?? "";

    public void Dispose()
    {
        InstrumentationHooks.Current = _previous;
        _listener.Dispose();
    }
}
