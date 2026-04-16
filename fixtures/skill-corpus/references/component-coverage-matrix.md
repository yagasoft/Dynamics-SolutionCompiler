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

| Family | Representative components | Doc | Seed | Source | Readback | Drift | FT | Intent | Reverse | Rebuild | Status | Generator class | B-010 target | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Solution structure | `solution.xml`, `customizations.xml`, root components, layering | done | partial | done | best-effort | done | done | partial | partial | partial | partial | authorable but partial | supporting evidence | Treat XML as evidence and packaging input, not the primary authoring surface. Classic export ZIPs now reverse-generate directly through internal PAC unpack normalization. |
| Schema core | tables, columns, relationships, relationships-to-root, views | done | done | done | done | done | done | done | done | done | done | full rebuildable | full rebuildable (structured) | Table shell, custom columns, and lookup-driven relationship fold-back are the strongest end-to-end authoring surface today. |
| Schema detail | option sets, managed properties, keys, indexes, maps, images | done | done | done | partial | done | partial | partial | done | partial | partial | authorable but partial | full rebuildable (structured) | `seed-core` proves local picklist, boolean, and one true global choice through both per-entity option-set normalization and top-level `OptionSets/<name>.xml` source parsing. `seed-alternate-key` now adds dedicated alternate-key proof across source, live readback, stable-overlap drift, tracked-source emission, source-backed package copying, and JSON-intent generation, with `EntityKeyIndexStatus` explicitly treated as operational state rather than release drift. `seed-image-config` now clears the full Wave 1 rebuild bar for the image lane: entity-image plus attribute-image proof across source, live readback, stable-overlap drift, tracked-source emission, direct classic-export reverse generation, package rebuild, PAC pack, and real live delete/reimport verification, with `publish` finalizing `entityimageconfigs` plus `attributeimageconfigs` and reverse generation recovering image config from `Other/Customizations.xml`. The current later-`B-007` slice now also proves local picklist and boolean option sets through fixture-backed live readback and stable-overlap; only system `state` / `status` option sets remain intentionally ignored as platform-owned operational surfaces. Managed-property proof still remains intentionally narrow to stable `IsCustomizable` on owning table and column artifacts rather than a new standalone public family, so the broader schema-detail lane remains partial until the non-image gaps close. |
| Model-driven forms | `systemform`, FormXml, tabs, sections, controls, quick views, subgrids | done | done | done | done | done | done | done | done | partial | done | authorable but partial | full rebuildable (structured) | Main forms are now export-backed rebuild-proven through the supported authoring subset. Supported `quick` and `card` forms plus `field`, `quickView`, and `subgrid` controls now reverse-generate and rebuild structurally, and that same quick/card subset now also clears strict entity-scoped live readback and stable-overlap in `seed-forms`, while unsupported control shapes or missing-fidelity field references fall back to explicit source-backed handling instead of silent loss. |
| Views and queries | `savedquery`, `userquery`, view metadata, visualizations | done | done | done | done | done | partial | partial | partial | partial | partial | authorable but partial | full rebuildable (structured) for supported authored subset; source-first / permanent best-effort for platform-generated views | Rebuild-safe, user-authored savedquery views are now export-backed rebuild-proven through the supported authoring subset. Platform-generated system, lookup, quick-find, and similar non-authorable views are an explicit permanent boundary and are omitted as `platformGeneratedArtifact` rather than hidden debt. Rebuild-safe authored saved-query visualizations now reverse-generate structurally into `tables[].visualizations[]` when the source metadata is complete, and `seed-advanced-ui` now clears both the full live export-backed rebuild loop for that supported chart subset and solution-scoped `savedqueryvisualizations` live readback with normalized chart-definition signatures, while user-owned visualizations and richer chart authoring remain future work. |
| App shell and config | app modules, site maps, web resources, environment variables, app settings, ribbon, custom controls | done | done | partial | partial | done | partial | partial | done | partial | partial | authorable but partial | full rebuildable (hybrid source-backed) | Entity-only app modules plus site maps and supported environment variable shapes are now export-backed rebuild-proven through the supported structured subset. `seed-app-shell` and `seed-advanced-ui` now also clear solution-scoped `webresourceset` live proof for the staged web-resource lane: `WebResource` artifacts project from component type `61`, compare on stable payload byte-length plus content-hash overlap, and keep missing or undecodable content as explicit warning-only best effort instead of silent loss. Site maps now also preserve canonical structured definition evidence across source, reverse-generation, package reread, and live readback for the current seeded navigation shapes, the current neutral seeds now also keep subarea adjunct detail such as `Icon` / `VectorIcon` plus explicit `Client` / `PassParams` / `AvailableOffline` behavior instead of flattening it away during round-trip, and the canonical GUID-backed dashboard, app-aware dashboard, canonical custom-page, custom-page record-context, plus canonical Dataverse `entitylist` / `entityrecord` deep-link subsets now survive the same path as structured targets instead of collapsing to generic URLs. Broader richer site-map target shapes and arbitrary parameter-rich `main.aspx` links now also preserve an explicit canonical raw-`url` boundary across source, live readback, reverse generation, package rebuild, and stable-overlap rather than remaining ambiguous debt. App-module role maps now also survive tracked-source summaries, reverse generation, and derived package rebuild through structured `roleIds` plus emitted `AppModuleRoleMaps`, while neutral live `appmodules` readback underreports `role_ids` and is therefore kept as an explicit best-effort parity boundary. Compact ribbon shell proof now also lands honestly as source-first: `RibbonDiff.xml` parses into typed `Ribbon` artifacts, emits deterministic tracked-source summaries, reverse-generates into `sourceBackedArtifacts[]`, preserves deterministic `Entities/<entity>/RibbonDiff.xml` package layout, and stays explicit non-blocking or unsupported-live until new neutral evidence overturns that boundary. Standalone custom controls now also have solution-scoped live readback through component type `66` plus `customcontrols`, with manifest/clientjson summaries captured as stable comparison-safe properties and drift recorded as explicit source-asymmetric best effort when unmanaged export omits matching source artifacts. `ComplexControl` and `CustomControlDefaultConfig` remain explicit no-row boundaries in the neutral corpus, so the lane still remains partial overall only because those explicit best-effort boundaries still exist. |
| Code and extensibility | plugin assembly, plugin type, step, step image, service endpoint, connector | done | done | done | done | done | done | done | done | done | done | full rebuildable (hybrid source-backed) | full rebuildable (hybrid source-backed) | `seed-plugin-registration` and `seed-service-endpoint-connector` now clear the full Wave 4 live bar. Both lanes still prove the neutral source/live/drift slices, reverse-generate into hybrid source-backed intent, rebuild back into package-inputs without silent omission, PAC-pack successfully, and survive a real `export -> reverse -> delete -> rebuild -> import/publish -> verify -> re-export` loop. The plug-in lane now preserves sharded `SdkMessageProcessingSteps/*.xml` metadata strongly enough to recreate the assembly, type, step, and step image from a fully deleted live state, while `dbm-sdk-registration` remains a project-specific source-first SDK registration example and code-level registration ingestion is still separate follow-up work. |
| Process and service policy | workflow, duplicate rule, routing rule, SLA, similarity rule, mobile offline profile | done | done | done | partial | done | done | partial | done | done | done | full rebuildable (hybrid source-backed) for duplicate/routing/mobile-offline; source-first / permanent best-effort for `SimilarityRule`/`Sla`/`SlaItem` | `seed-process-policy` proves duplicate-rule plus condition, routing-rule plus item, and mobile-offline profile plus item through source parsing, tracked-source emission, source-backed package-input copying, live readback, and stable-overlap drift. Those families now also clear the full live export/delete/rebuild/import/re-export loop through hybrid source-backed intent, while `SimilarityRule`, `SLA`, and `SLAItem` remain the explicit source-first boundary. |
| Security and access | roles, role privileges, privilege object type codes, field security profiles, field permissions, connection roles | done | done | done | partial | done | done | partial | done | done | done | full rebuildable (hybrid source-backed), with effective-access boundary | `seed-process-security` proves role shell, role privilege, field security profile, field permission, and connection role through source parsing, tracked-source emission, source-backed package-input copying, live readback, stable-overlap drift, and a full live export/delete/rebuild/import/re-export loop. Effective access still sits outside the compiler proof surface, but the supported owned-definition lane no longer needs a `RolePrivilege` carve-out in the current compact seed. |
| Import maps and data-source mappings | import map, child data-source mapping | done | partial | done | partial | done | done | none | none | none | partial | source-first / permanent best-effort | source-first / permanent best-effort | `seed-import-map` remains the explicit permanent source-first boundary. It has typed source parsing, tracked-source emission, package-input copying, and non-blocking drift diagnostics, but it is not part of the live rebuildability target unless new neutral evidence overturns that boundary. |
| Canvas apps | `canvasapp` metadata, `.msapp`, packaged assets | done | done | done | partial | done | done | partial | done | done | done | full rebuildable (hybrid source-backed) | full rebuildable (hybrid source-backed) | `seed-environment` now clears source parsing, tracked-source emission, reverse generation, hybrid rebuild, PAC pack, and the full live export/delete/rebuild/import/re-export loop for the packaged canvas-app lane. Runtime-only bindings remain out of drift and omission-safe in reverse generation. |
| Entity analytics | `entityanalyticsconfig` | done | done | done | done | done | done | partial | done | done | done | full rebuildable (hybrid source-backed) | full rebuildable (hybrid source-backed) | `seed-entity-analytics` now clears source parsing, tracked-source emission, reverse generation, hybrid apply-only rebuild, PAC pack, and the full live export/delete/rebuild/import/re-export loop. The post-import export writes the family back through `Other/Customizations.xml`, and reverse generation now preserves that shape explicitly. |
| AI families | `AI Project Type`, `AI Project`, `AI Configuration` | done | done | done | partial | done | done | partial | done | partial | done | source-first / permanent best-effort | source-first / permanent best-effort | `seed-ai-families` keeps typed source parsing, tracked-source emission, live readback, stable-overlap drift, reverse generation, and package-level hybrid rebuild, but the lane is now explicitly closed as a permanent boundary for the current environment. Live `publish` fails fast with a clear compiler diagnostic because Dataverse rejects `AITemplate` create with `OperationNotSupported`, so this family is no longer tracked as unresolved rebuildability debt inside `B-010`. |
| Reporting and legacy | display string, report, templates, attachments, mail merge, web wizard | partial | done | done | best-effort | done | done | none | done | partial | partial | source-first / permanent best-effort | outside current program | Compact source-first proof now covers `Report`, `Template`, `DisplayString`, `Attachment`, and `LegacyAsset` through typed source parsing, tracked-source summaries, reverse-generated `sourceBackedArtifacts[]`, deterministic package preservation, and explicit non-blocking drift. PAC pack is now explicitly boundary-tested and still fails root-component validation for the compact synthetic seed, so this lane remains an honest source-first boundary rather than rebuildable parity. |

