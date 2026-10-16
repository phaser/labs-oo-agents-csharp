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
- `Secrets.LoadSecretsIntoEnv` is layer-aware. Every layer: the name must match `^[A-Za-z_][A-Za-z0-9_]*$` and must not be in `SecretEnvDenylist` (one place; defence in depth): `LD_*`, `DYLD_*`, `DOTNET_*`, `COMPlus_*`, `NEMO_OO_*`, `NOOA_*`, `XDG_*`, `GIT_*`, `SSH_*`, `NPM_CONFIG_*`, `PIP_*`, `PATH`, `BASH_ENV`, `EDITOR`, `VISUAL`, `PAGER`, `ZDOTDIR`, `NODE_PATH`, shell and runtime option variables, CA-bundle variables, and the suffixes `*_PROXY`, `*_BASE_URL`, `*_API_BASE`, `*_API_URL`, `*_ENDPOINT`, `*_HOST`, `*_URL`. Project layer (`.nooa/secrets.yaml`, or any file inside the project directory or root, including one named by `NEMO_OO_SECRETS`): only credential-shaped names are accepted (`*_API_KEY`, `*_KEY`, `*_TOKEN`, `*_SECRET`, `*_PASSWORD`, case-insensitive). Trust fails closed: a file is trusted only when it is the user-layer file or a `NEMO_OO_SECRETS` path outside the project; paths compare case-insensitively on macOS and Windows (`NooaPaths.PathComparison`). An untrusted file cannot replace or delete a name that a trusted layer set, and cannot delete any name — **upstream lets the project layer override the user layer; the port does not**, because a project file could swap in an attacker-owned API key and receive the prompts. A refused project value never replaces a trusted one. A warning names the variable, never the value. The "never override an existing variable" rule stays. `WriteSecretEnv` refuses denylisted names.
- `LayeredConfig.ParseYaml` rejects YAML anchors and aliases (alias expansion can build a huge object graph) and nesting deeper than 64 levels. The check scans parser events before deserializing. `LoadLayeredYaml` skips the file with a warning that names the file, as for any bad layer. Direct callers get `InvalidDataException`.
**Why:** an attacker file could set `OPENAI_BASE_URL` and send an existing API key to another host, or load code through `LD_PRELOAD`. A YAML alias bomb could exhaust memory.
**Tests:** new tests in `SecretsTests` and `LayeredConfigTests`. Open item: project-layer `api_base` / `api_key_env` in `llm_config.yaml` can redirect a key too (T10).

## Event IR (`context_blocks/events.py`, `events.py`, `llm_types.py`, `_immutable_json.py`)

