---
pr:
pr_url:
jira:
epic: M4
status: open
created: 2026-10-16
updated: 2026-10-17
tags: []
wiki_refs: []
---

# T17 — CodeAct strategy and C# prompts

## Context

Port the default strategy: `execute_csharp` + `return_result` tool loop.

Depends on: T16. Plan: [`docs/port-plan.md`](../docs/port-plan.md).

Upstream (pin in the plan):
- `src/nooa/strategies/codeact.py`
- `src/nooa/strategies/codeact_errors.py`
- `src/nooa/strategies/loop_guard.py`
- `src/nooa/strategies/prefill.py`

## Progress

- [x] Port the generation loop: generate → tool calls → run cell or return result; `max_iterations`, `max_retries`
- [x] Port `_handle_return_result` coercion: variable name, JSON string; skip the AST constructor evaluator
- [x] Run pre- and postconditions in CodeAct and add the `CodeActConfig` fields (the types `MethodPrecondition`, `MethodPostcondition`, `InvariantException`, `StrategyConditions` are ported in T14)
- [x] Port `LoopGuard`
- [x] Port `InspectInputsPrefill`
- [x] Rewrite every strategy instruction and error hint for C#
- [x] Port quickstart 03 and 04 as offline acceptance tests
- [x] Port tests: CodeAct parts of `tests/strategies/`
- [x] Tracing hooks for cells (from T19): `BeforeCodeExecution` / `AfterCodeExecution`, `OpenInferenceHooks` TOOL span `code_execution`

## Notes

Exit check for M4.

Deferred `CodeActConfig` fields from T03 (add them with the types they use): `Restrictions` (T16), `Prefill` (this task), `Preconditions` and `Postconditions` (this task), `Sandbox` and `ExecutionBackend` (B01). Merge them as explicit-field properties (see `ConfigBase`).

From T21: expose `TodoManager` (`src/Nooa/Tools/`) to the REPL as `self.todo` (T17/T22). Register the context block `todo_status` with the expression `self.todo.status()`. Upstream also keeps `Todo.v` (T15), and `doc(self.todo)` must show only the non-`[Hidden]` methods.

### Shell tools exposure (from T20)

`Nooa.Tools.ShellTools` is a plain class with no agent wiring. When the REPL and skill registration exist, expose it like this:
- The agent owns an instance, created on purpose: `new ShellTools(new ShellOptions { Cwd = ... })`. Never create it by default. Never expose it to a REPL that runs untrusted input: it runs commands as the host user. Pass secrets only through `ShellOptions.ExtraEnvironment`.
- Expose the instance as a hidden-by-default skill member (`self.shell`) with `RunAsync`, `RunStreamAsync`, `ReadAsync`, `ReplaceAsync`, `WriteFileAsync`. Hide `Session` and `CloseAsync`.
- The prompt text is the class summary of `ShellTools`. It tells the model to use the four methods and that `result.Matches` feeds `ReplaceAsync`.
- Dispose the instance (`DisposeAsync`) when the agent ends. Dispose kills the process group.
- Upstream printed a hint line to the REPL stdout when a search had matches. `RunAsync` does not. Add the hint in the REPL layer if it is wanted.
- A benchmark harness that grades background services after the run uses `ShellLifecycle.PreserveBackgroundServices()`.

## Tracing hooks for cells and tools (from T19)

T19 traces agent calls, generations and LLM calls. Cell and tool spans need hooks that have callers only here.
- Add `BeforeCodeExecution` / `AfterCodeExecution` (and tool-execution if tools are traced) to `IInstrumentationHooks`, called through `InstrumentationHooks.CallBefore` / `CallAfter`. Skip them for a `[NoTrace]` method.
- Implement them in `Nooa.Tracing.OpenInferenceHooks`: TOOL span `code_execution`, parent = the generation span, `input.value` = JSON `{"code": ...}` (cap 10 000 characters), output = JSON `{stdout, stderr, returned_value}`.
- Write every value with `SpanAttributes.Set` (scrub and bound). Cell code and output are the most likely place for a secret.

## T17 result notes

- **Default strategy:** `Agent.ResolveStrategy` falls back to one lazy `CodeActStrategy` (order: call scope, `[Strategy]`, `AgentOptions.DefaultStrategy`, CodeAct). The old "No generation strategy" error is gone.
- **Types:** `CodeActStrategy` (public), `IPrefill`, `InspectInputsPrefill`; internal `CodeActRun` (the loop and tool handlers), `CodeActSession`, `LoopGuard` (+ `LoopGuardAction`, `LoopGuardVerdict`), `ReturnResultValidator` (+ `ReturnCheck`), `CodeActErrors`, `CodeActPrompts`, `CodeActScope`, `CellValues`.
- **`CodeActConfig` fields added:** `AllowedApis` (`RestrictedApiGroup`; upstream `restrictions`, renamed because the value lists what is allowed), `Prefill` (default `InspectInputsPrefill`, null turns it off), `Preconditions`, `Postconditions`. `Sandbox` and `ExecutionBackend` stay with B01.
- **Validator wiring:** one engine and one `CellCodeValidator` per call. `ForbiddenSelfCalls` = the running method, `ReturnType` = the return type, `Allow` = `AllowedApis`. Argument types: the strategy runs a hidden declaration (`T name = default!;`) through the validator first, so the argument names have real types (tested with `work.Result` on a `Task<int>` argument).
- **Abandoned cell:** `IExecutionEngine.IsAbandoned` (new). The run records the cell output, then throws `GenerationException`.
- **Engine additions (T15 files):** `IsAbandoned`, `TryGetVariable` (for `return_result("variable")`), `ReplArgument.Identifier` (internal), `PPrint(value, maxLength, maxString, maxDepth)` (for the prefill limits).
- **Bug found in T16:** the validator chained a cell that did not compile, and Roslyn then threw "Previous submission has errors" on the next cell. Fixed in `CellCodeValidator` with a test.
- **Shell and Todo:** no skill registry exists (B06). An agent class declares `public ShellTools Shell` / `public TodoManager Todo` itself, so `self.Shell` / `self.Todo` work in cells and appear in `Doc(self)`. `ShellTools.Session`, `CloseAsync`, `DisposeAsync` are now `[Hidden]`. Register the block with `ContextManager.SetDynamicProvider("todo_status", () => Todo.Status())` and close the shell with `EventManager.OnClose` (see `AnalysisAgent` and `CodeActRunTests`). `self.v` is not installed (upstream does not either).
- **Not done:** `TranslateToolCalls` (config field exists, no effect), `on_text_only` handler hook (default retry only), `MaxToolCalls`, per-call sampling (`Temperature`, `TopP`, `MaxTokens` only sets the reserve), tool-execution hook for `return_result`, harness metrics, `show()` and media in prefill, constructor-string evaluation.
