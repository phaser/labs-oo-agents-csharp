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

# T06 — Agentdoc PFormat() and truncation

## Context

Port `pformat()`: a reflection-based object printer with size budgets and truncation, used for the `state` block and REPL output.

Depends on: T05. Plan: [`docs/port-plan.md`](../docs/port-plan.md).

Upstream (pin in the plan):
- `src/nooa/agentdoc/_pformat.py`
- `src/nooa/agentdoc/_structured.py`
- `src/nooa/runtime/pprint.py`

## Progress

- [ ] Define the C# repr format for primitives, records, collections, dictionaries, enums, null
- [ ] Port depth / width / item-count truncation rules
- [ ] Handle cycles (reference tracking)
- [ ] Port `TruncatingStringIO` equivalent (`TruncatingTextWriter`)
- [ ] Port tests from `tests/agentdoc/` that cover `pformat`

## Notes

Largest single file upstream (2,085 lines). Split the PR if it grows past ~1.5k lines.
