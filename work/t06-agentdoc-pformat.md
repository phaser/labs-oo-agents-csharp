---
pr:
pr_url:
jira:
epic: M1
status: todo
created: 2026-10-16
updated: 2026-10-16
tags: []
wiki_refs: []
---

# T06 — Agentdoc PFormat() and truncation

## Context

Port `pformat()`: a reflection-based object printer with size budgets and truncation, used for the `state` block and REPL output.

Depends on: T05. Plan: [`docs/port-plan.md`](../docs/port-plan.md).

Upstream (pin in the plan):
- `src/nooa/agentdoc/_pformat.py`
- `src/nooa/agentdoc/_structured.py`
- `src/nooa/runtime/pprint.py`

## Progress

- [ ] Define the C# repr format for primitives, records, collections, dictionaries, enums, null
- [ ] Port depth / width / item-count truncation rules
- [ ] Handle cycles (reference tracking)
- [ ] Port `TruncatingStringIO` equivalent (`TruncatingTextWriter`)
- [ ] Port tests from `tests/agentdoc/` that cover `pformat`

## Notes

Largest single file upstream (2,085 lines). Split the PR if it grows past ~1.5k lines.

### From T05

- Replace `ValuePreview` (`src/Nooa/Agentdoc/ValuePreview.cs`) with the real printer. It prints field values and parameter defaults for `AgentDocs.Doc`, through one method `Format(object?)`. `Doc` applies no truncation today. Upstream passed `max_length`/`max_string`/`max_depth` limits there (for example 3/50/1 for field values, 5/50/1 for defaults).
- Not ported in T05 (`tests/agentdoc/`), all T06 unless marked:
  - pformat and truncation: `test_pformat_cycles`, `test_pformat_head_tail`, `test_pformat_remove_budget`, `test_pprint_respects_hidden`, `test_truncating_pformat`, `test_truncating_stream`, `test_file_backed_truncating_stream`, `test_event_max_string_override`, `test_structured`, `test_function_default_rendering` (default value text).
  - `doc()` parts that need the printer: the value-rendering cases of `test_core` (`pformat` calls) and `test_contracts`.
  - Python only, probably not applicable (confirm in T06 and record in porting-notes): `test_annotated_types`, `test_dataclass_properties`, `test_discover_pep563`, `test_discover_pydantic_forward_refs`, `test_namedtuple_future_annotations`, `test_typeddict_future_annotations`, `test_pandas_adapter`, `test_protocols`, `test_registry`, `test_metadata`, `test_docs` (`spec()`), `test_doc_config`, `test_visibility` (module variables, `hidden` blocks), `test_skill_lifecycle_visibility`, `test_referenced_types_demo`.

