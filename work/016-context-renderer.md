---
pr: 16
pr_url: https://github.com/phaser/labs-oo-agents-csharp/pull/16
jira:
epic: M3
status: merged
created: 2026-10-16
updated: 2026-10-17
tags: []
wiki_refs: []
---

# T13 — Context blocks renderer

## Context

Port block resolution and rendering of blocks + events into the LLM message list.

Depends on: T12, T06. Plan: [`docs/port-plan.md`](../docs/port-plan.md).

Upstream (pin in the plan):
- `src/nooa/context_blocks/ (renderer, formatters, scoped)`
- `src/nooa/runtime/context_builder.py`
- `src/nooa/runtime/event_query.py`

## Progress

- [x] Port `Context(value|provider, prefix)`, `DynamicContext`, `ResolvedBlock` (`expr` form moved to T18)
- [x] Port total-budget eviction (upstream has no per-block cut any more) and `ContextWindowStats`
- [x] Port XML and Markdown formatters and the cached layout order (static system, events, dynamic user message)
- [x] Port `EventQuery` filters
- [x] `ScopedContext` via `AsyncLocal`
- [x] Port tests: `tests/context_blocks/`

## Notes

From T04 (event IR): render `TaskEvent` under the name `Task`. Apply the "Not shown to the model" and "must not truncate" rules in the XML docs of the event properties, and suppress empty values (keep `0` and `false`). Port `LlmResponse.render_message` and `searchable_fields` (public values only, no native state). See `docs/porting-notes.md`, "Event IR".

`Context.expr` (a re-evaluated expression) waits for T18 because it needs the REPL evaluator.

Done in T13 (details in `docs/porting-notes.md`, "Context renderer, builder, event query"):
- `BlockFormatter` makes `LlmMessage` directly. The neutral `RenderedMessage` layer and the provider formatter are not ported (journal `parts`: T19).
- `ActorRuntime.BuildMessages()` (on `IRuntimeServices`) builds blocks and events into messages. `Agent.BlockFormatter`, `Agent.EventQuery` and `AgentOptions.Context` are wired. T14 strategies call `BuildMessages()` before `GenerateAsync`.
- Events render as `Name { Member = value }` with `[Hidden]` and `[MaxString]` rules (T04 notes done). `EventManager.Filter` uses the same searchable fields.
- Prompt injection hygiene: the XML wrapper turns `</` in content into `&lt;/`, and reduces block keys to safe names. Tests prove that argument text cannot close its block.
- For T18: add `Expr` / `SourceDynamic` to `BlockMetadata`, the `Context(expr=)` form, and the accessor text in `BlockFormatter.FormatDescription` and `ContextWindowStats.Format`.
- For T17: `CODEACT_INLINE_RETURN` skip in `BlockMessages`, and the strategy overrides (`GetBlockOverrides`, `GetStaticBlockKeys`, `GetBlockOrder`).
- For T19: `SystemPrompt` snapshot and `LlmResponse.DynamicContext`, journal `parts`.
