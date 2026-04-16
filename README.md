# Dataverse Solution KB

This repository is the persistent knowledge base and implementation workspace for the `.NET 10` Dataverse Solution Compiler.

## Scope

- Follow the approved `.NET 10` roadmap.
- Keep release guidance separate from implementation details.
- Capture what is proven, what is planned, and what still needs evidence.
- Treat Dataverse source, readback, and packaged artifacts as distinct proof surfaces.
- Keep the current `dataverse-metadata-synthesis` skill corpus available as the seed fixture set, not as a runtime dependency.

## What Exists Now

- A buildable `.NET 10` solution at [C:\Git\Dataverse-Solution-KB\DataverseSolutionCompiler.sln](C:\Git\Dataverse-Solution-KB\DataverseSolutionCompiler.sln).
- A typed domain model and compiler contracts in [C:\Git\Dataverse-Solution-KB\src\DataverseSolutionCompiler.Domain](C:\Git\Dataverse-Solution-KB\src\DataverseSolutionCompiler.Domain).
- A source-backed compiler core and capability registry in [C:\Git\Dataverse-Solution-KB\src\DataverseSolutionCompiler.Compiler](C:\Git\Dataverse-Solution-KB\src\DataverseSolutionCompiler.Compiler).
- Typed XML/ZIP readers, a compiler-native JSON intent reader, library-first live Dataverse Web API readback, family-semantic drift comparison, deterministic tracked-source emission, dual-mode package-input emission, and real PAC pack/import/check execution under [C:\Git\Dataverse-Solution-KB\src](C:\Git\Dataverse-Solution-KB\src).
- A reverse-authoring loop for the supported subset: tracked-source JSON can now be read back into the canonical IR and emitted as one editable compiler-native intent-spec JSON document plus a machine-readable omission report through `emit --layout intent-spec`.
- A hybrid intent authoring mode for broader rebuildability: `intent-spec.json` now supports `sourceBackedArtifacts[]`, so XML-heavy or asset-heavy families can rebuild through staged metadata and payload evidence without introducing a second public authoring format.
- A source-first Phase 4 foothold beyond canvas apps: neutral import-map and child data-source-mapping proof through source parsing, tracked-source emission, package-input copying, and an explicit permanent best-effort live/diff boundary in the neutral corpus.
- A second Phase 4 foothold for environment/config breadth: compact entity-analytics proof through typed source parsing, deterministic tracked/package emission, real `entityanalyticsconfigs` live projection, stable-overlap drift on compare-safe fields, and a live export/delete/rebuild/import/re-export proof through the hybrid `sourceBackedArtifacts[]` path.
- A third compact Phase 4 foothold for AI families: source-backed `AI Project Type`, `AI Project`, and `AI Configuration` proof through typed parsing, deterministic tracked/package emission, live readback, and stable-overlap drift, with `AI Configuration` anchored to the official `msdyn_aiconfiguration` Dataverse surface. This lane is now an explicit permanent boundary for `B-010`: the neutral seed can reverse-generate and rebuild at package level, but live create does not clear the same bar honestly because Dataverse rejects `AITemplate` create with `OperationNotSupported` against the real `msdyn_aitemplates` / `msdyn_aimodels` / `msdyn_aiconfigurations` surfaces in the current environment.
- A neutral code/extensibility lane that now includes both plugin registration and adjacent integration endpoints: compact proof for `PluginAssembly`, `PluginType`, `PluginStep`, `PluginStepImage`, `ServiceEndpoint`, and `Connector` through typed source parsing, deterministic tracked/package emission, live readback, stable-overlap drift, and full live export/delete/rebuild/import/re-export proof through the hybrid `sourceBackedArtifacts[]` path. Reverse generation now preserves the sharded plug-in step metadata needed for live recreate by carrying `handlerPluginTypeName` plus `sdkMessageId` forward out of exported `SdkMessageProcessingSteps/*.xml`, while code-first registration ingestion remains a separate follow-up concern.
- A process/service-policy lane that now proves `DuplicateRule`, `DuplicateRuleCondition`, `RoutingRule`, `RoutingRuleItem`, `MobileOfflineProfile`, and `MobileOfflineProfileItem` through typed source parsing, deterministic tracked/package emission, live readback, stable-overlap drift, and a full live export/delete/rebuild/import/re-export proof, with workflow and queue associations kept as explicit best-effort links.
- A security-definition lane that now proves `Role`, `RolePrivilege`, `FieldSecurityProfile`, `FieldPermission`, and `ConnectionRole` through typed source parsing, deterministic tracked/package emission, live readback, stable-overlap drift, and a full live export/delete/rebuild/import/re-export proof for the compact seed, while effective access stays out of scope.
- Source-first service-policy adjunct support for `SimilarityRule`, `Sla`, and `SlaItem`, with real parser, tracked-source, and package-input coverage plus explicit non-blocking live-readback and diff diagnostics rather than overclaimed neutral live parity.
- A dedicated alternate-key proof slice using the existing `ComponentFamily.Key` surface: neutral source, live readback, stable-overlap drift, tracked-source emission, source-backed package copying, and JSON-intent generation now all cover one real compact alternate-key contract end to end.
- A schema-detail proof slice for `ImageConfiguration`: compact entity-image plus attribute-image coverage through typed source parsing, deterministic tracked/package emission, real live metadata readback, and stable-overlap drift, with managed-property proof intentionally scoped to stable `isCustomizable` flags on owning table and column artifacts rather than a new standalone public family.
- A structured model-driven form expansion for the supported subset: JSON intent, reverse generation, and package rebuild now cover `main`, `quick`, and `card` forms plus supported `field`, `quickView`, and `subgrid` controls, while unsupported control shapes or missing-fidelity field references fall back to explicit source-backed handling instead of silent loss.
- A structured authored-chart step for rebuild-safe saved-query visualizations: `tables[].visualizations[]` can now carry chart ids, stable chart summaries, and raw data or presentation XML for the supported subset, and reverse generation prefers that structured surface when the source metadata is complete.
- A broader advanced UI and app-shell proof pass: rebuild-safe saved-query visualizations now reverse-generate structurally from both unpacked source and classic export ZIPs, and supported app-shell detail such as `appSettings` plus `entity` / `url` / `webResource` site map subareas keeps its structured shape while staged web resources remain source-backed.
- That advanced UI / broader app-shell lane now also clears the live export-backed rebuild bar: the compiler can reverse-generate the exported seed, rebuild and publish it, then re-export it back into the same hybrid intent shape with the system-table shell staged source-backed instead of regenerated.
- Wave 3 now has two fully live-proven hybrid lanes: `seed-environment` clears the export/delete/rebuild loop for packaged `CanvasApp` metadata plus `.msapp` assets, and `seed-entity-analytics` clears the same loop through the apply-only hybrid path that creates the solution shell, skips empty package import safely, and reprojects entity analytics back out of `Other/Customizations.xml`.
- Wave 5 now clears the live export-backed rebuild bar for the supported definition families: `seed-process-policy` round-trips duplicate rules, routing rules, and mobile offline profiles through export -> reverse -> delete -> rebuild -> import -> re-export, and `seed-process-security` now does the same for role shell, role privilege, field-security profile and permission, and connection role while keeping effective access out of scope.
- An operational CLI release path for `emit`, `readback`, `diff`, `pack`, `import`, `publish`, `check`, `read`, and `plan` in [C:\Git\Dataverse-Solution-KB\src\DataverseSolutionCompiler.Cli](C:\Git\Dataverse-Solution-KB\src\DataverseSolutionCompiler.Cli).
- A first greenfield compiler milestone that can read JSON intent, project it into the canonical IR, emit tracked-source, synthesize PAC-packable unpacked solution input, and round-trip back through the XML reader for the supported v1 families, now including table-owned alternate keys.
- A supported reverse-generation hardening slice that can take compiler-emitted tracked-source JSON, reconstruct the supported canonical subset, preserve form and view IDs where needed for rebuild fidelity, and round-trip that reconstructed intent back through package-inputs and XML reread without blocking drift for the supported subset.
- A rebuild-fidelity hardening slice that now proves a compact real `export zip -> emit --layout intent-spec -> delete live solution and owned records -> pack -> import/publish` loop for the supported authoring subset, covering a custom table, custom column, main form, rebuild-safe authored savedquery view, entity-only app module and site map, and environment variable definition plus current value.
- A broader hybrid rebuildability pass for credible touched families: process-policy, security-definition, plugin-registration, service endpoints/connectors, canvas app metadata plus packaged assets, web resources, authored charts, compact AI families, and entity analytics can now reverse-generate into `sourceBackedArtifacts[]` and rebuild back into package-inputs without silent omission. The full `B-010` completion bar is now closed for the touched families: Waves 1, 2, 4, 5, and 7 are live-proven, Wave 3 `CanvasApp` plus `EntityAnalyticsConfiguration` are live-proven, and compact AI is now an explicit permanent boundary instead of a hidden partial.
- Representative hybrid seed lanes now also clear direct classic-export reverse-generation plus real PAC pack proof beyond the core structured subset, including compact environment, process-policy, and security-definition exports.
- Hybrid package rebuild now also overlays staged web-resource payload assets case-insensitively, so reverse-generated app-shell intent no longer stops at `.data.xml` metadata files when HTML, SVG, or similar payload files are part of the supported source-backed surface.
- A first post-`B-010` breadth slice for reporting and legacy artifacts: compact source-first proof now covers `Report`, `Template`, `DisplayString`, `Attachment`, and `LegacyAsset` through typed source parsing, deterministic tracked-source summaries, reverse generation into `sourceBackedArtifacts[]`, deterministic package-layout preservation, and explicit non-blocking drift when honest live proof is absent. PAC pack is now also explicitly tested as a boundary: the compact synthetic reporting/legacy seed still fails root-component validation because these legacy artifacts are not defined in `Customizations.xml`, so the lane is closed honestly as source-first rather than overclaimed rebuild parity.
- A second post-`B-010` breadth slice for the app-shell lane: `readback` / `diff` now project solution-scoped `webresourceset` rows into `ComponentFamily.WebResource`, compute stable payload `byteLength` plus SHA-256 `contentHash` evidence from decoded content bytes, and keep missing or undecodable content as explicit warning-only best effort instead of silently dropping the artifact.
- A third post-`B-010` breadth slice for the app-shell lane now closes app-module role-map reverse/package fidelity: reverse generation carries structured `roleIds` in `appModules[]`, tracked-source summaries preserve role-map evidence, derived `AppModule.xml` rebuild writes `AppModuleRoleMaps`, and live role-map parity is now explicitly best effort because neutral `appmodules` readback underreports `role_ids`. That slice established the role-map boundary without reopening the broader app-shell lane.
- A fourth post-`B-010` breadth slice for the app-shell lane now closes compact ribbon shell proof honestly as source-first: `RibbonDiff.xml` parses into typed `Ribbon` artifacts, emits deterministic tracked-source summaries, reverse-generates into `sourceBackedArtifacts[]`, rebuilds back into deterministic `Entities/<entity>/RibbonDiff.xml` package layout, and keeps unsupported live parity explicit rather than silent app-shell debt. That reduced the remaining app-shell follow-up to the later standalone custom-control boundary plus deeper app-shell breadth.
- A fifth post-`B-010` breadth slice for the app-shell lane now closes the standalone custom-control boundary honestly: live readback projects solution-scoped component type `66` rows through `customcontrols`, manifest/clientjson evidence is summarized into stable comparison-safe properties, and stable-overlap emits an explicit source-asymmetric best-effort diagnostic when unmanaged export omits a matching standalone source artifact. `ComplexControl` and `CustomControlDefaultConfig` are now explicit no-row boundaries in the neutral corpus rather than ambiguous open debt, so broader app-shell work now remains active only for deeper app-shell breadth.
- A sixth post-`B-010` breadth slice for the app-shell lane now closes site-map definition live proof for the current seeded navigation shapes: source, reverse-generation, package reread, and live readback now all preserve the same canonical `SiteMapDefinitionJson`, including normalized `$webresource:` subareas, so stable-overlap no longer treats site maps as counts-only summaries. This slice stays intentionally narrow to the current seeded site-map shapes and does not overclaim richer navigation-target parity.
- A seventh post-`B-010` breadth slice for the app-shell lane now closes seeded site-map adjunct fidelity: source, reverse-generation, package rebuild, live readback, and stable-overlap now preserve the current seed’s explicit `Icon` / `VectorIcon` plus `Client` / `PassParams` / `AvailableOffline` subarea detail instead of silently flattening it during round-trip. This remains intentionally narrower than broader richer site-map target parity.
- An eighth post-`B-010` breadth slice for the app-shell lane now closes the canonical site-map dashboard-target subset: source parsing, reverse-generation, package rebuild, live readback, and stable-overlap now preserve GUID-backed dashboard subareas when the XML uses the canonical `/main.aspx?pagetype=dashboard&id=<guid>` deep-link shape instead of flattening them into generic URLs. Broader richer site-map targets, including custom pages and dashboard URLs with extra parameters, remain explicit follow-on app-shell work rather than being overclaimed here.
- A ninth post-`B-010` breadth slice for the app-shell lane now closes the canonical site-map custom-page target subset: source parsing, reverse-generation, package rebuild, live readback, and stable-overlap now preserve `/main.aspx?pagetype=custom&name=<logicalName>` subareas, with optional `appid=<guid>`, instead of flattening them into generic URLs. Richer custom-page URL shapes with extra parameters and broader site-map targets remain explicit follow-on app-shell work rather than being overclaimed here.
- A tenth post-`B-010` breadth slice for the app-shell lane now closes the canonical site-map Dataverse entity URL subsets: source parsing, reverse-generation, package rebuild, live readback, and stable-overlap now preserve app-aware `entitylist` deep links for specific views plus `entityrecord` deep links for specific records, with optional `appid`, optional `viewtype`, and narrow `extraqs=formid=<guid>` handling, instead of flattening them into generic URLs. Dashboard URLs with extra parameters, richer custom-page context parameters, and broader site-map targets remain explicit follow-on app-shell work rather than being overclaimed here.
- An eleventh post-`B-010` breadth slice for the app-shell lane now closes app-aware dashboard URLs plus custom-page record-context URLs: source parsing, reverse-generation, package rebuild, live readback, and stable-overlap now preserve `dashboard` targets with optional `appId`, plus `customPage` targets with optional `customPageEntityName` and `customPageRecordId`, instead of flattening those app-navigation shapes into generic URLs. Broader site-map target shapes and arbitrary parameter-rich URL forms remain explicit follow-on app-shell work rather than being overclaimed here.
- A twelfth post-`B-010` breadth slice for the app-shell lane now closes the canonical raw-`url` boundary for richer unsupported site-map links: source parsing, reverse-generation, package rebuild, live readback, and stable-overlap now preserve broader `main.aspx` target shapes as explicit raw `url` evidence with deterministic parameter ordering, GUID normalization, nested `extraqs` normalization, and boolean normalization instead of leaving the app-shell remainder ambiguous or overpromoting unsupported shapes into structured targets.
- A thirteenth post-`B-010` breadth slice now closes the current non-app-shell live/drift proof bar for local picklist and boolean option sets, quick/card forms, and solution-scoped saved-query visualizations: live readback no longer drift-ignores those seeded shapes, strict solution-scope entity filtering now keeps form, view, and visualization rows attached to the correct table, and saved-query visualizations now read back through component type `59` with normalized chart-definition signatures. System `state` / `status` option sets remain intentionally ignored, and user-owned or richer visualization shapes remain later work.
- A checked-in exhaustive owner-family audit layer now sits beside the touched-family rollout: [solutioncomponent-componenttype-inventory.json](C:\Git\Dataverse-Solution-KB\fixtures\skill-corpus\references\solutioncomponent-componenttype-inventory.json) accounts for the official current `solutioncomponent.componenttype` universe plus the local-observed `80` `App Module` supplement, and the coverage docs now treat every owner family as either covered, planned, or an explicit boundary instead of silently omitting untouched lanes.
- Bootstrap apply and agent orchestration entry points that still remain intentionally partial.
- Unit, golden, integration, and end-to-end tests under [C:\Git\Dataverse-Solution-KB\tests](C:\Git\Dataverse-Solution-KB\tests).
- A copied seed corpus under [C:\Git\Dataverse-Solution-KB\fixtures\skill-corpus](C:\Git\Dataverse-Solution-KB\fixtures\skill-corpus).

