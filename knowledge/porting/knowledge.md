# Porting: facts and patterns

## Config records (T03)

- Pattern: `public sealed record X : ConfigBase`, each property `{ get; init { field = value; MarkSet(); } } = default;`. Copy a config with `this with { }` and apply set fields with `CopySetFields`.
- A property initializer writes the backing field, so it does not mark the field as set. A test proves this (`ConfigBaseTests`).
- `with` on a record copies fields shallowly. `ConfigBase` has an explicit copy constructor so the copy gets its own set.
- `with` does not work on a generic `T`. The caller passes `this with { }` into the helper.
- Records compare backing fields of `field` properties. `ConfigBase.Equals` ignores the tracking set.
- Constructors cannot validate with object initializers. Use `Validate()`.

## Build rules that bite

- `.editorconfig` requires `_camelCase` for every private field, also `const` and `static readonly` (IDE1006 is an error in `dotnet format`).
- CA1416: `FileStreamOptions.UnixCreateMode` and `File.GetUnixFileMode` need an `OperatingSystem.IsWindows()` guard or `[UnsupportedOSPlatform("windows")]`.
- Test project has no implicit `using Xunit`; `Nooa.Tests.csproj` adds `<Using Include="Xunit" />`.
- `File.ReadAllText(path, encoding)` detects a BOM and ignores a strict encoding. Read bytes and decode with `UTF8Encoding(false, true)` to reject bad UTF-8.
- macOS temp paths contain symbolic links (`/var` -> `/private/var`). Compare paths through `NooaPaths.Canonicalize`.
- Tests that change environment variables use `[Collection(EnvironmentCollection.Name)]` and `TempConfigDirs`.
- `dotnet test --solution` exits with code 8 when a test project has no tests. `tests/Nooa.Generators.Tests` is empty until T11.

## YamlDotNet

- `WithAttemptingUnquotedStringTypeDeserialization()` gives `yaml.safe_load`-like scalar types. Quoted scalars stay strings.
- Deserializing to `object` gives `Dictionary<object, object>` and `List<object>`. `LayeredConfig` normalizes them.
- `SerializerBuilder.WithQuotingNecessaryStrings()` quotes strings such as `"true"` and `"0123"` so they read back as text.

## Events and JSON (T04)

- Polymorphic records: `[JsonPolymorphic]` on an abstract base needs the base in the static type. Serialize and deserialize through `EventBase`, not the derived type, or the discriminator is lost.
- `JsonSerializerOptions.AllowOutOfOrderMetadataProperties = true` lets the `event_type` discriminator sit anywhere in stored JSON.
- A positional-free base record can take the role through a protected constructor and a `[JsonIgnore]` get-only property. A derived record is then `record X() : EventBase(Role.User)`. Put `[JsonIgnore]` on computed properties, also on overrides.
- `JsonNamingPolicy.SnakeCaseLower` plus `JsonStringEnumConverter(SnakeCaseLower)` gives upstream wire names. Use `Nooa.Json.NooaJson.Options` for all persisted values.
- A name that clashes with `System.Threading.Tasks.Task` breaks every async file that imports the namespace. Rename the type and keep the persisted name in the discriminator.
- `JsonObject` has no collection-expression support. Write `= new();`.
- BSD `sed -i` on macOS needs `-i ''`. A failed `sed` before `&&` skips the rest of a command chain.

## Reflection (T05)

- `NullabilityInfoContext` reports unconstrained type parameters as nullable. Do not print `?` for a generic parameter.
- Compiler-generated record members (`Equals(T)`, `Deconstruct`, `<Clone>$`) carry `[CompilerGenerated]`. Filter on it. A record class is detectable by the `<Clone>$` method.
- `Attribute.IsDefined(member, type, inherit: true)` honors `inherit` for properties. `MemberInfo.GetCustomAttributes(bool)` does not.
- An auto-property has a backing field named `<Name>k__BackingField`. Use it to tell a property that is safe to read from a computed one.
- `init` setters are detectable: `SetMethod.ReturnParameter.GetRequiredCustomModifiers()` contains `IsExternalInit`.
- Fixture classes in tests need `#pragma warning disable CS0649` when only reflection reads their fields. Private `const` and `static readonly` fields need a `_` prefix too.
- macOS `sed` has no `\b`. Use `perl -pi -e` for word-boundary renames.

