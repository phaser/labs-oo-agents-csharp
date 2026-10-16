---
pr:
pr_url:
jira:
epic: M3
status: todo
created: 2026-10-16
updated: 2026-10-16
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

- [ ] Port `GenerationStrategy` and `IRuntimeServices`
- [ ] Port `TemplateStrategy`
- [ ] Port `PredictStrategy` with validation errors fed back to the model and `max_retries`
- [ ] Return-type schema: wrap scalar types as `{"result": T}`
- [ ] Port quickstart 01 and 02 as offline acceptance tests
- [ ] Port tests: Predict / Template parts of `tests/strategies/`

## Notes

Exit check for M3.

Deferred `PredictConfig` fields from T03: `Preconditions` and `Postconditions`. Add them here as explicit-field properties (see `ConfigBase`).
