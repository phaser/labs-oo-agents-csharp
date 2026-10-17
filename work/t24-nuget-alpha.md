---
pr:
pr_url:
jira:
epic: M8
status: todo
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

- [ ] Add MinVer (exact version, package rule) to the packable project only; remove the fixed `<Version>` from `src/Nooa/Nooa.csproj`
- [ ] `MinVerDefaultPreReleaseIdentifiers` = `alpha.0`; `fetch-depth: 0` in the workflows that pack
- [ ] `examples/PackageConsumer` takes the version that the pack produced (no second fixed version)
- [ ] Add `.github/workflows/publish.yml` on `v*` tags, with actions pinned by SHA and `permissions: contents: read`
- [ ] Document the release steps (tag, push, check on nuget.org) in the README or `docs/`
- [ ] User adds the `NUGET_API_KEY` secret to this repository
- [ ] User approves the first tag (`v0.1.0-alpha.1`); push it and check the package on nuget.org

## Notes

- `Nooa` is free on nuget.org (checked 2026-10-17). It is the upstream project name, so the package description must say that this is an unofficial C# port and credit NVIDIA.
- A published package version cannot be deleted from nuget.org, only unlisted. Each publish needs the user's approval.
