// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/agentdoc/_pformat.py

using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;

namespace Nooa.Agentdoc;

/// <summary>
/// Writes a value as C#-like text with size limits. One instance renders one value: it keeps the set of
/// objects that are being written, to detect cycles.
/// </summary>
/// <remarks>
/// Scalars are C# literals. Collections are <c>[1, 2]</c>, <c>(1, 2)</c>, <c>{1, 2}</c> and <c>{"k": v}</c>.
/// Objects are <c>Name { Field = value }</c>. A cut value is a marker, such as <c>List&lt;int&gt;(len=100, [:5]=[...], [-5:]=[...])</c>.
/// A container that fits in <see cref="_inlineWidth"/> characters is one line. A longer one is one item per line.
/// </remarks>
internal sealed class ValueRenderer(int? maxLength, int? maxString, int? maxDepth, bool expandAll)
{
    private const int _inlineWidth = 120;
    private const string _indentUnit = "    ";
    private const int _keyMaxString = 50;

    /// <summary>Nesting limit when the caller sets none. It stops a very deep structure from overflowing the stack.</summary>
    private const int _hardDepthLimit = 100;

    private int? _maxString = maxString;
    private readonly HashSet<object> _active = new(ReferenceEqualityComparer.Instance);
    private readonly int _depthLimit = Math.Min(maxDepth ?? int.MaxValue, _hardDepthLimit);

    /// <summary>Writes <paramref name="value"/> to <paramref name="writer"/>.</summary>
    public void Write(TextWriter writer, object? value, int depth = 0, int indent = 0)
    {
        // A one-line attempt that is already too wide is thrown away, so stop doing work for it.
        if (writer is WidthLimitedWriter { Overflowed: true })
        {
            return;
        }

        if (value is JsonNode node)
        {
            value = JsonNodeValues.ToPlain(node);
        }

        if (value is null)
        {
            writer.Write("null");
            return;
        }

        if (value is string text)
        {
            writer.Write(LiteralFormatter.String(text, _maxString));
            return;
        }

        if (LiteralFormatter.TryFormatScalar(value, out var scalar))
        {
            writer.Write(scalar);
            return;
        }

        var collection = CollectionReader.TryRead(value);
        if (collection is not null)
        {
            WriteCollection(writer, value, collection, depth, indent);
            return;
        }

        var type = value.GetType();
        if (!IsStructured(type))
        {
            writer.Write(FormatText(value, type));
        }
        else if (depth >= _depthLimit)
        {
            writer.Write(TypeNameFormatter.Format(type) + " { ... }");
        }
        else
        {
            WriteGuarded(writer, value, () => WriteObject(writer, value, type, depth));
        }
    }

    private void WriteGuarded(TextWriter writer, object value, Action body)
    {
        // A boxed value type has no identity and cannot contain itself.
        if (value.GetType().IsValueType)
        {
            body();
            return;
        }

        if (!_active.Add(value))
        {
            writer.Write("<cycle>");
            return;
        }

        try
        {
            body();
        }
        finally
        {
            _active.Remove(value);
        }
    }

    /// <summary>
    /// Tries to write a one-line form. It runs <paramref name="writeFlat"/> into a writer that gives up at the line
    /// width, and copies the text to <paramref name="writer"/> when it fits. Rendering into a limited writer keeps
    /// the cost low for deep structures: a form that is too wide stops early, instead of being built in full and then built again.
    /// </summary>
    private static bool TryWriteInline(TextWriter writer, Action<TextWriter> writeFlat)
    {
        // Already inside a one-line attempt: the parent decides. If this form is too wide, the parent is too wide.
        if (writer is WidthLimitedWriter)
        {
            writeFlat(writer);
            return true;
        }

        var attempt = new WidthLimitedWriter(_inlineWidth);
        writeFlat(attempt);
        if (attempt.Overflowed)
        {
            return false;
        }

        writer.Write(attempt.ToString());
        return true;
    }

    private void WriteJoined(TextWriter writer, IEnumerable<object?> items, int depth)
    {
        var first = true;
        foreach (var item in items)
        {
            if (!first)
            {
                writer.Write(", ");
            }

            first = false;
            Write(writer, item, depth + 1);
        }
    }

    private void WritePairs(TextWriter writer, IEnumerable<KeyValuePair<object?, object?>> pairs, int depth, string separatorAfterLast = "")
    {
        var first = true;
        foreach (var pair in pairs)
        {
            if (!first)
            {
                writer.Write(", ");
            }

            first = false;
            WriteKey(writer, pair.Key, depth);
            writer.Write(": ");
            Write(writer, pair.Value, depth + 1);
        }

        writer.Write(separatorAfterLast);
    }

