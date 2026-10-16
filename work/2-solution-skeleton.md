---
pr: 2
pr_url: https://github.com/phaser/labs-oo-agents-csharp/pull/2
jira:
epic: M0
status: open
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

- [x] Add `Nooa.slnx`, `src/Nooa`, `src/Nooa.Generators`, `tests/Nooa.Tests`, `tests/Nooa.Generators.Tests`, `examples/Quickstart`
- [x] Add `Directory.Build.props`: nullable, `TreatWarningsAsErrors`, `LangVersion latest`
- [x] Add `Directory.Packages.props` (central package versions)
- [x] Add `.editorconfig`; `dotnet format --verify-no-changes` passes
- [x] Add SPDX license header check (upstream `scripts/check_license_headers.py`)
- [x] Add GitHub Actions workflow: build, format check, `dotnet test --filter "Category!=Live"` on ubuntu + macos
- [ ] CI green on the PR
- [x] Add `scripts/clone-reference.sh` that clones upstream at the pinned commit

## Notes

Upstream CI: `.github/workflows/ci.yml`.

- `TargetFramework` is set per project, not in `Directory.Build.props`, because `Nooa.Generators` must target `netstandard2.0`.
- `GenerateDocumentationFile` is on (IDE0005 needs it on build); CS1591 is suppressed. It also validates XML doc syntax, and doc comments are prompts.
- xunit.v3 4.x needs Microsoft.Testing.Platform on the .NET 10 SDK. `global.json` opts in; `Microsoft.NET.Test.Sdk` and `xunit.runner.visualstudio` are not needed.
- CI passes `--ignore-exit-code 8` (zero tests ran) until T03 adds tests.
- Pinned packages (all GO-tier licenses, all ≥14 days old): xunit.v3 4.0.1 (Apache-2.0), Microsoft.CodeAnalysis.CSharp 5.3.0 (MIT), Microsoft.CodeAnalysis.Analyzers 5.3.0 (MIT). The SDK compiler is Roslyn 5.6, so the generator's Roslyn 5.3 is compatible.
- CI runs `ubuntu-latest` on PRs and `ubuntu-latest` + `macos-latest` on pushes to `main` (macOS minutes cost 10x on a private repo).
- Actions pinned by SHA: actions/checkout v7.0.1, actions/setup-dotnet v6.0.0.
