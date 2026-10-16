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
||||||| parent of a89d5c0 (Add the agent source generator and the runtime seam)

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

