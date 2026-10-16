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

- [ ] Port `EventBase` (id, tag, timestamp, metadata, role) as a record hierarchy
- [ ] Register event types with `[JsonPolymorphic]` / `[JsonDerivedType]` (explicit allowlist, D11)
- [ ] Port events from `events.py` (`Task`, `Message`, `Error`, `PythonOutput` → `CellOutput`, `Before/AfterAgentCall`, `LlmCallStart/End`, `Summary`, `SystemPrompt`)
- [ ] Port `LlmResponse` with ordered parts (`AssistantText`, `ToolCall`, `AssistantReasoning`), `LlmUsage`, finish reason, opaque `JsonNode Native`
- [ ] Round-trip JSON tests for every event type
- [ ] Port tests: `test_events*.py`, event parts of `tests/context_blocks/`

## Notes

—
