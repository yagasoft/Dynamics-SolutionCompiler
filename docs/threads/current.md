# Current Thread

## Thread State

- Purpose: continue the approved `.NET 10` compiler roadmap with rebuild-fidelity hardening as the first next lane before more breadth work.
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
  - direct classic export ZIP reverse-authoring: `emit --layout intent-spec` now accepts `solution.xml` / `customizations.xml` export zips and normalizes them internally through PAC unpack, so manual unpack is no longer required before reverse generation
  - operational CLI command surface for `read`, `plan`, `emit`, `readback`, `diff`, `pack`, `import`, `publish`, `check`, `doctor`, and `explain`
  - typed XML/ZIP readers for the strongest proven source families
  - deterministic tracked-source materialization for the same first-family slice
  - a real tracked-source subset reader for the supported reverse-generation families, so tracked-source output is no longer only a review surface for that subset
  - source-first import-map and child data-source-mapping proof through typed XML parsing, deterministic tracked-source emission, deterministic package-input copying, and an explicit permanent best-effort live/diff boundary in the neutral corpus
  - dedicated alternate-key proof through typed XML parsing, solution-aware live `Keys(...)` readback, stable-overlap drift that ignores operational key-index state, deterministic tracked-source emission, source-backed package-input preservation, and JSON-intent generation for table-owned alternate keys
  - compact image-configuration proof through typed XML parsing, solution-aware live image metadata readback with component-type `431` / `432` discovery plus fallback diagnostics, deterministic tracked/package emission, and stable-overlap drift over entity-image plus attribute-image linkage
  - narrow managed-property proof through stable `isCustomizable` flags on owning table and column artifacts rather than a new standalone public family
  - compact entity-analytics proof through typed XML parsing, deterministic tracked/package emission, real `entityanalyticsconfigs` live readback, and stable-overlap drift on compare-safe fields
  - compact AI-family proof through typed XML parsing, deterministic tracked/package emission, live readback, and stable-overlap drift for `AI Project Type`, `AI Project`, and `AI Configuration`, with `AI Configuration` anchored to the official `msdyn_aiconfiguration` surface and the other two families currently using the neutral seed as the live-shape authority
  - neutral plugin-registration proof through typed XML parsing, deterministic tracked/package emission, live readback, and stable-overlap drift for `PluginAssembly`, `PluginType`, `PluginStep`, and `PluginStepImage`
  - neutral integration-endpoint proof through typed XML parsing, deterministic tracked/package emission, live readback, and stable-overlap drift for `ServiceEndpoint` and `Connector`
  - process-policy proof through typed XML parsing, deterministic tracked/package emission, live readback, and stable-overlap drift for `DuplicateRule`, `DuplicateRuleCondition`, `RoutingRule`, `RoutingRuleItem`, `MobileOfflineProfile`, and `MobileOfflineProfileItem`, with workflow and queue links kept as best-effort associations
  - security-definition proof through typed XML parsing, deterministic tracked/package emission, live readback, and stable-overlap drift for `Role`, `FieldSecurityProfile`, `FieldPermission`, and `ConnectionRole`, with `RolePrivilege` kept definition-adjacent best effort and effective access intentionally out of scope
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
  - bootstrap apply and agent orchestration adapters
- The copied `dataverse-metadata-synthesis` corpus lives under `fixtures/skill-corpus`, and dedicated generator fixtures now live under `fixtures/intent-specs`.

## What Still Needs Attention

- Keep rebuild-fidelity hardening ahead of new breadth until the already-authorable subset is deeply trustworthy from real exports, especially for rebuild-safe authored views, app modules, entity-only site maps, and environment variables.
- Extend reverse-generation beyond the current supported subset only when later proven families can meet the same omission-typed, package-reread, and stable-overlap proof bar without silent loss.
- Broaden the JSON intent / generator surface beyond the current v1 families only after the supported subset stays clean through:
  - intent read
  - tracked-source reverse-generation
  - tracked/package emit
  - XML reread
  - stable-overlap compare
  - PAC pack
- Deepen typed reader coverage into later partial families such as ribbon, web resources beyond metadata-only handling, richer process-policy/security breadth beyond the current definition slices, and any remaining code-extensibility artifacts beyond the neutral plugin-registration plus integration-endpoint slices.
- Continue schema-detail breadth beyond the alternate-key and image-config slices, especially any broader managed-property surface that can be captured honestly without overclaiming a standalone family.
- Extend Phase 4 beyond the current canvas-app, explicit source-first import-map boundary, entity-analytics, and compact AI footholds by choosing the next honest environment/config family that can clear the same seed/readback/drift bar without overclaiming parity.
- Deepen drift coverage beyond the current first-family slice and reduce the remaining non-blocking warning surfaces around quick/card forms, under-reported local option sets, visualizations, and live-only platform relationship noise.
- Extend live readback proof into later partial families such as visualizations, web resources, richer app-role detail, deeper process/security surfaces, and later environment/config families beyond the explicit permanent best-effort import-map boundary.
- Broaden package-input coverage beyond the current proven release-path and generated-v1 directories so later families can ship through the same governed path.
- Keep PAC auth/profile management and advanced import flags intentionally narrow unless a later slice proves they are needed.
- Keep the skill corpus fixture manifest and generator fixtures in sync as the source skill and compiler surface evolve.

## Handoff Rule

- Future updates should preserve the distinction between planning, evidence, and acceptance.
- Do not mix schema-proof, packaging-proof, generator-proof, and project-specific examples in the same note unless the thread explicitly calls for it.
- When implementing new capability slices, update `docs/acceptance/ledger.md` and this file in the same change.
