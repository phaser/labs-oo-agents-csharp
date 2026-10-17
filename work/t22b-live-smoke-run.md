---
pr:
pr_url:
jira:
epic: M7
status: todo
created: 2026-10-17
updated: 2026-10-17
tags: [acceptance, live]
wiki_refs: []
---

# T22b — Live smoke run of quickstart 01–08

## Context

T22 ([#25](https://github.com/phaser/labs-oo-agents-csharp/pull/25)) added `QuickstartLiveTests` (`Category=Live`). The run needs a real API key and costs money. It waits for user approval.

Depends on: T22.

## Progress

- [ ] User approves the run and names the provider, the model and the key source
- [ ] Run `dotnet test --solution Nooa.slnx -c Release --filter "Category=Live"`
- [ ] Record retry counts and cost in `knowledge/`

## Notes
