// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/runtime/actor.py (execute_code)

using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Scripting;
using Nooa.Agentdoc;
using Nooa.ContextBlocks;
using Nooa.Errors;

namespace Nooa.Runtime;

/// <summary>
/// Runs code cells with Roslyn scripting. The cells form a chain, so a variable that one cell defines stays for the next cells.
/// </summary>
/// <remarks>
/// <para>
/// <b>This class is not a security boundary (D12).</b> The cells run in the host process with the rights of the host.
/// The reference list, the import list, the refusal of <c>#r</c> and <c>#load</c> and the optional <see cref="ICellValidator"/>
/// are guardrails against mistakes. They do not stop hostile code. The reference list hides assemblies that are not in the
/// list (for example <c>System.Console</c> and <c>System.Diagnostics.Process</c>), but it cannot hide the types of the core library
/// (for example <c>System.IO.File</c> and <c>System.Reflection</c>). A process sandbox is backlog item B01.
/// </para>
/// <para>
/// <b>State.</b> A cell that does not compile changes nothing. A cell that throws keeps the variables that it assigned before the throw.
/// A cell that times out or is cancelled and then stops keeps its variables in the same way.
/// </para>
/// <para>
/// <b>Time limit.</b> The limit is cooperative. It cancels <see cref="ReplGlobals.Cancellation"/>. Code that never checks the token,
/// for example a tight loop, cannot be stopped inside the process. After <see cref="ReplOptions.AbandonGrace"/> the engine gives up the cell,
/// returns an error, and refuses all later cells (backlog item B01 is the real fix).
/// </para>
/// <para>
/// <b>Memory.</b> Roslyn loads each cell as an assembly that the runtime does not unload. The memory grows with every cell.
/// See <c>knowledge/porting/knowledge.md</c> for the measured growth.
/// </para>
/// <para>One cell runs at a time. A second call while a cell runs throws <see cref="InvalidOperationException"/>.</para>
/// </remarks>
public sealed class RoslynExecutionEngine : IExecutionEngine
{
    private const string _argumentsFile = "Arguments";

    internal static readonly string[] FrameworkAssemblies =
    [
        "System.Runtime",
        "System.Collections",
        "System.Linq",
        "System.Text.Json",
    ];

    internal static readonly string[] DefaultImports =
    [
        "System",
        "System.Collections.Generic",
        "System.Linq",
        "System.Text",
        "System.Text.Json",
        "System.Threading",
        "System.Threading.Tasks",
    ];

    private readonly ReplGlobals _globals;
    private readonly ReplOptions _options;
    private readonly ScriptOptions _scriptOptions;
    private ScriptState<object>? _state;
    private int _executionCount;
    private int _running;
    private bool _abandoned;

    /// <summary>Creates an engine for a globals object. The globals type must be public.</summary>
    /// <exception cref="InvalidOperationException">An assembly has no file path (a single-file app), so a script cannot reference it.</exception>
    public RoslynExecutionEngine(ReplGlobals globals, ReplOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(globals);
        _globals = globals;
        _options = options ?? new ReplOptions();
        _scriptOptions = BuildScriptOptions(globals, _options);
    }

    /// <inheritdoc />
    public int ExecutionCount => _executionCount;

    /// <inheritdoc />
    public OutAccessor Out => _globals.Out;

    /// <inheritdoc />
    public bool IsAbandoned => _abandoned;

    /// <inheritdoc />
    public bool TryGetVariable(string name, out object? value)
    {
        ArgumentNullException.ThrowIfNull(name);
        var variable = _state?.GetVariable(name);
        value = variable?.Value;
        return variable is not null;
    }

