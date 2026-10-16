---
pr:
pr_url:
jira:
epic: M0
status: open
created: 2026-10-16
updated: 2026-10-16
tags: [plan]
wiki_refs: []
---

# T01 — Port plan

## Context

This repo ports [NVIDIA-NeMo/labs-OO-Agents](https://github.com/NVIDIA-NeMo/labs-OO-Agents) from Python to C#. No code exists yet.
A plan must fix the scope, the C# design for each upstream concept, and the milestone order before any port work starts.

Upstream HEAD at creation: [1454b78](https://github.com/NVIDIA-NeMo/labs-OO-Agents/commit/1454b78c8f0a85c97eab1c6a91cc8cfc95b34de5) (2026-10-16).

## Progress

- [x] Pin the upstream commit; clone it into `reference/`
- [x] Inventory upstream: packages, modules, public API, tests, examples, dependencies
- [x] Map each core concept to a C# design (see open questions)
- [x] Choose solution layout and project names
- [x] Choose the model-client abstraction and the first provider
- [x] Define scope for v1 (core only, or core plus extras)
- [x] Split the work into milestones and tasks; add them to `work/`
- [x] Write the plan to `docs/port-plan.md`; start `docs/porting-notes.md`
- [x] Review the plan with Cristian

## Notes

Result: [`docs/port-plan.md`](../docs/port-plan.md), [`docs/porting-notes.md`](../docs/porting-notes.md), tasks T02–T22 in `work/`.
Decisions from Cristian (2026-10-16): REPL language is C# via Roslyn (D1); v1 scope is core + quickstart 01–08 (D2).
Analysis: four read-only agents covered core, runtime/strategies, LLM/tools/MCP, and tracing/storage/extras.


Open questions for the plan:

- **`...` bodies → agentic methods.** Options: `abstract`/`partial` methods with a source generator, or a runtime proxy (`DispatchProxy` / Castle).
- **Docstrings → prompts.** XML doc comments need a source generator to reach runtime. Attributes are the simple alternative.
- **Code as action.** Upstream runs model-written Python in a REPL. C# options: Roslyn scripting (`Microsoft.CodeAnalysis.CSharp.Scripting`), or keep a Python sandbox. Sandboxing is a security decision.
- **Typed I/O with retry.** `System.Text.Json` plus JSON Schema generation (`JsonSchemaExporter`) for return types.
- **Live-object arguments by reference.** Needs a handle model between the REPL and the host objects.
- **Async and generators.** `Task<T>` for agentic methods; upstream rejects generated streams.
- **Model client.** `Microsoft.Extensions.AI` `IChatClient` is a candidate abstraction.
