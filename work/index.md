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
| [T03 Errors and configuration](3-errors-config.md) | [#3](https://github.com/phaser/labs-oo-agents-csharp/pull/3) | — | open | 2026-10-16 |
| [T03b Secrets trust: fail closed, no credential swap](4-secrets-trust.md) | [#4](https://github.com/phaser/labs-oo-agents-csharp/pull/4) | — | open | 2026-10-16 |
| [T04 Event IR and LlmResponse](6-event-ir.md) | [#6](https://github.com/phaser/labs-oo-agents-csharp/pull/6) | — | merged | 2026-10-16 |
| [T05 Agentdoc renderer: Doc()](5-agentdoc-renderer.md) | [#5](https://github.com/phaser/labs-oo-agents-csharp/pull/5) | — | merged | 2026-10-16 |
| [T06 Agentdoc PFormat() and truncation](t06-agentdoc-pformat.md) | — | — | todo | 2026-10-16 |
| [T07 Storage: in-memory and SQLite](8-storage.md) | [#8](https://github.com/phaser/labs-oo-agents-csharp/pull/8) | — | merged | 2026-10-16 |
| [T07b Storage hardening follow-up](10-storage-hardening.md) | [#10](https://github.com/phaser/labs-oo-agents-csharp/pull/10) | — | open | 2026-10-16 |

## M2 — LLM layer

| Item | PR | Jira | Status | Updated |
|------|----|------|--------|---------|
| [T08 ILlmClient and FakeLlmClient](7-llm-client-fake.md) | [#7](https://github.com/phaser/labs-oo-agents-csharp/pull/7) | — | merged | 2026-10-16 |
| [T09 IChatClient adapter, tools, structured output, retry](t09-chatclient-adapter.md) | — | — | todo | 2026-10-16 |
| [T10 Model registry and reasoning levels](t10-model-registry.md) | — | — | todo | 2026-10-16 |

## M3 — Agent model

| Item | PR | Jira | Status | Updated |
|------|----|------|--------|---------|
| [T11 Source generator for agents](12-source-generator.md) | [#12](https://github.com/phaser/labs-oo-agents-csharp/pull/12) | — | open | 2026-10-16 |
| [T12 Agent base, ActorRuntime, method wrapper](t12-agent-runtime.md) | — | — | todo | 2026-10-16 |
| [T13 Context blocks renderer](t13-context-renderer.md) | — | — | todo | 2026-10-16 |
| [T14 Template and Predict strategies](t14-template-predict.md) | — | — | todo | 2026-10-16 |

## M4 — CodeAct

| Item | PR | Jira | Status | Updated |
|------|----|------|--------|---------|
| [T15 Roslyn REPL engine](t15-roslyn-repl.md) | — | — | todo | 2026-10-16 |
| [T16 Code validator for REPL cells](t16-code-validator.md) | — | — | todo | 2026-10-16 |
| [T17 CodeAct strategy and C# prompts](t17-codeact.md) | — | — | todo | 2026-10-16 |

## M5 — Context and tracing

| Item | PR | Jira | Status | Updated |
|------|----|------|--------|---------|
| [T18 ContextApi, EventsApi, dynamic context](t18-context-events-api.md) | — | — | todo | 2026-10-16 |
| [T19 Tracing](t19-tracing.md) | — | — | todo | 2026-10-16 |

## M6 — Tools

| Item | PR | Jira | Status | Updated |
|------|----|------|--------|---------|
| [T20 ShellTools](t20-shell-tools.md) | — | — | todo | 2026-10-16 |
| [T21 TodoManager](t21-todo-manager.md) | — | — | todo | 2026-10-16 |

## M7 — Acceptance

| Item | PR | Jira | Status | Updated |
|------|----|------|--------|---------|
| [T22 Quickstart examples and acceptance](t22-quickstart-acceptance.md) | — | — | todo | 2026-10-16 |

## Backlog (after v1)

Items B01–B13 in [`docs/port-plan.md` §4](../docs/port-plan.md#4-v1-scope). Create a tracker item when one is scheduled.