## Current Completion Program

`B-010` is now complete. The `B-010 target` column records the intended end state each lane had to reach, and the current rows now reflect those explicit outcomes.

Execution order:
1. Wave 1: close schema-detail and form gaps with structured authoring
2. Wave 2: finish the broader app-shell lane
3. Wave 3: promote canvas, entity analytics, and AI to full hybrid rebuildable
4. Wave 4: promote code and extensibility to full hybrid rebuildable
5. Wave 5: promote process and security definitions to full hybrid rebuildable
6. Wave 6: permanently close the families that should not be forced into full generation
7. Wave 7: run one maximal supported end-to-end proof

Wave 7 maximal supported proof is now complete, compact AI has been closed as a permanent boundary, the first post-`B-010` `B-007` slice now lands reporting/legacy as an explicit source-first boundary with real parse/track/reverse/package evidence, the next `B-007` slice closes app-shell `WebResource` live proof without reopening broader app-shell scope, the following slice closes app-module role-map reverse/package fidelity while keeping live role-map parity explicit best effort, the next slice closes compact ribbon shell proof as a source-first boundary instead of leaving it as ambiguous app-shell debt, the later slices close the canonical site-map dashboard, custom-page, Dataverse entity URL, app-aware dashboard, custom-page record-context, and richer raw-`url` site-map boundaries without overclaiming unsupported deep-link parity, and the latest bundled slice closes current local picklist/boolean option-set, quick/card form, and solution-scoped saved-query visualization live proof without overclaiming the broader remaining schema-detail or visualization remainder. `B-007` remains the active breadth item from this baseline.