## SQLite and storage (T07)

- `Microsoft.Data.Sqlite`: set `Pooling=false` in the connection string. A pooled connection keeps the file open after `Dispose` (macOS file locks, WAL files stay). Use a full path as `DataSource`: a path such as `file:...` is read as a URI.
- `SqliteException.SqliteErrorCode` 11 (corrupt) and 26 (not a database) give corruption. `new SqliteException(message, code)` is public, so a test can build one.
- A `SqliteCommand` with a parameter list is all that is needed for safe SQL. Read a blob with `CASE WHEN length(CAST(data AS BLOB)) <= @max THEN data END` so an oversized row never loads.
- .NET `FileShare.None` takes a mandatory lock on Unix too (flock). Other readers fail with `IOException`, also with `FileShare.ReadWrite`. Keep readable data in a separate file from the lock file.
- A test overwrites pages after the first two with `0xAB` to get a real `SQLITE_CORRUPT`. Reads degrade; a write throws.
- Abstract xUnit test classes with `[Fact]` methods run once per derived class: use them for backend contract tests. `TheoryData` members can be `static` on the base class.
- Private `const` fields need a `_` prefix too (also for a default parameter value).
- Hostile SQLite file: `PRAGMA trusted_schema=OFF`, `SQLitePCL.raw.sqlite3_db_config(conn.Handle, 1010, 1, out _)` (DEFENSIVE), then compare `sqlite_master` with a reference in-memory database built from the same DDL. Stored SQL text drops `IF NOT EXISTS`.
- Keyset paging with a row value works in SQLite: `WHERE (insertion_order, rowid) > (@o, @r) ORDER BY insertion_order, rowid LIMIT n`. Stop reading a `SqliteDataReader` early to bound the page memory.

## Source generators (T11)

- Roslyn 5.3 analyzers build with SDK 10.0.301 without CS9057. RS2008 needs `AnalyzerReleases.Shipped.md` and `.Unshipped.md` as `AdditionalFiles`, or every `DiagnosticDescriptor` fails the build.
- netstandard2.0 generator: add an internal `IsExternalInit` polyfill for records. `ImplicitUsings` stays off. Models must hold no `ISymbol`, `Compilation` or `Location`. Use records with an `EquatableArray<T>`, and `LocationInfo`, so caching works.
- A `///` comment on the same line after `{` is trailing trivia of `{`, not leading trivia of the next member. Doc readers and test snippets must put it on its own line.
- Without `GenerateDocumentationFile` the `///` lines are plain `SingleLineCommentTrivia`. Read them as text and parse the XML yourself.
- `U+2028` and `U+0085` end a `//` comment in C#. A doc comment cannot hold them.
- `SymbolDisplay.FormatLiteral(text, quote: true)` escapes quotes, backslashes and line separators. Use it for every string that goes into emitted code.
- A partial method keeps its metadata order at the declaration site. `MetadataToken` order is still source order after the generator adds the body.
- Match reflection members from generated code by `MetadataToken` (unique per module). `ParameterInfo` and `MemberInfo` equality depends on the reflected type.
- `[ModuleInitializer]` needs a method that is `internal` or `public` in a type that is reachable. A private nested agent class cannot be called from it.
- Test a generator in memory: `CSharpGeneratorDriver` with `trackIncrementalGeneratorSteps: true`, compile and load the output with `Assembly.Load(bytes)`. Check caching through `TrackedOutputSteps` reasons.
- A generator test host needs the trusted platform assemblies (`AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")`) plus the `Nooa` assembly as references.
- File-scoped `namespace` must come first: add helper types as a separate syntax tree, not by string concatenation.
- xUnit v3 analyzer xUnit1051 requires `TestContext.Current.CancellationToken` in test methods that call token-taking APIs.

## Value printing (T06)

