---
pr:
pr_url:
jira:
epic: M8
status: open
created: 2026-10-17
updated: 2026-10-17
tags: [packaging]
wiki_refs: []
---

# T23 — NuGet package and a package consumer example

## Context

Today the examples and the tests use `ProjectReference`. A user of the library needs a NuGet package (`.nupkg`). The source generator (`Nooa.Generators`) must ship inside the package as an analyzer. A `[Agentic]` method then works with one `PackageReference`.

Depends on: T22.

## Progress

- [x] Make `src/Nooa` packable: package id `Nooa`, version, authors, description, `PackageLicenseExpression` Apache-2.0, README, repository URL, `NOTICE` in the package
- [x] Pack `Nooa.Generators` into `analyzers/dotnet/cs` of the same package (no separate package, no runtime reference)
- [x] Add symbols (`.snupkg`) and SourceLink, if no new package rule problem appears (exact version, 14 days old or more, GO-tier license)
- [x] `dotnet pack -c Release -o artifacts/packages` gives the package; inspect its content (lib, analyzers, dependencies)
- [x] Add `examples/PackageConsumer`: a small console app with one `[Agentic]` agent. It references the package through a local feed (`nuget.config` that points to `artifacts/packages`), not through `ProjectReference`
- [x] The example runs offline with `FakeLlmClient`, and online when an LLM key is set
- [x] CI: pack, then build and run the consumer offline against the new package (Linux leg only)
- [x] Keep the consumer out of `Nooa.slnx`, or make sure the solution build does not need the package first
- [x] README: install with `dotnet add package`, and a pointer to the consumer example
- [x] Notes in `docs/porting-notes.md` and `knowledge/`

## Notes

- Publishing to nuget.org is out of scope. It needs a user decision (the package name, an API key).
- Package `Nooa` 0.1.0 (`<Version>` in `src/Nooa/Nooa.csproj`). Content: `lib/net10.0/Nooa.dll` and XML docs, `analyzers/dotnet/cs/Nooa.Generators.dll`, `README.md`, `NOTICE`. Dependencies match `Directory.Packages.props`; the generator is not a dependency. `.snupkg` is built too.
- SourceLink: built into the .NET 10 SDK (`PublishRepositoryUrl`, `EmbedUntrackedSources`, `ContinuousIntegrationBuild` on GitHub Actions). No new package, so no package rule check.
- The consumer has its own `Directory.Build.props` and `Directory.Packages.props` (both empty) so it does not inherit repo wiring. `nuget.config` maps `Nooa` to the local feed only.
- The consumer version `0.1.0` repeats the package version. Update both on a version change.
- No unit test: the CI step (pack, then build and run the consumer) is the test. The build fails if the generator is not in the package, because the partial method has no body.
