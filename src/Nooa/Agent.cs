// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/agent.py

using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nooa.Agentdoc;
using Nooa.Config;
using Nooa.Errors;
using Nooa.Llm;
using Nooa.Runtime;
using Nooa.Storage;
using Nooa.Strategies;

namespace Nooa;

/// <summary>
/// Base class of every agent. Fields are state, methods are capabilities and the doc comments are prompts.
/// An <see cref="AgenticAttribute"/> method runs through <see cref="IAgenticTarget"/>: the runtime resolves the
/// strategy and the LLM, serializes generation and records events.
/// </summary>
/// <remarks>
/// <para>The LLM of a call comes from, in order:</para>
/// <list type="number">
/// <item>the call scope, <see cref="UseLlm(ILlmClient)"/>;</item>
/// <item>the method, <c>[AgentOptions(Llm = ...)]</c> on the method;</item>
/// <item>the instance, <see cref="AgentOptions"/> in the constructor;</item>
/// <item>the class, <c>[AgentOptions(Llm = ...)]</c> on the class or a base class;</item>
/// <item>the parent agent, when the agent is built inside the call of another agent.</item>
/// </list>
/// <para>
/// The last three are resolved in the constructor, so a missing LLM fails at construction. A model alias needs an
/// <see cref="ILlmClientResolver"/>: <see cref="AgentOptions.LlmResolver"/> or <see cref="DefaultLlmResolver"/>.
/// </para>
/// </remarks>
[NoSnapshot]
public abstract class Agent : IAgenticTarget
{
    private static volatile ILlmClientResolver? _defaultLlmResolver;

    private readonly Dictionary<string, ILlmClient> _aliasCache = [];
    private readonly Dictionary<Type, GenerationStrategy> _strategies = [];
    private readonly ILlmClientResolver? _llmResolver;
    private readonly GenerationStrategy? _defaultStrategy;
    private ILlmClient _llm;

    /// <summary>Creates the agent: ids, storage, event manager, protected context blocks and runtime.</summary>
    /// <exception cref="ArgumentException">The options set both an LLM client and an LLM alias.</exception>
    /// <exception cref="NooaRuntimeException">No LLM can be resolved.</exception>
    protected Agent(AgentOptions? options = null)
    {
        options ??= new AgentOptions();
        if (options.Llm is not null && options.LlmAlias is not null)
        {
            throw new ArgumentException("Set either Llm or LlmAlias in the agent options, not both.", nameof(options));
        }

        AgentId = Guid.NewGuid().ToString();
        Logger = options.Logger ?? NullLogger.Instance;
        _llmResolver = options.LlmResolver;
        _defaultStrategy = options.DefaultStrategy;
        foreach (var strategy in options.Strategies ?? [])
        {
            _strategies[strategy.GetType()] = strategy;
        }

        Storage = options.Storage ?? new InMemoryStorageManager();
        EventManager = new EventManager(Storage.EventBackend, Logger);
        _llm = ResolveInstanceLlm(options);
        Truncation = TruncationConfig.Default.MergeWith(options.Truncation);
        Execution = options.Execution ?? new ExecutionConfig();

        ContextManager = new ContextManager();
        ContextManager.SetStaticProtected(ContextManager.SystemPromptKey, ResolveSystemPrompt);
        ContextManager.SetStaticProtected(ContextManager.SelfKey, () => AgentDocs.Doc(GetType()));
        ContextManager.SetDynamicProtected(
            ContextManager.StateKey, () => Agentdoc.PFormat.Format(this, maxLength: 50, maxString: 500, maxDepth: 4));
        Runtime = new ActorRuntime(this);
    }

    /// <summary>
    /// The resolver for model aliases when the agent options set none. The application sets it once at startup.
    /// </summary>
    public static ILlmClientResolver? DefaultLlmResolver
    {
        get => _defaultLlmResolver;
        set => _defaultLlmResolver = value;
    }

