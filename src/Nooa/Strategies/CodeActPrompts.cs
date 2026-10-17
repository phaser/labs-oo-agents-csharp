// SPDX-FileCopyrightText: Copyright (c) 2026, NVIDIA CORPORATION & AFFILIATES. All rights reserved.
// SPDX-FileCopyrightText: Copyright (c) 2026 Cristian Bidea
// SPDX-License-Identifier: Apache-2.0
// Ported from: src/nooa/strategies/codeact.py

using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using Nooa.Agentdoc;
using Nooa.Llm;
using Nooa.Runtime;

namespace Nooa.Strategies;

/// <summary>
/// The model-facing text of the CodeAct strategy, written for C# cells: the strategy instructions, the execution context,
/// the tool definitions and the feedback messages. Text that embeds a model value bounds it first. The renderer escapes
/// the text of events when it builds the prompt.
/// </summary>
internal static class CodeActPrompts
{
    /// <summary>The name of the tool that runs a code cell.</summary>
    public const string CodeToolName = "execute_csharp";

    /// <summary>The name of the tool that submits the final answer.</summary>
    public const string ReturnToolName = "return_result";

    /// <summary>The receipt text of a cell tool call. It must not change after a generation has seen it.</summary>
    public const string CellReceipt = "status: accepted";

    /// <summary>The message of the error when a response stops at its output-token limit.</summary>
    public const string OutputTokensExhausted =
        "The model used all available output tokens before completing a tool call. "
        + "Increase `max_tokens` (16384 or more is often needed for reasoning models).";

    private const int _maxShownName = 80;
    private const int _maxShownCall = 300;
    private const int _maxReturnDocChars = 1200;

    /// <summary>The strategy instructions: how to use the two tools, and what a cell must not do.</summary>
    /// <param name="allowed">The groups of restricted APIs that the config allows. The restrictions list leaves them out.</param>
    public static string StrategyInstructions(RestrictedApiGroup allowed) =>
        $$"""
        ## Strategy

        Jupyter-like C# session (Roslyn script cells). The task parameters are already declared as typed variables, and variables, local functions and `using` directives persist across cells. Use `await` directly, `Print`/`PPrint` to debug, `Doc(obj)` to inspect types. You MUST call a tool each turn — **plain-text responses do NOT end the session**. To finish, call `return_result(value)`. Repeated text-only responses will abort the run with an error.

        **Your two tools:**
        - `execute_csharp(code)` — run a code cell
        - `return_result(value)` — submit your final answer (also callable from inside `execute_csharp` as `ReturnResult(value)`)

        ## When to use which tool

        Use `return_result(...)` directly for simple answers determinable from the inputs alone (yes/no, one field, a single lookup).

        Use `execute_csharp(...)` for lists/batches, arithmetic, multi-step computation, transforms, or iteration. Always iterate in code — never construct large collections by hand.

        For language tasks (classification, extraction, interpretation), use LLM reasoning — answer directly via `return_result`, or call a method on `self` that runs an LLM (see `Doc(self)`). Don't keyword-match or use regular expressions.

        ## Returning computed results

        After computing in code, call `ReturnResult(variable)` **from within** `execute_csharp`. This passes the object directly. Do NOT re-type computed values in a separate `return_result` tool call. A top-level `return value;` also ends the cell with that value. The value of the last expression is shown as `Out[n]` and does not finish the task.

        ## Helpers

        Define local functions at the top of the cell and call them by name. Methods on `self` are usable with `await self.Method(...)`. Local functions and variables persist across cells in this session. Declare no types: use tuples, anonymous types, lambdas and local functions.

        ```csharp
        string Normalize(string x) => x.Trim().ToLowerInvariant();

        var cleaned = values.Select(Normalize).ToList();
        ```

        ## Delegation

        ### LLM calls

        For per-item LLM work over a list, call a method on `self` that has an LLM strategy (see `Doc(self)`). Start the calls, keep each task in a variable, then `await Task.WhenAll(tasks)` to run them in parallel.

        ```csharp
        var tasks = messages.Select(m => self.DetectLanguage(m)).ToList();
        var codes = await Task.WhenAll(tasks);
        ReturnResult(codes);
        ```

        ### Subagents

        If `self` exposes a delegation method, use it for bounded work that benefits from an independent context. Inspect its documentation with `Doc(...)`. A delegated task must be strictly simpler than the current call to avoid infinite recursion.

        ## Restrictions (the cell is refused before it runs)

        {{Restrictions(allowed)}}
        """;

