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
**Tests:** new tests in `SecretsTests` and `LayeredConfigTests`. Open item: project-layer `api_base` / `api_key_env` in `llm_config.yaml` can redirect a key too (closed in T10, see below).

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
- Value text: `PFormat` prints values (T06). Doc limits: field values 3/50/1, parameter defaults 5/50/1 (`max_length`/`max_string`/`max_depth`), non-custom top-level object depth 3, as upstream.
- Not ported: `spec(expand=False)`, `spec.define_doc` and the registry, `DocConfig`, `methods()`, `variables()`, module docs, adapters, protocols, extra runtime attributes (C# types have fixed members).
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

## `PFormat` value printer (agentdoc, T06)

**C#:** `PFormat.Format(value, maxLength, maxString, maxDepth, expandAll, unquoteStrings)`, `PFormat.Format(value, FormatConfig)`, `PFormat.Write(TextWriter, ...)`,
`PFormat.TruncatingFormat(value, maxChars, ...)`, `TruncatingTextWriter` and `FileBackedTruncatingTextWriter`, in `Nooa.Agentdoc`. `FormatConfig.MaxLength/MaxString/MaxDepth` map to the parameters of the same name.
This is what the model reads as values in the `state` block and in REPL output.

| Value | Text |
|-------|------|
| `null`, `true`, `42`, `1.5`, `1.0`, `'x'` | as written. A whole `double`/`float` keeps `.0`. `NaN` and infinity are `double.NaN`, `double.PositiveInfinity`. Invariant culture. |
| string | C# regular literal with C# escapes (`"a\"b\n"`). A string with a line break and no other control character is a verbatim literal `@"..."` that keeps the line breaks (`"` becomes `""`). |
| enum | `Color.Red`, flags `Perm.R \| Perm.W`, unknown `(Perm)8` |
| `Type`, delegate | `typeof(List<int>)`, `<Func<int, int>>` |
| list, array, queue | `[1, 2]` |
| tuple | `(1, "a")` |
| set | `{1, 2}` |
| dictionary | `{"k": v}` |
| record, class, struct | `Person { Name = "Ada", Age = 36 }` (one line, `Name { }` when empty) |
| type with its own `ToString`, and framework objects | the `ToString` text (invariant culture, `DateTime` as ISO 8601). A throwing `ToString` gives `<TypeName>`. |
| cycle | `<cycle>` |

Truncation markers keep the upstream meaning (the marker means the value is not complete):
`string(len=N, [:H]="...", [-T:]="...")`, `List<int>(len=N, [:H]=[...], [-T:]=[...])` (ordered), `HashSet<int>(len=N, items={..., ...})` (unordered),
`Dictionary<string, int>(len=N, items={a: 1, ..., z: 26})`, `Type(text_len=N, [:H]="...", [-T:]="...")` (cut `ToString`), `Name { A = 1, ... }` (members), `{Dictionary<string, int>: 3 items}` / `[List<int>: 3 items]` / `Name { ... }` at the depth limit.
Head is the ceiling half and tail the floor half of the limit. A container whose one-line form is under 120 characters is one line. A longer one has one item per line (4-space indent). `expandAll` forces many lines.

**Why `{"k": v}` for dictionaries:** short, readable for the model, matches the upstream marker (`items={...}`), and cannot be mistaken for a set (a set has no colons). `{ ["k"] = v }` is longer for no gain. An empty set and an empty dictionary are both `{}`, as upstream.
**Why `Name { A = 1 }` for objects:** it is the shape of a C# record `ToString` and of an object initializer, so it reads as C#.
**Safety:** an object shows public fields and auto-properties only (`MemberReader`). It reads the backing field of an auto-property. No property getter runs, also not a virtual one that a derived type overrides with computed code. Members with a leading underscore, `[Hidden]` members and delegates are left out. Only user types are walked by reflection (`ReferencedTypes.IsCustom`): framework objects print `ToString`, so no framework internals are read. Only arrays, tuples, dictionaries, sets and types with a count are enumerated. A lazy `IEnumerable` never is. A reference is tracked while it is written, for `<cycle>`. Nesting is capped at 100 when the caller sets no depth, so a deep structure cannot overflow the stack. A one-line attempt stops when it reaches the width, so the cost stays low for deep structures.
`FileBackedTruncatingTextWriter` creates its file with a random name and mode 0600 (`CreateNew`). A prefix or suffix with a path separator throws. If the file fails, the writer falls back to memory (`FilePath` is null).
**Deviations:**
- `pformat(instance_mode="type")` and `concise` are `Doc()` (T05). They are not parameters of `PFormat`. `console` and `indent_guides` (Rich compatibility) are dropped.
- `pprint()` (`runtime/pprint.py`) is a wrapper that writes `pformat` plus a newline to stdout. `PFormat.Write(TextWriter, ...)` is the core. The REPL `pprint` global comes with the REPL task (T15).
- A limit of 0 or below throws `ArgumentOutOfRangeException` (upstream treats 0 as "no limit" in some paths). Use null for no limit.
- `unquoteStrings` returns the untruncated string as it is. Upstream strips the quotes of its `repr`, which left escapes in the text.
- A type with its own `ToString` prints that text, as upstream honors a custom `__repr__`. This runs user code (it can have side effects or show a `[Hidden]` field). Records keep member printing, because their `ToString` is generated.
- Not ported (Python only): `__instance_values__`, `spec(max_string=...)`, pydantic `exclude`, NamedTuple, `__slots__`, `_ClassRef`/`_InstanceRef`, `sys.stdout` printing.
- Per-field `max_string` override (`test_event_max_string_override`): needs the event types and the context formatter, so it moves to T13 with the "must not truncate" rule.
**Tests:** ported in `PFormatTests` (cycles, head and tail markers, budget parameters, truncating format), `ValueRendererTests` (hidden fields, objects), `LiteralFormatterTests`, `CollectionReaderTests`, `TruncatingTextWriterTests` and `FileBackedTruncatingTextWriterTests`.
Not applicable (Python only): `test_annotated_types`, `test_dataclass_properties`, `test_discover_pep563`, `test_discover_pydantic_forward_refs`, `test_namedtuple_future_annotations`, `test_typeddict_future_annotations`, `test_pandas_adapter`, `test_protocols`, `test_registry`, `test_metadata`, `test_docs` (`spec()`), `test_doc_config`, `test_visibility` (module variables, `hidden` blocks), `test_skill_lifecycle_visibility`, `test_referenced_types_demo`.
`test_structured` (type extraction) is covered by T05 (`MemberReader`, `ReferencedTypes`). `test_function_default_rendering` (lambda and function defaults) does not apply: C# defaults are compile-time constants. `test_pformat_remove_budget` becomes the limit validation tests. `test_pprint_respects_hidden` is ported except the `spec()`, `__instance_values__`, `__slots__`, pydantic and `instance_mode="type"` cases.

## `ChatClientLlmClient`, tool schema, retry, JSON parsing (T09)

**C#:** `Nooa.Llm.ChatClientLlmClient`, `ChatClientLlmOptions`, `RetryOptions`, `RetryPolicy`, `ToolSchema`, `LlmJson`, `ThinkTags`, `EmptyContentException`, `StructuredOutputException`, internal `ChatMessageMapper`.
- **Adapter:** one client over any `Microsoft.Extensions.AI.IChatClient`. `CompletionClient` and `ReasoningCompletionClient` become one class: `ExtractThinkTags` is an option, off by default. The `ResponsesClient`, Anthropic path, `_DUMMY_TOOL_SCHEMA` and the Bedrock schema sanitizer are not ported (B09).
- **Message mapping:** `JsonMessage` (system, user, tool, assistant with `tool_calls`) and `ResponseMessage` map to `ChatMessage`. Content part lists accept text parts only; an image part throws (no mapping yet). A `ResponseMessage` replays text and `ReplayToolCalls` only: reasoning and native state are not replayed (B09, as the task notes say).
- **Cache boundary:** the marker is consumed and has no wire form. OpenAI-compatible endpoints cache a stable prefix on their own. More than one marker throws, as upstream. Breakpoints for Anthropic and Responses are B09.
- **Response mapping:** `TextContent`, `TextReasoningContent` and `FunctionCallContent` become `AssistantText`, `AssistantReasoning` and `ToolCall`, in order. `Native` holds the SDK object as JSON when `ModelReaderWriter` can write it (the OpenAI SDK objects can). `RawResponse` is the SDK response, else the `ChatResponse`.
- **Exact arguments:** M.E.AI parses tool arguments into a dictionary. For an OpenAI raw tool call the adapter reads the exact string from the SDK object (`ChatToolCall.FunctionArguments`). For other chat clients the string is the dictionary written back as JSON, so whitespace and key order can differ from the model output.
- **Reasoning text:** the OpenAI M.E.AI adapter does not expose `reasoning_content`. Open models that use `<think>` tags are covered by `ExtractThinkTags`; separate reasoning fields need a custom `IChatClient` or B09.
- **Finish reason:** `length` and `content_filter` (error) win over tool calls, as upstream `_finish_reason_for_tool_calls`.
- **Usage:** `UsageDetails` becomes a JSON object and goes through `LlmUsage.FromProvider`. LiteLLM `response_cost` has no source here, so `CostUsd` stays 0.
- **Strict key:** Microsoft.Extensions.AI.OpenAI 10.10.1 reads `AdditionalProperties["strict"]` (not `strictJsonSchema`) from a tool declaration and from `ChatOptions`. A test checks the wire body.
- **Tool schema:** `ToolSchema.FromMethod` and `FromType` use `AIJsonUtilities` with `NooaJson.Options`. `Clean`, `ResolveRefs`, `IsStrictValid`, `IsStrictCompatible`, `Loose` port `_clean_schema`, `_resolve_schema_refs`, `_strict_schema_valid`, `_schema_strict_compatible` and `_loose_response_schema`. Tools use the Responses rule: strict only when every property is required and the strict schema is valid, else loose (`ChatClientLlmOptions.StrictTools` turns strict off). Two .NET differences: the schema of `object` is the boolean `true` (mapped to `{}`), and a recursive type is a `#/properties/x` pointer, not a `$defs` entry, so `ResolveRefs` also runs when `$defs` is absent (every unresolved reference becomes a plain object, as upstream).
- **Structured output:** the output type's schema goes into `ChatResponseFormat.ForJsonSchema` (strict when compatible, loose otherwise). A root that is not an object (scalar, list) is wrapped as `{"result": ...}` for the request and unwrapped on parse, because OpenAI needs an object root. Upstream did the same through a generated `ReturnResult` model. Parsing uses `LlmJson.ExtractAndParse` then `NooaJson.Options`. Empty content falls back to the reasoning text, as upstream. Any failure throws `StructuredOutputException : ValidationException`. Its message is a short description (type, error kind, JSON path) with no model text, because messages reach logs. `OutputType`, `Path`, `Detail` and `Content` (the raw text) are properties for T14 to build model feedback. `EmptyContentException` keeps the reasoning in `Reasoning`, not in the message. `LlmJson` messages hold no text preview (a security deviation from upstream, which put a 500-character preview in the error). A missing property does not fail: System.Text.Json fills the default unless the member is `required`. Use `required` on output types that must be complete (T14 may add a check).
- **`extract_and_parse_json`:** ported as `LlmJson.ExtractAndParse` (fence, bold markers, embedded object, control characters, escape repair, double-encoded strings, ellipsis message). It throws `JsonException`. The regexes have a 1 s timeout because the input is model output; a timeout is a `JsonException`. Metrics callbacks (`_record_llm_metric`) are not ported.
- **Retry:** `RetryPolicy` ports `with_retry`, `_calculate_delay` and `_is_retryable_error` (status first, then text, then exception type, then endpoint phrases). Status comes from `ClientResultException.Status` or `HttpRequestException.StatusCode`, also through inner exceptions. The delay and the random source are constructor arguments, so tests run instantly. `RetryOptions` is a plain record, not a `ConfigBase` (it is not a layered config). `retryable_exceptions` is fixed: `TimeoutException`, `IOException`, `SocketException`, `HttpRequestException`. No Polly. `sync_retry` and `RetryingWrapper` are not ported (async only; the wrapper is `ExecuteAsync`).
- **Deadline:** `RequestTimeout` (default 600 s, `HttpConfig.request_timeout`) bounds one attempt with a linked `CancellationTokenSource`. A deadline throws `TimeoutException` (retryable). A caller cancel throws `OperationCanceledException` and is never retried. Admission control (`_run_async_provider_call`) is B08. The httpx pool settings of `HttpConfig` have no C# counterpart: the caller builds the `HttpClient`.
- **Empty content:** with `RetryOnEmptyContent`, an answer with no text, no tool call and some reasoning throws `EmptyContentException` inside the attempt and is retried; after the last retry it propagates. Off by default, as upstream.
- **XML tool calls:** `_extract_xml_tool_calls` (Nemotron `<tool_call>` fallback) is not ported. It is model-specific and not in the task checklist; add it with T10 if a registry model needs it.
- **Security (prompt-injection checklist):** `MaxOutputTokens` (default 8192, configurable) limits each reply. The client logs retry warnings (attempt, error type, delay) and a Debug line (finish reason, counts). It never logs message text, tool arguments or the exception message; a test proves it. The API key lives only in the wrapped chat client (the Live test reads it from the environment).
- **Tests:** ported from `test_retry.py`, `test_retry_config.py`, `test_json_parsing.py`, `test_tool_schema_clean.py`, `test_strict_schema_review_fixes.py`, `test_response_format_strict_fallback.py` (schema parts), `test_empty_content_retry.py`, `test_finish_reason_propagation.py` (mapping), `test_output_model_instantiation.py`, `test_provider_call_deadline.py`. Skipped: litellm and httpx exception-type cases (replaced by .NET exceptions), `sync_retry`, Bedrock and `RootModel` cases, `test_chat_parts.py` and `test_reasoning_*` replay tests (B09), cache-policy tests (B09), `test_http_*`, admission and registry tests (T10, B08). Wire tests run the real OpenAI adapter against a stub `HttpMessageHandler` (no network). One `Live` test reads `NOOA_LIVE_BASE_URL`, `NOOA_LIVE_API_KEY`, `NOOA_LIVE_MODEL` and skips when they are not set.

## Agent, runtime, event manager, snapshots (T12)

- **`Agent`:** constructor takes `AgentOptions` (D6). Upstream `__init_subclass__` kwargs are the `[AgentOptions]` attribute, instance values are `AgentOptions`. `execution=`, `truncation=` and storage are instance options. Not ported yet: `decision_model` (B03), `render_config`, `context={}` overrides and `event_query` (T13). `Agent` has `[NoSnapshot]`, so an agent never nests in a snapshot. Public framework members carry `[Hidden]`.
- **LLM cascade:** call scope (`Agent.UseLlm`) -> method (`[AgentOptions(Llm = ...)]` on the method, found by reflection through name and parameter names) -> instance (`AgentOptions.Llm` or `LlmAlias`) -> class (`AgentMetadata.Llm`, walks base classes) -> parent agent (`AsyncLocal`). Instance, class and parent resolve in the constructor, as upstream does, so a missing LLM fails at construction. A string alias needs an `ILlmClientResolver` (`AgentOptions.LlmResolver` or `Agent.DefaultLlmResolver`). No resolver gives a clear `NooaRuntimeException`. Resolved aliases are cached per instance. The upstream call-site `llm=` keyword and callable `llm=lambda self: ...` do not exist: a C# method has a fixed signature. `UseLlm` scopes replace the call-site keyword. A callable is replaced by `SetLlm`.
- **Strategy cascade:** call scope (`Agent.UseStrategy`) -> `[Strategy(typeof(...))]` -> `AgentOptions.DefaultStrategy`. There is no default strategy yet (upstream default is CodeAct, T17): a call with none throws a clear error. An attribute type uses the instance in `AgentOptions.Strategies` or a new instance from its public parameterless constructor. The attribute cannot carry constructor arguments (upstream `@strategy(CodeActStrategy(max_iterations=5))`).
- **Method wrapper:** upstream wraps every method. Here only `[Agentic]` calls arrive (through `IAgenticTarget`), so a plain C# method has no events, hooks or middleware. Argument validation (`ArgumentValidator`) is gone: the compiler checks the types. `[NoTrace]` on the method or the class is honoured (parent id pushed, no hooks). `[NoSnapshot]` on a method has no effect. Scoped blocks and events (`_scoped_blocks_var`) are T13.
- **Generation lock (D10):** `SemaphoreSlim(1,1)` plus an `AsyncLocal` session flag. The wait honours the cancellation token. Upstream `_ensure_generation_lock_on_current_loop` has no equivalent. A call from a task that is inside a session runs inline, also in `Task.WhenAll`, as upstream.
- **`ActorRuntime`:** trimmed to `GenerateAsync`, `ExecuteNestedAsync`, generation ids, `LastPromptTokensActual`, and the dispatch. `GenerateAsync` takes ready `LlmMessage`s (building them from context and events is T13, the strategy calls the builder). Not ported: `execute_code`, `decide`, `evaluate_expression`, `expand_variables`, `list_methods`, `get_code`, context-window recovery and `max_tokens` reduction, the token calibration and `harness_metrics`. They belong to T13, T17 or later. `IRuntimeServices` lists only what exists.
- **Context vars:** `AsyncLocal` holder `AgentContext` (internal). `_in_exec_middleware` and the decorator context/events vars are not ported (they serve execute_code and T13).
- **`EventManager`:** `On` and `Intercept` return `IDisposable`. `Intercept<TContext>` is keyed by the context type (`AgentCallContext`, `LlmCallContext`, `DecisionCallContext`, `ExecuteCodeContext`) and replaces the string kinds. Decision and execute-code contexts are minimal (B03, T17). `Update` takes an `Action<EventBase>` (see the `IEventBackend` note). Removed-event-name validation (`LLMOutput`, `LLMComplete`) is dropped: C# has no legacy rows. `register_event_type` is dropped: the `[JsonDerivedType]` list is the registry (D11). `set_event_query` and `get_event_query` wait for `EventQuery` (T13). `Filter(query:)` searches the serialized public fields (not `id`, `metadata`, `status`, `tag`, `timestamp`); upstream uses `searchable_fields()`, which T13 will port, and the filter can then switch to it. A regex has a 1 second timeout, because the pattern may come from model-written code. A lock guards the backend, so parallel tasks of one agent can add events (the in-memory backend is not thread safe by itself). `aclose` is `CloseAsync`; it has no shielding from cancellation because it takes no token.
- **`ContextManager`:** a block holds a value or a `Func<object?>` provider instead of a Python expression (D5). `Context`, `DynamicContext` and `ResolvedBlock` are T13. The setter overloads are named (`SetStaticProvider`, `SetDynamicProvider`, ...) so a lambda never binds to `object`. `apply_override` is T13 (it needs `Context`). The protected blocks are providers: `system_prompt` -> `ResolveSystemPrompt()`, `self` -> `AgentDocs.Doc(type)`, `state` -> `PFormat.Format(this, 50, 500, 4)`. A class doc comment is the system prompt. An agent class with no doc comment gets a short default sentence, not the long upstream `Agent` docstring (T13 owns prompt text).
- **Hooks:** `IInstrumentationHooks` has `BeforeAgentCall`/`AfterAgentCall`/`BeforeGeneration`/`AfterGeneration` with no-op default methods. One process-wide `InstrumentationHooks.Current`. Composition (`compose_hooks`, `hooks_scope`), the overhead meter and the other six hooks (code execution, method invocation, tool execution, messages built) come with T17/T19. Tests `test_hooks.py` and `test_hook_composition.py` are not ported for that reason.
- **Snapshots (D11):** `AgentSnapshot` version 2 as JSON text (`SnapshotJson`, `StorageManagerExtensions`). Values go through `NooaJson.Options`. The file never names a type. A field is restored only when it is declared on the agent class, its declared type is eligible (`SnapshotTypes`: primitives, enums, JSON nodes, arrays, lists, sets, string-keyed dictionaries, records, structs, `[Snapshotable]` classes, polymorphic bases whose `[JsonDerivedType]` list is eligible) and the type name is in `type_allowlist`. A hostile file cannot reach `object`, interfaces, delegates, streams or ordinary classes. The version is checked before the body is read. All values are read before the first write, so a refused file changes nothing (upstream restores field by field). Context blocks hold JSON-like values only. A provider block is not saved (a delegate has no JSON form, upstream stores the expression). A protected block in the file, or a protected key in `disabled_context`, is ignored, so a file cannot rewrite or hide the system prompt. `is_static` is added to the block record so the partition survives. Upstream envelopes (`__type__`, `__class__`, pydantic, dataclass, `dict_class`), `SnapshotVars` and `PersistentVars` are not ported: they serve the Python object model, the CLI and the todo tool. `snapshotable` is the `[Snapshotable]` attribute, `nosnapshot` is `[NoSnapshot]` (now also valid on fields and properties), `__nosnapshot__` is `[NoSnapshot]` on a class. A field of an ineligible type is skipped with a warning (upstream: same for non-serializable values).
- **Tests not ported:** `core_runtime/test_code_caching.py`, `test_execution.py`, `test_implemented_plan.py` (code execution, T17). `test_task_queuing*.py`, `test_missing_await.py`, `test_signal_edge_cases.py`, `test_sandbox_edge_cases.py`, `test_builtin_shadowing.py` (Python runtime behaviour or sandbox). `test_child_agent_edge_cases.py`, `test_nested_generation_edge_cases.py` (ported in kind: parent LLM inheritance, nested inline call, lock). `test_event_auto_registration.py`, `test_event_types.py` (class registry; D11 allowlist covers it). `test_middleware_integration.py` (needs CodeAct and decisions; the agent_call and llm_call cases are in `AgentTests`). `runtime/test_event_manager_events.py` and `test_agent_call_events.py` (BeforeAgentCall/AfterAgentCall cases are in `AgentTests`; turn events are T14/T17). `storage/test_serialization.py`, `test_snapshot_vars.py` (envelopes, see above; the type policy tests are in `SnapshotTypesTests`).

## Model registry, reasoning levels, limits, quickstart (T10)

**C#:** `Nooa.Llm.ModelRegistry` (implements `ILlmClientResolver`), `ReasoningConfig`, `ContextLimits`; `Nooa.Config.ModelConfig`, `ResolvedConfig`, `ResolvedSecrets`, `SecretValue`; `Nooa.Util.Quickstart`; internal `LayerTrust` and `OpenAiChatClientFactory`.
- **Snapshot, not global:** upstream keeps a global `MODELS` dict, `reload_registry`, `ensure_loaded` and a lock. `ModelRegistry.Load(bundledPaths, logger)` builds an immutable snapshot from `LlmConfig.Chain`. To reload, call `Load` again. `get_model_config` is `ModelRegistry.GetModelConfig`; `resolved_config()` is `ResolvedConfig.Load`. `get_registry_config` (raw dict copy) is not needed: `ModelConfig` is immutable.
- **Alias required:** `Resolve(alias)` throws `KeyNotFoundException` for an unknown alias. Upstream passes an unknown name to litellm. There is no litellm here.
- **Routing:** a model string is a litellm routing string. `openai/`, `hosted_vllm/` and `nvidia_nim/` are stripped (`nvidia_nim/` also sets `https://integrate.api.nvidia.com/v1` when `api_base` is missing). Other prefixes stay in the name. The client is the M.E.AI OpenAI client with `api_base` as endpoint.
- **Client kinds:** `client_type: decision` throws `InvalidOperationException` (decisions are not ported). `responses` throws `NotSupportedException` (backlog B09).
- **Settings sent:** `temperature`, `top_p`, reply cap (`max_tokens`, `max_completion_tokens`, `max_output_tokens`), `reasoning_effort` (to `ChatOptions.Reasoning`), `retry_config`, `context_window`. `ChatClientLlmOptions` gained `Temperature`, `TopP`, `ReasoningEffort`. Other litellm passthrough keys (`extra_body`, `store`, `include`, `cache_breakpoint`, `allowed_openai_params`, nested `reasoning`) stay in `ModelConfig.Extra` and a warning names the keys that are not sent. `drop_params` is read but has no effect.
- **Reasoning levels:** `ReasoningConfig` ports the declaration checks (reserved fields, default must be declared, detached copy, re-check on use) and `apply_reasoning_level` (whole-block replace, reply cap replace and promote). Selection is `ModelRegistry.Create(alias, level)` or `reasoning_level` in the alias. The per-call route override checks of upstream do not apply: the C# client has no per-call keyword arguments.
- **`ContextLimits`:** ported with `ContextLimits.For(client, replyCap, fallbackReserve)`. `reduced_reply_params` is runtime recovery code (T12).
- **Invalid entries:** an alias with a wrong-typed value (for example `api_key_env: 12345`, `context_window: many`, a non-http `api_base`) is skipped with a warning that names the alias. Upstream keeps it and drops the field.
- **API keys:** missing `api_key_env` variable makes `Create` throw `ConfigValidationException` (names the variable, never a value). Upstream warns and lets litellm fall back to `OPENAI_API_KEY`, which would send that key to a custom host. No `api_key_env` and a custom host: placeholder key `unused`. No `api_key_env` and the default host: `OPENAI_API_KEY`. The NVIDIA key synonym fallback is ported. A literal `api_key` in YAML is never read. Keys never reach logs, exceptions or `ToString()`: `ModelConfig` prints the names of extra keys only, `SecretValue` prints a mask and serializes to `{}`.
- **`ModelConfig`:** a plain immutable record. It does not use `ConfigBase` field tracking, because a layer replaces a whole alias and nothing merges field by field.
- **`ResolvedSecrets`:** upstream `Secrets`. Renamed because `Nooa.Secrets` is the loader. `SecretValue` stands in for `SecretStr`.
- **Quickstart:** `Quickstart.CreateDefaultLlm` picks `NVIDIA_API_KEY`, then `OPENAI_API_KEY`, then `NVIDIA_INFERENCE_API_KEY` / `NVIDIA_INTERNAL_API_KEY`. No credential throws at once (upstream fails at first call). The `.env` loader (`load_dotenv(override=True)`) is not ported: a `.env` file in the working directory is untrusted and would override the shell. Use `Secrets.LoadSecretsIntoEnv`. The example types (`Artwork`, `StockHolding`, ...) and `autorun` belong to T22.

**Security (trust rule, closes the open item of T03/T04):** a project-layer `llm_config.yaml` (or a `NEMO_OO_LLM_CONFIG` file inside the project) is untrusted. `LayerTrust.IsTrusted` is the one trust test, extracted from `Secrets` and used by both. Bundled paths (caller-supplied) are trusted.
1. An untrusted layer cannot replace or remove an alias that a trusted layer defined. The entry is ignored with a warning. This blocks redirect of the prompts of a trusted alias to another host, and loss of its key setting.
2. An untrusted layer never sets `api_key_env`. The field is dropped with a warning that names the alias and the field, never a value. An alias that only untrusted layers define therefore has no key, unless it uses the default OpenAI host with `OPENAI_API_KEY`. This also covers route-prefix defaults: `ParseModel` can give a host (for example `nvidia_nim/` gives `integrate.api.nvidia.com`) when the layer sets no `api_base`. Such an alias gets the placeholder key. Rule 1 also stops an untrusted `model_name` from changing the route of a trusted alias.
3. `api_base` and `endpoint` must not contain user info, a query string or a fragment (`ModelConfig.FromRegistry` rejects them; the message does not echo the URL). `ModelConfig.ToString()` drops user info, query and fragment, because `with` skips validation. The registry logs the host at Debug level, not the URL.
**Why simple:** the rules do not look at which variables exist in the process, so it does not depend on the environment. The cost: a project file cannot tune a trusted alias (for example the temperature). Put such a change in the user file or a `NEMO_OO_LLM_CONFIG` file outside the project. `LayerTrust` has two users (`Secrets`, `ModelRegistry`); the extraction is a move, and one trust test for both lowers the risk that they drift apart.

**Tests:** `ModelRegistryTests` (ported from `test_model_registry.py`), `ModelRegistryTrustTests` (new), `OpenAiChatClientFactoryTests` (wire, no network), `ReasoningConfigTests` (from `test_reasoning_levels.py`), `ContextLimitsTests`, `ModelConfigTests` and `ResolvedConfigTests` (from `test_resolved_config.py`), `QuickstartTests`, and four more chain tests in `LlmConfigTests` (from `test_llm_config_chain.py`).
**Tests skipped:** concurrency and global-state tests (`test_concurrent_get_llm_client_during_reload_is_safe`, `test_reload_updates_in_place`, `test_no_args_marks_loaded`, `test_triggers_auto_load`: the snapshot has no global state); decision-model tests; litellm passthrough, `drop_params` and `cache_breakpoint` tests; `test_reasoning_levels.py` tests of `CompletionClient`/`ResponsesClient` dispatch, route override and `FakeLLMClient`; `test_reasoning_levels_wire.py` and the skill-example test (litellm, skills).
