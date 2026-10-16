// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

namespace Nooa.Llm;

/// <summary>
/// Turns a model alias (for example the value of <c>[AgentOptions(Llm = "...")]</c>) into a client.
/// </summary>
public interface ILlmClientResolver
{
    /// <summary>Returns the client for <paramref name="alias"/>.</summary>
    /// <exception cref="KeyNotFoundException">No model is configured under <paramref name="alias"/>.</exception>
    ILlmClient Resolve(string alias);
}