## Project Map

- `src/DataverseSolutionCompiler.Domain`: canonical IR, diagnostics, requests/results, and public interfaces.
- `src/DataverseSolutionCompiler.Compiler`: capability registry, bootstrap planner, explanation service, and kernel orchestration.
- `src/DataverseSolutionCompiler.Readers.*`: compiler-native JSON intent, unpacked XML, tracked-source, and live readback entry points.
- `src/DataverseSolutionCompiler.Emitters.*`: tracked-source, intent-spec, and packaging output entry points.
- `src/DataverseSolutionCompiler.Apply`: direct `Dev` apply placeholder.
- `src/DataverseSolutionCompiler.Diff`: stable-overlap drift comparison bootstrap.
- `src/DataverseSolutionCompiler.Packaging.Pac`: PAC wrapper bootstrap.
- `src/DataverseSolutionCompiler.Agent`: compiler-backed orchestration entry point.
- `src/DataverseSolutionCompiler.Cli`: CLI surface with `read`, `plan`, `emit`, `apply-dev`, `readback`, `diff`, `pack`, `import`, `publish`, `check`, `doctor`, and `explain`.

## Reverse Authoring

- `emit --layout tracked-source` produces the deterministic review/archive JSON tree.
- `emit --layout intent-spec` reverse-generates the supported subset into `intent-spec/intent-spec.json`.
- `intent-spec.json` now supports two authoring modes inside one document:
  - structured intent for stable metadata families
  - `sourceBackedArtifacts[]` for rebuildable families that are safest to preserve from staged XML or asset evidence
