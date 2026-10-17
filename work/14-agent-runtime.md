---
pr: 14
pr_url: https://github.com/phaser/labs-oo-agents-csharp/pull/14
jira:
epic: M3
status: merged
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

- [x] Port `Agent` init: id, storage, `EventManager`, `ContextManager` with protected blocks `system_prompt`, `self`, `state`
- [x] LLM resolution cascade: call → method → instance → class → parent agent (`AsyncLocal`)
- [x] Generation lock (D10) and nesting depth check
- [x] Method wrapper: argument validation, `BeforeAgentCall` / `AfterAgentCall`, middleware hooks, strategy dispatch
- [x] `EventManager`: add / filter / update / collapse / subscribe / intercept
- [x] Strategy precedence: call → `[Strategy]` → default
- [x] Port tests: `tests/core_runtime/`, `tests/edge_cases/`, `test_method_call_permutations.py`, `test_agent_llm_accessor.py`, event-manager tests

## Notes

Keep `actor.py` trimmed to what Template / Predict / CodeAct need.

From T07 (storage): `IStorageManager` has the blob API only (`SaveSnapshotJson`, `LoadSnapshotJson`, `GetLatestSnapshotId`, `ListSnapshots`). T12 adds:
- `AgentSnapshot` (version 2) and `snapshot_to_json` / `snapshot_from_json`: walk agent fields, context blocks and attributes. Check `version` and the type allowlist before restoring any value (the file can be untrusted). Never `BinaryFormatter` or `TypeNameHandling` (D11).
- `serialize` / `deserialize` with the type allowlist, `markers.py` (`nosnapshot`, `snapshotable`), `save_snapshot(agent)`, `restore_snapshot(id, agent)`, `restore_latest_snapshot`.
- The upstream tests that need an agent: `test_sqlite_storage_latest.py`, `test_sqlite_close_commit.py` (snapshot part), `test_session_resume_tag_collision.py` (agent and `EventManager` parts), `tests/storage/test_snapshot.py`, `test_serialization.py`, `test_markers.py`, `tests/runtime/test_snapshot.py`.
- `EventManager` allocates tags through `IEventBackend.AllocateNextTag()` so that several managers on one backend never collide.

### From T11 (the seam T12 implements)

- `Agent` must implement `Nooa.IAgenticTarget` (`InvokeAgenticAsync<T>` and the void overload). Generated `[Agentic]` bodies call it on `this` with a `Nooa.Strategies.CurrentCall`.
- `CurrentCall` holds: `Id`, `MethodName`, `Signature` (text), `Docstring` (expanded method prompt), `Arguments` (ordered `CallArgument(Name, Type, Value)`, no trailing `CancellationToken`), `ReturnType` (null for `Task`), `StrategyType` (from `[Strategy]`), `ParentId` (runtime sets it).
- Not ported yet, add here when needed: `decorator`, `is_async` (always async), `pre_ellipsis_code` (not supported), `param_specs` (needs the spec attribute), `session_locals`, `execution_locals`, `task_tag` (need the CodeAct types), upstream equality by `id`, `format_parameters_as_code` and `format_signature` (strategy rendering, T14). A `with` copy keeps all fields.
- Class data: `AgentMetadataRegistry.TryGet(agentType)` gives `Llm` (from `[AgentOptions]`) and the system prompt renderer (`Func<object,string>`, pass the agent). Walk base types yourself for inheritance.
- Doc text: the generated `IAgentDocProvider` is registered by a module initializer. Its text keeps `{Member.Path}` placeholders as written. Use `AgentMetadata.SystemPrompt` for the rendered prompt.
- Method prompts are already expanded in `CurrentCall.Docstring`. Parameter docs reach the model only through `AgentDocs`.
- `[NoSnapshot]` and `[NoTrace]` exist without behaviour. The wrapper reads them.

### Decisions and deviations (T12)

Details are in `docs/porting-notes.md`, section "Agent, runtime, event manager, snapshots (T12)".

- Seam: `src/Nooa/Llm/ILlmClientResolver.cs` is a byte-identical copy of the lead's file (T10 adds the same file and implements it). `Agent.DefaultLlmResolver` is the process-wide resolver. `AgentOptions.LlmResolver` beats it.
- Call-level LLM and strategy: `Agent.UseLlm(...)` and `Agent.UseStrategy(...)` return a scope (`IDisposable`, `AsyncLocal`). Method-level LLM: `[AgentOptions(Llm = ...)]` on the method (the attribute now allows methods). `[NoSnapshot]` now allows fields and properties.
- No default strategy until T14/T17: `AgentOptions.DefaultStrategy` or `[Strategy]` is needed.
- `GenerationStrategy` is an abstract class (`Name`, `Traceable`, `RequiresLock`, `ExecuteAsync`). `IRuntimeServices` has `GenerateAsync` over ready messages. T13 adds message building, T17 `execute_code`.
- Middleware is typed by context (`Intercept<TContext>`). Four contexts exist. The decision and execute-code contexts are minimal.
- Not ported: `decision_model` (B03), `render_config`, `context={}`, `event_query` (T13), hook composition (T19), `SnapshotVars`, `PersistentVars`, pydantic envelopes.
- Size: about 1.8k lines of non-test code without comments and blank lines (2.8k with doc comments). One PR.
- Existing tests changed: `AgentOptionsAttributeTests` and `NoSnapshotAttributeTests` (new attribute targets).
