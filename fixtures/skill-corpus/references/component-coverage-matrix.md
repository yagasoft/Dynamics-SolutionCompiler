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
| Schema detail | option sets, managed properties, keys, indexes, maps, images | done | done | done | partial | done | partial | partial | done | partial | partial | authorable but partial | `seed-core` proves local picklist, boolean, and one true global choice through both per-entity option-set normalization and top-level `OptionSets/<name>.xml` source parsing. `seed-alternate-key` now adds dedicated alternate-key proof across source, live readback, stable-overlap drift, tracked-source emission, source-backed package copying, and JSON-intent generation, with `EntityKeyIndexStatus` explicitly treated as operational state rather than release drift. `seed-image-config` now adds entity-image plus attribute-image proof across source, live readback, stable-overlap drift, tracked-source emission, reverse-generated intent, and package rebuild, while managed-property proof remains intentionally narrow to stable `IsCustomizable` on owning table and column artifacts rather than a new standalone public family. |
| Model-driven forms | `systemform`, FormXml, tabs, sections, controls, quick views, subgrids | done | done | done | done | done | done | done | done | partial | done | authorable but partial | Main forms are now export-backed rebuild-proven through the supported authoring subset. Supported `quick` and `card` forms plus `field`, `quickView`, and `subgrid` controls now reverse-generate and rebuild structurally as well, while unsupported control shapes or missing-fidelity field references fall back to explicit source-backed handling instead of silent loss. |
| Views and queries | `savedquery`, `userquery`, view metadata, visualizations | done | done | done | done | done | partial | partial | partial | partial | partial | authorable but partial | Rebuild-safe, user-authored savedquery views are now export-backed rebuild-proven through the supported authoring subset. Platform-generated system, lookup, quick-find, and similar non-authorable views are omitted explicitly as `platformGeneratedArtifact`. Rebuild-safe authored saved-query visualizations now reverse-generate structurally into `tables[].visualizations[]` when the source metadata is complete, including direct classic-export reverse generation for the advanced UI seed, while user-owned visualizations and richer chart authoring remain future work. |
| App shell and config | app modules, site maps, web resources, environment variables, app settings, ribbon, custom controls | done | done | partial | partial | done | partial | partial | done | partial | partial | authorable but partial | Entity-only app modules plus site maps and supported environment variable shapes are now export-backed rebuild-proven through the supported structured subset. `seed-app-shell` and `seed-advanced-ui` now also reverse-generate broader app-shell artifacts through hybrid source-backed intent, and supported app-shell detail such as `appSettings` plus `entity` / `url` / `webResource` site-map subareas now keeps its structured intent shape while staged web resources remain source-backed. Hybrid rebuild now stages the matching web-resource payload assets as well as `.data.xml` metadata, while ribbon, custom-control adjuncts, and deeper app-shell breadth remain partial. |
| Code and extensibility | plugin assembly, plugin type, step, step image, service endpoint, connector | done | done | done | done | done | done | partial | done | partial | done | authorable but partial | `seed-plugin-registration` and `seed-service-endpoint-connector` still prove the neutral source/live/drift slices, and they now also reverse-generate into hybrid source-backed intent and rebuild back into package-inputs without silent omission. `dbm-sdk-registration` remains a project-specific source-first SDK registration example, and code-level registration ingestion is still separate follow-up work. |
| Process and service policy | workflow, duplicate rule, routing rule, SLA, similarity rule, mobile offline profile | done | done | done | partial | done | done | partial | done | partial | partial | authorable but partial | `seed-process-policy` proves duplicate-rule plus condition, routing-rule plus item, and mobile-offline profile plus item through source parsing, tracked-source emission, source-backed package-input copying, live readback, and stable-overlap drift. Those families can now also reverse-generate into hybrid source-backed intent and rebuild package-inputs, while `SimilarityRule`, `SLA`, and `SLAItem` remain the explicit source-first boundary. |
| Security and access | roles, role privileges, privilege object type codes, field security profiles, field permissions, connection roles | done | done | done | partial | done | done | partial | done | partial | partial | authorable but partial | `seed-process-security` proves role shell, field security profile, field permission, and connection role through source parsing, tracked-source emission, source-backed package-input copying, live readback, and stable-overlap drift. Those families, including role privileges where seed fidelity exists, can now reverse-generate into hybrid source-backed intent and rebuild package-inputs, while effective access still sits outside the compiler proof surface. |
| Environment and configuration | data source mapping, import map, canvas app, AI project/config, entity analytics | done | partial | done | partial | done | done | partial | done | partial | partial | authorable but partial | Many artifacts are packaging- or tenant-dependent, so readback may stay partial. `seed-environment` proves one real `canvasapp` export, unpack, normalization, preserved `.msapp` asset, live readback, and stable-overlap drift slice, and canvas apps can now reverse-generate and rebuild through hybrid source-backed intent. `seed-import-map` remains an explicit permanent source-first or best-effort boundary for `importmap` plus child data-source mappings. `seed-entity-analytics` and `seed-ai-families` now also reverse-generate and rebuild through the hybrid intent path in addition to their existing source/live/drift proof. |
| Reporting and legacy | display string, report, templates, attachments, mail merge, web wizard | partial | partial | partial | best-effort | partial | planned | none | none | none | planned | source/readback only | Useful to inventory and package, but often weaker for clean round-trip analysis. |

## Current Rolling Focus

The next work should prioritize:
- keeping new authoring expansion on the same export-backed rebuild bar now proven for rebuild-safe savedquery views, entity-only app modules or site maps, and supported environment-variable shapes
- keeping omission typing honest so platform-generated and non-authorable artifacts do not masquerade as missing rebuild support
- only after that, choosing the next later family in `Schema detail`, `Environment and configuration`, or `Code and extensibility` that can clear the same seed/readback/drift bar without overclaiming parity

Use the matrix to decide whether a family needs a neutral seed, deeper readback, a generator/rebuild hardening pass, or just a source-only note.
