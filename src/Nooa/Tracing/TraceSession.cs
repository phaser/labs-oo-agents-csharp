// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/tracing/_session.py

using System.Globalization;
using System.Security.Cryptography;

namespace Nooa.Tracing;

/// <summary>
/// The trace session id of the current async flow. Every span that starts in the flow carries it as <c>session.id</c>.
/// </summary>
/// <remarks>
/// The id also names the JSONL file of the session, so <see cref="JsonlSpanExporter"/> accepts only a safe file name.
/// A flow with no session id uses the process default that <see cref="NooaTracing.Enable(ISpanExporter[])"/> sets.
/// </remarks>
public static class TraceSession
{
    private static readonly AsyncLocal<string?> _current = new();

    private static volatile string? _default;

    /// <summary>The session id of the current flow, or the process default, or null.</summary>
    public static string? Current => _current.Value is { Length: > 0 } value ? value : _default;

    /// <summary>Sets the session id of the current flow and of the flows that it starts. Null clears it.</summary>
    public static void Set(string? sessionId) => _current.Value = sessionId;

    /// <summary>Sets the session id until the returned scope is disposed, then restores the previous value.</summary>
    public static IDisposable Scope(string? sessionId)
    {
        var previous = _current.Value;
        _current.Value = sessionId;
        return new Restore(previous);
    }

    internal static void SetDefault(string? sessionId) => _default = sessionId;

    internal static string NewId() =>
        DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + "_" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(4));

    private sealed class Restore(string? previous) : IDisposable
    {
        public void Dispose() => _current.Value = previous;
    }
}
