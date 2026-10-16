---
pr:
pr_url:
jira:
epic: M1
status: todo
created: 2026-10-16
updated: 2026-10-16
tags: []
wiki_refs: []
---

# T03 — Errors and configuration

## Context

Port the exception hierarchy, LLM-facing error formatting, config records and layered YAML config.

Depends on: T02. Plan: [`docs/port-plan.md`](../docs/port-plan.md).

Upstream (pin in the plan):
- `src/nooa/errors/`
- `src/nooa/config/`
- `src/nooa/layered_config.py`
- `src/nooa/llm_config.py`
- `src/nooa/secrets.py`
- `src/nooa/paths.py`

## Progress

- [ ] Remove `--ignore-exit-code 8` from `.github/workflows/ci.yml` (added in T02 while no tests exist)
- [ ] Port `NemoOOAgentsError` hierarchy to `Nooa.Errors`
- [ ] Port `errors/formatting.py` (error text with cell line numbers)
- [ ] Port `ExecutionConfig`, `TruncationConfig` (merge rules), `CodeActConfig`, `PredictConfig` as records
- [ ] Port layered YAML config (bundled → user → project → env) with YamlDotNet
- [ ] Port `secrets.yaml` loading (never override existing env vars)
- [ ] Port tests: `tests/config/`, `test_layered_config.py`, `test_secrets.py`, `test_error_formatting.py`, `test_error_line_numbers.py`

## Notes

Secrets never go to logs or traces.
