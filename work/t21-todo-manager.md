---
pr:
pr_url:
jira:
epic: M6
status: open
created: 2026-10-16
updated: 2026-10-17
tags: []
wiki_refs: []
---

# T21 — TodoManager

## Context

Port the task list tool.

Depends on: T17, T07. Plan: [`docs/port-plan.md`](../docs/port-plan.md).

Upstream (pin in the plan):
- `src/nooa/tools/todo.py`

## Progress

- [x] Port `Todo` and `TodoManager` (dependencies, comments, active todo, merge)
- [x] Snapshot through storage
- [x] Port tests: `tests/unit/test_todo_comments.py`, `tests/unit/test_todo_status.py`, `src/nooa/tools/tests/test_todo_vars.py` (vars part only; the `v` proxy tests wait for T15)

## Notes

- Plain classes in `src/Nooa/Tools/`: `Todo`, `TodoComment`, `TodoStatus`, `TodoRef`, `TodoManager`, `TodoManagerJsonConverter`. No `Skill` base class exists in C#. `attach` and `detach` are not ported.
- `TodoRef` replaces `Todo | str`: a `Todo` or an ID string converts to it implicitly.
- Snapshot: `TodoManager` is `[Snapshotable]` with a JSON converter. The state is `{todos, active_id}` (`ToJson`, `Restore`, constructor). Upstream `to_dict` and `from_dict` are the same. Values go through `NooaJson.Options`. `Restore` is atomic and throws `DeserializationException`. Legacy `notes`, free-form `status` and comments without an ID migrate as upstream.
- Todo vars are `Dictionary<string, JsonNode?>`. A value must be JSON-like (`SnapshotValues.ToNode`). Other values are not stored, as upstream (`SnapshotVars`).
- Status is an enum (`open`, `done`): an invalid status cannot be built, so the upstream invalid-status tests are dropped.
- Deferred to T15: the `t.v` attribute proxy (`PersistentVars`) and its tests (`test_todo_vars.py`, reserved names). `Todo.Vars`, `SetVar`, `GetVar`, `DelVar` cover the data.
- Deferred to T17/T22: `self.todo` on the agent and the context block `todo_status` = `self.todo.status()`. See `work/t17-codeact.md`.
