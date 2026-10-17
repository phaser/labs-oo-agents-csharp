---
pr: 18
pr_url: https://github.com/phaser/labs-oo-agents-csharp/pull/18
jira:
epic: M6
status: open
created: 2026-10-16
updated: 2026-10-17
tags: []
wiki_refs: []
---

# T20 — ShellTools

## Context

Port the persistent bash session tool (POSIX only).

Depends on: T17. Plan: [`docs/port-plan.md`](../docs/port-plan.md).

Upstream (pin in the plan):
- `src/nooa/tools/shell_tools.py`
- `src/nooa/tools/_bash_session.py`
- `src/nooa/tools/shell_lifecycle.py`
- `src/nooa/tools/_results.py`

## Progress

- [x] Port `BashSession` with a sentinel on stdout (no fd 3)
- [x] Port `Run`, `Read`, `Replace`, `WriteFile`, grep with `Match` anchors
- [x] Interrupt and recovery; background job tracking
- [x] Do not pass the full host environment by default; allowlist variables
- [x] Port tests: `tests/tools/` shell tests (need bash and `rg`)

## Notes

Runs commands as the host user. The agent must opt in explicitly.

Code: `src/Nooa/Tools/` (`ShellTools`, `BashSession`, `ShellOptions`, `ShellResult`, `Match`, `FileWrite`, `StaleMatchException`, `SearchCommand`, `ShellLifecycle`, `BackgroundServiceScope`, `StreamEvent`, `StreamDone`). Tests: `tests/Nooa.Tests/Tools/`. Deviations: [`docs/porting-notes.md`](../docs/porting-notes.md#shelltools-and-bashsession-t20).

Decisions:
- Plain classes, no `Skill` base type and no `self.shell` wiring. T17/T22 expose the tool to the REPL (see the T17 note).
- Explicit opt-in: `new ShellTools(new ShellOptions { Cwd = ... })`. There is no default instance. Windows throws `PlatformNotSupportedException` at construction.
- Host environment is not inherited. The shell gets PATH, HOME, LANG, USER, SHELL, TMPDIR, `TERM=dumb` and `ShellOptions.ExtraEnvironment`.
- Sentinel frames use a fresh random value per command, on stdout and stderr. Commands go through a private 0700 scratch directory, not through stdin.
- Process group: bash starts under `setsid` (or a `perl` `setsid` fallback), so dispose and close end the group. Neither tool: `ShellToolException`.
- Not ported (reason in porting notes): the result types of `_results.py` that no code uses (`RunResult`, `ViewResult`, `EditResult`, `WriteResult`, `SearchResult`, `LsResult`), the event-loop change handling, and the Python drainer process of `keep_background_on_close`.
- CI: Linux and macOS jobs install ripgrep so the search tests run. Without `rg` the tests skip.