    /// <summary>The unique id of this agent instance.</summary>
    [Hidden]
    public string AgentId { get; }

    /// <summary>The runtime of this agent.</summary>
    [Hidden]
    public ActorRuntime Runtime { get; }

    /// <summary>The event manager. The agent owns it. The storage owns only the backend under it.</summary>
    [Hidden]
    public EventManager EventManager { get; }

    /// <summary>The store of the context blocks, with the protected blocks <c>system_prompt</c>, <c>self</c> and <c>state</c>.</summary>
    [Hidden]
    public ContextManager ContextManager { get; }

    /// <summary>The storage of this agent.</summary>
    [Hidden]
    public IStorageManager Storage { get; }

    /// <summary>The truncation settings: the defaults merged with the options.</summary>
    [Hidden]
    public TruncationConfig Truncation { get; }

    /// <summary>The execution guards.</summary>
    [Hidden]
    public ExecutionConfig Execution { get; }

    /// <summary>The logger for handler, hook and cleanup warnings.</summary>
    [Hidden]
    public ILogger Logger { get; }

    /// <summary>The LLM client of this agent: the instance client, the class alias or the parent client.</summary>
    [Hidden]
    public ILlmClient Llm => _llm;

    /// <summary>Replaces the LLM client of this agent, for example when a host switches the model.</summary>
    [Hidden]
    public void SetLlm(ILlmClient llm) => _llm = llm ?? throw new ArgumentNullException(nameof(llm));

    /// <summary>
    /// Makes every call of this agent inside the scope use <paramref name="client"/> (the call-level LLM).
    /// Dispose the result to end the scope.
    /// </summary>
    [Hidden]
    public IDisposable UseLlm(ILlmClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        var previous = AgentContext.LlmOverride;
        AgentContext.LlmOverride = (this, client);
        return new Subscription(() => AgentContext.LlmOverride = previous);
    }

    /// <summary>Like <see cref="UseLlm(ILlmClient)"/>, with a model alias that the agent resolves now.</summary>
    [Hidden]
    public IDisposable UseLlm(string alias) => UseLlm(ResolveAlias(alias, "UseLlm"));

    /// <summary>Makes every call of this agent inside the scope use <paramref name="strategy"/> (the call-level strategy).</summary>
    [Hidden]
    public IDisposable UseStrategy(GenerationStrategy strategy)
    {
        ArgumentNullException.ThrowIfNull(strategy);
        var previous = AgentContext.StrategyOverride;
        AgentContext.StrategyOverride = (this, strategy);
        return new Subscription(() => AgentContext.StrategyOverride = previous);
    }

    /// <summary>
    /// Runs registered background cleanup. The agent does not own its LLM client or its storage.
    /// Close those after this method returns.
    /// </summary>
    [Hidden]
    public Task CloseAsync() => EventManager.CloseAsync();

    /// <summary>
    /// Renders the system prompt: the class doc comment of the nearest class that has one, with placeholders filled in.
    /// </summary>
    [Hidden]
    public string ResolveSystemPrompt()
    {
        for (var type = GetType(); type is not null; type = type.BaseType)
        {
            if (AgentMetadataRegistry.TryGet(type, out var metadata) && metadata?.SystemPrompt is { } render)
            {
                return render(this);
            }
        }

        return $"You are {GetType().Name}, a C# agent working in an interactive session.";
    }

    /// <inheritdoc />
    async Task<T> IAgenticTarget.InvokeAgenticAsync<T>(CurrentCall call, CancellationToken cancellationToken)
    {
        var result = await Runtime.InvokeAsync(call, cancellationToken).ConfigureAwait(false);
        return result switch
        {
            T typed => typed,
            null when default(T) is null => default!,
            null => throw new GenerationException(
                $"{call.MethodName} returned no value, but its result type {typeof(T).Name} is not nullable."),
            JsonElement element => element.Deserialize<T>(Json.NooaJson.Options)!,
            _ => throw new GenerationException(
                $"{call.MethodName} returned a {result.GetType().Name}, which is not a {typeof(T).Name}."),
        };
    }