- A one-line attempt that nests inside another doubles the work at every level (render for the width check, then render again). Write the attempt into a writer that reports overflow at the line width, and let a nested container write straight into that writer. Then a deep structure costs about depth squared.
- xUnit `[InlineData]` turns a lone surrogate into U+FFFD. Build such a string in the test body.
- `FieldInfo.GetValue` on an auto-property backing field runs no user code. `PropertyInfo.GetValue` runs the getter, which can be a computed override of a virtual property.
- `FileStreamOptions { Mode = CreateNew, UnixCreateMode = ... }` gives the `mkstemp` behavior (new file, mode 0600). Guard `UnixCreateMode` with `!OperatingSystem.IsWindows()`.
- Bash heredoc without quotes expands `$` and `\`. Quote the delimiter (`<<'EOF'`) when the text is C# source.

## Microsoft.Extensions.AI and OpenAI (T09)

- Pin `Microsoft.Extensions.AI.Abstractions` and `.OpenAI` to the same version. The OpenAI package pulls `OpenAI`, `System.ClientModel` and the `Microsoft.Extensions.*.Abstractions` packages. `Microsoft.Extensions.Logging.Abstractions` stays at its own pin without a downgrade error.
- Both `Microsoft.Extensions.AI` and `OpenAI.Chat` define `ChatMessage` and `ChatFinishReason`. Do not import `OpenAI.Chat`; use an alias such as `using ChatToolCall = OpenAI.Chat.ChatToolCall;`.
- Strict mode key in M.E.AI.OpenAI 10.x is `AdditionalProperties["strict"]`, on an `AIFunctionDeclaration` and on `ChatOptions`. `strictJsonSchema` is ignored without any error. Check the request body in a test.
- Test the real OpenAI adapter without a network: `new OpenAIClientOptions { Transport = new HttpClientPipelineTransport(new HttpClient(stubHandler)), RetryPolicy = new ClientRetryPolicy(0) }`, then `GetChatClient(model).AsIChatClient()`.
- `AIJsonUtilities.CreateJsonSchema(typeof(object))` is the boolean `true`, not an object. A recursive type uses `{"$ref":"#/properties/x"}`, with no `$defs`.
- `AIFunctionDeclaration` can be subclassed (`Name`, `Description`, `JsonSchema`, `AdditionalProperties`) to carry a ready JSON schema.
- xUnit v3 analyzer xUnit1051: pass `TestContext.Current.CancellationToken` to every method with a token. `Assert.Skip(reason)` skips a test at run time (use it for `Live` tests without environment variables).
- Model output is untrusted input for `Regex`: use a `matchTimeoutMilliseconds` argument on `[GeneratedRegex]` and treat a `RegexMatchTimeoutException` as a parse failure.

## Agent runtime (T12)

- `AsyncLocal<T>` set inside an `async` method does not leak to the caller (the builder restores the execution context after the synchronous part). A scope API that must outlive the call (`using (agent.UseLlm(c)) { await ... }`) has to be a plain, non-async method that sets the value and returns an `IDisposable`.
- Overloads `Set(string, object?)` and `Set(string, Func<object?>)` bind a lambda to `object` through its natural type. Use different method names for providers.
- `System.Collections.Generic.OrderedDictionary<TKey,TValue>` (.NET 9+) keeps insertion order after removals. `Dictionary` does not promise it.
- `RegexParseException` derives from `ArgumentException`. Use `Assert.ThrowsAny<ArgumentException>`.
- xUnit1051 fires for a call to any method with an optional `CancellationToken`, also generated agentic methods. A file-level `#pragma warning disable xUnit1051` with a reason is the least noisy fix.
- Snapshot of untrusted JSON: never let the file choose a type. Resolve the target member from the live object, take the type from its declaration, check an eligibility policy and an allowlist, read all values first and write after. STJ ignores unknown properties such as `$type`, but a `[JsonPolymorphic]` base with an unknown discriminator throws `NotSupportedException`.
- `FieldInfo.SetValue` works on `readonly` instance fields and on auto-property backing fields (`<Name>k__BackingField`).
- A generated partial agent class needs `partial` also for a primary-constructor subclass in tests. Test fixtures with only reflection access need `#pragma warning disable CS0649`.

## Model registry (T10)

- YamlDotNet with `WithAttemptingUnquotedStringTypeDeserialization` returns the smallest fitting integral type (`Int16`, `Int32`, ...), and `double` for decimals. Convert numbers by value (`Convert.ToInt64` after an integral check), never with `is int`.
- Trust for a layered config file lives in `LayerTrust.IsTrusted(path, fileName, envVar)`. Use it for every new layered file kind. A project-layer file must not replace a trusted entry, and must not combine a key variable with a custom host.
- `ChatOptions.Reasoning = new ReasoningOptions { Effort = ReasoningEffort.High }` reaches the OpenAI wire as `reasoning_effort`. A test checks the body.
- A test that needs a trusted layer writes to the user dir. The project dir is untrusted.

