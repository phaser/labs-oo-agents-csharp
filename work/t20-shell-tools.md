---
pr:
pr_url:
jira:
epic: M6
status: todo
created: 2026-10-16
updated: 2026-10-16
tags: []
wiki_refs: []
---

# T20 — ShellTools

## Context

Port the persistent bash session tool (POSIX only).

Depends on: T17. Plan: [`docs/port-plan.md`](../docs/port-plan.md).

Upstream (pin in the plan):
- `src/nooa/tools/shell_tools.py`
- `src/nooa/tools/_bash_session.py`
- `src/nooa/tools/shell_lifecycle.py`
- `src/nooa/tools/_results.py`

## Progress

- [ ] Port `BashSession` with a sentinel on stdout (no fd 3)
- [ ] Port `Run`, `Read`, `Replace`, `WriteFile`, grep with `Match` anchors
- [ ] Interrupt and recovery; background job tracking
- [ ] Do not pass the full host environment by default; allowlist variables
- [ ] Port tests: `tests/tools/` shell tests (need bash and `rg`)

## Notes

Runs commands as the host user. The agent must opt in explicitly.
