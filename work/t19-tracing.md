---
pr:
pr_url:
jira:
epic: M5
status: todo
created: 2026-10-16
updated: 2026-10-16
tags: []
wiki_refs: []
---

# T19 — Tracing

## Context

Emit spans for agent calls, LLM calls and cells with OpenInference attribute names.

Depends on: T17. Plan: [`docs/port-plan.md`](../docs/port-plan.md).

Upstream (pin in the plan):
- `src/nooa/tracing/`
- `src/nooa/runtime/hooks.py`

## Progress

- [ ] `ActivitySource("Nooa")` spans for agent call, LLM call, cell, tool
- [ ] OpenInference attribute names
- [ ] OTLP export (OpenTelemetry .NET) and a JSONL file exporter
- [ ] Secret scrubber for span attributes
- [ ] Port quickstart 06 as an offline acceptance test
- [ ] Port the applicable `tests/tracing/` tests

## Notes

Langfuse, Phoenix and the journal exporter are out of scope.
