# Component Coverage Matrix

This is the rolling ledger for Dataverse component-family coverage in the skill.

Status legend:
- `done` - documented, seeded, normalized, read back, drift-checked, and forward-tested
- `partial` - some coverage exists, but more breadth or depth is still needed
- `best-effort` - the family is supported honestly, but readback or symmetry is incomplete
- `planned` - not yet seeded or normalized in a reusable way

Coverage columns:
- `Doc` - reference guidance exists
- `Seed` - a compact neutral unmanaged example exists
- `Source` - exported or unpacked source is parsed
- `Readback` - live Dataverse readback exists
- `Drift` - compare logic exists
- `FT` - forward tests exist

| Family | Representative components | Doc | Seed | Source | Readback | Drift | FT | Status | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Solution structure | `solution.xml`, `customizations.xml`, root components, layering | done | partial | done | best-effort | done | done | partial | Treat XML as evidence and packaging input, not the primary authoring surface. |
| Schema core | tables, columns, relationships, relationships-to-root, views | done | done | done | done | done | done | done | Use the canonical app model to decide what belongs in the table family. |
| Schema detail | option sets, managed properties, keys, indexes, maps, images | done | done | done | partial | done | partial | partial | `seed-core` now proves local picklist, boolean, and one true global choice through both per-entity option-set normalization and top-level `OptionSets/<name>.xml` source parsing. Key readback plumbing is in place, but live alternate-key proof is still thin and unattended key creation remains best-effort in this environment. Managed-property and image families still need neutral proof. |
| Model-driven forms | `systemform`, FormXml, tabs, sections, controls, quick views, subgrids | done | done | done | done | done | done | done | Prefer form summaries and normalized structure over raw XML inspection. |
| Views and queries | `savedquery`, `userquery`, view metadata, visualizations | done | done | done | done | done | partial | partial | View readback is strong, and `seed-advanced-ui` now proves one real saved-query visualization export/readback/drift slice through `Entities/<entity>/Visualizations/<id>.xml`. Broader chart families and user-owned visualizations still need expansion. |
| App shell and config | app modules, site maps, web resources, environment variables, app settings, ribbon, custom controls | done | done | partial | partial | done | partial | partial | `seed-app-shell` and `seed-advanced-ui` now prove app-module plus app-setting round-trip. Ribbon source analysis is deeper through `RibbonDiff.xml` summaries, and embedded FormXML `controlDescriptions` are now a first-class source-analysis surface with richer helper output. `seed-advanced-ui` also now proves one real standalone `customcontrol` in live solution scope, but the unmanaged export still omits the source artifact, so standalone control rows remain readback-proven yet source-asymmetric. The toolchain now also parses and normalizes `complexcontrol` and `customcontroldefaultconfig` families when source or readback rows exist, although the neutral seed still has zero live rows for them. |
| Code and extensibility | plugin assembly, plugin type, step, step image, service endpoint, connector | done | partial | partial | best-effort | partial | partial | partial | DBM baseline proves a real plugin assembly, the toolchain now carries richer plugin-step message/filter/handler semantics, and `dbm-sdk-registration` adds a source-first SDK-message registration example. Code bodies and some integration families still remain source-first unless a supported readback path exists. |
| Process and service policy | workflow, duplicate rule, routing rule, SLA, similarity rule, mobile offline profile | done | done | partial | best-effort | partial | partial | partial | Neutral seeds now cover workflow source shape, a real duplicate-rule round-trip, a real routing-rule round-trip, and a mobile-offline profile export plus best-effort readback/drift slice. Similarity-rule normalization now includes base or matching entity, inactive-record handling, `maxkeywords`, and `ngramsize`, and the corpus now includes compact source-only fixtures for both `SimilarityRule` and `SLA`. The Web API surface is still incomplete for normal create or list operations in parts of this family, so SLA remains source-first until a service-capable neutral live seed is practical. |
| Security and access | roles, role privileges, privilege object type codes, field security profiles, field permissions, connection roles | done | done | partial | best-effort | partial | partial | partial | Neutral seed now covers role shell, secured attribute plus field permission, field security profile, and connection role. Role privileges remain source-first and effective access still needs live proof. |
| Environment and configuration | data source mapping, import map, canvas app, AI project/config, entity analytics | done | partial | partial | partial | partial | partial | partial | Many artifacts are packaging- or tenant-dependent, so readback may stay partial. `seed-environment` now proves one real `canvasapp` export, unpack, normalization, preserved `.msapp` asset, live readback, and stable-overlap drift slice. `importmap`, data-source mapping, AI, and analytics families still need neutral proof. |
| Reporting and legacy | display string, report, templates, attachments, mail merge, web wizard | partial | partial | partial | best-effort | partial | planned | planned | Useful to inventory and package, but often weaker for clean round-trip analysis. |

## Current Rolling Focus

The next families to deepen are:
- `Code and extensibility`
- `Environment and configuration` beyond the new canvas-app foothold, especially `importmap` and data-source mapping
- `Schema detail` for alternate keys, image configs, and managed-property proof after the new option-set slice

Use the matrix to decide whether a family needs a neutral seed, deeper readback, better drift comparison, or just a source-only note.
