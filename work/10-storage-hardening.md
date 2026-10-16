---
pr: 10
pr_url: https://github.com/phaser/labs-oo-agents-csharp/pull/10
jira:
epic: M1
status: open
created: 2026-10-16
updated: 2026-10-16
tags: [security]
wiki_refs: []
---

# T07b — Storage hardening follow-up

## Context

A security review of merged PR [#8](https://github.com/phaser/labs-oo-agents-csharp/pull/8) found two gaps:

1. `SqliteSchema.Verify` keyed objects by name. A trigger that shares a table's name could hide behind the table row.
2. `SessionLock` checks for a link and then opens the path, which is not atomic, and .NET has no `O_NOFOLLOW`.

Depends on: T07.

## Progress

- [x] Compare full `sqlite_master` rows as a set; refuse triggers and views outright
- [x] Refuse a lock directory that is writable by group or others (Unix)
- [x] Tests; porting-notes entry

## Notes

—
