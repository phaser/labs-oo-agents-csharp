// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/context_blocks/events.py, src/nooa/llm_types.py

using System.Collections;
using System.Globalization;
using System.Text.Json.Nodes;
using Nooa.Agentdoc;
using Nooa.Config;
using Nooa.Llm;

namespace Nooa.ContextBlocks;

/// <summary>
/// Renders an event for the model and lists its searchable fields. An event shows its public members as
/// <c>Name { Member = value }</c>. A member marked <see cref="HiddenAttribute"/> is not shown, and an empty value
/// (null, an empty string or an empty collection) is not shown either. <c>0</c> and <c>false</c> stay, because they carry meaning.
/// </summary>
internal static class EventRenderer
{
    /// <summary>The persisted <c>event_type</c> is the shown name. For <see cref="Events.TaskEvent"/> it is <c>Task</c>.</summary>
    public static string Format(EventBase evt, FormatConfig? format = null)
    {
        format ??= new FormatConfig();
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        new ValueRenderer(format.MaxLength, format.MaxString, format.MaxDepth, expandAll: false)
            .WriteMembers(writer, evt.EventType, ShownMembers(evt), depth: 0);
        return writer.ToString();
    }

    /// <summary>
    /// The public values for search and debug export. A response shows its readable text, reasoning and tool calls
    /// and never its archived provider parts.
    /// </summary>
    public static IReadOnlyDictionary<string, object?> SearchableFields(EventBase evt)
    {
        var fields = ShownMembers(evt).ToDictionary(m => m.Name, m => m.Value);
        if (evt is LlmResponse response)
        {
            AddIfShown(fields, nameof(LlmResponse.Content), response.Content);
            AddIfShown(fields, nameof(LlmResponse.Reasoning), response.Reasoning);
            AddIfShown(fields, nameof(LlmResponse.ToolCalls), response.ToolCalls);
        }

        return fields;
    }

    /// <summary>The searchable fields as one text: strings as they are, other values as printed. No limit applies.</summary>
    public static string SearchableText(EventBase evt)
    {
        var parts = SearchableFields(evt).Values.Select(v => v as string ?? PFormat.Format(v)).ToList();
        return parts.Count == 0 ? evt.EventType : string.Join(' ', parts);
    }

    /// <summary>The text of a string member named <c>Content</c>, or null. Assistant events replay it verbatim.</summary>
    public static string? StringContent(EventBase evt)
    {
        var member = evt.GetType().GetProperty("Content");
        return member is not null && MemberReader.TryReadValue(member, evt, out var value) ? value as string : null;
    }

    private static List<ObjectMember> ShownMembers(EventBase evt)
    {
        var members = new List<ObjectMember>();
        foreach (var member in MemberReader.GetFields(evt.GetType()))
        {
            if (MemberReader.TryReadValue(member, evt, out var value) && !IsEmpty(value))
            {
                members.Add(new ObjectMember(member.Name, value, member));
            }
        }

        return members;
    }

    private static void AddIfShown(Dictionary<string, object?> fields, string name, object? value)
    {
        if (!IsEmpty(value))
        {
            fields[name] = value;
        }
    }

    private static bool IsEmpty(object? value) => value switch
    {
        null => true,
        string text => text.Length == 0,
        JsonValue => false,
        IEnumerable items => !items.GetEnumerator().MoveNext(),
        _ => false,
    };
}
