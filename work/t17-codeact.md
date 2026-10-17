---
pr:
pr_url:
jira:
epic: M4
status: todo
created: 2026-10-16
updated: 2026-10-16
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

- [ ] Port the generation loop: generate → tool calls → run cell or return result; `max_iterations`, `max_retries`
- [ ] Port `_handle_return_result` coercion: variable name, JSON string; skip the AST constructor evaluator
- [ ] Port pre- and postconditions (`strategy_validation.py`)
- [ ] Port `LoopGuard`
- [ ] Port `InspectInputsPrefill`
- [ ] Rewrite every strategy instruction and error hint for C#
- [ ] Port quickstart 03 and 04 as offline acceptance tests
- [ ] Port tests: CodeAct parts of `tests/strategies/`

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
