---
pr:
pr_url:
jira:
epic: M2
status: open
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

- [x] Define `ILlmClient.CallAsync(messages, tools, outputType, ct) → LlmResponse`
- [x] Port `FakeLlmClient` (scripted responses, call recording)
- [x] Port char/4 token counter
- [x] Port tests for the fake

## Notes

This task unblocks offline tests for every later milestone.

- `ILlmClient` also has `CountTokens` and `ContextWindow`. Messages are a closed `LlmMessage` hierarchy. Tools are `ToolDefinition`. No M.E.AI dependency: T09 owns it.
- Fake has fluent `Enqueue*` helpers and the upstream factories. Deviations are in `docs/porting-notes.md` (T08 section).
- Hand-off to T09: map `ToolDefinition` to `AIFunction`, project `ResponseMessage` and `CacheBoundary`, add per-call settings through client config.
