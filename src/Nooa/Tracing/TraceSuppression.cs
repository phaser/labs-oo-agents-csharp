// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

namespace Nooa.Tracing;

/// <summary>
/// True while the current flow runs the generation of a <see cref="NoTraceAttribute"/> method. No span may start in it,
/// because the spans would carry the arguments and the result that the attribute keeps out of traces.
/// </summary>
internal static class TraceSuppression
{
    private static readonly AsyncLocal<bool> _active = new();

    public static bool IsActive
    {
        get => _active.Value;
        set => _active.Value = value;
    }
}
