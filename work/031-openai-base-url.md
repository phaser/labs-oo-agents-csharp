---
pr: 31
pr_url: https://github.com/phaser/labs-oo-agents-csharp/pull/31
jira:
epic: M8
status: merged
created: 2026-10-17
updated: 2026-10-17
tags: [packaging, llm]
wiki_refs: []
---

# T23b — OPENAI_BASE_URL and NOOA_MODEL for the default LLM

## Context

The package consumer example could not use the local Claude gateway: `Quickstart.CreateDefaultLlm` had no setting for the endpoint or the model.

Depends on: T23.

## Progress

- [x] `OPENAI_BASE_URL` (with `OPENAI_API_KEY`): https, or http to a loopback host, no user info
- [x] `NOOA_MODEL` replaces the model of the chosen route
- [x] Tests; README; porting-notes
- [x] Package version 0.1.1 (the local feed caches 0.1.0)
- [x] Run the consumer through the package against the gateway with `sonnet`: pass

## Notes
