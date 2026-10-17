# Work Tracker

One row per tracked PR / in-progress item, grouped by epic (milestone; no Jira epic). Plan: [`docs/port-plan.md`](../docs/port-plan.md).

## M0 — Bootstrap

| Item | PR | Jira | Status | Updated |
|------|----|------|--------|---------|
| [T01 Port plan](1-port-plan.md) | [#1](https://github.com/phaser/labs-oo-agents-csharp/pull/1) | — | merged | 2026-10-16 |
| [T02 Solution skeleton and CI](2-solution-skeleton.md) | [#2](https://github.com/phaser/labs-oo-agents-csharp/pull/2) | — | open | 2026-10-16 |

## M1 — Foundations

| Item | PR | Jira | Status | Updated |
|------|----|------|--------|---------|
| [T03 Errors and configuration](3-errors-config.md) | [#3](https://github.com/phaser/labs-oo-agents-csharp/pull/3) | — | merged | 2026-10-16 |
| [T03b Secrets trust: fail closed, no credential swap](4-secrets-trust.md) | [#4](https://github.com/phaser/labs-oo-agents-csharp/pull/4) | — | merged | 2026-10-16 |
| [T04 Event IR and LlmResponse](6-event-ir.md) | [#6](https://github.com/phaser/labs-oo-agents-csharp/pull/6) | — | merged | 2026-10-16 |
| [T05 Agentdoc renderer: Doc()](5-agentdoc-renderer.md) | [#5](https://github.com/phaser/labs-oo-agents-csharp/pull/5) | — | merged | 2026-10-16 |
| [T06 Agentdoc PFormat() and truncation](9-agentdoc-pformat.md) | [#9](https://github.com/phaser/labs-oo-agents-csharp/pull/9) | — | merged | 2026-10-16 |
| [T07 Storage: in-memory and SQLite](8-storage.md) | [#8](https://github.com/phaser/labs-oo-agents-csharp/pull/8) | — | merged | 2026-10-16 |
| [T07b Storage hardening follow-up](10-storage-hardening.md) | [#10](https://github.com/phaser/labs-oo-agents-csharp/pull/10) | — | merged | 2026-10-16 |

## M2 — LLM layer

| Item | PR | Jira | Status | Updated |
|------|----|------|--------|---------|
| [T08 ILlmClient and FakeLlmClient](7-llm-client-fake.md) | [#7](https://github.com/phaser/labs-oo-agents-csharp/pull/7) | — | merged | 2026-10-16 |
| [T09 IChatClient adapter, tools, structured output, retry](11-chatclient-adapter.md) | [#11](https://github.com/phaser/labs-oo-agents-csharp/pull/11) | — | merged | 2026-10-16 |
| [T10 Model registry and reasoning levels](13-model-registry.md) | [#13](https://github.com/phaser/labs-oo-agents-csharp/pull/13) | — | merged | 2026-10-16 |

## M3 — Agent model

| Item | PR | Jira | Status | Updated |
|------|----|------|--------|---------|
| [T11 Source generator for agents](12-source-generator.md) | [#12](https://github.com/phaser/labs-oo-agents-csharp/pull/12) | — | merged | 2026-10-16 |
| [T12 Agent base, ActorRuntime, method wrapper](14-agent-runtime.md) | [#14](https://github.com/phaser/labs-oo-agents-csharp/pull/14) | — | merged | 2026-10-16 |
| [T13 Context blocks renderer](16-context-renderer.md) | [#16](https://github.com/phaser/labs-oo-agents-csharp/pull/16) | — | merged | 2026-10-17 |
| [T14 Template and Predict strategies](17-template-predict.md) | [#17](https://github.com/phaser/labs-oo-agents-csharp/pull/17) | — | merged | 2026-10-17 |

## M4 — CodeAct

| Item | PR | Jira | Status | Updated |
|------|----|------|--------|---------|
| [T15 Roslyn REPL engine](19-roslyn-repl.md) | [#19](https://github.com/phaser/labs-oo-agents-csharp/pull/19) | — | merged | 2026-10-17 |
| [T16 Code validator for REPL cells](20-code-validator.md) | [#20](https://github.com/phaser/labs-oo-agents-csharp/pull/20) | — | merged | 2026-10-17 |
| [T17 CodeAct strategy and C# prompts](23-codeact.md) | [#23](https://github.com/phaser/labs-oo-agents-csharp/pull/23) | — | merged | 2026-10-17 |

## M5 — Context and tracing

| Item | PR | Jira | Status | Updated |
|------|----|------|--------|---------|
| [T18 ContextApi, EventsApi, dynamic context](24-context-events-api.md) | [#24](https://github.com/phaser/labs-oo-agents-csharp/pull/24) | — | merged | 2026-10-17 |
| [T19 Tracing](21-tracing.md) | [#21](https://github.com/phaser/labs-oo-agents-csharp/pull/21) | — | merged | 2026-10-17 |
| [T19b Trace scrubber hardening follow-up](22-scrub-hardening.md) | [#22](https://github.com/phaser/labs-oo-agents-csharp/pull/22) | — | merged | 2026-10-17 |

## M6 — Tools

| Item | PR | Jira | Status | Updated |
|------|----|------|--------|---------|
| [T20 ShellTools](18-shell-tools.md) | [#18](https://github.com/phaser/labs-oo-agents-csharp/pull/18) | — | merged | 2026-10-17 |
| [T21 TodoManager](15-todo-manager.md) | [#15](https://github.com/phaser/labs-oo-agents-csharp/pull/15) | — | merged | 2026-10-17 |

## M7 — Acceptance

| Item | PR | Jira | Status | Updated |
|------|----|------|--------|---------|
| [T22 Quickstart examples and acceptance](25-quickstart-acceptance.md) | [#25](https://github.com/phaser/labs-oo-agents-csharp/pull/25) | — | merged | 2026-10-17 |
| [T22b Live smoke run of quickstart 01–08](t22b-live-smoke-run.md) | — | — | todo | 2026-10-17 |

## Backlog (after v1)

Items B01–B14 in [`docs/port-plan.md` §4](../docs/port-plan.md#4-v1-scope). Create a tracker item when one is scheduled.
