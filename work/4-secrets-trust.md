---
pr: 4
pr_url: https://github.com/phaser/labs-oo-agents-csharp/pull/4
jira:
epic: M1
status: merged
created: 2026-10-16
updated: 2026-10-16
tags: [security]
wiki_refs: []
---

# T03b — Secrets trust: fail closed, no credential swap

## Context

A security review of merged PR [#3](https://github.com/phaser/labs-oo-agents-csharp/pull/3) found two issues in `Secrets.LoadSecretsIntoEnv`:

1. A project-layer file could replace (or delete) a credential that the user layer set. An attacker-owned API key then receives the prompts.
2. The trust check compared paths case-sensitively, but macOS and Windows file systems ignore case. The check did not fail closed.

Depends on: T03.

## Progress

- [x] Untrusted layers cannot replace or delete a name set by a trusted layer, and cannot delete any name
- [x] Trust fails closed: only the user-layer file or a `NEMO_OO_SECRETS` path outside the project is trusted
- [x] `NooaPaths.PathComparison`, `PathEquals`, `IsInside` with the platform case rule
- [x] Tests; porting-notes updated (deviation: upstream lets the project layer win)

## Notes

—
