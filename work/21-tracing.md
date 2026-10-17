---
pr: 21
pr_url: https://github.com/phaser/labs-oo-agents-csharp/pull/21
jira:
epic: M5
status: merged
created: 2026-10-16
updated: 2026-10-17
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

- [x] `ActivitySource("Nooa")` spans for agent call, generation and LLM call (cell and tool spans moved to T17, see Notes)
- [x] OpenInference attribute names
- [x] OTLP export and a JSONL file exporter (no OpenTelemetry package, see Notes)
- [x] Secret scrubber for span attributes (and env credential values, `[NoTrace]`, size bounds)
- [x] Port quickstart 06 as an offline acceptance test
- [x] Port the applicable `tests/tracing/` tests

## Notes

Langfuse, Phoenix and the journal exporter are out of scope.

- Design and deviations: [`docs/porting-notes.md`](../docs/porting-notes.md) section "Tracing (T19)".
- **Package decision:** no NuGet package added. OTLP JSON is written by `OtlpJson`; `ActivitySource` is in the BCL. An application can use the OpenTelemetry SDK with `AddSource("Nooa")`.
- **Moved to T17:** cell (code execution), tool-execution and method-invocation spans. `IInstrumentationHooks` has no such methods. T17 adds `BeforeCodeExecution` and similar hooks with their callers, and `OpenInferenceHooks` implements them (use `SpanAttributes.Set`, honour `TraceSuppression`).
- Quickstart 06 readiness: `Quickstart06TracingTests` runs a `MathAgent` offline. T22 ports the example itself (`NooaTracing.Enable(new JsonlSpanExporter("traces/quickstart-06"))`, wrap the client in `TracingLlmClient`).
