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

# T10 — Model registry and reasoning levels

## Context

Port alias-based model configuration so agents can name a model by alias.

Depends on: T09, T03. Plan: [`docs/port-plan.md`](../docs/port-plan.md).

Upstream (pin in the plan):
- `src/nooa/unifiedllm/registry.py`
- `src/nooa/unifiedllm/reasoning.py`
- `src/nooa/unifiedllm/limits.py`
- `src/nooa/util/quickstart.py`

## Progress

- [ ] Port YAML alias registry (`model_name`, `api_base`, `api_key_env`, `context_window`, `drop_params`)
- [ ] Port reasoning levels map
- [ ] Port `ContextLimits`
- [ ] Port quickstart default LLM setup (env / dotenv)
- [ ] Port `ModelConfig`, `ResolvedConfig`, `get_model_config` (`config/model_config.py`, `config/resolved.py`), moved from T03; port `tests/config/test_resolved_config.py`
- [ ] Port tests: `test_model_registry`, `test_llm_config_chain.py`

## Notes

The onboarding wizard (`unifiedllm/connect/`) is out of scope.
