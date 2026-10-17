---
pr:
pr_url:
jira:
epic: M5
status: open
created: 2026-10-17
updated: 2026-10-17
tags: [security]
wiki_refs: []
---

# T19b — Trace scrubber hardening follow-up

## Context

A security review of merged PR [#21](https://github.com/phaser/labs-oo-agents-csharp/pull/21) found three gaps:

1. An unexpected exception in `OtlpHttpSpanExporter.SendAsync` stops the worker. All later spans are lost, and `Flush` waits for its full timeout.
2. In a JSON value, the scrubber checks a string value without its key. The key-anchored patterns (`token=<hex>`, `api_token=x`, `aws_secret_access_key=...`) do not match.
3. The scrubber matches key names only with separators. `apiKey` and `accesstoken` are not redacted.

Depends on: T19.

## Progress

- [x] Catch every exception for each batch in the HTTP exporter; count the spans as dropped
- [x] Check each JSON string value as `key=value` text when the value alone has no match
- [x] Match sensitive key names without separators (suffix match)
- [x] `ScrubValue` redacts the whole value on an unexpected failure (fail closed)
- [x] Tests; porting-notes entry

## Notes

- The collapsed suffix match can redact a harmless key that ends in a sensitive name. This is intentional (fail closed).
