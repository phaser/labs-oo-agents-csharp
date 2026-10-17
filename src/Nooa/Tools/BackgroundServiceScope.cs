// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/tools/shell_lifecycle.py

namespace Nooa.Tools;

/// <summary>
/// Owns the shells created while it is active, until the invocation ends. At the end it closes each shell and leaves the background
/// jobs running. The caller must stop those jobs later, for example after it verifies a service.
/// </summary>
/// <remarks>Create a scope with <see cref="ShellLifecycle.PreserveBackgroundServices"/>.</remarks>
public sealed class BackgroundServiceScope : IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly List<BashSession> _sessions = [];
    private readonly BackgroundServiceScope? _previous;
    private bool _closed;

    internal BackgroundServiceScope(BackgroundServiceScope? previous)
    {
        _previous = previous;
    }

    /// <summary>Includes a shell that was created before the scope began. The scope then keeps its background jobs alive on close.</summary>
    public void Adopt(BashSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        lock (_gate)
        {
            if (_closed)
            {
                throw new ShellToolException("The background service scope has already closed.");
            }

            session.KeepBackgroundOnClose();
            _sessions.Add(session);
        }
    }

    /// <summary>Closes every owned shell and keeps its background jobs. Throws the first close error after all shells closed.</summary>
    public ValueTask DisposeAsync()
    {
        ShellLifecycle.Restore(_previous);
        BashSession[] sessions;
        lock (_gate)
        {
            _closed = true;
            sessions = [.. _sessions];
            _sessions.Clear();
        }

        return new ValueTask(CloseAllAsync(sessions));
    }

    private static async Task CloseAllAsync(BashSession[] sessions)
    {
        var closing = sessions.Select(session => session.CloseAsync()).ToArray();
        try
        {
            await Task.WhenAll(closing).ConfigureAwait(false);
        }
        catch (Exception) when (closing.Any(task => task.IsFaulted))
        {
            throw closing.First(task => task.IsFaulted).Exception!.InnerExceptions[0];
        }
    }
}
