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

- [ ] `IExecutionEngine` with one implementation, `RoslynExecutionEngine`
- [ ] Typed globals: `self`, method arguments as members, `ReturnResult`, `Doc`, `PPrint`, `Out`
- [ ] Chain cells with `ScriptState.ContinueWithAsync`; variables persist across cells
- [ ] Restricted `ScriptOptions` references and imports (allowlist)
- [ ] Output capture through a globals `Console` shim (not `Console.SetOut`)
- [ ] Per-cell timeout with cooperative cancellation; record partial output on cancel
- [ ] Strip markdown fences; map compile diagnostics to `Cell In[N]` line numbers
- [ ] Measure memory per cell over 500 cells; record the result in knowledge/
- [ ] Port `errors/formatting.py` intent: LLM-facing diagnostics for C# compile/runtime errors with `Cell In[N]` line numbers, bounded size (moved from T03; ports the intent of `test_error_formatting.py` and `test_error_line_numbers.py`)
- [ ] Port the applicable executor and stream tests from `tests/runtime/`

## Notes

Not a security boundary (D12). Document this in the README and in the type's XML doc.

From T07 (storage): `storage/persistent_vars.py` and `storage/snapshot_vars.py` (REPL variables in snapshots) belong here. Store them through `IStorageManager.SaveSnapshotJson`. Values are JSON only (D11).
