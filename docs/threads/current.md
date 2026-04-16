# Current Thread

## Thread State

- Purpose: continue the approved `.NET 10` compiler roadmap after completing `B-010`, with `B-007` now resuming as the active breadth item from the explicit rebuildability baseline.
- Scope: source/readback canonicalization, generator breadth, release-path operation, docs, and tests.
- Roadmap boundary: approved `.NET 10` direction.

## What Is Ready

- The repository has a real `.NET 10` solution at `DataverseSolutionCompiler.sln`.
- `dotnet build` passes for the full solution.
- `dotnet test` passes for unit, golden, integration, and end-to-end suites.
- The compiler baseline includes:
  - typed domain contracts and canonical IR
  - capability registry and source-backed planner/kernel
  - compiler-native JSON intent ingestion for the supported v1 greenfield families
  - supported reverse generation from tracked-source JSON back into compiler-native intent-spec JSON through `emit --layout intent-spec`, including deterministic `reverse-generation-report.json` omissions, preserved form/view IDs when rebuild fidelity depends on them, and omission typing that now distinguishes unsupported families, unsupported shapes, platform-generated artifacts, and missing source fidelity
  - hybrid intent authoring through `sourceBackedArtifacts[]`, so one compiler-native intent document can now carry both structured authoring and staged raw metadata or payload references for XML-heavy or asset-heavy families
  - direct classic export ZIP reverse-authoring: `emit --layout intent-spec` now accepts `solution.xml` / `customizations.xml` export zips and normalizes them internally through PAC unpack, so manual unpack is no longer required before reverse generation
  - a compact live export/delete/reimport proof for the supported authoring subset now succeeds for a solution containing a custom table, custom column, main form, rebuild-safe authored savedquery view, entity-only app module plus site map, and environment variable definition plus current value
  - operational CLI command surface for `read`, `plan`, `emit`, `readback`, `diff`, `pack`, `import`, `publish`, `check`, `doctor`, and `explain`
  - typed XML/ZIP readers for the strongest proven source families
  - deterministic tracked-source materialization for the same first-family slice
  - a real tracked-source subset reader for the supported reverse-generation families, so tracked-source output is no longer only a review surface for that subset
  - source-first import-map and child data-source-mapping proof through typed XML parsing, deterministic tracked-source emission, deterministic package-input copying, and an explicit permanent best-effort live/diff boundary in the neutral corpus
  - dedicated alternate-key proof through typed XML parsing, solution-aware live `Keys(...)` readback, stable-overlap drift that ignores operational key-index state, deterministic tracked-source emission, source-backed package-input preservation, and JSON-intent generation for table-owned alternate keys
  - compact image-configuration proof through typed XML parsing, solution-aware live image metadata readback with component-type `431` / `432` discovery plus fallback diagnostics, deterministic tracked/package emission, and stable-overlap drift over entity-image plus attribute-image linkage
  - narrow managed-property proof through stable `isCustomizable` flags on owning table and column artifacts rather than a new standalone public family
  - structured form authoring and reverse-generation for the supported richer subset: `main`, `quick`, and `card` forms plus `field`, `quickView`, and `subgrid` controls now compile, reverse-generate, and rebuild through package-inputs, while unsupported form shapes or missing-fidelity field references fall back to explicit source-backed handling
  - structured authored-chart support for rebuild-safe saved-query visualizations: supported chart metadata now reverse-generates into `tables[].visualizations[]` and rebuilds back into `Entities/<entity>/Visualizations/<id>.xml`, while unsupported chart shapes can still fall back to explicit source-backed handling
  - direct classic export ZIP reverse-generation for the advanced UI seed now preserves both structured authored-chart intent and supported structured app-shell detail such as `appSettings` plus `entity` / `url` / `webResource` site map subareas, while staged web resources remain explicit `sourceBackedArtifacts[]`
  - compact entity-analytics proof through typed XML parsing, deterministic tracked/package emission, real `entityanalyticsconfigs` live readback, stable-overlap drift on compare-safe fields, and a full live export/delete/rebuild/import/re-export proof through the hybrid apply-only path
  - compact AI-family proof through typed XML parsing, deterministic tracked/package emission, live readback, stable-overlap drift, and hybrid package rebuild for `AI Project Type`, `AI Project`, and `AI Configuration`, now closed as an explicit permanent boundary for the current environment: package-level reverse/rebuild remains available, but live `publish` fails fast with a clear compiler diagnostic because Dataverse rejects `AITemplate` create with `OperationNotSupported`
  - neutral plugin-registration proof through typed XML parsing, deterministic tracked/package emission, live readback, and stable-overlap drift for `PluginAssembly`, `PluginType`, `PluginStep`, and `PluginStepImage`
  - neutral integration-endpoint proof through typed XML parsing, deterministic tracked/package emission, live readback, and stable-overlap drift for `ServiceEndpoint` and `Connector`
  - process-policy proof through typed XML parsing, deterministic tracked/package emission, live readback, and stable-overlap drift for `DuplicateRule`, `DuplicateRuleCondition`, `RoutingRule`, `RoutingRuleItem`, `MobileOfflineProfile`, and `MobileOfflineProfileItem`, with workflow and queue links kept as best-effort associations
  - security-definition proof through typed XML parsing, deterministic tracked/package emission, live readback, and stable-overlap drift for `Role`, `RolePrivilege`, `FieldSecurityProfile`, `FieldPermission`, and `ConnectionRole`, with effective access intentionally out of scope
  - source-first service-policy adjunct support for `SimilarityRule`, `Sla`, and `SlaItem`, with parser, tracked-source, and package-input coverage plus explicit unsupported-live or non-blocking diff behavior rather than overclaimed neutral live parity
  - code-first SDK registration ingestion still left as explicit follow-up work rather than a neutral runtime path
  - family-semantic stable-overlap drift for the same first-family slice, exercised against fixture-backed live snapshots and generated round-trip package rereads with non-blocking warning surfaces preserved
  - real library-first Dataverse Web API readback in `Readers.Live` using `DefaultAzureCredential` + raw `HttpClient`, with solution-aware discovery, OData paging, app-setting rejoin, and entity-scoped form/view fallback, now wired into CLI `readback` / `diff`
  - deterministic package-input materialization in two modes:
    - source-first copying for proven source-backed release directories
    - derived XML synthesis for the supported JSON v1 generator families
  - real PAC `pack` / `import` / optional `check` execution with captured diagnostics, plus CLI release-path orchestration for `pack`, `import`, `publish`, and `check`
  - conditional real-`pac` proof for both source-backed and generated package-inputs in the current environment
  - a compact live export/delete/reimport proof for `DemoTableToolTest` that now succeeds through the direct classic export ZIP -> `emit --layout intent-spec` -> pack -> import path without manual unpack
  - a richer live export/delete/reimport proof for `RebuildFidelityWave` that now succeeds for the supported rebuild-safe subset covering a savedquery view, entity-only app shell, and environment variable in addition to the table, column, and main form path
  - a Wave 1 live proof for `seed-image-config` that now succeeds end to end: `publish` performs a targeted post-import image metadata finalize step, live `diff` drops to non-blocking warnings only, and direct classic-export reverse generation now reconstructs `primaryImageAttribute`, `canStoreFullImage`, and `isPrimaryImage` by reading image configuration from `Other/Customizations.xml`
  - broader hybrid reverse/rebuild proof through automated source-backed tests for representative process-policy, security-definition, plugin-registration, service-endpoint, visualization, canvas-app, AI, and entity-analytics seeds
  - direct classic export ZIP reverse-generation plus real PAC pack proof for representative hybrid source-backed seeds in the compact environment, process-policy, and security-definition lanes
  - hybrid app-shell rebuild now correctly overlays staged web-resource payload assets in addition to `.data.xml` metadata files, so reverse-generated web-resource-backed shells rebuild without silent payload loss
  - conditional real-`pac` proof that a reverse-generated advanced UI intent document can rebuild and pack successfully after carrying structured authored-chart metadata plus the supported structured app-shell subset
  - a live Wave 2 export/delete/rebuild/import/re-export proof for `seed-advanced-ui` now succeeds: reverse-generated intent keeps the authored visualization structured, stages the `account` table shell as source-backed `Entities/Account/Entity.xml`, preserves supported app-shell detail (`appSettings`, `webResource` site-map subarea, environment variable), rebuilds and publishes successfully, and re-exports back into the same hybrid intent shape
  - a live Wave 3 export/delete/rebuild/import/re-export proof now succeeds for `seed-environment` and `seed-entity-analytics`: packaged canvas-app assets and entity-analytics metadata both survive direct classic-export reverse generation, rebuild, publish, and post-import re-reverse
  - a live Wave 4 export/delete/rebuild/import/re-export proof now succeeds for both code/extensibility compact seeds: `seed-service-endpoint-connector` survives direct classic-export reverse generation, rebuild, import, and post-import re-export through the real connector (`372`) plus service-endpoint source shape, and `seed-plugin-registration` now survives the same loop after sharded `SdkMessageProcessingSteps/*.xml` reverse generation starts preserving `handlerPluginTypeName` plus `sdkMessageId`, allowing clean recreate of the plug-in assembly, type, step, and step image from a fully deleted live state
  - a live Wave 5 export/delete/rebuild/import/re-export proof now succeeds for `seed-process-policy` and `seed-process-security`: duplicate-rule, routing-rule, mobile-offline, role shell, role privilege, field-security profile or permission, and connection-role artifacts all survive the reverse-generated hybrid rebuild loop, while effective access stays intentionally out of scope
  - a live Wave 7 maximal supported proof now succeeds through `CodexMetadataWave7Maximal`: the compiler can export the compact supported composite solution, reverse-generate it to intent, delete the live solution and owned components, rebuild package-inputs, pack, publish, re-export, reverse-generate again, and keep only the expected platform-generated-view/legacy omissions in `reverse-generation-report.json`
  - the first post-`B-010` `B-007` breadth slice now lands compact reporting/legacy source-first proof: `Report`, `Template`, `DisplayString`, `Attachment`, and `LegacyAsset` now parse from source, emit tracked-source summaries, reverse-generate into `sourceBackedArtifacts[]`, rebuild their deterministic package layout, and stay non-blocking in drift when honest live proof is absent
  - PAC pack is now explicitly boundary-tested for that reporting/legacy slice: the compact synthetic seed still fails root-component validation because those legacy artifacts are not defined in `Customizations.xml`, so the lane is closed honestly as source-first / permanent best-effort rather than overclaimed rebuild parity
  - the next post-`B-010` `B-007` breadth slice now lands app-shell `WebResource` live proof: solution component type `61` scopes `webresourceset` readback, live projection computes stable `byteLength` plus `contentHash` evidence from payload bytes, and missing or undecodable content stays explicit warning-only best effort instead of disappearing
  - fixture-backed overlap now proves that `seed-app-shell` and `seed-advanced-ui` keep `WebResource` source and live artifacts compare-cleanly on the stable overlap without reopening the broader app-shell boundary
  - the next post-`B-010` `B-007` breadth slice now closes app-module role-map reverse/package fidelity: reverse generation carries structured `roleIds` in `appModules[]`, tracked-source summaries keep role-map evidence explicit, and derived `AppModule.xml` rebuild now writes `AppModuleRoleMaps` instead of silently dropping role access
  - live app-module role-map parity is now an explicit best-effort boundary rather than ambiguous debt: stable-overlap emits an informational diagnostic when neutral `appmodules` readback underreports `role_ids`, but generator fidelity is now closed honestly
  - the next post-`B-010` `B-007` breadth slice now closes compact ribbon source-first proof: `RibbonDiff.xml` now parses into typed `Ribbon` artifacts, emits deterministic tracked-source summaries, reverse-generates into explicit `sourceBackedArtifacts[]`, and rebuilds back into `Entities/<entity>/RibbonDiff.xml` without silent package loss
  - ribbon live parity is now an explicit unsupported-live / non-blocking drift boundary rather than ambiguous app-shell debt: `readback` reports the family as unsupported, and stable-overlap keeps the source-first ribbon shell non-blocking until new neutral evidence overturns that boundary
  - the next post-`B-010` `B-007` breadth slice now closes the standalone custom-control boundary honestly: live readback projects solution-scoped component type `66` rows through `customcontrols`, manifest/clientjson evidence is summarized into stable comparison-safe properties, and stable-overlap emits an informational source-asymmetry diagnostic when unmanaged export omits a matching standalone source artifact
  - `ComplexControl` and `CustomControlDefaultConfig` are now explicit no-row boundaries in the neutral corpus rather than ambiguous app-shell debt
  - the next post-`B-010` `B-007` breadth slice now closes site-map definition live proof for the current seeded navigation shapes: source, reverse-generation, and package rebuild were already carrying structured site-map definitions, and live readback plus stable-overlap now preserve the same canonical `SiteMapDefinitionJson` instead of counts-only summaries
  - the next post-`B-010` `B-007` breadth slice now closes seeded site-map adjunct fidelity: source, reverse-generation, package rebuild, live readback, and stable-overlap now preserve explicit `Icon` / `VectorIcon` plus `Client` / `PassParams` / `AvailableOffline` subarea detail already present in `seed-app-shell` and `seed-advanced-ui`, instead of silently flattening it away during round-trip
  - the next post-`B-010` `B-007` breadth slice now closes the canonical site-map dashboard-target subset: source parsing, reverse-generation, package rebuild, live readback, and stable-overlap now preserve GUID-backed dashboard subareas when the XML uses the canonical `/main.aspx?pagetype=dashboard&id=<guid>` deep-link shape instead of flattening them into generic URLs
  - the next post-`B-010` `B-007` breadth slice now closes the canonical site-map custom-page target subset: source parsing, reverse-generation, package rebuild, live readback, and stable-overlap now preserve `/main.aspx?pagetype=custom&name=<logicalName>` subareas, with optional `appid=<guid>`, instead of flattening them into generic URLs
  - the next post-`B-010` `B-007` breadth slice now closes the canonical site-map Dataverse entity URL subsets: source parsing, reverse-generation, package rebuild, live readback, and stable-overlap now preserve app-aware `entitylist` deep links for specific views plus `entityrecord` deep links for specific records, with optional `appid`, optional `viewtype`, and narrow `extraqs=formid=<guid>` handling, instead of flattening them into generic URLs
  - the next post-`B-010` `B-007` breadth slice now closes app-aware dashboard URLs plus custom-page record-context URLs: source parsing, reverse-generation, package rebuild, live readback, and stable-overlap now preserve `dashboard` targets with optional `appId`, plus `customPage` targets with optional `customPageEntityName` and `customPageRecordId`, instead of flattening those app-navigation shapes into generic URLs
  - the next post-`B-010` `B-007` breadth slice now closes the canonical raw-`url` boundary for richer unsupported app-shell links: broader `main.aspx` site-map target shapes now stay explicit raw `url` evidence, and source parsing, reverse-generation, package rebuild, live readback, and stable-overlap all canonicalize parameter order, GUID forms, nested `extraqs`, and boolean literals instead of leaving that remainder ambiguous
  - the next post-`B-010` `B-007` breadth slice now closes the current non-app-shell live/drift gaps: local picklist and boolean option sets now compare through fixture-backed live readback while system `state` / `status` remain intentionally ignored, quick/card forms now compare cleanly after strict solution-scope entity filtering of `systemforms`, and solution-scoped saved-query visualizations now read back through component type `59` with normalized chart-definition signatures
  - the exhaustive owner-family universe pass now adds a checked-in audit inventory for the official current `solutioncomponent.componenttype` list plus the local-observed `80` `App Module` supplement, and the coverage docs now account for every owner family explicitly instead of only previously touched lanes
  - bootstrap apply and agent orchestration adapters
