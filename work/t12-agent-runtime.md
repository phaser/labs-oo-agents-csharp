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

# T12 — Agent base, ActorRuntime, method wrapper

## Context

Port the `Agent` base class and the runtime core that the generated bodies call.

Depends on: T04, T05, T08, T11. Plan: [`docs/port-plan.md`](../docs/port-plan.md).

Upstream (pin in the plan):
- `src/nooa/agent.py`
- `src/nooa/runtime/actor.py (generate, lock, dispatch)`
- `src/nooa/runtime/method_wrapper.py`
- `src/nooa/runtime/event_manager.py`
- `src/nooa/runtime/context_manager.py`
- `src/nooa/runtime/context_vars.py`
- `src/nooa/runtime/hooks.py`
- `src/nooa/runtime/middleware.py`
- `src/nooa/method_llm.py`

## Progress

- [ ] Port `Agent` init: id, storage, `EventManager`, `ContextManager` with protected blocks `system_prompt`, `self`, `state`
- [ ] LLM resolution cascade: call → method → instance → class → parent agent (`AsyncLocal`)
- [ ] Generation lock (D10) and nesting depth check
- [ ] Method wrapper: argument validation, `BeforeAgentCall` / `AfterAgentCall`, middleware hooks, strategy dispatch
- [ ] `EventManager`: add / filter / update / collapse / subscribe / intercept
- [ ] Strategy precedence: call → `[Strategy]` → default
- [ ] Port tests: `tests/core_runtime/`, `tests/edge_cases/`, `test_method_call_permutations.py`, `test_agent_llm_accessor.py`, event-manager tests

## Notes

Keep `actor.py` trimmed to what Template / Predict / CodeAct need.
