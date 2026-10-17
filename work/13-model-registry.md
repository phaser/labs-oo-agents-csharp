---
pr: 13
pr_url: https://github.com/phaser/labs-oo-agents-csharp/pull/13
jira:
epic: M2
status: open
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

- [x] Port YAML alias registry (`model_name`, `api_base`, `api_key_env`, `context_window`, `drop_params`)
- [x] Port reasoning levels map
- [x] Port `ContextLimits`
- [x] Port quickstart default LLM setup (env / dotenv)
- [x] Port `ModelConfig`, `ResolvedConfig`, `get_model_config` (`config/model_config.py`, `config/resolved.py`), moved from T03; port `tests/config/test_resolved_config.py`
- [x] Port tests: `test_model_registry`, `test_llm_config_chain.py`

## Notes

Project-layer `api_base` / `api_key_env` in llm_config.yaml can redirect a key to another host — decide a trust rule in T10.

The onboarding wizard (`unifiedllm/connect/`) is out of scope.

## Result

Done in PR (see `pr`). Details and deviations: `docs/porting-notes.md` section "Model registry, reasoning levels, limits, quickstart (T10)".
- Trust rule (security note): untrusted layer cannot override a trusted alias, and cannot combine `api_key_env` with `api_base`/`endpoint`. Shared `LayerTrust` helper extracted from `Secrets`.
- Seam: `ILlmClientResolver` copied verbatim; `ModelRegistry` implements it.
- Not ported: `.env` loader (use `Secrets.LoadSecretsIntoEnv`), global `MODELS`, decision models, Responses API (B09), quickstart example types (T22).
- No new NuGet package.