**C#:** Records under `Nooa.ContextBlocks` (`EventBase`, `UserEvent`, `AssistantEvent`, `ToolCallEvent`, `ToolResult`, enums), `Nooa.Events` (runtime and trace events) and `Nooa.Llm` (`LlmResponse`, parts, `LlmUsage`). `Nooa.Json` has `NativeJson` and `NooaJson.Options`.
- **Persistence (D11):** `EventBase` carries `[JsonPolymorphic("event_type")]` and one `[JsonDerivedType]` per event. The strings equal upstream class names (`Task`, `LLMResponse`, `LLMCallStart`, ...). A test checks that every concrete event is in the list. There is no auto-registry and no open `Metadata` subclass with `extra="allow"`: a new type needs a line in the list. Serialize and deserialize through `EventBase`, with `NooaJson.Options` (snake_case names and enum values).
- **Names:** `Task` is `TaskEvent` (clash with `System.Threading.Tasks.Task`). `PythonOutput` is `CellOutput` and persists as `CellOutput` (the cell language is C#, values are not Python objects). `LLM*` is `Llm*` in C#. T13 must render `TaskEvent` as `Task` for the model.
- **Rendering hooks:** `pformat`, `__instance_values__`, `searchable_fields` and the `repr=False` / `spec(max_string=...)` field rules are not ported (they need Agentdoc, T05/T06). The XML doc of each property says "Not shown to the model" or "must not truncate" for the same fields as upstream. T13 turns these into a renderer rule (attribute or table). Empty-value suppression (`None`, `""`, empty containers; keep `0` and `false`) goes to the same place. `render_message` and `CacheBoundary` also move to T13 / T09.
- **Arbitrary values:** `Any` fields (`PythonOutput.value`, `Notification.value`, `images`, `Task.images`) are `JsonNode?` / `IReadOnlyList<JsonNode>`. The caller converts to JSON, so `_json_safe` (the pformat fallback for un-encodable objects) is not needed here. The producer (T15, T18) owns the conversion and the fallback.
- **`LlmResponse`:** no flat constructor (`content=`, `tool_calls=`), no legacy flat-archive migration, no `llm_state` rejection, no Mapping protocol (`__getitem__` etc.). `PublicMessage()` replaces the mapping view. `model_copy` guarding is `ReplaceParts` / `ReplaceText`; a plain `with { Parts = ... }` keeps native state (documented on the type). `raw_response` and `parsed` are `[JsonIgnore]` `object?`. `assistant_message` takes `ToolCall` only (no synthetic dictionary calls). `LlmUsage.FromProvider` takes `JsonNode?`; Python object shapes (`_asdict`, `model_dump`) do not apply.
- **`NativeJson`:** deep copy on construction and on read, JSON-object only, structural equality. It replaces `freeze` / `NativeJSON`.
- **Not ported:** `Reasoning` (legacy, nothing emits it), `TuiSessionResumed`, `TuiSessionCleared` (TUI is outside v1), `DecisionCallStart`, `DecisionCallEnd`, `DecisionRecord` (decisions are B03), `ExecutionResult`, `ExecutionSignal` (REPL: T15), `CODEACT_INLINE_RETURN` (T17), `EventStatus`-independent `Event` union aliases.
- **Timestamp:** `DateTimeOffset.Now` (upstream: naive local `datetime`).
**Tests:** ported from `tests/context_blocks/test_events.py`, `tests/test_events.py` (event types, summary, `PythonOutput` fields), `tests/unifiedllm/test_llm_types.py` (usage), `test_llm_response_event.py` (durable fields), `test_replay_copy_ownership.py` (metadata ownership). Skipped: `test_event_auto_registration.py`, `test_event_types.py` alias and `EventManager` parts (registry, EventManager: T18/T07), `ExecutionResult` tests (T15), pformat and privacy tests (`test_response_display_privacy.py`: T06/T13), `FakeLLMClient` alias test (T08).

## `doc()` rendering (agentdoc, T05)

**C#:** `AgentDocs.Doc(obj | objs, concise, inlineDepth, provider)` in `Nooa.Agentdoc`. The object is a `Type`, a `MethodInfo` or an instance.
Output is C# declaration text. A type shows `///` summary lines, a header (`class`, `record`, `struct`, `enum`, `interface`, `static class`, `abstract class`, and ` : Base` for a custom base type),
then fields and properties, a blank line, and methods. Fields and properties are public instance members, base type first.
Methods are public (instance and `static`), derived type first. Members of `System` base types are left out.
`Description` text shows as a `// text` comment after a field. With no description, the first line of the field type summary is used.
A method shows its summary, then `Args:` and `Returns:` lines (from parameter and return descriptions), then the signature.
Referenced types follow under `## Referenced Types`, in concise form, sorted by name, each type once (`inlineDepth` levels, default 1).
A standalone method shows its type name (`Warehouse.CanFulfillOrder`). Instance values are shown for fields and auto-properties. Computed properties are never evaluated.

```
/// Checks stock.
class Warehouse
{
    int Count = 7;  // Items in stock
    string Name { get; set; } = "East";

    /// Check if the order can be filled.
    /// Args:
    ///   budget: Maximum price
    Task<OrderResult> CanFulfillOrder(List<string> items, double budget);
    static Warehouse Create(int size = 5, string? label = null);
}

## Referenced Types

class OrderResult
{
    bool Ok;
}
```

**Why:** the prompt shows C# to a model that writes C#. Nullable annotations come from `NullabilityInfoContext`. Unconstrained type parameters never get `?` (reflection reports them as nullable).
**Deviations:**
- Source order: reflection has none. `MetadataToken` order approximates it within one type. Fields come before properties. Members of partial types in several files can order differently.
- Hidden members: `[Hidden]` (root namespace `Nooa`) replaces `hidden` and `spec(hidden=True)`. `_`-prefixed and non-public members are excluded. There is no `spec(hidden=False)` opt-in, so the upstream "child unhides parent field" tests do not apply.
- Defaults: upstream shows source defaults of fields. Reflection cannot read field initializers, so a type shows no field defaults. An instance shows its current values. Parameter defaults show.
- Descriptions: `[Description]` replaces `Annotated[T, "desc"]` and `spec(description=...)`. Doc comment text comes from `IAgentDocProvider` (see T11).
- Value text: a placeholder in `ValuePreview` prints values until T06.
- Not ported: `spec(expand=False)`, `spec.define_doc` and the registry, `DocConfig`, `methods()`, `variables()`, module docs, adapters, protocols, extra runtime attributes (C# types have fixed members), `max_length`/`max_string`/`max_depth` limits (T06).
**Tests:** ported in `AgentDocsTests`, `MemberReaderTests`, `ReferencedTypesTests`, `DocFormatterTests`. The rest are listed in `work/t06-agentdoc-pformat.md`.

