// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/agentdoc/_pformat.py

using System.Reflection;
using System.Runtime.CompilerServices;

namespace Nooa.Agentdoc;

/// <summary>Writes the C# declaration text of a type, an instance or a method (the <c>_format_*_info</c> functions).</summary>
internal sealed class DocFormatter(IAgentDocProvider provider, bool concise)
{
    private const string _indent = "    ";
    private const int _fieldMaxLength = 3;
    private const int _defaultMaxLength = 5;
    private const int _valueMaxString = 50;
    private const int _valueMaxDepth = 1;

    private readonly NullabilityInfoContext _nullability = new();

    /// <summary>Formats a type. With an <paramref name="instance"/>, fields and auto-properties show their current values.</summary>
    public string FormatType(Type type, object? instance = null)
    {
        var lines = new List<string>();
        AppendSummary(lines, string.Empty, provider.GetSummary(type), null, null);
        lines.Add(Header(type));
        lines.Add("{");

        if (type.IsEnum)
        {
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                lines.Add($"{_indent}{field.Name} = {field.GetRawConstantValue()},");
            }
        }
        else
        {
            var fields = MemberReader.GetFields(type);
            var methods = MemberReader.GetMethods(type);
            lines.AddRange(fields.Select(m => _indent + FormatField(m, instance)));
            if (fields.Count > 0 && methods.Count > 0)
            {
                lines.Add(string.Empty);
            }

            foreach (var method in methods)
            {
                AppendMethod(lines, _indent, method, qualified: false);
            }
        }

