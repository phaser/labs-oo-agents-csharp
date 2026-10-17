---
pr:
pr_url:
jira:
epic: M7
status: todo
created: 2026-10-17
updated: 2026-10-17
tags: [acceptance, live]
wiki_refs: []
---

# T22b — Live smoke run of quickstart 01–08

## Context

T22 ([#25](https://github.com/phaser/labs-oo-agents-csharp/pull/25)) added `QuickstartLiveTests` (`Category=Live`). The run needs a real API key and costs money. It waits for user approval.

Depends on: T22.

## Progress

- [x] User approves the run and names the provider, the model and the key source (2026-10-17: the local Claude gateway, `sonnet`)
- [x] Add `NOOA_LIVE_BASE_URL`, `NOOA_LIVE_MODEL` and `NOOA_LIVE_API_KEY` to `QuickstartLiveTests` (test only)
- [x] Run the live tests
- [x] Record the results in `knowledge/`
- [x] Move the live run of examples 02 and 04 (needs `response_format`) to backlog B15

## Notes

- **Endpoint:** the local Claude gateway at `http://127.0.0.1:11435/v1`, model `sonnet`, with the key from the Keychain item `local-claude-api-key`.
- **Result:** 6 of 8 examples pass (01, 03, 05, 06, 07, 08), in 2 min 5 s in total.
- **Failures:** examples 02 and 04 (Predict strategy) and `ChatClientLlmClientLiveTests` fail with `HTTP 400 'response_format' is not supported by this server`. This is a gateway limit, not a port defect.
- **Cost:** the run uses the Claude plan behind the gateway. A call has 8k to 27k prompt tokens, because the gateway adds the Claude Code system prompt.
