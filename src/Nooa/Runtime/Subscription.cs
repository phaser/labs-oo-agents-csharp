// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

namespace Nooa.Runtime;

/// <summary>Handle that removes a handler or middleware. Disposing it twice is safe.</summary>
internal sealed class Subscription(Action remove) : IDisposable
{
    private Action? _remove = remove;

    public void Dispose() => Interlocked.Exchange(ref _remove, null)?.Invoke();
}
