---
pr:
pr_url:
jira:
epic: M0
status: todo
created: 2026-10-16
updated: 2026-10-16
tags: []
wiki_refs: []
---

# T02 — Solution skeleton and CI

## Context

Create the solution layout from plan §3 with no feature code. Add build settings and CI.

Depends on: T01. Plan: [`docs/port-plan.md`](../docs/port-plan.md).

Upstream (pin in the plan):
- —

## Progress

- [ ] Add `Nooa.slnx`, `src/Nooa`, `src/Nooa.Generators`, `tests/Nooa.Tests`, `tests/Nooa.Generators.Tests`, `examples/Quickstart`
- [ ] Add `Directory.Build.props`: `net10.0`, nullable, `TreatWarningsAsErrors`, `LangVersion latest`
- [ ] Add `Directory.Packages.props` (central package versions)
- [ ] Add `.editorconfig`; `dotnet format --verify-no-changes` passes
- [ ] Add SPDX license header check (upstream `scripts/check_license_headers.py`)
- [ ] Add GitHub Actions workflow: build, format check, `dotnet test --filter "Category!=Live"` on ubuntu + macos
- [ ] Add `scripts/clone-reference.sh` that clones upstream at the pinned commit

## Notes

Upstream CI: `.github/workflows/ci.yml`.