    /// <inheritdoc />
    public async Task DeclareArgumentsAsync(IReadOnlyList<ReplArgument> arguments, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        BeginCall();
        try
        {
            var values = new Dictionary<string, object?>();
            var source = new StringBuilder();
            foreach (var argument in arguments)
            {
                if (!SyntaxFacts.IsValidIdentifier(argument.Name) || !values.TryAdd(argument.Name, argument.Value))
                {
                    throw new InvalidOperationException($"The argument name '{argument.Name}' is not a valid and unique C# identifier.");
                }

                if (!argument.Type.IsVisible)
                {
                    throw new InvalidOperationException($"The type of argument '{argument.Name}' ({argument.Type}) is not public, so the script cannot name it.");
                }

                var type = CSharpTypeName.Of(argument.Type);
                source.Append($"{type} {argument.Identifier} = ({type})__args[\"{argument.Name}\"];\n");
            }

            _globals.__args = values;
            _state ??= await CSharpScript.RunAsync(string.Empty, _scriptOptions, _globals, _globals.GetType(), cancellationToken).ConfigureAwait(false);
            try
            {
                _state = await _state.ContinueWithAsync(source.ToString(), _scriptOptions.WithFilePath(_argumentsFile), null, cancellationToken).ConfigureAwait(false);
            }
            catch (CompilationErrorException e)
            {
                throw new InvalidOperationException("The arguments cannot be declared:\n" + ErrorFormatting.FormatDiagnostics(e.Diagnostics), e);
            }
        }
        finally
        {
            EndCall();
        }
    }

