// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/strategies/codeact.py

using Nooa.Config;
using Nooa.ContextBlocks;
using Nooa.Errors;
using Nooa.Runtime;

namespace Nooa.Strategies;

/// <summary>
/// The default strategy. The model works in a C# session: it runs code cells with <c>execute_csharp</c> and ends with
/// <c>return_result</c> (also callable in a cell as <c>ReturnResult(value)</c>). The task parameters are typed variables, <c>self</c> is the
/// agent, and variables persist across cells. The loop ends with the result, or with a <see cref="GenerationException"/> when the
/// iterations or retries run out.
/// </summary>
/// <remarks>
/// <para>
/// Each call gets its own <see cref="RoslynExecutionEngine"/> and its own <see cref="CellCodeValidator"/>. The validator checks every
/// cell before it compiles, with the running method in <see cref="CellValidatorOptions.ForbiddenSelfCalls"/>, so the model cannot recurse.
/// The cells run in the host process: this is a guardrail and not a security boundary (D12). Do not expose a type to the agent
/// that the model must not use, such as <see cref="Nooa.Tools.ShellTools"/>, on a model that you do not trust.
/// </para>
/// <para>
/// The agent type must be public, so that cell code can name its members. A cell sees the namespaces of the agent type, the return type
/// and the argument types. The differences from upstream are in <c>docs/porting-notes.md</c> (T17).
/// </para>
/// </remarks>
public sealed class CodeActStrategy : GenerationStrategy
{
    private const string _strategyPromptKey = "strategy_prompt";
    private const string _executionContextKey = "execution_context";

    private static readonly IReadOnlySet<string> _staticKeys = new HashSet<string> { _executionContextKey, _strategyPromptKey };
    private static readonly IReadOnlyList<string> _blockOrder = ["system_prompt", _strategyPromptKey, _executionContextKey, "self"];

    /// <summary>Creates the strategy with the default config.</summary>
    public CodeActStrategy()
        : this(new CodeActConfig())
    {
    }

    /// <summary>Creates the strategy.</summary>
    /// <param name="config">Limits, timeouts, prefill, conditions and allowed APIs.</param>
    /// <exception cref="ConfigValidationException">The config is not valid.</exception>
    public CodeActStrategy(CodeActConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        config.Validate();
        Config = config;
    }

    /// <summary>The config of this strategy.</summary>
    public CodeActConfig Config { get; }

    /// <inheritdoc />
    public override string Name => "CODEACT";

    /// <inheritdoc />
    public override IReadOnlyDictionary<string, object?>? GetBlockOverrides() =>
        new Dictionary<string, object?>
        {
            [_strategyPromptKey] = new DynamicContext(() => CodeActPrompts.StrategyInstructions(Config.AllowedApis)),
            [_executionContextKey] = new DynamicContext(ExecutionContext),
        };

    /// <inheritdoc />
    public override IReadOnlySet<string>? GetStaticBlockKeys() => _staticKeys;

    /// <inheritdoc />
    public override IReadOnlyList<string>? GetBlockOrder() => _blockOrder;

    /// <inheritdoc />
    /// <exception cref="GenerationException">
    /// The agent type is not public, the arguments cannot be declared, the loop runs out of iterations or retries,
    /// the model fails, or a cell does not stop after its time limit.
    /// </exception>
    /// <exception cref="LoopDetectedException">The loop guard stops a repeated tool call.</exception>
    public override async Task<object?> ExecuteAsync(IRuntimeServices runtime, CurrentCall call, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(call);
        var agentType = runtime.Agent.GetType();
        if (!agentType.IsVisible)
        {
            throw new GenerationException(
                $"CodeAct needs a public agent type. {agentType.Name} is not public, so cell code cannot name its members.");
        }

        var types = CodeActScope.Types(agentType, call);
        var imports = CodeActScope.Namespaces(types);
        var references = CodeActScope.Assemblies(types);
        var globals = (ReplGlobals)Activator.CreateInstance(typeof(ReplGlobals<>).MakeGenericType(agentType), runtime.Agent)!;
        var validator = new CellCodeValidator(new CellValidatorOptions
        {
            Allow = Config.AllowedApis,
            GlobalsType = globals.GetType(),
            ForbiddenSelfCalls = [call.MethodName],
            ReturnType = call.ReturnType,
            References = references,
            Imports = imports,
        });
        var engine = new RoslynExecutionEngine(globals, new ReplOptions
        {
            CellTimeout = Config.CellTimeout ?? ReplOptions.DefaultCellTimeout,
            Capture = runtime.TruncationConfig.Capture,
            References = references,
            Imports = imports,
            Validator = validator,
        });

        var arguments = call.Arguments.Select(a => new ReplArgument(a.Name, a.Type, a.Value)).ToList();
        try
        {
            await engine.DeclareArgumentsAsync(arguments, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException e)
        {
            throw new GenerationException($"CodeAct cannot declare the arguments of `{call.MethodName}`: {e.Message}", e);
        }

        TeachValidatorTheArguments(validator, arguments);
        return await new CodeActRun(Name, Config, runtime, call, engine).RunAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string ExecutionContext()
    {
        var call = AgentContext.CurrentCall;
        var agent = AgentContext.ParentAgent;
        return call is null || agent is null
            ? "## Execution Context\n\nThe agent instance (`self`) and the C# standard names are available."
            : CodeActPrompts.ExecutionContext(agent.GetType(), call);
    }

    /// <summary>
    /// The engine declares the arguments in a hidden cell, which the validator does not see. A declaration with the same
    /// types, run through the validator first, gives the names their real types in every later cell.
    /// </summary>
    private static void TeachValidatorTheArguments(CellCodeValidator validator, IReadOnlyList<ReplArgument> arguments)
    {
        if (arguments.Count == 0)
        {
            return;
        }

        var declarations = string.Join('\n', arguments.Select(a => $"{CSharpTypeName.Of(a.Type)} {a.Identifier} = default!;"));
        try
        {
            validator.Validate(declarations);
        }
        catch (RestrictedCodeException)
        {
            // A parameter of a restricted type (for example Type). The validator then refuses the names of restricted types by text,
            // which is the fail-closed fallback of T16.
        }
    }
}