    /// <summary>The context block that lists what is in scope inside a cell.</summary>
    /// <param name="agentType">The type of the agent that runs the call.</param>
    /// <param name="call">The call. Its arguments are declared as variables.</param>
    public static string ExecutionContext(Type agentType, CurrentCall call)
    {
        var namespaces = CodeActScope.Namespaces(CodeActScope.Types(agentType, call));
        var usings = RoslynExecutionEngine.DefaultImports.Concat(namespaces).Select(n => $"using {n};");
        var text = new StringBuilder();
        text.Append("## Execution Context\n\n");
        text.Append($"These names are already in scope inside `{CodeToolName}()` (state persists across cells). Call them, do not declare them again. ");
        text.Append("Use `Doc(name)` to inspect any type or method in detail.\n\n");
        text.Append("```csharp\n").Append(string.Join('\n', usings)).Append("\n```\n");
        if (call.Arguments.Count > 0)
        {
            var parameters = call.Arguments.Select(a => $"`{TypeNameFormatter.Format(a.Type)} {a.Name}`");
            text.Append($"Task parameters (typed variables): {string.Join(", ", parameters)}.\n");
        }

        text.Append($"Always available: `self` (the agent, a `{TypeNameFormatter.Format(agentType)}`), `Print(value)`, `PPrint(value)`, `Doc(obj)`, `ReturnResult(value)`, `Out[n]`, `Cancellation`.");
        return text.ToString();
    }

    /// <summary>The task message that opens a call.</summary>
    public static string TaskMessage(CurrentCall call) =>
        $"""
        ## Task: {call.MethodName}

        {call.Docstring}

        You are executing `{call.MethodName}` — code runs in the Execution Context above. Calling `self.{call.MethodName}(...)` would recurse.
        """;

    /// <summary>The correction that follows a reply with text and no tool call.</summary>
    public static string TextOnlyCorrection(CurrentCall call, string? validationError = null) =>
        "Your last reply was plain text with no tool call. It was preserved, "
        + "but a bare message cannot end the turn or run code. "
        + $"To finish `{call.MethodName}`, call `return_result(value)`. "
        + $"To do more work, call `{CodeToolName}(code)`. "
        + "Re-issue your response now as one of those tool calls."
        + (validationError is null ? "" : $"\n\nThe attempted result was invalid:\n{validationError}");

    /// <summary>The reminder after an empty response.</summary>
    public static string ToolUseReminder(string reason) =>
        $"{reason} Use `{CodeToolName}(code)` to run code, or `return_result(...)` to submit your answer.";

    /// <summary>The error of a cell tool call whose code is empty. It names misplaced argument keys.</summary>
    public static string EmptyCodeError(JsonObject args)
    {
        var extra = args.Select(p => p.Key).Where(k => k != "code").Order(StringComparer.Ordinal).Select(Bound).ToList();
        if (args.ContainsKey("code") || extra.Count == 0)
        {
            return "Execution error: empty code provided.";
        }

        return $"Execution error: {CodeToolName} takes a single `code` argument containing C# source, "
            + $"but received {string.Join(", ", extra.Select(k => $"`{k}`"))}. "
            + "Pass C# source as {\"code\": \"...\"}; inspect Print(Doc(self)) for the agent's APIs.";
    }

