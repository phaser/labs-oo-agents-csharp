---
pr:
pr_url:
jira:
epic: M1
status: todo
created: 2026-10-16
updated: 2026-10-16
tags: []
wiki_refs: []
---

# T07 — Storage: in-memory and SQLite

## Context

Port event and snapshot persistence.

Depends on: T04. Plan: [`docs/port-plan.md`](../docs/port-plan.md).

Upstream (pin in the plan):
- `src/nooa/storage/`
- `src/nooa/runtime/event_backend.py`

## Progress

- [ ] Port `IEventBackend` and the in-memory backend
- [ ] Port `StorageManager`
- [ ] Port SQLite backend with the upstream DDL (`events`, `active_tags`, `snapshots`, `schema_version`) via Microsoft.Data.Sqlite
- [ ] JSON blobs only; deserialize through the event allowlist (D11)
- [ ] Skip virtiofs detection; record in porting-notes
- [ ] Port tests: `tests/storage/`, `test_sqlite_*.py`

## Notes

Persistent REPL variables (`persistent_vars.py`) wait for T15.
