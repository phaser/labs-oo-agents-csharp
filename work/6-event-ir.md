---
pr: 6
pr_url: https://github.com/phaser/labs-oo-agents-csharp/pull/6
jira:
epic: M1
status: open
created: 2026-10-16
updated: 2026-10-16
tags: []
wiki_refs: []
---

# T04 — Event IR and LlmResponse

## Context

Port the event base types, roles, the event catalogue and the provider-neutral response model.

Depends on: T03. Plan: [`docs/port-plan.md`](../docs/port-plan.md).

Upstream (pin in the plan):
- `src/nooa/context_blocks/events.py`
- `src/nooa/context_blocks/roles.py`
- `src/nooa/events.py`
- `src/nooa/llm_types.py`
- `src/nooa/_immutable_json.py`

## Progress

- [x] Port `EventBase` (id, tag, timestamp, metadata, role) as a record hierarchy
- [x] Register event types with `[JsonPolymorphic]` / `[JsonDerivedType]` (explicit allowlist, D11)
- [x] Port events from `events.py` (`Task`, `Message`, `Error`, `PythonOutput` → `CellOutput`, `Before/AfterAgentCall`, `LlmCallStart/End`, `Summary`, `SystemPrompt`)
- [x] Port `LlmResponse` with ordered parts (`AssistantText`, `ToolCall`, `AssistantReasoning`), `LlmUsage`, finish reason, opaque `JsonNode Native`
- [x] Round-trip JSON tests for every event type
- [x] Port tests: `test_events*.py`, event parts of `tests/context_blocks/`

## Notes

Decisions and deviations: `docs/porting-notes.md`, "Event IR". Deferred: rendering hooks (T13), `CacheBoundary` (T09), Tui/Decision/legacy events (not v1 / B03), `ExecutionResult` (T15).

No new NuGet package.
