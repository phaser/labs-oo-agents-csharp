// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;

namespace Nooa.Agentdoc;

/// <summary>One member of an object that <c>ValueRenderer</c> writes. <paramref name="Source"/> supplies a <see cref="MaxStringAttribute"/>.</summary>
internal readonly record struct ObjectMember(string Name, object? Value, MemberInfo? Source = null);
