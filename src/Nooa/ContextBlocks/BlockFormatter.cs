// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/context_blocks/formatter.py

using Microsoft.Extensions.Logging;
using Nooa.Config;
using Nooa.Llm;

namespace Nooa.ContextBlocks;

/// <summary>
/// Turns resolved blocks (system blocks and event blocks) into the message list of a model request. A formatter
/// owns the choices of format: how a block is wrapped, how an event is serialized and where the system blocks go.
/// </summary>
/// <remarks>
/// Upstream also has a provider formatter that reshapes a neutral message list. C# produces <see cref="LlmMessage"/>
/// directly, and <see cref="ILlmClient"/> adapts the messages to the provider.
/// </remarks>
public abstract class BlockFormatter
{
    /// <summary>
    /// Converts all blocks into messages in display order. System blocks come before event blocks. The content of an
    /// event block is already serialized by <see cref="ContextRenderer"/>.
    /// </summary>
    /// <param name="blocks">All resolved blocks.</param>
    /// <param name="logger">Receives a warning when a turn is left out. Null discards it.</param>
    public abstract IReadOnlyList<LlmMessage> Format(IReadOnlyList<ResolvedBlock> blocks, ILogger? logger = null);

    /// <summary>
    /// Serializes an event into text. A tool call event is not serialized here: <see cref="Format"/> handles it
    /// structurally.
    /// </summary>
    /// <remarks>
    /// A response and an assistant event with string content replay verbatim, so the past turns of the model look
    /// like its own output. Every other event shows its public members within the bounds of <paramref name="eventFormat"/>.
    /// </remarks>
    /// <param name="evt">The event.</param>
    /// <param name="eventFormat">The bounds for string length, collection size and depth. Null uses the defaults of <see cref="FormatConfig"/>.</param>
    public virtual string FormatEvent(EventBase evt, FormatConfig? eventFormat = null)
    {
        ArgumentNullException.ThrowIfNull(evt);
        if (evt is LlmResponse response)
        {
            return response.ReplayContent;
        }

        if (evt.EventRole == Role.Assistant && EventRenderer.StringContent(evt) is { } content)
        {
            return content;
        }

        return EventRenderer.Format(evt, eventFormat);
    }

    /// <summary>
    /// Describes the block format for the model. The default agent system prompt embeds the text.
    /// </summary>
    public virtual string FormatDescription() => "";
}
