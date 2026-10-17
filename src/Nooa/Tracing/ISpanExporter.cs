// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace Nooa.Tracing;

/// <summary>
/// A destination for finished spans. <see cref="NooaTracing"/> calls <see cref="Export"/> on the thread that ends the span,
/// so an exporter that does slow work must queue it.
/// </summary>
public interface ISpanExporter
{
    /// <summary>A one-line description of the destination, for the start-up message.</summary>
    string Describe();

    /// <summary>Exports finished spans. The attribute values are already scrubbed. Returns false when the export failed.</summary>
    /// <param name="spans">The finished spans.</param>
    /// <param name="resource">The attributes of the resource that produced the spans.</param>
    bool Export(IReadOnlyList<Activity> spans, IReadOnlyDictionary<string, object?> resource);

    /// <summary>Waits until every span that was exported so far has been delivered, or the timeout ends. Returns false on timeout.</summary>
    bool Flush(TimeSpan timeout);

    /// <summary>Flushes and releases the resources of the exporter.</summary>
    void Shutdown();
}
