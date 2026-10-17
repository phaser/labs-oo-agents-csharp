// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/tools/_results.py

namespace Nooa.Tools;

/// <summary>The last item of a stream. The command is complete.</summary>
/// <param name="ReturnCode">The exit code of the command.</param>
/// <param name="TimedOut">True when the command ran past its timeout.</param>
public sealed record StreamDone(int ReturnCode, bool TimedOut) : ShellStreamEvent;
