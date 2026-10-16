---
pr:
pr_url:
jira:
epic: M7
status: todo
created: 2026-10-16
updated: 2026-10-16
tags: []
wiki_refs: []
---

# T22 — Quickstart examples and acceptance

## Context

Ship quickstart 01–08 as runnable examples and close v1.

Depends on: T14, T17, T18, T19, T20, T21. Plan: [`docs/port-plan.md`](../docs/port-plan.md).

Upstream (pin in the plan):
- `examples/quickstart/01..08`

## Progress

- [ ] Port quickstart 01–08 to `examples/Quickstart`
- [ ] Record fake responses; one offline acceptance test per quickstart
- [ ] Live smoke run of all 8 against a real model; record retry counts and cost in knowledge/
- [ ] README: install, first agent, security note (D12), upstream credit
- [ ] Update `docs/porting-notes.md` and close the v1 milestone

## Notes

Exit check for v1.