Do not retroactively reopen `B-010` in the planning docs unless new evidence genuinely overturns one of these explicit end states.

## Exhaustive Owner-Family Universe

The checked-in [solutioncomponent-componenttype-inventory.json](C:\Git\Dataverse-Solution-KB\fixtures\skill-corpus\references\solutioncomponent-componenttype-inventory.json) is now the audit source for exhaustive owner-family accounting.

Scope rule:
- rows below track owner families only
- subordinate and internal-only component types stay in the inventory, but they are not backlog-tracked as standalone lanes by default
- official current Learn omits component type `80` `App Module`, so the owner matrix includes that one local-observed supplement explicitly

| Owner family | Component types | Runtime family | Status | Notes |
| --- | --- | --- | --- | --- |
| Table | `1` | `Table` | done | Strongest end-to-end structured authoring lane. |
| Column | `2` | `Column` | done | Strong structured authoring lane with live proof. |
| Relationship | `3` | `Relationship` | done | Strong structured authoring lane with live proof. |
| Option set | `9` | `OptionSet` | done | Supported local, boolean, and shared global choices are evidence-backed; system `state` / `status` stay outside the owner lane. |
| Managed property | `13` | none | best-effort | Explicit owner-metadata boundary: only narrow stable `IsCustomizable` proof is tracked today, not a standalone compiler authoring lane. |
| Key | `14` | `Key` | done | Alternate-key owner lane is fully evidence-backed for the supported subset. |
| Role | `20` | `Role` | done | Supported security-definition owner lane. |
| Display string | `22` | `DisplayString` | best-effort | Source-first reporting or legacy boundary, not rebuildable parity. |
| Form | `24`, `60` | `Form` | done | Main plus supported quick/card form subsets now clear the current proof bar. |
| Organization settings | `25` | none | best-effort | Explicit solution-shell-adjacent boundary rather than a silent omission. |
| View | `26` | `View` | done | Supported authored savedquery lane is evidence-backed; platform-generated system or lookup or quick-find views remain an explicit boundary. |
| Workflow | `29` | `Workflow` | planned | Official owner family already exists in the runtime model, but the repo still lacks an explicit workflow proof or permanent-boundary decision. |
| Report | `31` | `Report` | best-effort | Source-first reporting boundary. |
| Attachment | `35` | `Attachment` | best-effort | Source-first reporting or legacy boundary. |
| Template | `36`, `37`, `38`, `39` | `Template` | best-effort | Source-first reporting or legacy boundary across the current template classes. |
| Duplicate rule | `44` | `DuplicateRule` | done | Supported process-policy owner lane. |
| Entity map | `46` | `EntityMap` | planned | Official owner family exists in the runtime model but still lacks explicit parser or readback or drift or package closure. |
| Ribbon | `55` | `Ribbon` | best-effort | Closed honestly as source-first / unsupported-live unless new neutral evidence overturns that boundary. |
| Visualization | `59` | `Visualization` | partial | Supported saved-query visualizations are proven; richer or user-owned visualization breadth remains active. |
| Web resource | `61` | `WebResource` | done | Source, solution-scoped live readback, and stable-overlap proof are explicit. |
| Site map | `62` | `SiteMap` | done | Structured site-map definition plus explicit canonical raw-`url` boundary are both closed. |
| Connection role | `63` | `ConnectionRole` | done | Supported security-definition owner lane. |
| Complex control | `64` | none | best-effort | Explicit no-row boundary in the neutral corpus. |
| Hierarchy rule | `65` | none | planned | Official owner family is now tracked explicitly instead of being silently omitted. |
| Custom control | `66` | `CustomControl` | best-effort | Standalone custom-control live readback exists, but unmanaged export still omits matching source artifacts in the neutral corpus. |
| Custom control default config | `68` | none | best-effort | Explicit no-row boundary in the neutral corpus. |
| Field security profile | `70` | `FieldSecurityProfile` | done | Supported security-definition owner lane. |
| Field permission | `71` | `FieldPermission` | done | Supported security-definition owner lane. |
| App module | `80` local observed | `AppModule` | best-effort | Local-observed owner family omitted from current Learn choices; source and rebuild are strong, but neutral live role-map parity still underreports `role_ids`. |
| Plugin type | `90` | `PluginType` | done | Supported code and extensibility owner lane. |
| Plugin assembly | `91` | `PluginAssembly` | done | Supported code and extensibility owner lane. |
| Plugin step | `92` | `PluginStep` | done | Supported plug-in registration owner lane. |
| Plugin step image | `93` | `PluginStepImage` | done | Supported plug-in registration owner lane. |
| Service endpoint | `95` | `ServiceEndpoint` | done | Supported integration-endpoint owner lane. |
| Routing rule | `150` | `RoutingRule` | done | Supported process-policy owner lane. |
| SLA | `152` | `Sla` | best-effort | Explicit source-first boundary in the current neutral environment. |
| Convert rule | `154` | none | planned | Official owner family is now tracked explicitly instead of being silently omitted. |
| Mobile offline profile | `161` | `MobileOfflineProfile` | done | Supported process-policy owner lane. |
| Similarity rule | `165` | `SimilarityRule` | best-effort | Explicit source-first boundary in the current neutral environment. |
| Data source mapping | `166` | `DataSourceMapping` | best-effort | Explicit permanent source-first boundary. |
| Import map | `208` | `ImportMap` | best-effort | Explicit permanent source-first boundary. |
| Legacy asset | `210` | `LegacyAsset` | best-effort | Source-first reporting or legacy boundary. |
| Canvas app | `300` | `CanvasApp` | done | Hybrid source-backed live rebuild proof is complete. |
| Connector | `371`, `372` | `Connector` | done | Supported integration-endpoint owner lane across the currently observed connector component types. |
| Environment variable definition | `380` | `EnvironmentVariableDefinition` | done | Supported app-shell and configuration owner lane. |
| Environment variable value | `381` | `EnvironmentVariableValue` | done | Supported app-shell and configuration owner lane. |
| AI project type | `400` | `AiProjectType` | best-effort | Explicit permanent boundary in the current neutral environment. |
| AI project | `401` | `AiProject` | best-effort | Explicit permanent boundary in the current neutral environment. |
| AI configuration | `402` | `AiConfiguration` | best-effort | Explicit permanent boundary in the current neutral environment. |
| Entity analytics configuration | `430` | `EntityAnalyticsConfiguration` | done | Hybrid source-backed live rebuild proof is complete. |
| Image configuration | `431`, `432` | `ImageConfiguration` | done | Supported image-configuration owner lane across attribute-image and entity-image shapes. |

Current explicit owner-family remainder after the exhaustive pass:
- still planned under `B-007`: `Entity map`, `Workflow`, `Hierarchy rule`, and `Convert rule`
- explicit owner-level boundaries, not silent omissions: `Managed property`, `Organization settings`, `Complex control`, and `Custom control default config`
- subordinate and internal-only types still remain fully accounted for in the inventory even when they do not get standalone matrix rows