- The copied `dataverse-metadata-synthesis` corpus lives under `fixtures/skill-corpus`, and dedicated generator fixtures now live under `fixtures/intent-specs`.

## What Still Needs Attention

- `B-010` is complete.
- Completion outcome:
  - every currently touched family now lands in one explicit end state
  - Wave 7 maximal supported proof is green
  - compact AI is now a documented permanent boundary rather than a hidden partial
- Active execution focus:
  - resume `B-007` breadth-first work from the completed `B-010` baseline
  - keep reporting/legacy closed as an explicit source-first boundary, not as hidden future debt
  - keep the former site-map target-shape remainder closed as an explicit canonical raw-`url` boundary: broader app-shell links now preserve raw `url` evidence instead of pretending structured parity
  - keep the new owner-family audit result explicit: `EntityMap`, `Workflow`, `HierarchyRule`, and `ConvertRule` are now the still-open owner lanes that need either honest proof or an explicit permanent boundary
  - continue `B-007` beyond the current local option-set, quick/card form, and solution-scoped saved-query visualization proof, especially the four owner lanes above, the remaining non-image schema-detail gaps, and richer user-owned or otherwise unsupported view/query/visualization breadth
  - keep future authoring expansion on the same export-backed rebuild bar already proven for the supported subset
- Permanent-boundary targets unless new evidence overturns them:
  - `ManagedProperty` as a standalone family beyond the current narrow owner-metadata `IsCustomizable` boundary
  - `Organization` as a standalone compiler family beyond the current solution-shell-adjacent boundary
  - `ImportMap`
  - `DataSourceMapping`
  - compact AI families in the current environment because live Dataverse create rejects `AITemplate` with `OperationNotSupported`
  - `SimilarityRule`
  - `Sla`
  - `SlaItem`
  - `ComplexControl`
  - `CustomControlDefaultConfig`
  - platform-generated system/default/lookup/quick-find views
  - effective access/runtime privilege expansion
  - reporting/legacy live rebuild parity beyond the current compact source-first slice, because the synthetic seed still fails PAC root-component validation

## Handoff Rule

- Future updates should preserve the distinction between planning, evidence, and acceptance.
- Do not mix schema-proof, packaging-proof, generator-proof, and project-specific examples in the same note unless the thread explicitly calls for it.
- When implementing new capability slices, update `docs/acceptance/ledger.md` and this file in the same change.