    /// <summary>Explains how to express a rejected tool call as a cell. The text shows a concrete cell for the rejected name.</summary>
    public static string UnknownToolFeedback(string toolName, JsonObject args, Type agentType)
    {
        var shown = Bound(toolName);
        var header = $"Unknown tool `{shown}`. Available tools: {CodeToolName}, {ReturnToolName}.";
        var path = (toolName.StartsWith("self.", StringComparison.Ordinal) ? toolName["self.".Length..] : toolName).Split('.');
        var expression = "self." + string.Join('.', path);
        var target = Resolve(agentType, path);
        string code;
        string hint;
        if (target is null)
        {
            code = "Print(Doc(self));";
            hint = $"`{Bound(expression)}` does not exist. Inspect the available APIs with the cell above, then call them inside {CodeToolName}.";
        }
        else if (target is MethodInfo method)
        {
            var call = $"{expression}({string.Join(", ", args.Select(p => $"{p.Key}: {Literal(p.Value)}"))})";
            call = call.Length > _maxShownCall ? $"{expression}(...)" : call;
            var awaited = typeof(Task).IsAssignableFrom(method.ReturnType)
                || (method.ReturnType.IsGenericType && method.ReturnType.GetGenericTypeDefinition() == typeof(ValueTask<>));
            code = $"var result = {(awaited ? "await " : "")}{call};\nPrint(result);";
            hint = $"Check the arguments with Print(Doc({expression})) inside {CodeToolName} if unsure.";
        }
        else
        {
            code = $"Print(Doc({expression}));";
            hint = $"`{Bound(expression)}` is an object, not a function. After inspecting it, call its documented methods as {Bound(expression)}.Method(...) inside {CodeToolName}; await async methods.";
        }

        var arguments = new JsonObject { ["code"] = code }.ToJsonString();
        return $"{header}\n\n"
            + $"Call the {CodeToolName} tool and put C# code in its `code` argument:\n\n"
            + $"Tool name: {CodeToolName}\n"
            + $"Arguments: {arguments}\n\n"
            + $"{hint} Do not call `{shown}` as a tool again.";
    }

