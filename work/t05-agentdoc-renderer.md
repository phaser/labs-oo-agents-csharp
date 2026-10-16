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

# T05 — Agentdoc renderer: Doc()

## Context

Port `doc()`: render an agent type's fields, public methods, signatures and doc comments as prompt text.

Depends on: T03. Plan: [`docs/port-plan.md`](../docs/port-plan.md).

Upstream (pin in the plan):
- `src/nooa/agentdoc/ (except _pformat.py)`

## Progress

- [ ] Read type metadata by reflection (`NullabilityInfoContext` for nullable)
- [ ] Read doc comment text from generator-emitted metadata (T11 provides it; use a stub provider until then)
- [ ] Respect `[Hidden]` and private members; source order for methods
- [ ] Render C# signatures (not Python) in the output
- [ ] Port `spec()` / `Annotated` parameter descriptions as `[Description]`
- [ ] Port tests from `tests/agentdoc/` that cover `doc()`; list skipped ones in porting-notes

## Notes

Output format changes from Python to C# syntax. Record the format in porting-notes.
