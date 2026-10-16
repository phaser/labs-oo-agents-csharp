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

# T09 — IChatClient adapter, tools, structured output, retry

## Context

Implement `ILlmClient` over Microsoft.Extensions.AI for an OpenAI-compatible endpoint.

Depends on: T08. Plan: [`docs/port-plan.md`](../docs/port-plan.md).

Upstream (pin in the plan):
- `src/nooa/unifiedllm/ (CompletionClient, tool schema, retry.py, json parsing)`

## Progress

- [ ] Map messages and `LlmResponse` parts to and from M.E.AI types
- [ ] Tool schema from method signatures (`AIJsonUtilities` / `JsonSchemaExporter`), strict mode with loose fallback
- [ ] Structured output for a return type; validate and surface errors
- [ ] Retry policy: backoff with jitter, retryable codes {408,429,500,502,503,504}, retry on empty content, per-attempt deadline
- [ ] Usage normalisation (`LlmUsage`)
- [ ] `<think>` tag extraction for open models
- [ ] Port pure-logic tests: retry, tool schema, json parsing, chat parts
- [ ] Add one `Live` test against an OpenAI-compatible endpoint

## Notes

Anthropic / Responses adapters and lossless reasoning replay are B09. Set a token budget per call (prompt-injection checklist rule 5).