    private void WriteKey(TextWriter writer, object? key, int depth)
    {
        if (key is string s)
        {
            writer.Write(LiteralFormatter.String(s, _keyMaxString));
        }
        else
        {
            Write(writer, key, depth + 1);
        }
    }

    private static string Indent(int level) => string.Concat(Enumerable.Repeat(_indentUnit, level));

    // ---- objects ----

    /// <summary>
    /// True for a user type that prints its members. A type with a hand-written <c>ToString</c> prints that text
    /// instead, as upstream honors a custom <c>__repr__</c>. A record keeps member printing: its <c>ToString</c> is generated.
    /// </summary>
    private static bool IsStructured(Type type)
    {
        if (!ReferencedTypes.IsCustom(ReferencedTypes.Normalize(type)))
        {
            return false;
        }

        var toString = type.GetMethod(nameof(ToString), BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes);
        return toString is null
            || toString.DeclaringType == typeof(object)
            || toString.DeclaringType == typeof(ValueType)
            || toString.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false);
    }

    private void WriteObject(TextWriter writer, object value, Type type, int depth)
    {
        var members = new List<ObjectMember>();
        foreach (var member in MemberReader.GetFields(type))
        {
            if (MemberReader.TryReadValue(member, value, out var memberValue) && memberValue is not Delegate)
            {
                members.Add(new ObjectMember(member.Name, memberValue, member));
            }
        }

        WriteMembers(writer, TypeNameFormatter.Format(type), members, depth);
    }

    /// <summary>
    /// Writes <c>Name { Member = value, ... }</c>. A member with a <see cref="MaxStringAttribute"/> uses its own string
    /// limit for its value.
    /// </summary>
    public void WriteMembers(TextWriter writer, string name, IReadOnlyList<ObjectMember> members, int depth)
    {
        var truncated = maxLength is { } max && members.Count > max;
        var shown = truncated ? members.Take(maxLength!.Value) : members;
        writer.Write(name);
        if (members.Count == 0)
        {
            writer.Write(" { }");
            return;
        }

        writer.Write(" { ");
        var first = true;
        foreach (var member in shown)
        {
            writer.Write(first ? member.Name + " = " : ", " + member.Name + " = ");
            first = false;
            var previous = _maxString;
            if (member.Source is not null && Attribute.GetCustomAttribute(member.Source, typeof(MaxStringAttribute), inherit: true) is MaxStringAttribute limit)
            {
                _maxString = limit.Limit;
            }

            try
            {
                Write(writer, member.Value, depth + 1);
            }
            finally
            {
                _maxString = previous;
            }
        }

        writer.Write(truncated ? ", ... }" : " }");
    }

    /// <summary>The text of a value that is not a scalar, a collection or a user type: its <c>ToString</c>, cut to the string limit.</summary>
    private string FormatText(object value, Type type)
    {
        string text;
        try
        {
            text = (value is IFormattable formattable ? formattable.ToString(null, CultureInfo.InvariantCulture) : value.ToString()) ?? string.Empty;
        }
        catch (Exception)
        {
            return "<" + TypeNameFormatter.Format(type) + ">";
        }

        return _maxString is { } max && text.Length > max
            ? LiteralFormatter.Marker(TypeNameFormatter.Format(type), "text_len", text, max)
            : text;
    }

    // ---- collections ----

    private void WriteCollection(TextWriter writer, object value, CollectionInfo collection, int depth, int indent)
    {
        var (open, close) = Brackets(collection.Kind);
        if (collection.Count == 0)
        {
            writer.Write(open + close);
        }
        else if (depth >= _depthLimit)
        {
            writer.Write(collection.Kind == CollectionKind.Dictionary
                ? $"{{{collection.TypeName}: {collection.Count} items}}"
                : $"{open}{collection.TypeName}: {collection.Count} items{close}");
        }
        else if (collection.Kind == CollectionKind.Dictionary)
        {
            WriteGuarded(writer, value, () => WriteDictionary(writer, collection, depth, indent));
        }
        else
        {
            WriteGuarded(writer, value, () => WriteSequence(writer, collection, depth, indent));
        }
    }

    private static (string Open, string Close) Brackets(CollectionKind kind) => kind switch
    {
        CollectionKind.Tuple => ("(", ")"),
        CollectionKind.Set or CollectionKind.Dictionary => ("{", "}"),
        _ => ("[", "]"),
    };

    private void WriteSequence(TextWriter writer, CollectionInfo collection, int depth, int indent)
    {
        var (open, close) = Brackets(collection.Kind);
        var items = collection.Items;
        var total = items.Count;
        var truncated = maxLength is { } max && total > max;
        var ordered = collection.Kind != CollectionKind.Set;
        var headCount = total;
        var tailCount = 0;
        if (truncated)
        {
            headCount = ordered ? (maxLength!.Value + 1) / 2 : maxLength!.Value;
            tailCount = ordered ? maxLength.Value - headCount : 0;
        }

        var head = items.Take(headCount).ToList();
        var tail = items.Skip(total - tailCount).Take(tailCount).ToList();
        var inner = Indent(indent + 1);

        if (!truncated)
        {
            if (!expandAll && TryWriteInline(writer, w =>
            {
                w.Write(open);
                WriteJoined(w, head, depth);
                w.Write(close);
            }))
            {
                return;
            }

            writer.Write(open + "\n");
            foreach (var item in head)
            {
                writer.Write(inner);
                Write(writer, item, depth + 1, indent + 1);
                writer.Write(",\n");
            }

            writer.Write(Indent(indent) + close);
            return;
        }

        var name = collection.TypeName;
        if (!expandAll && TryWriteInline(writer, w =>
        {
            if (ordered)
            {
                w.Write($"{name}(len={total}, [:{headCount}]={open}");
                WriteJoined(w, head, depth);
                w.Write(close);
                if (tailCount > 0)
                {
                    w.Write($", [-{tailCount}:]={open}");
                    WriteJoined(w, tail, depth);
                    w.Write(close);
                }

                w.Write(")");
            }
            else
            {
                w.Write($"{name}(len={total}, items={open}");
                WriteJoined(w, head, depth);
                w.Write($", ...{close})");
            }
        }))
        {
            return;
        }

        if (ordered)
        {
            writer.Write($"{name}(len={total},\n");
            WriteChunk(writer, $"[:{headCount}]=", head, open, close, depth, indent);
            if (tailCount > 0)
            {
                WriteChunk(writer, $"[-{tailCount}:]=", tail, open, close, depth, indent);
            }

            writer.Write(Indent(indent) + ")");
        }
        else
        {
            writer.Write($"{name}(len={total}, items={open}\n");
            foreach (var item in head)
            {
                writer.Write(inner);
                Write(writer, item, depth + 1, indent + 1);
                writer.Write(",\n");
            }

            writer.Write($"{inner}...\n{Indent(indent)}{close})");
        }
    }

    private void WriteChunk(TextWriter writer, string label, List<object?> chunk, string open, string close, int depth, int indent)
    {
        writer.Write($"{Indent(indent + 1)}{label}{open}\n");
        foreach (var item in chunk)
        {
            writer.Write(Indent(indent + 2));
            Write(writer, item, depth + 1, indent + 2);
            writer.Write(",\n");
        }

        writer.Write($"{Indent(indent + 1)}{close},\n");
    }

    private void WriteDictionary(TextWriter writer, CollectionInfo collection, int depth, int indent)
    {
        var entries = collection.Entries;
        var total = entries.Count;
        var truncated = maxLength is { } max && total > max;
        var headCount = total;
        var tailCount = 0;
        if (truncated)
        {
            headCount = (maxLength!.Value + 1) / 2;
            tailCount = maxLength.Value - headCount;
        }

        var head = entries.Take(headCount).ToList();
        var tail = entries.Skip(total - tailCount).Take(tailCount).ToList();
        var inner = Indent(indent + 1);

        void WriteLines(IEnumerable<KeyValuePair<object?, object?>> source)
        {
            foreach (var entry in source)
            {
                writer.Write(inner);
                WriteKey(writer, entry.Key, depth);
                writer.Write(": ");
                Write(writer, entry.Value, depth + 1, indent + 1);
                writer.Write(",\n");
            }
        }

        if (!truncated)
        {
            if (!expandAll && TryWriteInline(writer, w =>
            {
                w.Write("{");
                WritePairs(w, head, depth, "}");
            }))
            {
                return;
            }

            writer.Write("{\n");
            WriteLines(head);
            writer.Write(Indent(indent) + "}");
            return;
        }

        var name = collection.TypeName;
        if (!expandAll && TryWriteInline(writer, w =>
        {
            w.Write($"{name}(len={total}, items={{");
            WritePairs(w, head, depth);
            if (tail.Count > 0)
            {
                w.Write(", ..., ");
                WritePairs(w, tail, depth);
            }

            w.Write("})");
        }))
        {
            return;
        }

        writer.Write($"{name}(len={total}, items={{\n");
        WriteLines(head);
        if (tail.Count > 0)
        {
            writer.Write($"{inner}...\n");
            WriteLines(tail);
        }

        writer.Write(Indent(indent) + "})");
    }
}
