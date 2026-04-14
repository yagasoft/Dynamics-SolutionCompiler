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
- A source-first Phase 4 foothold beyond canvas apps: neutral import-map and child data-source-mapping proof through source parsing, tracked-source emission, package-input copying, and an explicit permanent best-effort live/diff boundary in the neutral corpus.
- A second Phase 4 foothold for environment/config breadth: compact entity-analytics proof through typed source parsing, deterministic tracked/package emission, real `entityanalyticsconfigs` live projection, and stable-overlap drift on compare-safe fields.
- A third compact Phase 4 foothold for AI families: source-backed `AI Project Type`, `AI Project`, and `AI Configuration` proof through typed parsing, deterministic tracked/package emission, live readback, and stable-overlap drift, with `AI Configuration` anchored to the official `msdyn_aiconfiguration` Dataverse surface.
- A neutral code/extensibility lane that now includes both plugin registration and adjacent integration endpoints: compact proof for `PluginAssembly`, `PluginType`, `PluginStep`, `PluginStepImage`, `ServiceEndpoint`, and `Connector` through typed source parsing, deterministic tracked/package emission, live readback, and stable-overlap drift, while code-first registration ingestion remains a separate follow-up concern.
- A process/service-policy lane that now proves `DuplicateRule`, `DuplicateRuleCondition`, `RoutingRule`, `RoutingRuleItem`, `MobileOfflineProfile`, and `MobileOfflineProfileItem` through typed source parsing, deterministic tracked/package emission, live readback, and stable-overlap drift, with workflow and queue associations kept as explicit best-effort links.
- A security-definition lane that now proves `Role`, `FieldSecurityProfile`, `FieldPermission`, and `ConnectionRole` through typed source parsing, deterministic tracked/package emission, live readback, and stable-overlap drift, while `RolePrivilege` remains definition-adjacent best effort and effective access stays out of scope.
- Source-first service-policy adjunct support for `SimilarityRule`, `Sla`, and `SlaItem`, with real parser, tracked-source, and package-input coverage plus explicit non-blocking live-readback and diff diagnostics rather than overclaimed neutral live parity.
- A dedicated alternate-key proof slice using the existing `ComponentFamily.Key` surface: neutral source, live readback, stable-overlap drift, tracked-source emission, source-backed package copying, and JSON-intent generation now all cover one real compact alternate-key contract end to end.
- A schema-detail proof slice for `ImageConfiguration`: compact entity-image plus attribute-image coverage through typed source parsing, deterministic tracked/package emission, real live metadata readback, and stable-overlap drift, with managed-property proof intentionally scoped to stable `isCustomizable` flags on owning table and column artifacts rather than a new standalone public family.
- An operational CLI release path for `emit`, `readback`, `diff`, `pack`, `import`, `publish`, `check`, `read`, and `plan` in [C:\Git\Dataverse-Solution-KB\src\DataverseSolutionCompiler.Cli](C:\Git\Dataverse-Solution-KB\src\DataverseSolutionCompiler.Cli).
- A first greenfield compiler milestone that can read JSON intent, project it into the canonical IR, emit tracked-source, synthesize PAC-packable unpacked solution input, and round-trip back through the XML reader for the supported v1 families, now including table-owned alternate keys.
- A supported reverse-generation hardening slice that can take compiler-emitted tracked-source JSON, reconstruct the supported canonical subset, preserve form and view IDs where needed for rebuild fidelity, and round-trip that reconstructed intent back through package-inputs and XML reread without blocking drift for the supported subset.
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
- Classic exported solution zips are now first-class reverse-authoring input. The compiler normalizes `solution.xml` / `customizations.xml` exports through `pac solution unpack` internally, so manual unpack is no longer required before reverse generation.
- Unsupported families and unsupported shapes are never silently dropped; they are listed in `intent-spec/reverse-generation-report.json`.
- The omission report now distinguishes:
  - `unsupportedFamily`
  - `unsupportedShape`
  - `platformGeneratedArtifact`
  - `missingSourceFidelity`
- The current reverse-generation slice is intentionally subset-only. It covers the current JSON intent v1 families and keeps later families out of the authoring surface until they have the same proof bar.

## Generator Status

Generator readiness is now tracked separately from source/readback breadth.

| Family lane | Intent author | Reverse from tracked-source | Rebuild from intent | Current class |
| --- | --- | --- | --- | --- |
| Schema core: tables, custom columns, lookup-driven relationships | done | done | done | full rebuildable |
| Schema detail subset: global/local choices, alternate keys | done | done | done | full rebuildable |
| Main forms and rebuild-safe authored savedquery views | partial | partial | partial | authorable but partial |
| App modules, entity-only site maps, environment variables | partial | partial | partial | authorable but partial |
| Image configuration, entity analytics, AI, plugin registration, service endpoints, connectors, process/security families | none | none | none | source/readback only |
| Import maps, similarity rules, SLAs | none | none | none | source-first / permanent best-effort |

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
