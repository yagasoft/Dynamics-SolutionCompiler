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
- Bootstrap apply and agent orchestration entry points that still remain intentionally partial.
- Unit, golden, integration, and end-to-end tests under [C:\Git\Dataverse-Solution-KB\tests](C:\Git\Dataverse-Solution-KB\tests).
- A copied seed corpus under [C:\Git\Dataverse-Solution-KB\fixtures\skill-corpus](C:\Git\Dataverse-Solution-KB\fixtures\skill-corpus).

## Project Map

- `src/DataverseSolutionCompiler.Domain`: canonical IR, diagnostics, requests/results, and public interfaces.
- `src/DataverseSolutionCompiler.Compiler`: capability registry, bootstrap planner, explanation service, and kernel orchestration.
- `src/DataverseSolutionCompiler.Readers.*`: compiler-native JSON intent, unpacked XML, tracked-source, and live readback entry points.
- `src/DataverseSolutionCompiler.Emitters.*`: tracked-source and packaging output entry points.
- `src/DataverseSolutionCompiler.Apply`: direct `Dev` apply placeholder.
- `src/DataverseSolutionCompiler.Diff`: stable-overlap drift comparison bootstrap.
- `src/DataverseSolutionCompiler.Packaging.Pac`: PAC wrapper bootstrap.
- `src/DataverseSolutionCompiler.Agent`: compiler-backed orchestration entry point.
- `src/DataverseSolutionCompiler.Cli`: CLI surface with `read`, `plan`, `emit`, `apply-dev`, `readback`, `diff`, `pack`, `import`, `publish`, `check`, `doctor`, and `explain`.

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
