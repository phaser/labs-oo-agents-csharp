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


## `ILlmClient`, `FakeLlmClient`, token counter (T08)

**C#:** `Nooa.Llm.ILlmClient`, `LlmMessage` (`JsonMessage`, `ResponseMessage`, `CacheBoundary`), `ToolDefinition`, `FakeLlmClient`, `FakeLlmCall`, `FakeLlmResponseExhaustedException`, `TokenCounter`.
- **Interface:** `CallAsync(messages, tools, outputType, ct)` plus `CountTokens(string)` and `ContextWindow` (the runtime needs both). No sync `call`. No per-call `**kwargs`: call settings belong to the client config (T09, T10). `FakeLlmCall.kwargs` is not ported for the same reason.
- **Messages:** upstream passes `dict | LLMResponse | CacheBoundary`. C# uses a closed record hierarchy: `JsonMessage(JsonObject)` for OpenAI-style messages, `ResponseMessage(LlmResponse)` for a replayed response, `CacheBoundary`. `LlmMessage.User/System/Tool` build the common messages. `CacheBoundary` has no mapping view (that was for NeMo Relay JSON; use `PublicMessage()`-style projection in T13/T09 if needed).
- **Tools:** `ToolDefinition(Name, Description, ParametersSchema)`. The upstream `Tool.callable` stays in the runtime. Schema generation (`get_parameter_schema`, `_clean_schema`) is T09/T11. `output_model` is `Type?`.
- **Fake:** no `prepare_chat_messages` or `apply_cache_policy` pass over recorded messages (T09/T13 own message projection). `LastMessages` and `Calls[i].Messages` hold the input as given, with `JsonMessage` deep copies. Transcript response copy is a record `with` copy plus a deep metadata copy (no `model_copy(deep=True)` fallback chain). The non-copyable leaf test is skipped: records hold no such leaves.
- **Fluent script API (new):** `Enqueue`, `EnqueueText`, `EnqueueToolCall`, `EnqueueReasoning`, `EnqueueStructured<T>`. The static factories (`SimpleMessage`, `WithToolCall`, `WithReasoning`, `WithCodeResponses`) use them, so usage numbers match upstream (text: 10 in, one out per word; tool call 10/5; reasoning 15/10). `with_tool_call` arguments are any object serialized with default `System.Text.Json` options (names are not renamed).
- **Exhaustion:** `FakeLlmResponseExhaustedException : NooaException` (upstream `RuntimeError`). The legacy-subclass factory test is skipped (Python-only).
- **Token counter:** `TokenCounter.CharApproximate` counts Unicode code points (runes), as Python `len` does, not UTF-16 units. `FakeLlmClient.CountTokens` is `len/4 + 1`, as upstream fake.
- **Tests:** ported from `tests/test_llm.py` and `tests/unifiedllm/test_fake_transcript.py`. Skipped: the `FrozenInstanceError` and `MappingProxyType` mutation checks (records and read-only lists; no runtime failure to assert), the non-copyable leaf test, the legacy subclass test.

## Storage (`runtime/event_backend.py`, `storage/`, T07)