## Todo tool (T21)

- A `[Snapshotable]` class that is a stateful object needs a `JsonConverter<T>` (attribute on the class) so it keeps private state. Snapshot eligibility checks only its public properties, so give it none.
- `SnapshotValues.ToNode` (internal) is the JSON-like value filter. Reuse it for any "store only serializable values" rule. It throws `SerializationException`.
- A property of type `string` that gets `null` from STJ stays `null`. Remove null keys from a legacy JSON object before `Deserialize<T>` so the initializers apply.
- A reference type that must convert from two types (`Todo | str`) works as a `readonly record struct` with two implicit operators.
- C# 14 `x?.Prop = value` compiles in this repo.

## Context renderer (T13)

- `cond ? (long)x : doubleValue` has type `double` in C#, so an integer prints as `5.0`. Cast one branch to `object` when the result must keep its kind.
- `JsonValue.GetValue<double>()` throws for a value that was made from an `int`. Read a number with `JsonSerializer.SerializeToElement(value)` and `TryGetInt64`, then `GetDouble`.
- A positional record can redeclare a parameter as an explicit property (`public Func<object?> Provider { get; } = Provider ?? throw ...`). That gives a null check or a default without a constructor body.
- `[Hidden]` is read by `PFormat` from the backing field of an auto-property, so a get-only property that the constructor sets (`EventRole`) shows up unless it is hidden. Computed properties never show.
- To keep text from closing a delimiter, escape every `</` in the content, not only the tag names that you know. The set of tags (block keys) is not fixed, and a key can come from model-written code.
- Equal message lists in a test: `JsonMessage` holds a `JsonObject`, which compares by reference. Compare `ToJsonString()`.
- xUnit `Assert.Contains` on a long string shows only the head of the text. Use `Assert.Fail(text)` once to see the whole text.
- `dotnet format` sorts `using` directives: it fails for a `System.*` using after the others and for a `using` before the file-scoped `namespace` in a test file. Run `dotnet format Nooa.slnx` once, then `--verify-no-changes`.

## Shell and child processes (T20)

- `Process` cannot set a new session or pass an extra fd. Start bash under `setsid` (Linux) or `perl -MPOSIX -e 'setsid(); exec @ARGV'` (macOS has no `setsid`). The shell PID is then the process group id, and `kill(-pid, sig)` ends the group.
- .NET has no public API for SIGTERM or SIGINT. Use `[DllImport("libc")] kill`. `LibraryImport` fails the build unless the whole project sets `AllowUnsafeBlocks`.
- `ProcessStartInfo.Environment.Clear()` then add only the allowlist. `Process.Start` resolves `FileName` through the parent PATH, not the child PATH.
- Bash `${var//%/x}` is ambiguous: write `${var//\%/x}`. `$_` holds the last argument of the previous command, so a sentinel passed as last argument is visible to the next command.
- `jobs -p` inside `$( )` lists the parent's jobs in bash. Put the whole frame in one `printf`: one `write(2)` below PIPE_BUF stays atomic against background-job output.
- A Linux zombie still answers `kill(pid, 0)`. Test liveness with `/proc/<pid>/stat` state `Z`.
- `process.WaitForExitAsync` does not wait for stream EOF when the code reads `StandardOutput` itself, so a background job that holds the pipe does not block it.
- Class `Path` property shadows `System.IO.Path` inside the class. Write `System.IO.Path.X` there.
- Test method with `Task.Run` or `Task.Delay` needs `TestContext.Current.CancellationToken` (xUnit1051), also in a lambda.
- `File.GetUnixFileMode` in a test needs an `if (!OperatingSystem.IsWindows())` block around the call (CA1416); `Assert.False(IsWindows)` is not a guard.
- `docker run mcr.microsoft.com/dotnet/sdk:10.0` runs the Linux tests locally: copy the tree without `bin`/`obj`, `apt-get install ripgrep`. `rm /usr/bin/setsid` tests the perl path.
- Spawning `ps` to find child processes timed out under a busy parallel test run (macOS) and broke timeout recovery. `libproc` `proc_listchildpids` (DllImport, `int[]` buffer, returns the count) needs no process and no timeout.

## Predict strategy (T14)

