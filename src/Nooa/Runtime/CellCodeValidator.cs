// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/runtime/code_validator.py, src/nooa/runtime/restrictions.py, src/nooa/runtime/async_safety.py

using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Nooa.Errors;

namespace Nooa.Runtime;

/// <summary>
/// Checks the code of a cell with Roslyn before the engine compiles it. It refuses type declarations, blocking calls
/// (<c>.Result</c>, <c>.Wait()</c>, <c>Thread.Sleep</c>, <c>Task.WaitAll</c>), calls to async methods without <c>await</c>
/// (CS4014), recursion into the running method, names that hide the return type, and the APIs of the groups in
/// <see cref="RestrictedApiGroup"/> (files, network, processes, environment, reflection, native code, <c>dynamic</c>, <c>unsafe</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>This is a guardrail and not a security boundary (D12).</b> The validator is defence in depth. It reduces the chance that a model
/// breaks the host by mistake. It cannot stop hostile code, because the cells run in the host process and a static check
/// does not cover every way to reach an API. A process sandbox is backlog item B01.
/// </para>
/// <para>
/// The validator binds symbols with the semantic model, so aliases, <c>global::</c>, <c>using static</c> and full names do not bypass it.
/// It chains the cells that it accepted and that compile, like the engine does, so a variable of an earlier cell has its real type. It cannot see the
/// variables that the engine declares for arguments. For an unbound name it refuses the names of restricted types by their text.
/// </para>
/// <para>Use one validator for one engine. A validator keeps the earlier cells and is not thread-safe. The engine runs one cell at a time.</para>
/// </remarks>
public sealed class CellCodeValidator : ICellValidator
{
    private const string _unawaitedId = "CS4014";

    private static readonly CSharpParseOptions _parseOptions = new(LanguageVersion.Latest, kind: SourceCodeKind.Script);

    private readonly CellValidatorOptions _options;
    private readonly List<MetadataReference> _references;
    private readonly HashSet<string> _protectedNames;
    private Compilation? _previous;
    private int _count;

    /// <summary>Creates a validator.</summary>
    /// <exception cref="InvalidOperationException">An assembly has no file path, so the validator cannot reference it.</exception>
    public CellCodeValidator(CellValidatorOptions? options = null)
    {
        _options = options ?? new CellValidatorOptions();
        _references = BuildReferences(_options);
        _protectedNames = ProtectedNames(_options.ReturnType);
    }

    /// <inheritdoc />
    /// <exception cref="RestrictedCodeException">The code breaks a rule. The message lists every issue with its line.</exception>
    public void Validate(string code)
    {
        ArgumentNullException.ThrowIfNull(code);
        var tree = CSharpSyntaxTree.ParseText(code, _parseOptions, "Cell");
        var imports = RoslynExecutionEngine.DefaultImports.Concat(_options.Imports).Distinct(StringComparer.Ordinal);
        var compilationOptions = new CSharpCompilationOptions(
            OutputKind.DynamicallyLinkedLibrary,
            usings: imports,
            specificDiagnosticOptions: new Dictionary<string, ReportDiagnostic> { [_unawaitedId] = ReportDiagnostic.Error });
        var compilation = CSharpCompilation.CreateScriptCompilation(
            "CellValidation" + ++_count,
            tree,
            _references,
            compilationOptions,
            _previous as CSharpCompilation,
            typeof(object),
            _options.GlobalsType);
        var model = compilation.GetSemanticModel(tree);

        var walker = new CellValidationWalker(model, _options, _protectedNames);
        walker.Visit(tree.GetRoot());
        var issues = walker.Issues.ToList();
        var diagnostics = model.GetDiagnostics();
        issues.AddRange(diagnostics
            .Where(d => d.Id == _unawaitedId && d.Location.IsInSource)
            .Select(UnawaitedIssue));

        if (issues.Count > 0)
        {
            var ordered = issues
                .GroupBy(i => (i.Line, i.Key))
                .Select(g => g.First())
                .OrderBy(i => i.Line)
                .ThenBy(i => i.Column)
                .ToList();
            throw new RestrictedCodeException(Format(code, ordered), ordered[0].Line + 1);
        }

        // The engine does not run a cell that fails to compile, so it does not chain it. Roslyn refuses a previous
        // submission with errors, so the validator must not chain it either.
        if (!diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
        {
            _previous = compilation;
        }
    }

    private static CellIssue UnawaitedIssue(Diagnostic diagnostic)
    {
        var span = diagnostic.Location.GetLineSpan();
        var length = span.StartLinePosition.Line == span.EndLinePosition.Line ? span.EndLinePosition.Character - span.StartLinePosition.Character : 0;
        return new CellIssue(
            span.StartLinePosition.Line,
            span.StartLinePosition.Character,
            length,
            "unawaited",
            "This call returns a task that nobody awaits. Add await before the call. To start work and await it later, assign the task to a variable.");
    }

    private static string Format(string code, IReadOnlyList<CellIssue> issues)
    {
        var lines = code.Split('\n');
        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"The code breaks {issues.Count} rule{(issues.Count == 1 ? "" : "s")} and was not run.\n");
        foreach (var issue in issues)
        {
            sb.Append(CultureInfo.InvariantCulture, $"\nline {issue.Line + 1}\n");
            if (issue.Line < lines.Length)
            {
                var source = lines[issue.Line].TrimEnd('\r');
                var column = Math.Min(issue.Column, source.Length);
                var indent = string.Concat(source.Take(column).Select(c => c == '\t' ? '\t' : ' '));
                var marker = new string('^', Math.Clamp(issue.Length, 1, Math.Max(1, source.Length - column)));
                sb.Append(CultureInfo.InvariantCulture, $"  {source}\n  {indent}{marker}\n");
            }

            sb.Append(issue.Message).Append('\n');
        }

        return sb.ToString().TrimEnd();
    }

    private static List<MetadataReference> BuildReferences(CellValidatorOptions options)
    {
        var directory = System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory();
        var paths = RoslynExecutionEngine.FrameworkAssemblies.Select(name => Path.Combine(directory, name + ".dll")).ToList();
        // The facade assemblies forward to the core library. Roslyn scripting references it too, so the engine resolves the same types.
        var assemblies = new List<System.Reflection.Assembly> { typeof(object).Assembly, typeof(ReplGlobals).Assembly };
        if (options.GlobalsType is { } globals)
        {
            assemblies.Add(globals.Assembly);
            assemblies.AddRange(globals.GenericTypeArguments.Select(t => t.Assembly));
        }

        assemblies.AddRange(options.References);
        foreach (var assembly in assemblies)
        {
            if (assembly.Location.Length == 0)
            {
                throw new InvalidOperationException($"The assembly {assembly.GetName().Name} has no file path, so the validator cannot reference it.");
            }

            paths.Add(assembly.Location);
        }

        return paths.Distinct(StringComparer.Ordinal).Select(p => (MetadataReference)MetadataReference.CreateFromFile(p)).ToList();
    }

    private static HashSet<string> ProtectedNames(Type? type)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        Collect(type, names);
        return names;
    }

    private static void Collect(Type? type, HashSet<string> names)
    {
        if (type is null)
        {
            return;
        }

        if (type.HasElementType)
        {
            Collect(type.GetElementType(), names);
            return;
        }

        if (!(type.Namespace ?? string.Empty).StartsWith("System", StringComparison.Ordinal))
        {
            var name = type.Name;
            var tick = name.IndexOf('`', StringComparison.Ordinal);
            names.Add(tick < 0 ? name : name[..tick]);
        }

        foreach (var argument in type.GenericTypeArguments)
        {
            Collect(argument, names);
        }
    }
}