**C#:** `Nooa.Runtime`: `IEventBackend`, `InMemoryEventBackend`. `Nooa.Storage`: `IStorageManager`, `InMemoryStorageManager`, `SqliteStorageManager`, `SqliteEventBackend`, `SqliteJournalMode`, `SnapshotInfo`. `Nooa.Errors`: `SessionAlreadyActiveException`, `CorruptDatabaseException`. The SQLite DDL is the upstream DDL (`events`, `active_tags`, `snapshots`, `schema_version`, same indexes). Package: `Microsoft.Data.Sqlite` 10.0.12 (MIT).
- **Update:** `IEventBackend.Update(tag, Action<EventBase>)` replaces `update(tag, **fields)`. Events are records with init-only properties. The caller sets the mutable members (`Status`, `Tag`, `Metadata`, `ToolCallEvent.Result`). The SQLite backend deserializes, runs the delegate, and saves the event with its `status` column. The in-memory backend changes the live object.
- **Persistence (D11):** events serialize only through `EventBase` with `NooaJson.Options`. The restore allowlist is the `[JsonDerivedType]` list (`EventTypeAllowlist`). No `register_event_type`: a custom event type needs a line in the list. Upstream falls back to a generic `Metadata` event for an unknown `event_type`. The port throws `DeserializationException` that names the type. Corrupt JSON, a non-object root, a missing `event_type` and a bad field are skipped with a warning, as upstream does. The legacy `LLMOutput` to `LLMResponse` migration is not ported (no C# sessions exist).
- **Snapshots:** `IStorageManager` has the blob API only: `SaveSnapshotJson`, `LoadSnapshotJson`, `GetLatestSnapshotId`, `ListSnapshots`. A snapshot is a JSON object text. `AgentSnapshot`, `serialize`/`deserialize`, `markers.py`, `json_snapshot.py`, `save_snapshot(agent)`, `restore_snapshot` and `restore_latest_snapshot` walk agent fields and move to T12. `persistent_vars.py` and `snapshot_vars.py` move to T15.
- **Untrusted files (security deviation):** a session file can come from another machine or a cloned repository. Upstream says the database must be trusted because its restore runs `exec()`. The port has no code execution on restore, and adds: the schema version is checked when the file opens, before any event is read (`InvalidDataException` on a mismatch); each stored event and snapshot has a size limit, and the database has a count and a total size limit (`StorageLimits`, one place: 64 MiB per row, 1,000,000 events, 1 GiB of event data). Open checks `COUNT(*)` and `SUM(length(data))`. `AllEvents` reads pages (at most 100 rows or 16 MiB) and deserializes row by row, so memory stays bounded and the limits hold while reading. A row over a limit is never read into memory and throws `DeserializationException`; `LoadSnapshotJson` checks that the text is a JSON object (depth limit 64); the database path is made a full path, so SQLite never reads it as a URI; all SQL is parameterized. A caller must still check the snapshot `version` and its types (T12).
- **Hostile database objects (security review, hardens upstream):** a session file can carry triggers, views, generated columns or changed table definitions that run when this program queries it. Right after open the connection sets `PRAGMA trusted_schema=OFF` and `SQLITE_DBCONFIG_DEFENSIVE` (through `SQLitePCL.raw.sqlite3_db_config`; `Microsoft.Data.Sqlite` has no own API). Before any other query, `SqliteSchema.Verify` compares every `sqlite_master` entry with the entry that the DDL creates in an empty database (name, type, table, SQL text). Any other object (trigger, view, extra table or index, changed definition) throws `StorageSchemaException` and the lock is released. A missing object is allowed; `Ensure` creates it. An existing non-empty file also runs `PRAGMA quick_check(1)`; a failure throws `CorruptDatabaseException`. Tests: `SqliteStorageManagerTests` (untrusted file section).
- **Session lock:** upstream uses `flock` on `<name>.lock` with a `<pid> <hostname>` record. .NET takes a mandatory file-share lock, which blocks readers of the locked file. The port locks `<name>.lock.guard` and keeps the record in `<name>.lock`, so `ReadLockOwner` works while the lock is held. `DeleteDatabase` and `ReadLockOwner` are static methods of `SqliteStorageManager`. Security review: the lock path and the guard path are refused when they are symbolic links (`FileInfo.LinkTarget`, `UnauthorizedAccessException`), and both files are created with mode 0600 on Unix. The record is written by path, not through the guard handle, because the guard handle is locked against readers; the link check and the open are not atomic, so the lock directory must be writable by the user only. An owner check (file uid equals current uid) is not done: .NET has no uid API without P/Invoke.
- **Not ported:** virtiofs detection (the default journal mode is WAL; `SqliteJournalMode.Delete` is the explicit choice for a shared mount); reconnect and retry on `disk I/O error` (needs fault injection to test); `check_same_thread` (one connection with one lock serializes all use); `must_exist` throws `FileNotFoundException` in place of `sqlite3.OperationalError`; `save_snapshot_json` is `SaveSnapshotJson` and also requires a JSON object (upstream: any JSON).
- **Small differences:** `GetLatestSnapshotId` breaks a `created_at` tie by `rowid`. `AllEvents` on the in-memory backend returns a copy. `InMemoryStorageManager` returns null and an empty list for the snapshot queries and throws `StorageNotConfiguredException` for save and load.
**Tests:** the backend contract (`tests/test_event_backend_protocol.py`, round-trip types of `test_event_backend_roundtrip.py`) runs against both backends through the abstract `IEventBackendTests`. `SqliteEventBackendTests`, `SqliteStorageManagerTests` and `SessionLockTests` port `test_sqlite_corruption_resilience.py`, `test_sqlite_specific.py`, `test_sqlite_close_commit.py`, `test_session_resume_tag_collision.py` (SQLite parts) and `tests/storage/test_sqlite_public_options.py`. New tests cover unknown event types, oversized rows, tampered snapshots, schema mismatch and SQL text in tags and ids.
**Skipped tests:** `test_sqlite_reconnect.py` and the virtiofs tests (not ported); registry and `LLMOutput` tests in `test_sqlite_specific.py`; `DecisionRecord`, `Reasoning`, `PythonOutput` value and `_json_safe` tests in `test_event_backend_roundtrip.py` (types or helpers not in the port); every test that needs an `Agent` or `EventManager` (T12); `tests/storage/test_snapshot.py`, `test_serialization.py`, `test_markers.py`, `test_snapshot_vars.py` and `tests/runtime/test_snapshot.py` (T12, T15).

## Storage hardening follow-up (T07b)

**C#:** `SqliteSchema.Verify` compares full `sqlite_master` rows (type, name, table, sql) as a set and refuses any trigger or view outright. Keying by name alone let a trigger that shares a table's name hide behind the table row. `SessionLock.Acquire` refuses a lock directory that is writable by group or others (Unix): .NET has no `O_NOFOLLOW` open, so the symlink check and the open are not atomic, and a directory that only the user can write closes that race.
**Why:** security review of PR #8.
**Tests:** `Open_TriggerNamedLikeATable_IsRefused`, `Acquire_DirectoryWritableByOthers_IsRefused`.
||||||| parent of a89d5c0 (Add the agent source generator and the runtime seam)

## Agent source generator (`metaclass.py`, `decorators.py`, `ellipsis_detection.py`, `strategies/current_call.py`)

**C#:** `Nooa.Generators` (incremental generator). `[Agentic]` partial methods get a body that builds a `CurrentCall` and calls `IAgenticTarget`. See `work/t11-source-generator.md` for the diagnostics `NOOA0001`-`NOOA0009`.
**Why:** C# has no metaclass, decorators or `...` bodies (decisions D3-D6).
**Differences:**
- `{Member.Path}` placeholders cover member paths on `this` only. Upstream `eval` also reaches method parameters and any expression. Use `{{` and `}}` for literal braces.
- `@strategy(llm=..., decision_model=..., context=..., truncation=...)` keeps only the strategy type (`[Strategy(typeof(X))]`). The rest returns with T12 and T14 as attributes or call options.
- Only the trailing `CancellationToken` is special. `CurrentCall` has one ordered argument list instead of `args` and `kwargs`.
- No `async def` / sync method split: every agentic method returns a task.
**Tests:** `test_metaclass.py` cases about wrapping, tracing, hooks, call stacks, generators with spans and context managers do not apply to a generator. Runtime parts move to T12. `test_decorator_strategy.py` (`_strategy_override` attributes) becomes `CurrentCall.StrategyType` tests. `test_pure_python_method_rejection.py` belongs to the REPL (T15).

