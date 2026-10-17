---
pr:
pr_url:
jira:
epic: M8
status: todo
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

- [ ] Make `src/Nooa` packable: package id `Nooa`, version, authors, description, `PackageLicenseExpression` Apache-2.0, README, repository URL, `NOTICE` in the package
- [ ] Pack `Nooa.Generators` into `analyzers/dotnet/cs` of the same package (no separate package, no runtime reference)
- [ ] Add symbols (`.snupkg`) and SourceLink, if no new package rule problem appears (exact version, 14 days old or more, GO-tier license)
- [ ] `dotnet pack -c Release -o artifacts/packages` gives the package; inspect its content (lib, analyzers, dependencies)
- [ ] Add `examples/PackageConsumer`: a small console app with one `[Agentic]` agent. It references the package through a local feed (`nuget.config` that points to `artifacts/packages`), not through `ProjectReference`
- [ ] The example runs offline with `FakeLlmClient`, and online when an LLM key is set
- [ ] CI: pack, then build and run the consumer offline against the new package (Linux leg only)
- [ ] Keep the consumer out of `Nooa.slnx`, or make sure the solution build does not need the package first
- [ ] README: install with `dotnet add package`, and a pointer to the consumer example
- [ ] Notes in `docs/porting-notes.md` and `knowledge/`

## Notes

- Publishing to nuget.org is out of scope. It needs a user decision (the package name, an API key).
