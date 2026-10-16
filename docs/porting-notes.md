# Porting notes

One entry per deliberate deviation from upstream. Upstream pin: see [`port-plan.md`](port-plan.md).

Format: `## <upstream concept>` → **C#:** what we do. **Why:** reason. **Tests:** upstream tests skipped or changed.

## `...` method bodies

**C#:** `[Agentic] public partial Task<T> M(...);`. The source generator emits the body.
**Why:** C# has no `...` body. A partial method with no body is a compile error, so the generator is mandatory.
**Tests:** `test_ellipsis_detection_exec.py` and the bytecode fallback tests do not apply.

## Pre-ellipsis code

**C#:** Not supported in v1. Upstream runs statements before the `...` first.
**Why:** a partial declaration has no body. A later option is an `OnBefore<Method>` partial hook.
**Tests:** `test_pre_ellipsis_code.py` is deferred.

## Docstrings as prompts

**C#:** XML doc comments. The generator embeds `<summary>` (and `<remarks>`) as string constants.
**Why:** doc comments are not in the compiled assembly.

## REPL action language

**C#**, through Roslyn scripting. Upstream uses Python `exec`.
**Why:** decision D1 in the plan. All prompts and error hints are rewritten for C#.

## Dynamic method guard

**C#:** Not ported. `__setattr__` guards are not needed in a static language.
**Tests:** `test_dynamic_method_guard.py` does not apply.

## Error hierarchy (`errors/__init__.py`, `errors/storage.py`)

**C#:** `Nooa.Errors`, one file per type. The root is `NooaException`. Names follow the C# `*Exception` convention.
**Why:** C# convention. Each type has only the constructors it needs: `(message)`, plus `(message, innerException)` where wrapping is plausible.

| Upstream | C# |
|----------|----|
| `NemoOOAgentsError` | `NooaException` |
| `GenerationError` | `GenerationException` |
| `GenerationAborted` | `GenerationAbortedException` |
| `LoopDetectedError` | `LoopDetectedException` |
| `ValidationError` | `ValidationException` |
| `XMLFormatError` | `XmlFormatException` |
| `RestrictedCodeError` | `RestrictedCodeException` (`LineNumber`; `original_exception` is `InnerException`) |
| `NemoOOAgentsRuntimeError` | `NooaRuntimeException` |
| `SerializationError` | `SerializationException` |
| `DeserializationError` | `DeserializationException` |
| `SnapshotNotFoundError` | `SnapshotNotFoundException` |
| `StorageNotConfiguredError` | `StorageNotConfiguredException` |
| (new) | `ConfigValidationException` (see "Config validation") |

`DynamicMethodAdditionError` is not ported (see "Dynamic method guard").

## `errors/formatting.py`

**C#:** Not ported in T03. Moved to T15 (`work/t15-roslyn-repl.md`).
**Why:** it formats IPython/Python tracebacks. The C# version needs Roslyn diagnostics, so it belongs with the REPL engine.
**Tests:** `test_error_formatting.py` and `test_error_line_numbers.py` move with it.

## Config records (pydantic `model_fields_set`)

