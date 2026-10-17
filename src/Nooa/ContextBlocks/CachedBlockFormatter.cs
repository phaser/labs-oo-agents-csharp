// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/context_blocks/renderers/cached.py

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nooa.Llm;

namespace Nooa.ContextBlocks;

/// <summary>
/// Splits the system blocks by <see cref="BlockMetadata.Static"/> to make a stable, cacheable prefix.
/// </summary>
/// <remarks>
/// <para>The layout is:</para>
/// <list type="number">
/// <item>One system message with the static blocks. It is stable across turns.</item>
/// <item>The event history. It only grows at the end.</item>
/// <item>A <see cref="CacheBoundary"/>.</item>
/// <item>
/// One trailing user message with the dynamic blocks in a <c>&lt;context&gt;</c> envelope. It is always its own
/// message and never merged into an earlier event, so the bytes of every earlier message stay the same between
/// renders, and the provider cache hits on the event history.
/// </item>
/// </list>
/// <para>
/// When no block is static, all blocks go into the system message and there is no boundary and no trailing message.
/// Block content cannot close its block or the envelope (see <see cref="BlockWrapper"/>).
/// </para>
/// </remarks>
public sealed class CachedBlockFormatter : BlockFormatter
{
    /// <inheritdoc />
    public override IReadOnlyList<LlmMessage> Format(IReadOnlyList<ResolvedBlock> blocks, ILogger? logger = null)
    {
        var systemBlocks = blocks.Where(b => b.Role == Role.System).ToList();
        var eventBlocks = blocks.Where(b => b.Role != Role.System);
        var staticBlocks = systemBlocks.Where(b => b.Metadata.Static).ToList();
        var dynamicBlocks = systemBlocks.Where(b => !b.Metadata.Static).ToList();

        if (staticBlocks.Count == 0)
        {
            staticBlocks = dynamicBlocks;
            dynamicBlocks = [];
        }

        var messages = new List<LlmMessage>();
        if (staticBlocks.Count > 0)
        {
            messages.Add(LlmMessage.System(string.Join("\n\n", staticBlocks.Select(BlockWrapper.XmlSystemBlock))));
        }

        messages.AddRange(BlockMessages.FromEventBlocks(eventBlocks, BlockWrapper.XmlMessageContent, logger ?? NullLogger.Instance));

        if (dynamicBlocks.Count > 0)
        {
            var tag = BlockWrapper.ContextEnvelopeTag;
            var inner = string.Join("\n", dynamicBlocks.Select(BlockWrapper.XmlSystemBlock));
            messages.Add(new CacheBoundary());
            messages.Add(LlmMessage.User($"<{tag}>\n{inner}\n</{tag}>"));
        }

        return messages;
    }

    /// <inheritdoc />
    public override string FormatDescription() =>
        "Your prompt is organized in XML context blocks: `<name>CONTENT</name>`.\n"
        + "Event history: system entries in `<sys tag=\"N\">`.";
}
