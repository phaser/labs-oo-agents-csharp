// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/tools/shell_lifecycle.py

namespace Nooa.Tools;

/// <summary>Invocation-scoped ownership for shells whose services are graded after completion.</summary>
public static class ShellLifecycle
{
    private static readonly AsyncLocal<BackgroundServiceScope?> _current = new();

    internal static BackgroundServiceScope? Current => _current.Value;

    /// <summary>
    /// Preserves the background jobs of every shell created in this async flow. Tasks started inside the scope inherit it.
    /// Unrelated concurrent flows do not. Use <c>await using</c>. This method is not async on purpose: an async method
    /// would drop the scope when it returns.
    /// </summary>
    public static BackgroundServiceScope PreserveBackgroundServices()
    {
        var scope = new BackgroundServiceScope(_current.Value);
        _current.Value = scope;
        return scope;
    }

    internal static void Restore(BackgroundServiceScope? previous) => _current.Value = previous;
}
