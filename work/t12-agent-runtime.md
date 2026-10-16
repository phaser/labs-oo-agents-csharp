---
pr:
pr_url:
jira:
epic: M3
status: todo
created: 2026-10-16
updated: 2026-10-16
tags: []
wiki_refs: []
---

# T12 — Agent base, ActorRuntime, method wrapper

## Context

Port the `Agent` base class and the runtime core that the generated bodies call.

Depends on: T04, T05, T08, T11. Plan: [`docs/port-plan.md`](../docs/port-plan.md).

Upstream (pin in the plan):
- `src/nooa/agent.py`
- `src/nooa/runtime/actor.py (generate, lock, dispatch)`
- `src/nooa/runtime/method_wrapper.py`
- `src/nooa/runtime/event_manager.py`
- `src/nooa/runtime/context_manager.py`
- `src/nooa/runtime/context_vars.py`
- `src/nooa/runtime/hooks.py`
- `src/nooa/runtime/middleware.py`
- `src/nooa/method_llm.py`

## Progress

- [ ] Port `Agent` init: id, storage, `EventManager`, `ContextManager` with protected blocks `system_prompt`, `self`, `state`
- [ ] LLM resolution cascade: call → method → instance → class → parent agent (`AsyncLocal`)
- [ ] Generation lock (D10) and nesting depth check
- [ ] Method wrapper: argument validation, `BeforeAgentCall` / `AfterAgentCall`, middleware hooks, strategy dispatch
- [ ] `EventManager`: add / filter / update / collapse / subscribe / intercept
- [ ] Strategy precedence: call → `[Strategy]` → default
- [ ] Port tests: `tests/core_runtime/`, `tests/edge_cases/`, `test_method_call_permutations.py`, `test_agent_llm_accessor.py`, event-manager tests

## Notes

Keep `actor.py` trimmed to what Template / Predict / CodeAct need.

From T07 (storage): `IStorageManager` has the blob API only (`SaveSnapshotJson`, `LoadSnapshotJson`, `GetLatestSnapshotId`, `ListSnapshots`). T12 adds:
- `AgentSnapshot` (version 2) and `snapshot_to_json` / `snapshot_from_json`: walk agent fields, context blocks and attributes. Check `version` and the type allowlist before restoring any value (the file can be untrusted). Never `BinaryFormatter` or `TypeNameHandling` (D11).
- `serialize` / `deserialize` with the type allowlist, `markers.py` (`nosnapshot`, `snapshotable`), `save_snapshot(agent)`, `restore_snapshot(id, agent)`, `restore_latest_snapshot`.
- The upstream tests that need an agent: `test_sqlite_storage_latest.py`, `test_sqlite_close_commit.py` (snapshot part), `test_session_resume_tag_collision.py` (agent and `EventManager` parts), `tests/storage/test_snapshot.py`, `test_serialization.py`, `test_markers.py`, `tests/runtime/test_snapshot.py`.
- `EventManager` allocates tags through `IEventBackend.AllocateNextTag()` so that several managers on one backend never collide.