    /// <summary>The definition of the tool that runs a cell.</summary>
    public static ToolDefinition CodeTool() =>
        new(
            CodeToolName,
            "Run C# code in the agent's environment. Variables and local functions persist across calls. "
            + "Access `self`, the task parameters and the namespaces of the Execution Context. "
            + "Use `ReturnResult(value)` or `return value;` to finish with a result.",
            new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["code"] = new JsonObject { ["type"] = "string", ["description"] = "C# code to run." },
                },
                ["required"] = new JsonArray("code"),
            });

    /// <summary>
    /// The definition of the <c>return_result</c> tool. The schema is <c>{"result": &lt;return type&gt;}</c>. A type with no
    /// JSON schema falls back to an open schema, and the description tells the model to build the object in code.
    /// </summary>
    /// <param name="returnType">The return type, or null when the method returns nothing.</param>
    public static ToolDefinition ReturnTool(Type? returnType)
    {
        if (returnType is null)
        {
            return new ToolDefinition(
                ReturnToolName,
                "Signal task completion. Call this when you have finished the task. No parameters required.",
                new JsonObject { ["type"] = "object", ["properties"] = new JsonObject() });
        }

        var type = Nullable.GetUnderlyingType(returnType) ?? returnType;
        var name = TypeNameFormatter.Format(returnType);
        JsonNode? result = null;
        try
        {
            result = ToolSchema.ResolveRefs(ToolSchema.FromType(type));
        }
        catch (Exception e) when (e is NotSupportedException or ArgumentException or InvalidOperationException or System.Text.Json.JsonException)
        {
            // An opaque type has no schema. The model builds it in a cell.
        }

        string description;
        if (result is null)
        {
            description =
                "Return the final result for the task. Call this ONLY when you have computed the final answer. "
                + $"Expected return type: {name}. "
                + "IMPORTANT: This type cannot be passed directly via this tool. "
                + $"Construct the object in {CodeToolName}() and call ReturnResult(variable) from within the code instead.";
            if (ReturnTypeDoc(type) is { } doc)
            {
                description += $"\n\nReturn type reference:\n{doc}";
            }

            result = new JsonObject();
        }
        else
        {
            description =
                "Return the final result for the task. Call this ONLY when you have computed the final answer. "
                + $"Expected return type: {name}. "
                + $"Tip: prefer calling ReturnResult(variable) from within {CodeToolName}() to pass computed results directly.";
        }

        var schema = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject { ["result"] = result },
            ["required"] = new JsonArray("result"),
        };
        return new ToolDefinition(ReturnToolName, description, schema);
    }

    /// <summary>The text of the generation failure when the loop ends without a result.</summary>
    public static string FailureMessage(CodeActSession session) =>
        session.ErrorCount >= session.MaxRetries
            ? $"Generation failed after {session.ErrorCount} errors (max_retries={session.MaxRetries}). "
                + $"Unable to generate valid code for `{session.MethodName}`."
            : $"Generation failed after {session.Iteration} iterations (max_iterations={session.MaxIterations}). "
                + $"Unable to complete `{session.MethodName}`.";

    /// <summary>Bounds a name that came from the model before it goes into a message.</summary>
    internal static string Bound(string text) =>
        text.Length <= _maxShownName ? text : text[..(_maxShownName - 3)] + "...";

    private static string Restrictions(RestrictedApiGroup allowed)
    {
        var lines = new List<string>
        {
            "- Type declarations: `class`, `record`, `struct`, `enum`, `interface`, `delegate`",
            "- Blocking calls: `.Result`, `.Wait()`, `Task.WaitAll`, `Thread.Sleep` (use `await`)",
            "- A call that returns a task without `await` (assign the task to a variable to start work and await it later)",
            "- Calling the running method on `self`: it would recurse",
        };
        var groups = new (RestrictedApiGroup Group, string Text)[]
        {
            (RestrictedApiGroup.FileSystem, "- Files and directories: `File`, `Directory`, `FileStream`"),
            (RestrictedApiGroup.Network, "- Network and database clients: `System.Net`, `System.Data`"),
            (RestrictedApiGroup.Process, "- Processes and the debugger: `Process`, `Environment.Exit`, `Debugger`"),
            (RestrictedApiGroup.Environment, "- The host environment: `Environment` variables and folders"),
            (RestrictedApiGroup.Reflection, "- Reflection and dynamic code: `Type`, `Activator`, `Assembly`, `dynamic`, `GetType()`"),
            (RestrictedApiGroup.NativeCode, "- Native code and memory: `Marshal`, `unsafe`, `extern`"),
        };
        lines.AddRange(groups.Where(g => !allowed.HasFlag(g.Group)).Select(g => g.Text));
        return string.Join('\n', lines);
    }

    private static string? ReturnTypeDoc(Type type)
    {
        try
        {
            var doc = AgentDocs.Doc(type).Trim();
            return doc.Length == 0 ? null : doc.Length > _maxReturnDocChars
                ? doc[.._maxReturnDocChars].TrimEnd() + "\n… (call Doc(<type>) for the full definition)"
                : doc;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            // The documentation is advice. A failure must not break the tool build.
            return null;
        }
    }

    private static MemberInfo? Resolve(Type agentType, string[] path)
    {
        var type = agentType;
        MemberInfo? member = null;
        foreach (var part in path)
        {
            if (part.Length == 0 || part[0] == '_')
            {
                return null;
            }

            member = MemberReader.GetFields(type).FirstOrDefault(m => m.Name == part)
                ?? MemberReader.GetMethods(type).FirstOrDefault(m => m.Name == part);
            if (member is null)
            {
                return null;
            }

            type = member switch
            {
                PropertyInfo p => p.PropertyType,
                FieldInfo f => f.FieldType,
                _ => type,
            };
        }

        return member;
    }

    private static string Literal(JsonNode? node) => node switch
    {
        JsonValue v when v.TryGetValue<bool>(out var b) => b ? "true" : "false",
        JsonValue v => v.ToJsonString(),
        null => "null",
        _ => "...",
    };
}
