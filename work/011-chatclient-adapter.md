---
pr: 11
pr_url: https://github.com/phaser/labs-oo-agents-csharp/pull/11
jira:
epic: M2
status: merged
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

- [x] Map messages and `LlmResponse` parts to and from M.E.AI types
- [x] Tool schema from method signatures (`AIJsonUtilities` / `JsonSchemaExporter`), strict mode with loose fallback
- [x] Structured output for a return type; validate and surface errors
- [x] Retry policy: backoff with jitter, retryable codes {408,429,500,502,503,504}, retry on empty content, per-attempt deadline
- [x] Usage normalisation (`LlmUsage`)
- [x] `<think>` tag extraction for open models
- [x] Port pure-logic tests: retry, tool schema, json parsing, chat parts
- [x] Add one `Live` test against an OpenAI-compatible endpoint

## Notes

From T04: port `CacheBoundary` and the message-list element type here (it is not in T04). `LlmUsage.FromProvider` takes `JsonNode?`.

Anthropic / Responses adapters and lossless reasoning replay are B09. Set a token budget per call (prompt-injection checklist rule 5).

### T09 notes (decisions)

- Full list of decisions and deviations: `docs/porting-notes.md`, section "ChatClientLlmClient, tool schema, retry, JSON parsing (T09)".
- Packages (checked 2026-10-16 on nuget.org, all MIT): `Microsoft.Extensions.AI.Abstractions` 10.10.1 (2026-09-25), `Microsoft.Extensions.AI.OpenAI` 10.10.1 (2026-09-25). 10.11.0 was only 2 days old, so 10.10.1 is pinned.
- Transitive packages: `OpenAI` 2.14.0 (2026-09-15, MIT), `System.ClientModel` 1.15.0 (2026-08-04, MIT), `System.Memory.Data` 10.0.9, `Microsoft.Extensions.Configuration.Abstractions` 10.0.9, `Microsoft.Extensions.DependencyInjection.Abstractions` 10.0.12, `Microsoft.Extensions.Diagnostics.Abstractions` 10.0.9, `Microsoft.Extensions.FileProviders.Abstractions` 10.0.9, `Microsoft.Extensions.Hosting.Abstractions` 10.0.9, `Microsoft.Extensions.Options` 10.0.9, `Microsoft.Extensions.Primitives` 10.0.9 (all .NET team packages, MIT).
- Not done here: Bedrock schema sanitizing, Responses and Anthropic adapters, cache breakpoints, native reasoning replay (B09); admission control (B08); registry and reasoning levels (T10); XML `<tool_call>` fallback.
- Live test not run here (no endpoint available): it skips cleanly without the three environment variables.
