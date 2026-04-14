# Backlog

This backlog is intentionally evidence-driven. It now tracks the work that starts after the `.NET 10` bootstrap baseline already exists.

## Priorities

- `P0` - required for roadmap continuity or release safety.
- `P1` - important next step with strong evidence value.
- `P2` - useful follow-up once the core proof surface is stable.

## Active Items

| ID | Status | Priority | Item | Why it matters | Acceptance |
| --- | --- | --- | --- | --- | --- |
| B-001 | done | P0 | Implement real unpacked XML readers for the strongest proven families | Turns the bootstrap structural inventory into usable typed source ingestion | `XmlSolutionReader` emits family-aware entities for schema core, forms, views, app shell, environment variables, charts, and canvas-app metadata from neutral fixtures |
| B-002 | done | P0 | Implement tracked-source emitters that materialize files, not just plans | The compiler needs deterministic output to become a real release tool | `Emitters.TrackedSource` writes stable tracked-source trees and golden tests prove ordering and content stability |
| B-003 | done | P0 | Deepen stable-overlap diff beyond family/logical-name matching | Honest drift classification is one of the core product promises | `Diff` compares family-specific stable fields for the first proven families and suppresses expected platform noise |
| B-004 | done | P1 | Wire live readback through Dataverse Web API with `Azure.Identity` | Readback must graduate from placeholder to proof | `Readers.Live` can capture at least schema core, forms, views, app modules, environment variables, and canvas apps into the canonical IR |
| B-005 | done | P1 | Complete the release path with deterministic package-input emission, PAC execution, and CLI/compiler wiring | Packaging/import/publish/check must move from registered to operational as one governed release slice | `Emitters.Package` writes stable package-input trees, `Packaging.Pac` executes `pack` / `import` / optional `check`, and the compiler/CLI release path drives `emit`, `readback`, `diff`, `pack`, `import`, `publish`, and `check` against the proven slice |
| B-006 | done | P1 | Deliver the JSON-driven greenfield generator v1 for core families | This is the first real compiler milestone beyond source readback and release-path proof | The compiler reads compiler-native JSON intent, projects it into the canonical IR, emits tracked-source, synthesizes PAC-packable unpacked solution input for the supported v1 families, and round-trips through `XmlSolutionReader` plus stable-overlap diff without blocking drift |
| B-008 | done | P1 | Reverse-generate supported tracked-source JSON into compiler-native intent-spec JSON | The compiler needs an editable reverse-authoring loop so teams can inspect exported source, reconstruct supported intent, and rebuild from authoring JSON instead of staying trapped in raw XML or tracked-source snapshots | `Readers.TrackedSource` reconstructs the supported canonical subset, `emit --layout intent-spec` writes deterministic `intent-spec.json` plus `reverse-generation-report.json`, optional preserved `forms[].id` and `views[].id` round-trip, and tracked-source -> intent-spec -> package-inputs -> XML reread stays free of blocking drift for the supported subset |
| B-009 | active | P1 | Harden rebuild fidelity for the already-supported authoring subset before adding more breadth | The weakest remaining surface is not raw family count; it is the trust gap between “we touched it” and “we can export it, reverse it, edit it, rebuild it, and reimport it safely” | Classic exported solution ZIPs reverse-generate directly through `emit --layout intent-spec` without manual unpack, omission reporting distinguishes unsupported families vs platform-generated artifacts vs missing source fidelity, at least one compact real export/delete/reimport loop stays green, and docs/coverage explicitly track `Intent author`, `Reverse from tracked-source`, and `Rebuild from intent` readiness |
| B-007 | active | P2 | Deepen harder families one-by-one across read, emit, live, and generator paths, continuing breadth after alternate keys, the explicit source-first import-map boundary, entity analytics, the compact AI proof slice, neutral plugin-registration plus integration-endpoint proof, process-policy proof, security-definition proof, and source-first similarity or SLA handling | Breadth still matters after the baseline and first greenfield slice | Each new family reaches `read -> plan -> emit -> readback -> diff`, and where generation is appropriate it also reaches `intent -> package-inputs -> PAC pack`, or it is explicitly marked permanent best-effort |

## Next Review

- Keep `B-009` ahead of more breadth until the supported authoring subset is consistently trustworthy from real classic exports, especially for rebuild-safe authored views, app modules, entity-only site maps, and environment variables.
- Treat `B-007` as a follow-on breadth stream, not the default next slice, until the rebuild-fidelity surface stops depending on subset caveats and omission reports for the already-authorable families.
- Deepen reverse-generation only when the next family is already proven through canonical read, tracked/package emission, stable-overlap reread, and a clean classification in the omission report; keep unsupported tracked-source families explicit until that bar is met.
