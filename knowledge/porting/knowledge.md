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
