// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0

namespace Nooa.Runtime;

/// <summary>An API that the validator refuses. <see cref="Group"/> None means that an agent cannot opt in.</summary>
/// <param name="Group">The group that the agent can allow.</param>
/// <param name="Label">The name of the API in the message.</param>
/// <param name="Reason">Why the API is refused.</param>
/// <param name="Hint">What the model can do instead.</param>
internal sealed record RestrictedApiRule(RestrictedApiGroup Group, string Label, string Reason, string Hint);
