// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/tools/_bash_session.py

namespace Nooa.Tools;

/// <summary>The result of one command in a <see cref="BashSession"/>.</summary>
/// <param name="Stdout">The standard output, trimmed and bounded.</param>
/// <param name="Stderr">The standard error, trimmed and bounded.</param>
/// <param name="ExitCode">The exit code. 124 means timeout. -1 means the shell died before it reported a status.</param>
/// <param name="TimedOut">True when the command ran past its timeout.</param>
public readonly record struct BashCommandResult(string Stdout, string Stderr, int ExitCode, bool TimedOut);
