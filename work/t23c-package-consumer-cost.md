---
pr:
pr_url:
jira:
epic: M8
status: in-progress
created: 2026-10-18
updated: 2026-10-18
tags: [packaging, examples, cost]
wiki_refs: []
---

# T23c — Cost reporting and OpenRouter in PackageConsumer

## Context

The package consumer showed tokens nowhere and had no OpenRouter route. Its agent call could not answer what one
run costs, and `LlmUsage.CostUsd` stays 0 for OpenAI-compatible providers because the adapter does not map a cost
field. Add OpenRouter support for `deepseek/deepseek-v4.1-flash-1017` and print the cost of the run at the end.

Depends on: T23, T23b.

## Progress

- [x] `OPENROUTER_API_KEY` selects `https://openrouter.ai/api/v1` and the DeepSeek V4.1 Flash 1017 model
- [x] `NOOA_MODEL` still replaces the model of the selected route
- [x] Sum `LLMResponse` usage at the end and print tokens and cost
- [x] Prefer `LlmUsage.CostUsd`, then OpenRouter `usage.cost` from the raw response, then list prices
- [x] Bound the run with `MaxOutputTokens = 2048` and `ReasoningEffort.Low`
- [x] Offline run with `FakeLlmClient` prints an estimated cost
- [x] Live run: 289 input, 175 output tokens; OpenRouter reported $0.000119

## Notes

- OpenRouter bills the upstream provider it picks, so the per-token list prices are only a fallback. The raw
  `ChatCompletion` JSON keeps `usage.cost`, and `ModelReaderWriter` reads it.
- The default 8192 reply budget let the model reason for minutes. 2048 tokens with low effort keeps the example short.
- The key comes from the environment, never from the file. The run used the existing OpenRouter credential.