- Classic exported solution zips are now first-class reverse-authoring input. The compiler normalizes `solution.xml` / `customizations.xml` exports through `pac solution unpack` internally, so manual unpack is no longer required before reverse generation.
- Unsupported families and unsupported shapes are never silently dropped; they are listed in `intent-spec/reverse-generation-report.json`.
- Reverse-generated source-backed families are listed explicitly too, so they do not masquerade as either structured authoring or unsupported omission.
- The supported rebuild-safe subset is now live-proven through export/delete/reimport for authored savedquery views, entity-only app modules or site maps, and environment variables in addition to the earlier table/column/form path.
- The omission report now distinguishes:
  - `unsupportedFamily`
  - `unsupportedShape`
  - `platformGeneratedArtifact`
  - `missingSourceFidelity`
  - `sourceBackedArtifact`
- The current reverse-generation slice is no longer limited to the original structured subset. It now covers the current JSON intent v1 families plus staged source-backed references for broader rebuildable families while keeping truly unsupported or non-authorable surfaces explicit in the report.
- Direct classic-export reverse generation now also covers the advanced UI seed path for structured authored charts plus supported structured app-shell detail, while the matching staged web resources remain explicit `sourceBackedArtifacts[]` instead of being silently flattened.

## Generator Status

Generator readiness is now tracked separately from source/readback breadth.

