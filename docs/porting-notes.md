# Porting notes

One entry per deliberate deviation from upstream. Upstream pin: see [`port-plan.md`](port-plan.md).

Format: `## <upstream concept>` → **C#:** what we do. **Why:** reason. **Tests:** upstream tests skipped or changed.

## `...` method bodies

**C#:** `[Agentic] public partial Task<T> M(...);`. The source generator emits the body.
**Why:** C# has no `...` body. A partial method with no body is a compile error, so the generator is mandatory.
**Tests:** `test_ellipsis_detection_exec.py` and the bytecode fallback tests do not apply.

## Pre-ellipsis code

**C#:** Not supported in v1. Upstream runs statements before the `...` first.
**Why:** a partial declaration has no body. A later option is an `OnBefore<Method>` partial hook.
**Tests:** `test_pre_ellipsis_code.py` is deferred.

## Docstrings as prompts

**C#:** XML doc comments. The generator embeds `<summary>` (and `<remarks>`) as string constants.
**Why:** doc comments are not in the compiled assembly.

## REPL action language

**C#**, through Roslyn scripting. Upstream uses Python `exec`.
**Why:** decision D1 in the plan. All prompts and error hints are rewritten for C#.

## Dynamic method guard

**C#:** Not ported. `__setattr__` guards are not needed in a static language.
**Tests:** `test_dynamic_method_guard.py` does not apply.
