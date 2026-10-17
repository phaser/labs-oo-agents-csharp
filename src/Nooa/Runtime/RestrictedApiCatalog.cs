// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/runtime/restrictions.py

using Microsoft.CodeAnalysis;

namespace Nooa.Runtime;

/// <summary>
/// The APIs that cells must not use, by namespace, type and member. It maps the deny lists of upstream
/// (<c>DEFAULT_BLOCKED_MODULES</c>, <c>DEFAULT_BLOCKED_CALLS</c>, <c>RESTRICTED_MODULES</c>) to .NET.
/// The lists are guardrails and not a security boundary (D12).
/// </summary>
internal static class RestrictedApiCatalog
{
    private const string _files = "File system access is not allowed in cells.";
    private const string _filesHint = "Use a tool of the agent to read or write files.";
    private const string _net = "Network and database access is not allowed in cells.";
    private const string _netHint = "Use a tool of the agent to reach an external service.";
    private const string _process = "Processes, the console and the debugger are not available in cells.";
    private const string _processHint = "Use Print(...) for output and ReturnResult(...) to finish. Use a tool of the agent to run a command.";
    private const string _env = "The environment of the host is not available in cells.";
    private const string _envHint = "Only Environment.NewLine and Environment.ProcessorCount are allowed.";
    private const string _reflect = "Reflection and dynamic code are not allowed in cells.";
    private const string _reflectHint = "Call the members of self and the other types directly.";
    private const string _native = "Native code and unsafe memory access are not allowed in cells.";
    private const string _nativeHint = "Use managed code only.";
    private const string _block = "A blocking call freezes the agent.";
    private const string _blockHint = "Use await with the async form, for example await Task.Delay(...) or await task.";

    private static readonly string[] _allowedEnvironmentMembers = ["NewLine", "ProcessorCount"];

    private static readonly Dictionary<string, RestrictedApiRule> _namespaces = BuildTable(
        (RestrictedApiGroup.FileSystem, _files, _filesHint, ["System.IO.MemoryMappedFiles", "System.IO.Enumeration"]),
        (RestrictedApiGroup.Network, _net, _netHint, ["System.Net", "System.Data", "Microsoft.Data"]),
        (RestrictedApiGroup.Reflection, _reflect, _reflectHint, ["System.Reflection", "System.Runtime.Loader", "System.CodeDom", "Microsoft.CodeAnalysis"]),
        (RestrictedApiGroup.NativeCode, _native, _nativeHint, ["System.Runtime.InteropServices", "Microsoft.Win32"]));

    private static readonly Dictionary<string, RestrictedApiRule> _types = BuildTable(
        (RestrictedApiGroup.FileSystem, _files, _filesHint, ["System.IO.File", "System.IO.Directory", "System.IO.FileInfo", "System.IO.DirectoryInfo", "System.IO.FileSystemInfo", "System.IO.FileStream", "System.IO.FileSystemWatcher", "System.IO.DriveInfo", "System.IO.Compression.ZipFile", "System.IO.Compression.ZipFileExtensions", "System.IO.Pipes.NamedPipeServerStream", "System.IO.Pipes.NamedPipeClientStream"]),
        (RestrictedApiGroup.Process, _process, _processHint, ["System.Diagnostics.Process", "System.Diagnostics.ProcessStartInfo", "System.Diagnostics.Debugger", "System.Console"]),
        (RestrictedApiGroup.Reflection, _reflect, _reflectHint, ["System.Type", "System.Activator", "System.AppDomain", "System.ActivationContext", "System.RuntimeTypeHandle", "System.RuntimeMethodHandle", "System.RuntimeFieldHandle", "Nooa.Runtime.RoslynExecutionEngine", "Nooa.Runtime.ReplOptions", "Nooa.Runtime.ICellValidator", "Nooa.Runtime.CellCodeValidator"]),
        (RestrictedApiGroup.NativeCode, _native, _nativeHint, ["System.Runtime.CompilerServices.Unsafe", "System.TypedReference", "System.ArgIterator"]));

    private static readonly Dictionary<(string Type, string Member), RestrictedApiRule> _members = BuildMembers(
        (RestrictedApiGroup.Process, _process, "Return from the cell or throw an exception to stop.", ["System.Environment.Exit", "System.Environment.FailFast"]),
        (RestrictedApiGroup.Reflection, _reflect, _reflectHint, ["System.Object.GetType", "System.Delegate.DynamicInvoke", "System.Delegate.CreateDelegate", "System.Linq.Expressions.LambdaExpression.Compile", "System.Linq.Expressions.Expression.Compile", "Nooa.Runtime.ReplGlobals.__args"]),
        (RestrictedApiGroup.None, _block, _blockHint, ["System.Threading.Thread.Sleep", "System.Threading.Thread.Join", "System.Threading.Tasks.Task.Wait", "System.Threading.Tasks.Task.WaitAll", "System.Threading.Tasks.Task.WaitAny", "System.Threading.Tasks.Task.Result", "System.Threading.Tasks.ValueTask.Result", "System.Threading.WaitHandle.WaitOne", "System.Threading.WaitHandle.WaitAll", "System.Threading.WaitHandle.WaitAny", "System.Threading.WaitHandle.SignalAndWait", "System.Threading.SemaphoreSlim.Wait", "System.Threading.ManualResetEventSlim.Wait", "System.Threading.CountdownEvent.Wait", "System.Threading.Barrier.SignalAndWait", "System.Threading.Monitor.Wait", "System.Threading.Monitor.Enter", "System.Threading.Monitor.TryEnter"]));

