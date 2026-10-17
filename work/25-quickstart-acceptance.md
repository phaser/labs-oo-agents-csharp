---
pr: 25
pr_url: https://github.com/phaser/labs-oo-agents-csharp/pull/25
jira:
epic: M7
status: merged
created: 2026-10-16
updated: 2026-10-17
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

- [x] Port quickstart 01–08 to `examples/Quickstart`
- [x] Record fake responses; one offline acceptance test per quickstart
- [ ] Live smoke run of all 8 against a real model; record retry counts and cost in knowledge/ (awaiting user approval; `QuickstartLiveTests` is written, see porting-notes for how to run)
- [x] Make the `RoslynExecutionEngineTests` timeout tests stable under a loaded full run (they fail now and then on main)
- [x] README: install, first agent, security note (D12), upstream credit
- [x] Update `docs/porting-notes.md` and close the v1 milestone

## Notes

Exit check for v1. Details: `docs/porting-notes.md`, section "Quickstart examples and acceptance (T22)".

- The unified suite is `tests/Nooa.Tests/Acceptance/QuickstartAcceptanceTests.cs`. It replaces four older acceptance files.
- Flake cause: the cell time limit includes the compile. Fixed with 3 second limits.
- Open: the live smoke run (needs a key, costs money).