    /// <inheritdoc />
    async Task IAgenticTarget.InvokeAgenticAsync(CurrentCall call, CancellationToken cancellationToken) =>
        await Runtime.InvokeAsync(call, cancellationToken).ConfigureAwait(false);

    /// <summary>Resolves a model alias through the resolver and caches the client for this instance.</summary>
    internal ILlmClient ResolveAlias(string alias, string origin)
    {
        lock (_aliasCache)
        {
            if (_aliasCache.TryGetValue(alias, out var cached))
            {
                return cached;
            }
        }

        var resolver = _llmResolver ?? _defaultLlmResolver
            ?? throw new NooaRuntimeException(
                $"The model alias '{alias}' ({origin}) on {GetType().Name} needs an ILlmClientResolver. "
                + "Set AgentOptions.LlmResolver or Agent.DefaultLlmResolver.");
        ILlmClient client;
        try
        {
            client = resolver.Resolve(alias);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new NooaRuntimeException(
                $"The model alias '{alias}' ({origin}) on {GetType().Name} could not be resolved: {ex.GetType().Name}: {ex.Message}", ex);
        }

        lock (_aliasCache)
        {
            _aliasCache[alias] = client;
        }

        return client;
    }

    /// <summary>The strategy cascade of a call: call override, then the method attribute, then the agent default.</summary>
    internal GenerationStrategy ResolveStrategy(CurrentCall call)
    {
        if (AgentContext.StrategyOverride is { } scoped && ReferenceEquals(scoped.Agent, this))
        {
            return scoped.Strategy;
        }

        if (call.StrategyType is { } type)
        {
            lock (_strategies)
            {
                if (_strategies.TryGetValue(type, out var known))
                {
                    return known;
                }

                if (!typeof(GenerationStrategy).IsAssignableFrom(type) || type.IsAbstract || type.GetConstructor(Type.EmptyTypes) is null)
                {
                    throw new NooaRuntimeException(
                        $"The strategy type {type.FullName} of {call.MethodName} must derive from GenerationStrategy and have a public parameterless constructor, or the agent options must supply an instance.");
                }

                return _strategies[type] = (GenerationStrategy)Activator.CreateInstance(type)!;
            }
        }

        return _defaultStrategy
            ?? throw new NooaRuntimeException(
                $"No generation strategy for {call.MethodName}. Add [Strategy(typeof(...))] to the method, "
                + "set AgentOptions.DefaultStrategy, or use Agent.UseStrategy.");
    }

    private ILlmClient ResolveInstanceLlm(AgentOptions options)
    {
        if (options.Llm is not null)
        {
            return options.Llm;
        }

        if (options.LlmAlias is not null)
        {
            return ResolveAlias(options.LlmAlias, "agent options");
        }

        for (var type = GetType(); type is not null; type = type.BaseType)
        {
            if (AgentMetadataRegistry.TryGet(type, out var metadata) && metadata?.Llm is { Length: > 0 } alias)
            {
                return ResolveAlias(alias, $"[AgentOptions] on {type.Name}");
            }
        }

        if (AgentContext.ParentAgent is { } parent)
        {
            return parent.Llm;
        }

        throw new NooaRuntimeException(
            $"No LLM available for {GetType().Name}. Resolution attempted:\n"
            + "  1. Instance: AgentOptions.Llm or AgentOptions.LlmAlias not set\n"
            + "  2. Class: no [AgentOptions(Llm = ...)] on the class or a base class\n"
            + "  3. Parent: no parent agent in the current call\n\n"
            + "Solutions:\n"
            + "  - Pass AgentOptions { Llm = client } to the constructor\n"
            + "  - Add [AgentOptions(Llm = \"alias\")] to the class\n"
            + "  - Build the agent inside the call of a parent agent");
    }
}
