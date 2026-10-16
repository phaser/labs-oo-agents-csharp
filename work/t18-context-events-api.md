---
pr:
pr_url:
jira:
epic: M5
status: todo
created: 2026-10-16
updated: 2026-10-16
tags: []
wiki_refs: []
---

# T18 — ContextApi, EventsApi, dynamic context

## Context

Port the model-facing `self.Context` and `self.Events` APIs and re-evaluated context expressions.

Depends on: T17. Plan: [`docs/port-plan.md`](../docs/port-plan.md).

Upstream (pin in the plan):
- `src/nooa/runtime/context.py`
- `src/nooa/runtime/events.py`
- `src/nooa/context_blocks/`

## Progress

- [ ] Port `ContextApi` (indexer, `SetDynamic`, `Disable`, `Enable`)
- [ ] Port `EventsApi` (`Query`, `Get`, `Collapse`, `Keys`)
- [ ] Evaluate `Context.expr` as a C# expression through the REPL engine
- [ ] Port quickstart 05, 07 and 08 as offline acceptance tests
- [ ] Port tests: `tests/runtime/` context and events API tests

## Notes

—