| Family lane | Intent author | Reverse from tracked-source | Rebuild from intent | Current class |
| --- | --- | --- | --- | --- |
| Schema core: tables, custom columns, lookup-driven relationships | done | done | done | full rebuildable |
| Schema detail subset: global/local choices, alternate keys | done | done | done | full rebuildable |
| Main forms and rebuild-safe authored savedquery views | done | done | done | full rebuildable |
| App modules, entity-only site maps, environment variables | done | done | done | full rebuildable |
| Schema detail extension: image configuration and narrow `isCustomizable` flags | done | done | done | full rebuildable |
| Advanced model-driven UI: quick/card or control-rich forms, authored charts | done | done | partial | authorable but partial |
| Broader app shell: web resources, richer app modules, non-entity site map targets, app settings | partial | done | partial | authorable but partial |
| Canvas app metadata plus packaged assets | partial | done | done | full rebuildable (hybrid source-backed) |
| Entity analytics configuration | partial | done | done | full rebuildable (hybrid source-backed) |
| Compact AI families | partial | done | partial | source-first / permanent best-effort |
| Plugin registration, service endpoints, connectors | partial | done | done | full rebuildable (hybrid source-backed) |
| Process-policy supported families | partial | done | done | full rebuildable (hybrid source-backed) |
| Security-definition supported families | partial | done | done | full rebuildable (hybrid source-backed) |
| Reporting and legacy artifacts | none | done | partial | source-first / permanent best-effort |
| Import maps, similarity rules, SLAs | none | none | none | source-first / permanent best-effort |

