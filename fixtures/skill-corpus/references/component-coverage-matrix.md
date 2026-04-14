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
- `Intent` - compiler-native intent authoring exists
- `Reverse` - tracked-source or normalized source can reverse-generate intent
- `Rebuild` - intent can regenerate package-inputs credibly enough to pack or import

Generator class:
- `full rebuildable` - intent authoring, reverse generation, and rebuild are all evidence-backed for the supported subset
- `authorable but partial` - some authoring or rebuild exists, but omission rules or family-shape limits still apply
- `source/readback only` - strong source or live proof exists, but the family is not part of the current authoring surface
- `source-first / permanent best-effort` - intentionally kept out of full live/generator parity

| Family | Representative components | Doc | Seed | Source | Readback | Drift | FT | Intent | Reverse | Rebuild | Status | Generator class | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Solution structure | `solution.xml`, `customizations.xml`, root components, layering | done | partial | done | best-effort | done | done | partial | partial | partial | partial | authorable but partial | Treat XML as evidence and packaging input, not the primary authoring surface. Classic export ZIPs now reverse-generate directly through internal PAC unpack normalization. |
| Schema core | tables, columns, relationships, relationships-to-root, views | done | done | done | done | done | done | done | done | done | done | full rebuildable | Table shell, custom columns, and lookup-driven relationship fold-back are the strongest end-to-end authoring surface today. |
| Schema detail | option sets, managed properties, keys, indexes, maps, images | done | done | done | partial | done | partial | partial | partial | partial | partial | authorable but partial | `seed-core` proves local picklist, boolean, and one true global choice through both per-entity option-set normalization and top-level `OptionSets/<name>.xml` source parsing. `seed-alternate-key` now adds dedicated alternate-key proof across source, live readback, stable-overlap drift, tracked-source emission, source-backed package copying, and JSON-intent generation, with `EntityKeyIndexStatus` explicitly treated as operational state rather than release drift. `seed-image-config` now adds entity-image plus attribute-image proof across source, live readback, stable-overlap drift, tracked-source emission, and source-backed package copying, while managed-property proof remains intentionally narrow to stable `IsCustomizable` on owning table and column artifacts rather than a new standalone public family. |
| Model-driven forms | `systemform`, FormXml, tabs, sections, controls, quick views, subgrids | done | done | done | done | done | done | partial | partial | partial | done | authorable but partial | Main forms are authorable and reverse-generatable; quick forms, card forms, and richer embedded control surfaces remain outside the rebuild-safe intent subset. |
| Views and queries | `savedquery`, `userquery`, view metadata, visualizations | done | done | done | done | done | partial | partial | partial | partial | partial | authorable but partial | Only rebuild-safe, user-authored savedquery views belong in the intent surface. Platform-generated system, lookup, quick-find, and similar non-authorable views are now omitted explicitly as `platformGeneratedArtifact`. Broader chart families and user-owned visualizations still need expansion. |
| App shell and config | app modules, site maps, web resources, environment variables, app settings, ribbon, custom controls | done | done | partial | partial | done | partial | partial | partial | partial | partial | authorable but partial | `seed-app-shell` and `seed-advanced-ui` now prove app-module plus app-setting round-trip. Ribbon source analysis is deeper through `RibbonDiff.xml` summaries, and embedded FormXML `controlDescriptions` are now a first-class source-analysis surface with richer helper output. `seed-advanced-ui` also now proves one real standalone `customcontrol` in live solution scope, but the unmanaged export still omits the source artifact, so standalone control rows remain readback-proven yet source-asymmetric. The toolchain now also parses and normalizes `complexcontrol` and `customcontroldefaultconfig` families when source or readback rows exist, although the neutral seed still has zero live rows for them. |
| Code and extensibility | plugin assembly, plugin type, step, step image, service endpoint, connector | done | done | done | done | done | done | none | none | none | done | source/readback only | `seed-plugin-registration` proves the neutral plugin-registration slice, and `seed-service-endpoint-connector` now proves the adjacent neutral integration-endpoint slice for `serviceendpoint` and `connector` across source, tracked-source, package-inputs, live readback, and stable-overlap drift. `dbm-sdk-registration` still provides a project-specific source-first SDK registration example, and code-level registration ingestion remains a separate follow-up concern. |
| Process and service policy | workflow, duplicate rule, routing rule, SLA, similarity rule, mobile offline profile | done | done | done | partial | done | done | none | none | none | partial | source/readback only | `seed-process-policy` now proves duplicate-rule plus condition, routing-rule plus item, and mobile-offline profile plus item through source parsing, tracked-source emission, source-backed package-input copying, live readback, and stable-overlap drift, with workflow and queue links kept best-effort. `source-only-similarity-rule` plus `source-only-sla` now define the explicit source-first boundary for `SimilarityRule`, `SLA`, and `SLAItem` rather than an untracked gap in live parity. |
| Security and access | roles, role privileges, privilege object type codes, field security profiles, field permissions, connection roles | done | done | done | partial | done | done | none | none | none | partial | source/readback only | `seed-process-security` now proves role shell, field security profile, field permission, and connection role through source parsing, tracked-source emission, source-backed package-input copying, live readback, and stable-overlap drift. `RolePrivilege` remains definition-adjacent best effort, and effective access still sits outside the compiler proof surface. |
| Environment and configuration | data source mapping, import map, canvas app, AI project/config, entity analytics | done | partial | done | partial | done | done | none | none | none | partial | source/readback only | Many artifacts are packaging- or tenant-dependent, so readback may stay partial. `seed-environment` proves one real `canvasapp` export, unpack, normalization, preserved `.msapp` asset, live readback, and stable-overlap drift slice. `seed-import-map` now stands as an explicit permanent source-first or best-effort boundary for `importmap` plus child data-source mappings through typed source parsing, tracked-source emission, package-input copying, and non-blocking drift. `seed-entity-analytics` now adds a compact neutral `entityanalyticsconfig` proof through typed source parsing, deterministic tracked/package emission, real `entityanalyticsconfigs` live projection, and stable-overlap drift. `seed-ai-families` now adds a compact neutral proof for `AI Project Type`, `AI Project`, and `AI Configuration` through typed source parsing, deterministic tracked/package emission, live readback, and stable-overlap drift, with `AI Configuration` anchored to the official `msdyn_aiconfiguration` surface and the other two families still relying on the neutral seed as the live-shape authority. |
| Reporting and legacy | display string, report, templates, attachments, mail merge, web wizard | partial | partial | partial | best-effort | partial | planned | none | none | none | planned | source/readback only | Useful to inventory and package, but often weaker for clean round-trip analysis. |

## Current Rolling Focus

The next work should prioritize:
- rebuild-fidelity hardening for the already-authorable subset before adding more breadth, especially rebuild-safe savedquery views, app modules, entity-only site maps, and environment variables from real classic exports
- keeping omission typing honest so platform-generated and non-authorable artifacts do not masquerade as missing rebuild support
- only after that, choosing the next later family in `Schema detail`, `Environment and configuration`, or `Code and extensibility` that can clear the same seed/readback/drift bar without overclaiming parity

Use the matrix to decide whether a family needs a neutral seed, deeper readback, a generator/rebuild hardening pass, or just a source-only note.
