// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/runtime/hooks.py

using Microsoft.Extensions.Logging;

namespace Nooa.Runtime;

/// <summary>
/// The process-wide <see cref="IInstrumentationHooks"/> and the calls that never let a hook failure reach the agent.
/// </summary>
public static class InstrumentationHooks
{
    private static volatile IInstrumentationHooks? _current;

    /// <summary>The installed hooks, or null. Set it once at startup.</summary>
    public static IInstrumentationHooks? Current
    {
        get => _current;
        set => _current = value;
    }

    internal static object? CallBefore(ILogger logger, Func<IInstrumentationHooks, object?> before)
    {
        if (_current is not { } hooks)
        {
            return null;
        }

        try
        {
            return before(hooks);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Instrumentation before-hook failed");
            return null;
        }
    }

    internal static void CallAfter(ILogger logger, Action<IInstrumentationHooks> after)
    {
        if (_current is not { } hooks)
        {
            return;
        }

        try
        {
            after(hooks);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Instrumentation after-hook failed");
        }
    }
}