    /// <inheritdoc />
    public async Task<CellResult> ExecuteAsync(string code, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(code);
        BeginCall();
        try
        {
            return await RunCellAsync(code, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _globals.Console.Attach(TextWriter.Null, TextWriter.Null);
            EndCall();
        }
    }

    private async Task<CellResult> RunCellAsync(string code, CancellationToken caller)
    {
        var count = ++_executionCount;
        var cleaned = ResponseCleanup.StripCodeFences(code).Code;
        var capture = _options.Capture;
        using var stdout = new TruncatingTextWriter(capture.MaxStdout, capture.Tail);
        using var stderr = new TruncatingTextWriter(capture.MaxStderr, capture.Tail);
        _globals.Console.Attach(TextWriter.Synchronized(stdout), TextWriter.Synchronized(stderr));

        CellResult Failed(Exception error, ResultStatus status = ResultStatus.Error) => new(
            count,
            status,
            stdout.ToString(),
            stderr.ToString(),
            ErrorFormatting.FormatForLlm(error, cleaned, count, capture.MaxError, capture.Tail));

        SyntaxTree tree;
        try
        {
            tree = Preprocess(cleaned);
            _options.Validator?.Validate(cleaned);
        }
        catch (ValidationException e)
        {
            return Failed(e);
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(caller);
        if (_options.CellTimeout is { } timeout)
        {
            cts.CancelAfter(timeout);
        }

        _globals.BeginCell(cts.Token);
        var options = _scriptOptions.WithFilePath($"Cell In[{count}]");
        var run = Task.Run(
            async () =>
            {
                _state ??= await CSharpScript.RunAsync(string.Empty, _scriptOptions, _globals, _globals.GetType(), cts.Token).ConfigureAwait(false);
                return await _state.ContinueWithAsync(cleaned, options, _ => true, cts.Token).ConfigureAwait(false);
            },
            CancellationToken.None);

        var finished = await Task.WhenAny(run, GiveUpAfterCancelAsync(cts.Token, _options.AbandonGrace)).ConfigureAwait(false);
        if (finished != run)
        {
            _abandoned = true;
            _ = run.ContinueWith(t => _ = t.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
            return Failed(
                new TimeoutException(
                    $"Cell In[{count}] did not stop {_options.AbandonGrace.TotalSeconds:0.##} seconds after the time limit or the cancel. "
                    + "The session cannot run more cells. Check for infinite loops, and pass the Cancellation token to long calls."),
                caller.IsCancellationRequested ? ResultStatus.Cancelled : ResultStatus.Error);
        }

        ScriptState<object> next;
        try
        {
            next = await run.ConfigureAwait(false);
        }
        catch (CompilationErrorException e)
        {
            return Failed(e);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            return Interrupted();
        }

        _state = next;
        switch (next.Exception)
        {
            case ReplReturnSignal:
                return Completed(_globals.ReturnedValue, explicitReturn: true);
            case OperationCanceledException when cts.IsCancellationRequested:
                return Interrupted();
            case { } error:
                return Failed(error);
            default:
                return _globals.HasReturned
                    ? Completed(_globals.ReturnedValue, explicitReturn: true)
                    : Completed(next.ReturnValue, HasTopLevelReturn(tree));
        }

        CellResult Completed(object? value, bool explicitReturn)
        {
            _globals.Out.Record(count, value);
            return new CellResult(count, ResultStatus.Complete, stdout.ToString(), stderr.ToString(), Value: value, ExplicitReturn: explicitReturn);
        }

        CellResult Interrupted()
        {
            if (caller.IsCancellationRequested)
            {
                return Failed(new OperationCanceledException($"Cell In[{count}] was cancelled."), ResultStatus.Cancelled);
            }

            var seconds = _options.CellTimeout!.Value.TotalSeconds;
            return Failed(new TimeoutException($"Code execution timed out after {seconds:0.##} seconds. Check for infinite loops or blocking operations."));
        }
    }

    private void BeginCall()
    {
        if (_abandoned)
        {
            throw new InvalidOperationException("An earlier cell did not stop after its time limit. The session state is not safe. Create a new engine.");
        }

        if (Interlocked.Exchange(ref _running, 1) == 1)
        {
            throw new InvalidOperationException("A cell is already running. An engine runs one cell at a time.");
        }
    }

    private void EndCall() => Volatile.Write(ref _running, 0);

    private static async Task GiveUpAfterCancelAsync(CancellationToken cancelled, TimeSpan grace)
    {
        try
        {
            await Task.Delay(Timeout.Infinite, cancelled).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }

        await Task.Delay(grace, CancellationToken.None).ConfigureAwait(false);
    }

    private static SyntaxTree Preprocess(string code)
    {
        var tree = CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(kind: SourceCodeKind.Script));
        foreach (var trivia in tree.GetRoot().DescendantTrivia(descendIntoTrivia: true))
        {
            if (trivia.GetStructure() is ReferenceDirectiveTriviaSyntax or LoadDirectiveTriviaSyntax)
            {
                var line = trivia.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                throw new RestrictedCodeException(
                    "The directives #r and #load are not allowed. Use the namespaces that are already available.",
                    line);
            }
        }

        return tree;
    }

    private static bool HasTopLevelReturn(SyntaxTree tree) =>
        tree.GetRoot()
            .DescendantNodes(n => n is not (LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax or BaseMethodDeclarationSyntax or BaseTypeDeclarationSyntax))
            .OfType<ReturnStatementSyntax>()
            .Any();

    private static ScriptOptions BuildScriptOptions(ReplGlobals globals, ReplOptions options)
    {
        var directory = RuntimeEnvironment.GetRuntimeDirectory();
        var paths = FrameworkAssemblies.Select(name => Path.Combine(directory, name + ".dll")).ToList();
        var assemblies = new List<Assembly> { typeof(ReplGlobals).Assembly, globals.GetType().Assembly };
        assemblies.AddRange(globals.GetType().GenericTypeArguments.Select(t => t.Assembly));
        assemblies.AddRange(options.References);
        foreach (var assembly in assemblies)
        {
            if (assembly.Location.Length == 0)
            {
                throw new InvalidOperationException($"The assembly {assembly.GetName().Name} has no file path, so a script cannot reference it.");
            }

            paths.Add(assembly.Location);
        }

        return ScriptOptions.Default
            .WithReferences(paths.Distinct(StringComparer.Ordinal).Select(p => MetadataReference.CreateFromFile(p)))
            .WithImports(DefaultImports.Concat(options.Imports).Distinct(StringComparer.Ordinal))
            .WithEmitDebugInformation(true)
            .WithFileEncoding(Encoding.UTF8)
            .WithAllowUnsafe(false);
    }
}