**C#:** `ConfigBase` (a record) keeps the set of explicitly set property names. Each property is
`{ get; init { field = value; MarkSet(); } } = default;` (C# 14 `field` keyword). A property initializer writes the backing field
directly, so a default is not "set". `MergeWith` copies only the set properties of `other`, and an explicit `null` counts as set.
`with` copies get their own set. The tracking set does not take part in value equality.
**Why:** pydantic merge semantics. Frozen models become immutable records (`init` only).
**Tests:** `ConfigBaseTests` verify the tracking. Upstream `test_*_frozen` tests are replaced by an init-only check. `model_construct`/`model_validate` tests do not apply.
**Message:** the "no fields set" error is an `ArgumentException`. The text is adapted: it does not mention `model_dump()`/`model_validate()`.

## Config validation

**C#:** Validation is explicit: `config.Validate()` throws `ConfigValidationException` with the upstream multi-line format
(`Invalid CaptureConfig:\n  - ...`). `TruncationConfig.Validate()` validates its sub-configs first. Bullets use C# property names.
**Why:** pydantic validates in the constructor. A C# object initializer runs after the constructor, so construction cannot validate.
`MergeWith` does not re-validate (upstream `model_copy` does not either). Callers validate the final config.

## Config types in T03 and deferred ones

**Ported:** `ExecutionConfig`, `CaptureConfig`, `MediaCaptureConfig`, `FormatConfig`, `TruncationConfig` (`TruncationConfig.Default` replaces `DEFAULT_TRUNCATION_CONFIG`),
`LoopGuardConfig`, `CodeActConfig` (scalar fields), `PredictConfig` (`OutputSerialization` is the enum `PredictOutputSerialization`).
**Changed:** `CodeActConfig.CellTimeout` is `TimeSpan?` (upstream `float | None` seconds).
**Deferred:**
- `CodeActConfig.restrictions`, `sandbox`, `execution_backend`, `prefill`, `preconditions`, `postconditions`: T17 and T16, or B01 (sandbox).
- `PredictConfig.preconditions`, `postconditions`: T14.
- `ModelConfig`, `ResolvedConfig`, `get_model_config` (`config/model_config.py`, `config/resolved.py`): T10 (they need the model registry).
- `ReflexionConfig` (B10) and the summarizer configs (B04): backlog items.
- The `CodeActConfig` validator that rejects the removed `text_only_*` options: it guards Python keyword arguments. It does not apply to C#.
**Tests skipped:** `test_blocked_config.py` (restrictions), `test_resolved_config.py` (`ModelConfig`), `test_summarizer_configs.py`, `TestReflexionConfig`, `TestStrategyWiring`, `test_max_nesting_depth_enforced` (runtime).

## Layered config, `llm_config.py`, `paths.py`

**C#:** `Nooa.LayeredConfig`, `Nooa.LlmConfig`, `Nooa.Secrets`, `Nooa.NooaPaths` (static classes, root namespace). YamlDotNet parses YAML.
Env var names stay as upstream (`NEMO_OO_USER_DIR`, `NEMO_OO_PROJECT_DIR`, `NEMO_OO_LLM_CONFIG`, `NEMO_OO_SECRETS`) so existing files keep working.
- Warnings go to an optional `ILogger` parameter (`Microsoft.Extensions.Logging.Abstractions`). Logs never include file content, because files can hold secrets.
- Bundled defaults: upstream uses the `nooa.bundled_configs` entry-point group. Plugin discovery is not in v1 (D14), so `LlmConfig.Chain(bundledPaths)` takes an explicit list of file paths.
- Project root: upstream walks up to `pyproject.toml`. C# walks up from the current directory to the nearest directory with a `.nooa` directory or a `.git` entry. It falls back to the current directory.
- YAML scalars: unquoted `true`, `3`, `0.5` and `null` become bool, integer, floating-point and null values. Quoted scalars stay strings. This matches `yaml.safe_load`.
- Symbolic links: `NooaPaths.Canonicalize` replaces `Path.resolve()`.
- `write_secret_env` becomes `Secrets.WriteSecretEnv`. It raises `ArgumentException` (bad input) or `InvalidDataException` (bad file) in place of `ValueError`. File mode is 0600 on Unix.

## Untrusted config files (security deviations)

**C#:** Two protections that upstream does not have. A project-layer `.nooa/` directory can come from a cloned repository, so its files are untrusted.
- `Secrets.LoadSecretsIntoEnv` is layer-aware. Every layer: the name must match `^[A-Za-z_][A-Za-z0-9_]*$` and must not be in `SecretEnvDenylist` (one place; defence in depth): `LD_*`, `DYLD_*`, `DOTNET_*`, `COMPlus_*`, `NEMO_OO_*`, `NOOA_*`, `XDG_*`, `GIT_*`, `SSH_*`, `NPM_CONFIG_*`, `PIP_*`, `PATH`, `BASH_ENV`, `EDITOR`, `VISUAL`, `PAGER`, `ZDOTDIR`, `NODE_PATH`, shell and runtime option variables, CA-bundle variables, and the suffixes `*_PROXY`, `*_BASE_URL`, `*_API_BASE`, `*_API_URL`, `*_ENDPOINT`, `*_HOST`, `*_URL`. Project layer (`.nooa/secrets.yaml`, or any file inside the project directory or root, including one named by `NEMO_OO_SECRETS`): only credential-shaped names are accepted (`*_API_KEY`, `*_KEY`, `*_TOKEN`, `*_SECRET`, `*_PASSWORD`, case-insensitive). The user-layer file is always trusted. A refused project value never replaces a trusted one. A warning names the variable, never the value. The "never override an existing variable" rule stays. `WriteSecretEnv` refuses denylisted names.
- `LayeredConfig.ParseYaml` rejects YAML anchors and aliases (alias expansion can build a huge object graph) and nesting deeper than 64 levels. The check scans parser events before deserializing. `LoadLayeredYaml` skips the file with a warning that names the file, as for any bad layer. Direct callers get `InvalidDataException`.
**Why:** an attacker file could set `OPENAI_BASE_URL` and send an existing API key to another host, or load code through `LD_PRELOAD`. A YAML alias bomb could exhaust memory.
**Tests:** new tests in `SecretsTests` and `LayeredConfigTests`. Open item: project-layer `api_base` / `api_key_env` in `llm_config.yaml` can redirect a key too (T10).
