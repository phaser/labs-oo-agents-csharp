// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/config/strategy_config.py

namespace Nooa.Config;

/// <summary>
/// How the Predict output is serialized back into the conversation history.
/// </summary>
public enum PredictOutputSerialization
{
    /// <summary>The response event stays and replays as a plain assistant message (raw JSON content, no wrapper).</summary>
    Event,

    /// <summary>
    /// The response event stays and a synthetic <c>return_result()</c> tool call event is appended.
    /// It renders natively through the provider formatter, which is clearer for tool-using models that read the history.
    /// </summary>
    ToolCall,
}
