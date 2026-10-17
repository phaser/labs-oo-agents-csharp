// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/context_blocks/formatter.py

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nooa.Llm;

namespace Nooa.ContextBlocks;

/// <summary>
/// Wraps each system block in an XML tag and each user event in a <c>&lt;sys&gt;</c> tag. All system blocks are
/// joined into one system message. A closing tag inside the content cannot end its block (see <see cref="BlockWrapper"/>).
/// </summary>
/// <example>
/// <code>
/// &lt;notes&gt;
/// Here are my notes...
/// &lt;/notes&gt;
/// </code>
/// </example>
public sealed class XmlBlockFormatter : BlockFormatter
{
    /// <inheritdoc />
    public override IReadOnlyList<LlmMessage> Format(IReadOnlyList<ResolvedBlock> blocks, ILogger? logger = null) =>
        BlockMessages.Build(blocks, BlockWrapper.XmlSystemBlock, BlockWrapper.XmlMessageContent, logger ?? NullLogger.Instance);

    /// <inheritdoc />
    public override string FormatDescription() =>
        "Your prompt is organized in XML context blocks: `<name>CONTENT</name>`.\n"
        + "Event history: system entries in `<sys tag=\"N\">`. Your own messages appear as plain assistant turns.";
}
