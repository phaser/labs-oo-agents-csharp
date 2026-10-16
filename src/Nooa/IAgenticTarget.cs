// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using Nooa.Strategies;

namespace Nooa;

/// <summary>
/// The runtime seam for generated <see cref="AgenticAttribute"/> bodies. The generated body builds a
/// <see cref="CurrentCall"/> and calls this interface on <c>this</c>. The agent runtime implements it.
/// </summary>
public interface IAgenticTarget
{
    /// <summary>Runs the call and returns the result as <typeparamref name="T"/>.</summary>
    Task<T> InvokeAgenticAsync<T>(CurrentCall call, CancellationToken cancellationToken);

    /// <summary>Runs the call that has no result value.</summary>
    Task InvokeAgenticAsync(CurrentCall call, CancellationToken cancellationToken);
}