Current explicit boundaries:
- `ImportMap`, `DataSourceMapping`, `SimilarityRule`, `Sla`, and `SlaItem` are permanent source-first or best-effort lanes unless new neutral evidence overturns that classification.
- Platform-generated system, lookup, and quick-find views remain intentionally out of rebuildable intent.
- Effective access and runtime privilege expansion remain out of scope even though the owned security-definition lane is now rebuild-proven.
- Reporting/legacy now has compact source-first proof for parsing, tracked-source, reverse-generated `sourceBackedArtifacts[]`, deterministic package preservation, and explicit non-blocking drift, but PAC pack remains an explicit boundary because the current compact seed fails root-component validation for those legacy families.
- App-module role-map generator fidelity is now closed through tracked-source summaries, reverse-generated `roleIds`, and derived `AppModuleRoleMaps`, but live role-map parity remains an explicit best-effort boundary because neutral `appmodules` readback underreports `role_ids`.
- `WebResource` now has solution-scoped live readback plus stable-overlap proof, site maps now preserve canonical structured definition overlap plus the current seeded subarea adjunct detail (`Icon` / `VectorIcon` and explicit `Client` / `PassParams` / `AvailableOffline` behavior) as well as the canonical dashboard, app-aware dashboard, canonical custom-page, custom-page record-context, Dataverse entity URL target subsets, and the broader canonical raw-`url` boundary for unsupported richer app-shell links. Compact ribbon shells now have source-first parse/track/reverse/package proof with explicit unsupported-live handling, and standalone custom controls now have explicit solution-scoped live readback plus source-asymmetric drift handling. The remaining app-shell partials are explicit best-effort boundaries rather than unresolved target-shape debt. `ComplexControl` and `CustomControlDefaultConfig` remain explicit no-row boundaries in the neutral corpus.
- `ManagedProperty` and `Organization` are now also explicit owner-level boundaries in the planning docs rather than silent omissions.
- The active later-`B-007` remainder is now explicit rather than inferred: `EntityMap`, `Workflow`, `HierarchyRule`, and `ConvertRule` are the still-open owner lanes from the exhaustive audit, and broader non-image schema-detail gaps plus user-owned or richer visualization breadth remain active after them.
- Compact AI families are now an explicit permanent boundary inside the touched hybrid rebuildability lanes: package-level hybrid rebuild remains available, but live `publish` fails fast with a clear diagnostic because Dataverse rejects `AITemplate` create with `OperationNotSupported` in the current environment.

