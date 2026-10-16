---
pr:
pr_url:
jira:
epic: M3
status: todo
created: 2026-10-16
updated: 2026-10-16
tags: []
wiki_refs: []
---

# T11 — Source generator for agents

## Context

Emit bodies and metadata for `[Agentic]` partial methods (decisions D3–D6).

Depends on: T02. Plan: [`docs/port-plan.md`](../docs/port-plan.md).

Upstream (pin in the plan):
- `src/nooa/metaclass.py`
- `src/nooa/decorators.py`
- `src/nooa/ellipsis_detection.py`
- `src/nooa/strategies/current_call.py`

## Progress

- [ ] Add `[Agentic]`, `[AgentOptions]`, `[Strategy]`, `[Hidden]`, `[NoSnapshot]`, `[NoTrace]` attributes
- [ ] Emit each `[Agentic]` body: build `CurrentCall` (name, args, return type, prompt) and call the runtime
- [ ] Embed class and method XML doc comments as prompt constants
- [ ] Compile `{Member.Path}` placeholders to `Func<TAgent,string>` (D5); diagnostic for anything else
- [ ] Emit per-type metadata for agentdoc (members in source order, doc text)
- [ ] Diagnostics: class not partial, method not `Task`/`Task<T>`, `[Agentic]` on a generator or static method
- [ ] Snapshot tests (Verify.SourceGenerators) and diagnostic tests
- [ ] Port the applicable cases of `test_metaclass.py`, `test_decorator_strategy.py`, `test_pure_python_method_rejection.py`

## Notes

Pre-ellipsis code is not supported (see porting-notes).
