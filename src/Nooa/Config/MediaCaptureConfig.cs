// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/config/truncation_config.py

namespace Nooa.Config;

/// <summary>
/// Cap on multimodal content (images, audio, files) that <c>Show()</c> captures during execution.
/// </summary>
/// <remarks>
/// The cap is per cell, not per turn or per agent run. Each cell gets a new buffer, so a turn with several cells
/// can attach up to the cap in each. When the cap is reached, later <c>Show()</c> calls in the same cell are dropped
/// and a notice goes to stdout so the model can see the spillover.
/// </remarks>
public sealed record MediaCaptureConfig : ConfigBase
{
    /// <summary>Maximum media attachments that <c>Show()</c> captures per cell.</summary>
    public int MaxAttachmentsPerExecution { get; init { field = value; MarkSet(); } } = 5;

    public override void Validate()
    {
        if (MaxAttachmentsPerExecution <= 0)
        {
            ThrowIfInvalid(
                nameof(MediaCaptureConfig),
                [$"media_capture.{nameof(MaxAttachmentsPerExecution)} must be > 0, got {MaxAttachmentsPerExecution}"]);
        }
    }
}
