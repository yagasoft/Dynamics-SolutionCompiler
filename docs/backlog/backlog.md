# Backlog

This backlog is intentionally evidence-driven. It now tracks the work that starts after the `.NET 10` bootstrap baseline already exists.

## Priorities

- `P0` - required for roadmap continuity or release safety.
- `P1` - important next step with strong evidence value.
- `P2` - useful follow-up once the core proof surface is stable.

## Active Items

| ID | Priority | Item | Why it matters | Acceptance |
| --- | --- | --- | --- | --- |
| B-001 | P0 | Implement real unpacked XML readers for the strongest proven families | Turns the bootstrap structural inventory into usable typed source ingestion | `XmlSolutionReader` emits family-aware entities for schema core, forms, views, app shell, environment variables, charts, and canvas-app metadata from neutral fixtures |
| B-002 | P0 | Implement tracked-source emitters that materialize files, not just plans | The compiler needs deterministic output to become a real release tool | `Emitters.TrackedSource` writes stable tracked-source trees and golden tests prove ordering and content stability |
| B-003 | P0 | Deepen stable-overlap diff beyond family/logical-name matching | Honest drift classification is one of the core product promises | `Diff` compares family-specific stable fields for the first proven families and suppresses expected platform noise |
| B-004 | P1 | Wire live readback through Dataverse Web API with `Azure.Identity` | Readback must graduate from placeholder to proof | `Readers.Live` can capture at least schema core, forms, views, app modules, environment variables, and canvas apps into the canonical IR |
| B-005 | P1 | Add deterministic PAC wrapper execution | Packaging/import/publish/check must move from registered to operational | `Packaging.Pac` executes PAC commands with validated inputs, captured outputs, and integration coverage |
| B-006 | P1 | Add greenfield generator passes for core families | This is the first real “compiler” milestone | The compiler can generate a new solution, publisher, tables, columns, relationships, forms, views, app shell basics, and environment variables from declarative intent |
| B-007 | P2 | Deepen harder families one-by-one | Breadth still matters after the baseline | Each new family reaches `read -> plan -> emit -> readback -> diff` or is explicitly marked permanent best-effort |

## Next Review

- Start with `B-001`, `B-002`, and `B-003` together because they unlock the first deterministic core-family round trip.
- Promote `B-004` only after the typed core-family source/read pipeline is stable enough to compare against live state.
