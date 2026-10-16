---
pr: 8
pr_url: https://github.com/phaser/labs-oo-agents-csharp/pull/8
jira:
epic: M1
status: open
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

- [x] Port `IEventBackend` and the in-memory backend
- [x] Port `StorageManager` (`IStorageManager`: event backend plus snapshot blob API; `save_snapshot(agent)` / `restore_snapshot(id, agent)` move to T12)
- [x] Port SQLite backend with the upstream DDL (`events`, `active_tags`, `snapshots`, `schema_version`) via Microsoft.Data.Sqlite
- [x] JSON blobs only; deserialize through the event allowlist (D11)
- [x] Skip virtiofs detection; record in porting-notes
- [x] Port tests: `tests/storage/`, `test_sqlite_*.py` (the storage-only part; the rest is listed in `docs/porting-notes.md` and moved to T12/T15)

## Notes

Persistent REPL variables (`persistent_vars.py`, `snapshot_vars.py`) wait for T15.

- Package: `Microsoft.Data.Sqlite` 10.0.12, published 2026-09-08, MIT. Transitive `SQLitePCLRaw.*` 2.1.12, Apache-2.0.
- `IEventBackend.Update(tag, Action<EventBase>)` replaces `update(tag, **fields)`. Events are records with init-only properties.
- Unknown `event_type` in a stored row throws `DeserializationException` (upstream: generic `Metadata` fallback). Corrupt JSON is skipped with a warning, as upstream.
- No `register_event_type` and no legacy `LLMOutput` migration (D11: the allowlist is the `[JsonDerivedType]` list).
- Reconnect on disk I/O error is not ported (tests need fault injection). `busy_timeout=5000`, `synchronous=FULL` and the journal mode are ported.
- New safety limits (`StorageLimits`): 64 MiB per row, 1,000,000 events, 1 GiB total; the schema version and `sqlite_master` are checked at open; `trusted_schema=OFF` and defensive mode; paged `AllEvents`; the session lock refuses symbolic links.
- Moved to T12: `AgentSnapshot`, `serialize`/`deserialize` with type allowlist, `markers.py`, `json_snapshot.py`, `save_snapshot(agent)`, `restore_snapshot`, `restore_latest_snapshot`, tests that need an agent or `EventManager`.
- Moved to T15: `persistent_vars.py`, `snapshot_vars.py`.
