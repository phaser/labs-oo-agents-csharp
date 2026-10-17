// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/runtime/context_manager.py (expression evaluation)

using System.Reflection;
using Microsoft.CodeAnalysis.CSharp;
using Nooa.ContextBlocks;
using Nooa.Errors;
using Nooa.Strategies;

namespace Nooa.Runtime;

/// <summary>
/// Turns the expression text of a <see cref="Context"/> block into a provider. Each evaluation runs the text as a cell
/// in a new C# session (<see cref="RoslynExecutionEngine"/>) where <c>self</c> is the agent.
/// </summary>
/// <remarks>
/// <para>
/// An expression is code, so it gets the same checks as a model cell: the text must be one expression, and the
/// <see cref="CellCodeValidator"/> must accept it with no restricted API group allowed. The session has a short time limit.
/// The expression must not call an <see cref="AgenticAttribute"/> method of the agent: a call would run the model inside
/// a prompt build.
/// </para>
/// <para>
/// An evaluation that hits the time limit or is abandoned marks its expression as failed, and the expression is not evaluated
/// again. An agent keeps at most <see cref="MaxAbandoned"/> abandoned evaluations (each holds a thread that cannot be stopped in
/// process): after that no expression runs. An agentic call that starts while an expression runs fails
/// (<see cref="AgentContext.EvaluatingExpression"/>).
/// </para>
/// <para>
/// Only code that the host or the model writes in the process reaches this class. A snapshot or a config file never
/// does: <c>AgentSnapshot</c> does not save a block with a provider, and no loader turns stored text into an expression.
/// A new session for each evaluation keeps one evaluation from leaking variables into the next, and an engine that
/// gives up a cell does not poison later turns. The cost is one compile per block per turn.
/// </para>
/// </remarks>
internal sealed class ContextExpressions(Agent agent, TimeSpan? timeout = null, TimeSpan? abandonGrace = null)
{
    /// <summary>The time limit of one evaluation.</summary>
    internal static readonly TimeSpan EvaluationTimeout = TimeSpan.FromSeconds(5);

    private static readonly string[] _extraImports = ["Nooa.Agentdoc"];

    /// <summary>The most abandoned evaluations that one agent keeps. A later evaluation is refused.</summary>
    internal const int MaxAbandoned = 3;

    private readonly Type _agentType = agent.GetType();
    private readonly TimeSpan _timeout = timeout ?? EvaluationTimeout;
    private readonly TimeSpan _abandonGrace = abandonGrace ?? new ReplOptions().AbandonGrace;
    private readonly HashSet<string> _failed = [];
    private readonly Lock _gate = new();
    private int _abandoned;

    /// <summary>Checks the expression now and returns a provider that evaluates it.</summary>
    /// <exception cref="ArgumentException">The text is empty, too long, or not one expression.</exception>
    /// <exception cref="RestrictedCodeException">The validator refuses the expression.</exception>
    /// <exception cref="InvalidOperationException">The agent type is not public, so the expression cannot name its members.</exception>
    public Func<object?> Bind(string expression)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);
        if (expression.Length > Context.MaxExpressionLength)
        {
            throw new ArgumentException($"The expression is longer than {Context.MaxExpressionLength} characters.", nameof(expression));
        }

        if (!_agentType.IsVisible)
        {
            throw new InvalidOperationException(
                $"A context expression needs a public agent type. {_agentType.Name} is not public, so the expression cannot name its members.");
        }

        RequireSingleExpression(expression);
        NewSession().Validator.Validate(expression);
        return () => Evaluate(expression);
    }

    private static void RequireSingleExpression(string text)
    {
        var parsed = SyntaxFactory.ParseExpression(text);
        if (parsed.GetDiagnostics().Any(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
            || parsed.FullSpan.Length != text.Length)
        {
            throw new ArgumentException("The text must be one C# expression, with no semicolon and no statement.", nameof(text));
        }
    }

    private object? Evaluate(string expression)
    {
        lock (_gate)
        {
            if (_failed.Contains(expression))
            {
                throw new ValidationException("This expression did not stop in time earlier, so it is not evaluated again. Set the block again with a faster expression.");
            }

            if (_abandoned >= MaxAbandoned)
            {
                throw new ValidationException($"{MaxAbandoned} expression evaluations of this agent did not stop. No expression is evaluated any more.");
            }
        }

        var (engine, _) = NewSession();
        var started = System.Diagnostics.Stopwatch.StartNew();
        CellResult result;
        AgentContext.EvaluatingExpression = true;
        try
        {
            result = Task.Run(() => engine.ExecuteAsync(expression)).GetAwaiter().GetResult();
        }
        finally
        {
            AgentContext.EvaluatingExpression = false;
        }

        var abandoned = engine.IsAbandoned;
        if (abandoned || started.Elapsed >= _timeout)
        {
            lock (_gate)
            {
                _failed.Add(expression);
                if (abandoned)
                {
                    _abandoned++;
                }
            }
        }

        return result.Status == ResultStatus.Complete
            ? result.Value
            : throw new ValidationException(result.Error);
    }

    private (RoslynExecutionEngine Engine, CellCodeValidator Validator) NewSession()
    {
        var types = new[] { _agentType };
        var imports = CodeActScope.Namespaces(types).Concat(_extraImports).Distinct(StringComparer.Ordinal).ToList();
        var references = CodeActScope.Assemblies(types);
        var globals = (ReplGlobals)Activator.CreateInstance(typeof(ReplGlobals<>).MakeGenericType(_agentType), agent)!;
        var validator = new CellCodeValidator(new CellValidatorOptions
        {
            GlobalsType = globals.GetType(),
            ForbiddenSelfCalls = AgenticMethodNames(_agentType),
            References = references,
            Imports = imports,
        });
        var engine = new RoslynExecutionEngine(globals, new ReplOptions
        {
            CellTimeout = _timeout,
            AbandonGrace = _abandonGrace,
            References = references,
            Imports = imports,
            Validator = validator,
        });
        return (engine, validator);
    }

    private static List<string> AgenticMethodNames(Type agentType)
    {
        var names = new List<string>();
        for (var type = agentType; type is not null && type != typeof(object); type = type.BaseType)
        {
            names.AddRange(
                type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .Where(m => m.IsDefined(typeof(AgenticAttribute), inherit: false))
                    .Select(m => m.Name));
        }

        return names;
    }
}
