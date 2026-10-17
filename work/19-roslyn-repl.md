---
pr: 19
pr_url: https://github.com/phaser/labs-oo-agents-csharp/pull/19
jira:
epic: M4
status: open
created: 2026-10-16
updated: 2026-10-17
tags: []
wiki_refs: []
---

# T15 — Roslyn REPL engine

## Context

C# code-as-action engine: persistent script state, typed globals, output capture, timeouts.

Depends on: T14. Plan: [`docs/port-plan.md`](../docs/port-plan.md).

Upstream (pin in the plan):
- `src/nooa/runtime/actor.py (execute_code)`
- `src/nooa/runtime/stream_wrappers.py`
- `src/nooa/runtime/out_accessor.py`
- `src/nooa/storage/persistent_vars.py`

## Progress

- [x] `IExecutionEngine` with one implementation, `RoslynExecutionEngine`
- [x] Typed globals: `self`, method arguments as members, `ReturnResult`, `Doc`, `PPrint`, `Out`
- [x] Chain cells with `ScriptState.ContinueWithAsync`; variables persist across cells
- [x] Restricted `ScriptOptions` references and imports (allowlist)
- [x] Output capture through a globals `Console` shim (not `Console.SetOut`)
- [x] Per-cell timeout with cooperative cancellation; record partial output on cancel
- [x] Strip markdown fences; map compile diagnostics to `Cell In[N]` line numbers
- [x] Measure memory per cell over 500 cells; record the result in knowledge/
- [x] Port `errors/formatting.py` intent: LLM-facing diagnostics for C# compile/runtime errors with `Cell In[N]` line numbers, bounded size (moved from T03; ports the intent of `test_error_formatting.py` and `test_error_line_numbers.py`)
- [x] Port the applicable executor and stream tests from `tests/runtime/`

## Notes

Not a security boundary (D12). Document this in the README and in the type's XML doc.

From T07 (storage): `storage/persistent_vars.py` and `storage/snapshot_vars.py` (REPL variables in snapshots) belong here. Store them through `IStorageManager.SaveSnapshotJson`. Values are JSON only (D11).

From T21: `Todo.v` (the `PersistentVars` attribute proxy over `Todo.Vars`, upstream `todo.py` `Todo.v`) is not ported. Add it with the REPL persistent vars, with the proxy tests from `src/nooa/tools/tests/test_todo_vars.py` and `test_todo_status.py` (reserved names `keys`, `items`, `get`, `set`, `clear`). `Todo.Vars` (`Dictionary<string, JsonNode?>`), `Todo.SetVar`, `TodoManager.SetVar/GetVar/DelVar` exist.

## Result

- Package `Microsoft.CodeAnalysis.CSharp.Scripting` 5.3.0, published 2026-03-10, MIT.
- Decisions and deviations: see `docs/porting-notes.md` section "Roslyn REPL engine (T15)".
- Memory: about 260 KiB per cell over 500 cells (`knowledge/porting/knowledge.md`).
- T16 plugs in through `ICellValidator`. The reference list does not hide core-library types (`System.IO.File`): the validator must deny them.
- Not done: `CaptureConfig.FileBacked` is ignored by the engine, `show()` images, `strip_xml_wrapper`, `self.v` on `Agent`.