    private static readonly RestrictedApiRule _awaiterRule = new(RestrictedApiGroup.None, "GetAwaiter().GetResult()", _block, _blockHint);
    private static readonly RestrictedApiRule _environmentRule = new(RestrictedApiGroup.Environment, "System.Environment", _env, _envHint);
    private static readonly RestrictedApiRule _pathStreamRule = new(RestrictedApiGroup.FileSystem, "StreamReader/StreamWriter with a file path", _files, _filesHint);

    private static readonly Dictionary<string, RestrictedApiRule> _unresolved = BuildUnresolved();

    private static readonly SymbolDisplayFormat _fullName = new(typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces);

    /// <summary>
    /// Returns the rule for a name that the validator cannot bind. The engine can bind a name that the validator cannot,
    /// for example a variable that the engine declared for an argument, so the validator refuses these names by their text.
    /// </summary>
    public static RestrictedApiRule? FindUnresolved(string name, RestrictedApiGroup allowed) =>
        _unresolved.TryGetValue(name, out var rule) && (allowed & rule.Group) != rule.Group ? rule : null;

    /// <summary>Returns the rule that forbids the symbol, or null. A rule with a group in <paramref name="allowed"/> does not apply.</summary>
    public static RestrictedApiRule? Find(ISymbol symbol, RestrictedApiGroup allowed)
    {
        var rule = FindAny(symbol);
        return rule is null || (rule.Group != RestrictedApiGroup.None && (allowed & rule.Group) == rule.Group) ? null : rule;
    }

    private static RestrictedApiRule? FindAny(ISymbol symbol)
    {
        switch (symbol)
        {
            case INamespaceSymbol ns:
                return ForNamespace(ns);
            case INamedTypeSymbol type:
                return ForType(type);
            case IMethodSymbol or IPropertySymbol or IFieldSymbol or IEventSymbol:
                return ForMember(symbol);
            default:
                return null;
        }
    }

    private static RestrictedApiRule? ForMember(ISymbol member)
    {
        if (member.ContainingType is not { } owner)
        {
            return null;
        }

        var ownerName = FullName(owner);
        if (_members.TryGetValue((ownerName, member.Name), out var memberRule))
        {
            return memberRule;
        }

        if (member.Name == "GetResult" && owner.ContainingNamespace.ToDisplayString() == "System.Runtime.CompilerServices" && AwaiterName(owner).Contains("Awaiter", StringComparison.Ordinal))
        {
            return _awaiterRule;
        }

        if (member is IMethodSymbol { MethodKind: MethodKind.Constructor, Parameters: [{ Type.SpecialType: SpecialType.System_String }, ..] }
            && ownerName is "System.IO.StreamReader" or "System.IO.StreamWriter")
        {
            return _pathStreamRule;
        }

        if (ownerName == "System.Environment")
        {
            return _allowedEnvironmentMembers.Contains(member.Name, StringComparer.Ordinal) ? null : _environmentRule;
        }

        return ForType(owner);
    }

    private static string AwaiterName(INamedTypeSymbol type) => type.ContainingType is { } outer ? outer.Name + "." + type.Name : type.Name;

    private static RestrictedApiRule? ForType(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.ContainingType)
        {
            if (_types.TryGetValue(FullName(current), out var rule))
            {
                return rule;
            }
        }

        return ForNamespace(type.ContainingNamespace);
    }

    private static RestrictedApiRule? ForNamespace(INamespaceSymbol? ns)
    {
        for (var current = ns; current is { IsGlobalNamespace: false }; current = current.ContainingNamespace)
        {
            if (_namespaces.TryGetValue(current.ToDisplayString(), out var rule))
            {
                return rule;
            }
        }

        return null;
    }

    private static string FullName(INamedTypeSymbol type) => type.OriginalDefinition.ToDisplayString(_fullName);

    private static Dictionary<string, RestrictedApiRule> BuildTable(params (RestrictedApiGroup Group, string Reason, string Hint, string[] Names)[] groups)
    {
        var table = new Dictionary<string, RestrictedApiRule>(StringComparer.Ordinal);
        foreach (var (group, reason, hint, names) in groups)
        {
            foreach (var name in names)
            {
                table.Add(name, new RestrictedApiRule(group, name, reason, hint));
            }
        }

        return table;
    }

    private static Dictionary<(string Type, string Member), RestrictedApiRule> BuildMembers(params (RestrictedApiGroup Group, string Reason, string Hint, string[] Names)[] groups)
    {
        var table = new Dictionary<(string Type, string Member), RestrictedApiRule>();
        foreach (var (group, reason, hint, names) in groups)
        {
            foreach (var name in names)
            {
                var split = name.LastIndexOf('.');
                var shortType = name[..split];
                table.Add((shortType, name[(split + 1)..]), new RestrictedApiRule(group, name[(shortType.LastIndexOf('.') + 1)..], reason, hint));
            }
        }

        return table;
    }

    private static Dictionary<string, RestrictedApiRule> BuildUnresolved()
    {
        var table = new Dictionary<string, RestrictedApiRule>(StringComparer.Ordinal)
        {
            ["GetType"] = new(RestrictedApiGroup.Reflection, "GetType", _reflect, _reflectHint),
            ["Reflection"] = new(RestrictedApiGroup.Reflection, "System.Reflection", _reflect, _reflectHint),
            ["InteropServices"] = new(RestrictedApiGroup.NativeCode, "System.Runtime.InteropServices", _native, _nativeHint),
            ["Marshal"] = new(RestrictedApiGroup.NativeCode, "System.Runtime.InteropServices.Marshal", _native, _nativeHint),
        };
        foreach (var (type, rule) in _types)
        {
            table.TryAdd(type[(type.LastIndexOf('.') + 1)..], rule);
        }

        return table;
    }
}
