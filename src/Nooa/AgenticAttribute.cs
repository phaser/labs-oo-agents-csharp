// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

namespace Nooa;

/// <summary>
/// Marks a <c>partial</c> method of an agent class as LLM-run. The source generator emits the body. The method must
/// return <see cref="Task"/>, <see cref="Task{TResult}"/>, <see cref="ValueTask"/> or <see cref="ValueTask{TResult}"/>.
/// The class must be <c>partial</c> and implement <see cref="IAgenticTarget"/>. The method XML doc comment is the prompt.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class AgenticAttribute : Attribute
{
}
