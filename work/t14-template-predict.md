---
pr:
pr_url:
jira:
epic: M3
status: open
created: 2026-10-16
updated: 2026-10-17
tags: []
wiki_refs: []
---

# T14 — Template and Predict strategies

## Context

First end-to-end agentic call: single-shot structured output with validation retry.

Depends on: T13. Plan: [`docs/port-plan.md`](../docs/port-plan.md).

Upstream (pin in the plan):
- `src/nooa/strategies/base.py`
- `src/nooa/strategies/template.py`
- `src/nooa/strategies/predict.py`
- `src/nooa/strategies/composite.py`
- `src/nooa/strategies/generated_code.py (ArgumentValidator, ReturnValueValidator)`

## Progress

- [x] Port `GenerationStrategy` and `IRuntimeServices` (done in T12; T14 uses them)
- [x] Port `TemplateStrategy`
- [x] Port `PredictStrategy` with validation errors fed back to the model and `max_retries`
- [x] Return-type schema: wrap scalar types as `{"result": T}`
- [x] Port quickstart 01 and 02 as offline acceptance tests
- [x] Port tests: Predict / Template parts of `tests/strategies/`

## Notes

Exit check for M3.

Done in T14 (details in `docs/porting-notes.md`, "Template and Predict strategies (T14)"):
- `TemplateStrategy`, `PredictStrategy` (`src/Nooa/Strategies/`), name `TEMPLATE` and `PREDICT` as upstream.
- `PredictConfig.Preconditions` and `Postconditions` (explicit-field properties). `MethodPrecondition`, `MethodPostcondition`, `InvariantException` and `StrategyConditions` (`strategy_validation.py`). As upstream, Predict does not run them yet. T17 runs them in CodeAct.
- `CurrentCall.FormatParametersAsCode`. `StructuredOutputParser` (shared by the client and Predict) and `StructuredOutputException.IsMalformedJson`.
- Default strategy: there is no implicit default. A method needs `[Strategy(typeof(...))]` or `AgentOptions.DefaultStrategy`. T17 sets CodeAct as the default, as upstream. Quickstart 01 (no strategy attribute) sets `DefaultStrategy = new PredictStrategy()` in its test.
- Not ported (reasons in porting-notes): media params, `Temperature`/`TopP` forwarding, `[Hidden]` output members, span attributes and harness metrics, the `required` check for missing members.

