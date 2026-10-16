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

# T13 — Context blocks renderer

## Context

Port block resolution and rendering of blocks + events into the LLM message list.

Depends on: T12, T06. Plan: [`docs/port-plan.md`](../docs/port-plan.md).

Upstream (pin in the plan):
- `src/nooa/context_blocks/ (renderer, formatters, scoped)`
- `src/nooa/runtime/context_builder.py`
- `src/nooa/runtime/event_query.py`

## Progress

- [ ] Port `Context(value|expr, prefix)`, `DynamicContext`, `ResolvedBlock`
- [ ] Port block and total-budget truncation
- [ ] Port XML and Markdown formatters and the cached layout order (static system, events, dynamic user message)
- [ ] Port `EventQuery` filters
- [ ] `ScopedContext` via `AsyncLocal`
- [ ] Port tests: `tests/context_blocks/`

## Notes

From T04 (event IR): render `TaskEvent` under the name `Task`. Apply the "Not shown to the model" and "must not truncate" rules in the XML docs of the event properties, and suppress empty values (keep `0` and `false`). Port `LlmResponse.render_message` and `searchable_fields` (public values only, no native state). See `docs/porting-notes.md`, "Event IR".

`Context.expr` (a re-evaluated expression) waits for T18 because it needs the REPL evaluator.
