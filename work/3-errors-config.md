---
pr: 3
pr_url: https://github.com/phaser/labs-oo-agents-csharp/pull/3
jira:
epic: M1
status: open
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

- [x] Remove `--ignore-exit-code 8` from `.github/workflows/ci.yml`: moved to T11 (`tests/Nooa.Generators.Tests` still has no tests, so the flag is still needed)
- [x] Port `NemoOOAgentsError` hierarchy to `Nooa.Errors` (as `NooaException`)
- [x] Port `errors/formatting.py`: moved to T15 (Python-traceback specific)
- [x] Port `ExecutionConfig`, `TruncationConfig` (merge rules), `CodeActConfig` (scalars), `LoopGuardConfig`, `PredictConfig` as records; deferred fields listed in porting-notes
- [x] Port layered YAML config (bundled -> user -> project -> env) with YamlDotNet
- [x] Port `secrets.yaml` loading (never override existing env vars) and `WriteSecretEnv`
- [x] Port tests: `tests/config/` (applicable), `test_layered_config.py`, `test_secrets.py`, truncation config tests; `test_error_formatting.py` and `test_error_line_numbers.py` moved to T15

## Notes

Secrets never go to logs or traces. Tests assert that log text and exception messages hold no secret value.

Decisions (details in `docs/porting-notes.md`):
- `ConfigBase` record: explicit-field tracking with C# 14 `field` keyword, `MergeWith`, explicit `Validate()`.
- Exceptions: `*Exception` names, root `NooaException`.
- `LlmConfig.Chain` takes bundled paths as a parameter (no entry points, D14).
- Project root = nearest ancestor with `.nooa` or `.git` (upstream: `pyproject.toml`).
- Warnings go to an optional `ILogger`.

Packages:

| Package | Version | Published | License |
|---------|---------|-----------|---------|
| YamlDotNet | 18.1.0 | 2026-06-26 | MIT |
| Microsoft.Extensions.Logging.Abstractions | 10.0.12 | 2026-09-08 | MIT |

Deferred and moved: `ModelConfig`/`ResolvedConfig` to T10; `errors/formatting.py` to T15; `CodeActConfig` restrictions/sandbox/prefill/conditions to T17; `PredictConfig` conditions to T14; `--ignore-exit-code 8` removal to T11; `ReflexionConfig` and summarizer configs stay in the backlog (B10, B04).
