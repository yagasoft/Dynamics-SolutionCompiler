# Roadmap

This roadmap follows a compiler-first, release-pipeline-first strategy for Dataverse solution work on `.NET 10`.

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

This phase is now complete.

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
- keep platform-generated system views, effective-access/runtime privilege expansion, and import-map/SLA/similarity-rule parity out of the rebuildability target unless new evidence overturns that boundary

Completion outcome:
- Waves 1, 2, 4, 5, and 7 now clear the live export-backed rebuild bar
- Wave 3 `CanvasApp` plus `EntityAnalyticsConfiguration` also clear it
- Wave 6 permanent-boundary closure is complete for import-map, SLA, similarity-rule, platform-generated-view, effective-access, and reporting/legacy surfaces
- compact AI is now an explicit permanent boundary for the current environment because Dataverse rejects `AITemplate` create with `OperationNotSupported`

## Phase 6: Operational Dev Apply Workflow

This phase is now complete.

Goal:
- turn `apply-dev` into a real supported Dev-proof workflow instead of a thin `compile -> apply` helper
- move that staged flow into a reusable agent or domain orchestration layer so verification is library-first, not CLI-only
- keep the live-mutation scope frozen to the families the current `WebApiApplyExecutor` already supports

Exit criteria:
- `apply-dev` runs one fixed staged flow: `compile -> apply -> readback -> diff`
- `apply-dev` reports stage outcomes plus aggregated diagnostics
- `apply-dev` exits non-zero on apply failure, readback failure, or blocking drift after verification
- `apply-dev` succeeds explicitly on supported-scope no-op instead of treating that case as an error
- broader agent autonomy, publish unification, and new live-mutable families remain future work rather than being implied by this phase

Completion outcome:
- `apply-dev` now requires `--environment` and performs mandatory verification against the same environment and solution scope through the reusable `AgentOrchestrator` workflow runner
- the workflow runner now has domain request/result contracts, per-stage outcomes, aggregated diagnostics, and supported-scope verification filtering
- v1 live mutation remains intentionally frozen to `ImageConfiguration`, `EntityAnalyticsConfiguration`, `PluginAssembly`, `PluginType`, `PluginStep`, `PluginStepImage`, `ServiceEndpoint`, `Connector`, `MobileOfflineProfile`, `MobileOfflineProfileItem`, and `ConnectionRole`

## Phase 7: Release Workflow Hardening

This phase is now complete.

Goal:
- harden the release-side workflow around the new orchestration layer instead of leaving `pack`, `check`, and `publish` as CLI-only wiring
- make stage outcomes plus aggregated diagnostics first-class for release operations the same way `apply-dev` now does for Dev proof
- preserve current `publish` semantics exactly, especially the explicit apply-only empty-package branch

Exit criteria:
- `pack` and `check` route through a reusable `compile -> emit package-inputs -> pack` workflow
- `publish` routes through a reusable `compile -> emit package-inputs -> pack -> import? -> finalize apply` workflow
- the empty-package branch remains explicit through an import-skipped state instead of hidden CLI branching
- `publish` is not widened into a broader verification command in this phase

Completion outcome:
- the domain workflow layer now carries package-build and publish request/result contracts in addition to Dev apply
- `AgentOrchestrator` now owns `RunDevApply`, `RunPackageBuild`, and `RunPublish`, while `Analyze(...)` remains intact
- `CompilerCliRuntime` now resolves package-build and publish workflow runners the same way it already resolved Dev apply
- the live finalize-apply scope remains intentionally frozen to the current `WebApiApplyExecutor` family set

## Phase 8: Public Repository Onboarding

This phase is now complete.

Goal:
- make the top-level repository entrypoint usable for GitHub visitors without relying on workspace-specific paths or thread-handoff context
- keep the public README aligned with the audited backlog, coverage, and workflow state

Exit criteria:
- `README.md` is a concise public-facing entrypoint with relative links, current status, quick-start workflows, repo map, and explicit proof/boundary summary
- planning and acceptance docs record the same completed state
- regression tests guard the public README against workspace-specific absolute paths

Completion outcome:
- `README.md` now uses relative links, public-facing workflow examples, a concise repo map, and explicit current boundary language
- the planning and acceptance docs now record the documentation slice as complete instead of leaving it as untracked polish
- a unit test now protects the public README from drifting back to workspace-specific absolute paths

## Next Priority

- no active backlog items remain after Phases 5 through 8
- future work should reopen only when new evidence overturns one of the current explicit boundaries or a new operational program is approved
- keep future authoring expansion on the same proof bar: `export zip -> intent-spec -> package-inputs -> pack -> import/publish`, plus honest omission typing and explicit boundary classification