        lines.Add("}");
        return string.Join("\n", lines);
    }

    /// <summary>Formats one method as a doc comment and a signature. A standalone method carries its type name.</summary>
    public string FormatMethod(MethodInfo method)
    {
        var lines = new List<string>();
        AppendMethod(lines, string.Empty, method, qualified: true);
        return string.Join("\n", lines);
    }

    private static string Header(Type type)
    {
        var kind = type.IsInterface ? "interface"
            : type.IsEnum ? "enum"
            : type.IsValueType ? "struct"
            : type.GetMethod("<Clone>$") is not null ? "record"
            : type.IsAbstract && type.IsSealed ? "static class"
            : type.IsAbstract ? "abstract class"
            : "class";
        var header = $"{kind} {TypeNameFormatter.Format(type)}";
        if (type.BaseType is { } baseType && ReferencedTypes.IsCustom(ReferencedTypes.Normalize(baseType)))
        {
            header += " : " + TypeNameFormatter.Format(baseType);
        }

        return header;
    }

    private string FormatField(MemberInfo member, object? instance)
    {
        string text;
        Type valueType;
        if (member is FieldInfo field)
        {
            valueType = field.FieldType;
            text = $"{TypeNameFormatter.Format(valueType, _nullability.Create(field))} {field.Name}";
        }
        else
        {
            var property = (PropertyInfo)member;
            valueType = property.PropertyType;
            text = $"{TypeNameFormatter.Format(valueType, _nullability.Create(property))} {property.Name} {{ {Accessors(property)} }}";
        }

        object? current = null;
        var hasValue = instance is not null && MemberReader.TryReadValue(member, instance, out current);
        if (hasValue)
        {
            text += " = " + PFormat.Format(current, maxLength: _fieldMaxLength, maxString: _valueMaxString, maxDepth: _valueMaxDepth);
        }

        if (hasValue || member is FieldInfo)
        {
            text += ";";
        }

        var description = provider.GetSummary(member);
        if (string.IsNullOrWhiteSpace(description) && ReferencedTypes.IsCustom(ReferencedTypes.Normalize(Nullable.GetUnderlyingType(valueType) ?? valueType)))
        {
            description = provider.GetSummary(ReferencedTypes.Normalize(Nullable.GetUnderlyingType(valueType) ?? valueType));
        }

        description = FirstLine(description);
        return description.Length > 0 ? $"{text}  // {description}" : text;
    }

    private static string Accessors(PropertyInfo property)
    {
        var parts = new List<string>();
        if (property.GetMethod?.IsPublic == true)
        {
            parts.Add("get;");
        }

        if (property.SetMethod?.IsPublic == true)
        {
            var isInit = property.SetMethod.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(IsExternalInit));
            parts.Add(isInit ? "init;" : "set;");
        }

        return string.Join(" ", parts);
    }

    private void AppendMethod(List<string> lines, string indent, MethodInfo method, bool qualified)
    {
        var parameters = method.GetParameters();
        var args = new List<(string Name, string? Description)>();
        var signature = new List<string>();
        foreach (var p in parameters)
        {
            signature.Add(FormatParameter(p));
            args.Add((p.Name ?? string.Empty, provider.GetParameterSummary(p)));
        }

        var returns = provider.GetParameterSummary(method.ReturnParameter);
        AppendSummary(lines, indent, provider.GetSummary(method), args, returns);

        var returnType = TypeNameFormatter.Format(method.ReturnType, _nullability.Create(method.ReturnParameter));
        var name = qualified && method.DeclaringType is { } owner ? $"{TypeNameFormatter.Format(owner)}.{method.Name}" : method.Name;
        var generics = method.IsGenericMethodDefinition
            ? "<" + string.Join(", ", method.GetGenericArguments().Select(a => a.Name)) + ">"
            : string.Empty;
        var modifier = method.IsStatic ? "static " : string.Empty;
        lines.Add($"{indent}{modifier}{returnType} {name}{generics}({string.Join(", ", signature)});");
    }

    private string FormatParameter(ParameterInfo p)
    {
        var prefix = p.IsOut ? "out "
            : p.ParameterType.IsByRef ? (p.IsIn ? "in " : "ref ")
            : p.IsDefined(typeof(ParamArrayAttribute), inherit: false) ? "params "
            : string.Empty;
        var text = $"{prefix}{TypeNameFormatter.Format(p.ParameterType, _nullability.Create(p))} {p.Name}";
        if (p.HasDefaultValue)
        {
            var value = p.DefaultValue;
            if (value is null && p.ParameterType.IsValueType && Nullable.GetUnderlyingType(p.ParameterType) is null)
            {
                text += " = default";
            }
            else
            {
                var type = Nullable.GetUnderlyingType(p.ParameterType) ?? p.ParameterType;
                text += " = " + PFormat.Format(value is not null && type.IsEnum ? Enum.ToObject(type, value) : value, maxLength: _defaultMaxLength, maxString: _valueMaxString, maxDepth: _valueMaxDepth);
            }
        }

        return text;
    }

    /// <summary>Writes <c>///</c> lines: the summary, then (unless concise) the parameter and return descriptions.</summary>
    private void AppendSummary(
        List<string> lines,
        string indent,
        string? summary,
        List<(string Name, string? Description)>? args,
        string? returns)
    {
        var text = new List<string>();
        var trimmed = (summary ?? string.Empty).Trim();
        if (trimmed.Length > 0)
        {
            text.AddRange(concise ? [FirstLine(trimmed)] : trimmed.Split('\n').Select(l => l.TrimEnd()));
        }

        if (!concise)
        {
            var described = args?.Where(a => !string.IsNullOrWhiteSpace(a.Description)).ToList() ?? [];
            if (described.Count > 0)
            {
                text.Add("Args:");
                text.AddRange(described.Select(a => $"  {a.Name}: {Collapse(a.Description!)}"));
            }

            if (!string.IsNullOrWhiteSpace(returns))
            {
                text.Add($"Returns: {Collapse(returns)}");
            }
        }

        lines.AddRange(text.Select(l => l.Length == 0 ? $"{indent}///" : $"{indent}/// {l}"));
    }

    private static string FirstLine(string? text)
    {
        var trimmed = (text ?? string.Empty).Trim();
        var end = trimmed.IndexOf('\n', StringComparison.Ordinal);
        return (end < 0 ? trimmed : trimmed[..end]).Trim();
    }

    private static string Collapse(string text) => string.Join(" ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
