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