- The real `ChatClientLlmClient` parses structured output inside `CallAsync` and throws `StructuredOutputException`, so a failed response never becomes an event. `FakeLlmClient.EnqueueText` sets no `Parsed`: a strategy must parse `Content` itself when `Parsed` is null. Use `StructuredOutputParser.Parse` (internal, `Nooa.Llm`).
- A test through a generated agent needs `using Nooa.Strategies;` for `[Strategy(typeof(PredictStrategy))]`, even inside `Nooa.Tests.Strategies`. Set `AgentOptions.DefaultStrategy` for a method with no attribute.
- Messages sent to the fake client: the system message holds `<system_prompt>` and `<self>`, then one user message per event (`<sys tag="N">Task { Prompt = @"..." }</sys>`, quotes doubled), a `CacheBoundary`, and a last user message with `<context>` (state and strategy blocks).
- `ActorRuntime` adds `call_id` to the metadata of every recorded event. Assert single metadata keys, not the whole object.
- A local function or lambda has a compiler-made method name. Use a named static method when a test checks `Delegate.Method.Name`.
- `Agent` needs an LLM at construction, also in a test that only passes the agent to a helper: `new AgentOptions { Llm = new FakeLlmClient() }`.


## Roslyn scripting REPL (T15)

- `ScriptOptions.Default` has only a minimal reference set (no `System.Linq`). `WithReferences` replaces the set. Resolve framework assemblies from `RuntimeEnvironment.GetRuntimeDirectory()`.
- `WithFilePath("Cell In[N]")` plus `WithEmitDebugInformation(true)` gives `StackFrame.GetFileName()` and `GetFileLineNumber()` for cell frames, also for the in-memory assembly. Compile diagnostics carry the same path in `GetLineSpan().Path`.
- `state.ContinueWithAsync(code, options, catchException: _ => true)`: a runtime exception returns a state with `Exception` set and keeps the variables assigned before the throw. A compile error throws `CompilationErrorException` and leaves the old state valid. `ScriptOptions` imports apply per submission: pass them on every `ContinueWithAsync`.
- A member of the globals object (`Console`) wins over a type from an import. A missing assembly is reported as CS1069 ("type has been forwarded to assembly System.Console ... consider adding a reference"), CS0234 for others.
- The `System.Runtime` facade forwards to `System.Private.CoreLib`, so the core types (`System.IO.File`, `Environment`, reflection) compile with only the facade referenced. Hiding them needs a validator, not a reference list.
- Synchronous cell code runs on the calling thread until the first await. Wrap the run in `Task.Run` so a tight loop does not block the caller, and give up after a grace period.
- A stack frame for the top-level code is `Submission#N+<<Initialize>>d__0.MoveNext`. Local functions are `<<Initialize>>g__Name|0_0`.
- `typeof(Outer<int>.Inner<bool>).DeclaringType` is the open definition `Outer<T>`. Take the outer type arguments from the constructed type, or a type-name writer recurses forever.
- `Assert.SkipUnless(condition, reason)` skips at run time. Use it to keep a benchmark out of CI.
- `TruncatingTextWriter.ToString()` wraps cut text in `<truncated-output>` lines.

## Memory growth of the REPL (T15 measurement, 2026-10-17)

500 trivial cells (`var vN = N; vN + warm`), Release, macOS arm64, .NET 10, forced GC before each sample (test `RoslynExecutionEngineMemoryTests`, run with `NOOA_BENCHMARK=1`):

| Cells | Managed (MiB) | Working set (MiB) | Elapsed (s) |
|-------|---------------|-------------------|-------------|
| start | 23.7 | 230.9 | 0 |
| 100 | 42.5 | 254.5 | 0.5 |
| 200 | 63.2 | 269.7 | 1.0 |
| 300 | 90.9 | 290.2 | 1.5 |
| 400 | 119.4 | 311.0 | 2.0 |
| 500 | 150.2 | 332.0 | 2.5 |

About 260 KiB managed per cell, and the growth per 100 cells rises from 19 to 31 MiB (each submission compilation references all earlier ones). Time is about 5 ms per cell. Script assemblies are not unloaded, so this is real retention. For a session of a few hundred cells it is acceptable. A long session needs a cell cap or a worker process (B01). Hypothesis in `hypotheses.md`: the growth is more than linear.
