// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics;
using Nooa.Tracing;

namespace Nooa.Tests.Tracing;

/// <summary>Test exporter: records the spans and the resource, and can throw.</summary>
public sealed class RecordingSpanExporter : ISpanExporter
{
    public ConcurrentQueue<Activity> Spans { get; } = new();

    public IReadOnlyDictionary<string, object?>? Resource { get; private set; }

    public bool Throw { get; init; }

    public bool IsShutDown { get; private set; }

    public string Describe() => "recording";

    public bool Export(IReadOnlyList<Activity> spans, IReadOnlyDictionary<string, object?> resource)
    {
        Resource = resource;
        foreach (var span in spans)
        {
            Spans.Enqueue(span);
        }

        return Throw ? throw new InvalidOperationException("exporter failed") : true;
    }

    public bool Flush(TimeSpan timeout) => true;

    public void Shutdown() => IsShutDown = true;
}
