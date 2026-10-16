---
pr:
pr_url:
jira:
epic: M2
status: todo
created: 2026-10-16
updated: 2026-10-16
tags: []
wiki_refs: []
---

# T08 — ILlmClient and FakeLlmClient

## Context

Define the LLM client interface on top of `IChatClient` and port the deterministic fake.

Depends on: T04. Plan: [`docs/port-plan.md`](../docs/port-plan.md).

Upstream (pin in the plan):
- `src/nooa/unifiedllm/unifiedllm.py (UnifiedLLM base)`
- `src/nooa/unifiedllm/fake.py`
- `src/nooa/token_counter.py`

## Progress

- [ ] Define `ILlmClient.CallAsync(messages, tools, outputType, ct) → LlmResponse`
- [ ] Port `FakeLlmClient` (scripted responses, call recording)
- [ ] Port char/4 token counter
- [ ] Port tests for the fake

## Notes

This task unblocks offline tests for every later milestone.
