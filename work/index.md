# Work Tracker

One row per tracked PR / in-progress item, grouped by epic (milestone; no Jira epic). Plan: [`docs/port-plan.md`](../docs/port-plan.md).

## M0 — Bootstrap

| Item | PR | Jira | Status | Updated |
|------|----|------|--------|---------|
| [T01 Port plan](001-port-plan.md) | [#1](https://github.com/phaser/labs-oo-agents-csharp/pull/1) | — | merged | 2026-10-16 |
| [T02 Solution skeleton and CI](002-solution-skeleton.md) | [#2](https://github.com/phaser/labs-oo-agents-csharp/pull/2) | — | merged | 2026-10-16 |

## M1 — Foundations

| Item | PR | Jira | Status | Updated |
|------|----|------|--------|---------|
| [T03 Errors and configuration](003-errors-config.md) | [#3](https://github.com/phaser/labs-oo-agents-csharp/pull/3) | — | merged | 2026-10-16 |
| [T03b Secrets trust: fail closed, no credential swap](004-secrets-trust.md) | [#4](https://github.com/phaser/labs-oo-agents-csharp/pull/4) | — | merged | 2026-10-16 |
| [T04 Event IR and LlmResponse](006-event-ir.md) | [#6](https://github.com/phaser/labs-oo-agents-csharp/pull/6) | — | merged | 2026-10-16 |
| [T05 Agentdoc renderer: Doc()](005-agentdoc-renderer.md) | [#5](https://github.com/phaser/labs-oo-agents-csharp/pull/5) | — | merged | 2026-10-16 |
| [T06 Agentdoc PFormat() and truncation](009-agentdoc-pformat.md) | [#9](https://github.com/phaser/labs-oo-agents-csharp/pull/9) | — | merged | 2026-10-16 |
| [T07 Storage: in-memory and SQLite](008-storage.md) | [#8](https://github.com/phaser/labs-oo-agents-csharp/pull/8) | — | merged | 2026-10-16 |
| [T07b Storage hardening follow-up](010-storage-hardening.md) | [#10](https://github.com/phaser/labs-oo-agents-csharp/pull/10) | — | merged | 2026-10-16 |

## M2 — LLM layer

| Item | PR | Jira | Status | Updated |
|------|----|------|--------|---------|
| [T08 ILlmClient and FakeLlmClient](007-llm-client-fake.md) | [#7](https://github.com/phaser/labs-oo-agents-csharp/pull/7) | — | merged | 2026-10-16 |
| [T09 IChatClient adapter, tools, structured output, retry](011-chatclient-adapter.md) | [#11](https://github.com/phaser/labs-oo-agents-csharp/pull/11) | — | merged | 2026-10-16 |
| [T10 Model registry and reasoning levels](013-model-registry.md) | [#13](https://github.com/phaser/labs-oo-agents-csharp/pull/13) | — | merged | 2026-10-16 |

## M3 — Agent model

| Item | PR | Jira | Status | Updated |
|------|----|------|--------|---------|
| [T11 Source generator for agents](012-source-generator.md) | [#12](https://github.com/phaser/labs-oo-agents-csharp/pull/12) | — | merged | 2026-10-16 |
| [T12 Agent base, ActorRuntime, method wrapper](014-agent-runtime.md) | [#14](https://github.com/phaser/labs-oo-agents-csharp/pull/14) | — | merged | 2026-10-16 |
| [T13 Context blocks renderer](016-context-renderer.md) | [#16](https://github.com/phaser/labs-oo-agents-csharp/pull/16) | — | merged | 2026-10-17 |
| [T14 Template and Predict strategies](017-template-predict.md) | [#17](https://github.com/phaser/labs-oo-agents-csharp/pull/17) | — | merged | 2026-10-17 |

## M4 — CodeAct

| Item | PR | Jira | Status | Updated |
|------|----|------|--------|---------|
| [T15 Roslyn REPL engine](019-roslyn-repl.md) | [#19](https://github.com/phaser/labs-oo-agents-csharp/pull/19) | — | merged | 2026-10-17 |
| [T16 Code validator for REPL cells](020-code-validator.md) | [#20](https://github.com/phaser/labs-oo-agents-csharp/pull/20) | — | merged | 2026-10-17 |
| [T17 CodeAct strategy and C# prompts](023-codeact.md) | [#23](https://github.com/phaser/labs-oo-agents-csharp/pull/23) | — | merged | 2026-10-17 |

## M5 — Context and tracing

| Item | PR | Jira | Status | Updated |
|------|----|------|--------|---------|
| [T18 ContextApi, EventsApi, dynamic context](024-context-events-api.md) | [#24](https://github.com/phaser/labs-oo-agents-csharp/pull/24) | — | merged | 2026-10-17 |
| [T19 Tracing](021-tracing.md) | [#21](https://github.com/phaser/labs-oo-agents-csharp/pull/21) | — | merged | 2026-10-17 |
| [T19b Trace scrubber hardening follow-up](022-scrub-hardening.md) | [#22](https://github.com/phaser/labs-oo-agents-csharp/pull/22) | — | merged | 2026-10-17 |

## M6 — Tools

| Item | PR | Jira | Status | Updated |
|------|----|------|--------|---------|
| [T20 ShellTools](018-shell-tools.md) | [#18](https://github.com/phaser/labs-oo-agents-csharp/pull/18) | — | merged | 2026-10-17 |
| [T21 TodoManager](015-todo-manager.md) | [#15](https://github.com/phaser/labs-oo-agents-csharp/pull/15) | — | merged | 2026-10-17 |

## M7 — Acceptance

| Item | PR | Jira | Status | Updated |
|------|----|------|--------|---------|
| [T22 Quickstart examples and acceptance](025-quickstart-acceptance.md) | [#25](https://github.com/phaser/labs-oo-agents-csharp/pull/25) | — | merged | 2026-10-17 |
| [T22b Live smoke run of quickstart 01–08](028-live-smoke-run.md) | [#28](https://github.com/phaser/labs-oo-agents-csharp/pull/28) | — | merged | 2026-10-17 |

## M8 — Packaging

| Item | PR | Jira | Status | Updated |
|------|----|------|--------|---------|
| [T23 NuGet package and a package consumer example](030-nupkg.md) | [#30](https://github.com/phaser/labs-oo-agents-csharp/pull/30) | — | open | 2026-10-17 |

## Backlog (after v1)

Items B01–B15 in [`docs/port-plan.md` §4](../docs/port-plan.md#4-v1-scope). Create a tracker item when one is scheduled.
