# Roadmap

This roadmap follows a compiler and release-pipeline-first strategy for Dataverse solution work on `.NET 10`.

## Phase 1: Canonical Compiler Spine

Goal:
- define the canonical intermediate model
- establish source, readback, compare, and release boundaries

Exit criteria:
- solution, entity, form, view, app shell, extensibility, and configuration families all map into the same model
- the roadmap and acceptance docs can explain what is proven versus partial

## Phase 2: Proven Families

Goal:
- deepen the families already supported by the neutral corpus and current skill coverage

Priority order:
- schema core
- schema detail
- forms and views
- app shell and configuration
- code and extensibility
- process and service policy

Exit criteria:
- each of the above has a clean source/readback path and a family-aware compare story

## Phase 3: Release Pipeline

Goal:
- make the compiler usable in a governed release flow

Pipeline:
1. normalize source
2. capture live readback when available
3. compare source versus readback
4. emit tracked source
5. package release artifacts
6. publish only after validation passes

Exit criteria:
- release output is deterministic and evidence-backed
- direct Dev proof is optional for synthesis, not required for shipping

## Phase 4: Environment And Configuration

Goal:
- add the next honest environment/config slice without overstating parity

Priority order:
- canvas app
- import map
- data source mapping
- entity analytics

Exit criteria:
- each family has a documented source shape, a compare-safe field set, and a clear boundary for what remains best-effort

## Phase 5: Completion Program For All Remaining Incomplete Dataverse Families

This phase is now complete. While it was active, narrower execution slices were allowed to refine sequencing, but not to replace the program in the roadmap, backlog, current-thread baton, acceptance ledger, or coverage docs.

Goal:
- finish every still-incomplete touched family into one explicit end state:
  - `full rebuildable (structured)`
  - `full rebuildable (hybrid source-backed)`
  - `source-first / permanent best-effort`
- keep one compiler-native `intent-spec.json` document and retain `sourceBackedArtifacts[]` as the hybrid completion surface for XML-heavy or asset-heavy families

Program-wide completion rule:
- a family is promoted only when it clears:
  - source read
  - tracked-source emit
  - reverse-generation to `intent-spec.json`
  - package-input rebuild
  - PAC pack
  - live `export -> reverse -> delete -> rebuild -> import/publish -> verify`
- do not leave any currently touched family in an ambiguous middle state after this program
- keep platform-generated system views, effective-access/runtime privilege expansion, and import-map or SLA or similarity-rule parity out of the rebuildability target unless new evidence overturns that boundary

Wave order:
1. Close schema-detail and form gaps with structured authoring.
   - Promote image authoring, reverse `ImageConfiguration` into the owning table authoring surface, expand structured `main` / `quick` / `card` forms with supported `field` / `subgrid` / `quickView` controls, and promote supported authored-chart support.
   - Completion target: schema-detail extension and advanced model-driven UI become `full rebuildable (structured)` for the supported subset.
2. Finish the broader app-shell lane.
   - Keep stable structured authoring for supported site-map subareas and `appModules[].appSettings[]`.
   - Keep `WebResource`, ribbon payloads, and custom-control manifests/assets on the hybrid source-backed path unless a clearly safe structured shape is proven.
   - Completion target: broader app shell becomes fully rebuildable, using structured intent where stable and hybrid source-backed intent for payload-heavy UI assets.
3. Promote canvas, entity analytics, and AI to `full rebuildable (hybrid source-backed)`.
4. Promote code and extensibility to `full rebuildable (hybrid source-backed)`.
   - Keep payload-heavy pieces such as plugin binaries and connector or endpoint adjunct files source-backed.
   - Keep code-first SDK registration ingestion out of scope.
5. Promote process and security definitions to `full rebuildable (hybrid source-backed)`.
   - Keep these as definition-only families, never runtime/effective-access families.
   - Allow `RolePrivilege` to remain an explicit carve-out if export-backed live parity is not stable enough.
6. Permanently close the families that should not be forced into full generation.
   - `ImportMap`
   - `DataSourceMapping`
   - `SimilarityRule`
   - `Sla`
   - `SlaItem`
   - platform-generated system/default/lookup/quick-find views
   - effective access/runtime privilege expansion
   - reporting/legacy as separate future work rather than hidden debt inside this program
7. Run a program-end maximal proof.
   - Build one compact maximal supported solution containing one representative artifact from every family promoted by Waves 1-5.
   - Run classic export, direct reverse generation, delete, rebuild, pack, import/publish, and verify.

Completion outcome:
- Wave 1 image proof, Wave 2 advanced UI / broader app-shell proof, Wave 3 `CanvasApp` plus `EntityAnalyticsConfiguration`, Wave 4 code/extensibility, Wave 5 process-policy plus security-definition, and Wave 7 maximal supported proof now all clear the required live bar.
- Wave 6 permanent-boundary closure is complete: import-map, SLA, similarity-rule, platform-generated-view, effective-access, and reporting/legacy surfaces now have explicit non-silent boundaries instead of hidden rebuildability debt.
- Compact AI families are now also closed as an explicit permanent boundary for the current environment: package-level reverse/rebuild remains available, but live Dataverse create rejects `AITemplate` with `OperationNotSupported`, so that lane is not treated as rebuildable.

Next priority:
- resume `B-007` breadth-first work from this completed baseline
- the first post-`B-010` breadth slice now lands compact reporting/legacy source-first proof: typed source parsing, deterministic tracked-source summaries, reverse generation into `sourceBackedArtifacts[]`, deterministic package preservation, and an explicit PAC-pack boundary rather than silent rebuildability debt
- keep future authoring expansion on the same proof bar: `export zip -> intent-spec -> package-inputs -> pack -> import/publish` plus honest omission typing

Exit criteria:
- every currently touched family lands in one explicit end state:
  - `full rebuildable (structured)`
  - `full rebuildable (hybrid source-backed)`
  - `source-first / permanent best-effort`
- the program-end maximal proof succeeds for one compact supported solution
- coverage docs no longer imply hidden rebuildability debt for the permanently bounded families

Those exit criteria are now met.
