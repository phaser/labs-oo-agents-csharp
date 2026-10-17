// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/context_blocks/formatter.py

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nooa.Llm;

namespace Nooa.ContextBlocks;

/// <summary>
/// Renders each system block as a Markdown section and wraps each event with a Markdown header. A Markdown block has
/// no closing delimiter, so its content can imitate a header. Use <see cref="XmlBlockFormatter"/> for untrusted data.
/// </summary>
/// <example>
/// <code>
/// # Notes
///
/// Here are my notes...
/// </code>
/// </example>
public sealed class MarkdownBlockFormatter : BlockFormatter
{
    /// <inheritdoc />
    public override IReadOnlyList<LlmMessage> Format(IReadOnlyList<ResolvedBlock> blocks, ILogger? logger = null) =>
        BlockMessages.Build(blocks, BlockWrapper.MarkdownSystemBlock, BlockWrapper.MarkdownMessageContent, logger ?? NullLogger.Instance);

    /// <inheritdoc />
    public override string FormatDescription() =>
        "Your prompt is organized in context blocks with markdown headers: `# Block Name`.";
}
