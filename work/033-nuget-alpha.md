---
pr: 33
pr_url: https://github.com/phaser/labs-oo-agents-csharp/pull/33
jira:
epic: M8
status: open
created: 2026-10-17
updated: 2026-10-17
tags: [packaging, release]
wiki_refs: []
---

# T24 — Public alpha packages on nuget.org

## Context

The repository is public. Publish `Nooa` to nuget.org as an alpha (prerelease) package. Do it the same way as `~/projects/dbos-transact-csharp`:

- MinVer sets the version from the git tag. A commit before any tag gives `0.1.0-alpha.0.N` (`MinVerDefaultPreReleaseIdentifiers` = `alpha.0`).
- `.github/workflows/publish.yml` runs on a `v*` tag: restore, build, test, pack, `dotnet nuget push` with the `NUGET_API_KEY` secret and `--skip-duplicate`, then it uploads the packages as an artifact.
- CI packs on each run to check the packaging.

Depends on: T23, T23b.

## Progress

- [x] Add MinVer (exact version, package rule) to the packable project only; remove the fixed `<Version>` from `src/Nooa/Nooa.csproj`
- [x] `MinVerDefaultPreReleaseIdentifiers` = `alpha.0`; `fetch-depth: 0` in the workflows that pack
- [x] `examples/PackageConsumer` takes the version that the pack produced (no second fixed version)
- [x] Add `.github/workflows/publish.yml` on `v*` tags, with actions pinned by SHA and `permissions: contents: read`
- [x] Document the release steps (tag, push, check on nuget.org) in the README or `docs/`
- [x] Use nuget.org Trusted Publishing in place of a stored `NUGET_API_KEY` (`NuGet/login` v1.2.0)
- [ ] User adds the Trusted Publishing policy on nuget.org and the `NUGET_USER` repository variable
- [ ] User approves the first tag (`v0.1.0-alpha.1`); push it and check the package on nuget.org

## Notes

- `Nooa` is free on nuget.org (checked 2026-10-17). It is the upstream project name, so the package description must say that this is an unofficial C# port and credit NVIDIA.
- A published package version cannot be deleted from nuget.org, only unlisted. Each publish needs the user's approval.
- MinVer 8.0.0 (published 2026-09-05, Apache-2.0). It is in `src/Nooa` only. `Nooa.Generators` has `IsPackable=false` and stays inside `Nooa` as an analyzer.
- `MinVerMinimumMajorMinor` = 0.1 and `MinVerTagPrefix` = v. Without the minimum, an untagged commit gives `0.0.0-alpha.0.N`. Local result: `0.1.0-alpha.0.32`.
- SourceLink is built into the .NET 10 SDK. `Microsoft.SourceLink.GitHub` is not added.
- `PackageConsumer` takes `-p:NooaVersion=<version>`. The build fails with a message if it is not set. CI reads the version from the packed file name. The consumer is not in the solution.
- `dotnet nuget push *.nupkg` also pushes the matching `.snupkg` (documented NuGet behavior; not tested here, no push was done).
- `actions/upload-artifact` is pinned to v7.0.1 (2026-04-10). v7.0.2 is only 10 days old.
- Local check: the package holds `lib/net10.0`, `analyzers/dotnet/cs/Nooa.Generators.dll`, `README.md`, `NOTICE`. The consumer runs offline.
