// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/tools/_results.py

namespace Nooa.Tools;

/// <summary>An item of <see cref="ShellTools.RunStreamAsync"/>: a <see cref="StreamEvent"/> chunk or the final <see cref="StreamDone"/>.</summary>
public abstract record ShellStreamEvent;