Use `reverse-generation-report.json` as the authority for what was intentionally omitted from any one reverse-generated intent document.

## Working Commands

- `dotnet build C:\Git\Dataverse-Solution-KB\DataverseSolutionCompiler.sln`
- `dotnet test C:\Git\Dataverse-Solution-KB\DataverseSolutionCompiler.sln`

## Doc Map

- [docs/roadmap.md](C:\Git\Dataverse-Solution-KB\docs\roadmap.md) - the phase plan.
- [docs/architecture.md](C:\Git\Dataverse-Solution-KB\docs\architecture.md) - compiler shape, IR, and boundaries.
- [docs/backlog/backlog.md](C:\Git\Dataverse-Solution-KB\docs\backlog\backlog.md) - prioritized implementation work.
- [docs/acceptance/ledger.md](C:\Git\Dataverse-Solution-KB\docs\acceptance\ledger.md) - current proven status by surface.
- [docs/threads/current.md](C:\Git\Dataverse-Solution-KB\docs\threads\current.md) - live baton for the next Codex thread.
- [fixtures/skill-corpus/manifest.json](C:\Git\Dataverse-Solution-KB\fixtures\skill-corpus\manifest.json) - copied skill corpus inventory.

## Working Rules

- Prefer short, evidence-based notes over long narrative.
- Separate design intent from artifact proof.
- Record gaps honestly when a family is still unproven.
- Keep each roadmap slice release-safe for .NET 10.
