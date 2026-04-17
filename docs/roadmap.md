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

## Phase 9: Code-First Plugin Registration And Deployment

This phase is now complete.

Goal:
- add a narrow code-first input path for the known DBM-style raw C# plug-in registration pattern
- stage deployable classic `.dll` or plug-in package `.nupkg` assets outside the source tree
- wire that path into the existing `apply-dev` and `publish` workflows without widening the supported family set or pretending solution-zip parity for plug-in packages

Exit criteria:
- the compiler can detect and read the supported `CodeFirstSdkRegistration` source kind after stronger source kinds lose
- tracked-source and reverse-generated intent preserve the code-first registration metadata and staged source evidence
- `apply-dev` builds staged code assets before deploy, and `publish` keeps code-first plug-in deployment in finalize apply
- classic and package proof seeds cover the supported parser, build, workflow, readback, and drift path
- broader arbitrary C# registration patterns remain explicit unsupported-shape diagnostics instead of silent inference

Completion outcome:
- the compiler now reads the supported DBM-style registration shape directly from raw C# projects into the existing `PluginAssembly`, `PluginType`, `PluginStep`, and `PluginStepImage` families
- the new code-asset builder stages signed `.dll` or `.nupkg` outputs under a compiler-managed staging root outside the source tree
- tracked-source, reverse-generated `sourceBackedArtifacts[]`, `apply-dev`, and `publish` now all preserve the same code-first plug-in evidence without adding a new component family
- the neutral `seed-code-plugin-classic` and `seed-code-plugin-package` fixtures now prove the supported path, while plug-in package deployment remains explicit live finalize apply rather than a claim that `.nupkg` payloads live inside `solution.zip`

## Phase 10: Wider Code-First Registration And Custom Workflow Activity Phase A

This phase is now complete.

Goal:
- widen the supported code-first plug-in registration parser beyond the first object-initializer-only DBM shape
- add custom workflow activity support under the existing `PluginAssembly` and `PluginType` families without reopening the owner-level `Workflow` family
- keep workflow-activity deployment honest by supporting only the classic assembly lane

Exit criteria:
- the compiler reads the seeded imperative DBM helper registration shape into the existing plug-in families
- tracked-source and reverse-generated `sourceBackedArtifacts[]` preserve that wider code-first registration evidence
- custom workflow activities read, reverse, drift, and read back as a `PluginType` subtype instead of reopening `Workflow`
- classic custom workflow activity deployment works, and plug-in package deployment stops with an explicit diagnostic

Completion outcome:
- the code-first reader now supports the seeded imperative DBM helper shape in addition to the first supported object-initializer shape
- `PluginType` artifacts now preserve `pluginTypeKind`, including `customWorkflowActivity`, while `workflowActivityGroupName` remains part of the stable overlap
- the new `seed-code-plugin-imperative`, `seed-code-workflow-activity-classic`, and `seed-code-workflow-activity-package` fixtures now cover the widened parser, classic workflow-activity path, and explicit package boundary
- custom workflow activities now flow through tracked-source, reverse generation, live readback, drift, `apply-dev`, and `publish` under the existing plug-in families, while the owner-level `Workflow` lane stays closed as a separate explicit boundary
- plug-in package deployment for workflow activities is now an explicit classic-only boundary with a clear compiler diagnostic rather than a silent fallback

## Phase 11: Helper-Based Code-First Registration And Custom Workflow Activity Phase B

This phase is now complete.

Goal:
- cover one more real DBM-style code-first registration shape without widening into arbitrary helper frameworks or reopening the owner-level `Workflow` family
- support helper-returned registration collections where the code still stays direct and inspectable
- support the more realistic DBM `GetMessage(service, entity, message, handler)` imperative lookup shape

Exit criteria:
- the compiler can resolve zero-argument helper-returned registration collections for the supported `Types`, `Steps`, and `Images` object-array shapes
- the compiler can still read the imperative DBM helper lane when `GetMessage(...)` carries a leading service or context argument before the stable `entity`, `message`, and `handler` strings
- helper-backed mixed plug-in plus custom workflow activity assemblies survive tracked-source, reverse generation, live readback, and stable-overlap under the existing plug-in families
- no new `ComponentFamily` values are introduced, and the owner-level `Workflow` boundary remains unchanged

Completion outcome:
- the code-first reader now resolves zero-argument helper-returned registration collections through direct helper return expressions or local variables initialized from them
- the helper-backed `seed-code-plugin-helper` fixture now proves mixed `PluginType` catalogs containing both a normal plug-in type and a `customWorkflowActivity` subtype, plus helper-returned structured `Steps` and `Images`
- the new `seed-code-plugin-imperative-service` fixture now proves the service-aware DBM-style imperative `GetMessage(service, entity, message, handler)` lookup shape without broadening into arbitrary control-flow inference
- the same helper and service-aware shapes now survive tracked-source, reverse-generated `sourceBackedArtifacts[]`, classic staged build, `apply-dev`, live readback, and stable-overlap while broader helper-heavy or control-flow-heavy code shapes remain explicit unsupported boundaries

## Phase 12: Code-Extensibility And Workflow Closure

This phase is now complete.

Goal:
- widen the code-first C# registration parser from the earlier narrow DBM lane into a broader but still bounded common-idiom static-analysis lane
- close regular plug-in package solution-zip parity honestly instead of implying that staged `.nupkg` payloads are already part of exported or rebuilt solution zips
- close custom workflow activity package deployment as a permanent classic-only product boundary
- reopen the owner `Workflow` lane only for the supported classic workflow and custom-action source-backed subset that the current neutral source, readback, reverse, and package evidence can actually support

Exit criteria:
- the code-first reader supports reducible common idioms such as member or local indirection, `const`, `static readonly`, `nameof`, simple interpolation, switch or ternary reductions, reducible helpers, direct collection builders, and simple `yield return` aggregators without crossing into arbitrary program execution
- unsupported dynamic or non-reducible registration shapes still emit explicit file or line `unsupportedShape` diagnostics
- regular plug-in packages remain explicit live finalize-apply through `pac plugin push --type Nuget`, and the repo no longer implies solution-zip parity without captured neutral export evidence
- custom workflow activity package deployment fails with the same explicit product-boundary diagnostic across build, `apply-dev`, and `publish`
- supported workflow and custom-action source-backed seeds survive source parse, tracked-source emission, reverse-generated `sourceBackedArtifacts[]`, package-input emission with root component `29`, live readback, and stable-overlap drift

Completion outcome:
- the code-first reader now evaluates a bounded common-idiom lane instead of only the earlier seed-shaped DBM forms, while reflection, dynamic dispatch, non-reducible helper frameworks, external data, and other arbitrary code paths stay explicit unsupported boundaries
- regular plug-in packages now stay documented as a permanent live finalize-apply boundary until a stable package-bearing solution export shape is captured and proven end to end
- custom workflow activity package deployment is now a cited permanent classic-only boundary, aligned with current Microsoft guidance for workflow extensions
- the owner `Workflow` lane is reopened for the current curated classic workflow and custom-action subset through source-backed workflow metadata plus `.xaml` parsing, live `workflow` readback, tracked-source summaries, reverse-generated `sourceBackedArtifacts[]`, package-input emission, and stable-overlap drift without widening direct live mutation or claiming broader workflow-family parity

## Phase 13: Workflow XAML And Business Process Flow Closure

This phase is now active.

Goal:
- move the reopened owner `Workflow` lane from mostly synthetic shell proof to export-backed source truth
- add business process flow definition support first under `Workflow` category `4`, not as a new owner family
- keep direct live mutation frozen while finishing source, reverse, package, live readback, and drift support for the supported workflow subset

Exit criteria:
- exported `Workflows/*.xaml.data.xml` plus `.xaml` source is parsed for classic workflows, custom actions, and a single-table BPF subset
- tracked-source, reverse-generated `sourceBackedArtifacts[]`, package-input emission with root component `29`, live readback, and stable-overlap all preserve workflow shell metadata plus XAML/client-data fidelity
- BPF readback also expands `processstage` rows into one stable stage-definition payload
- environment-gated runtime proof exists for classic workflow execution, custom action invocation, and single-table BPF stage navigation
- broader workflow execution parity, cross-table BPF breadth, dialogs, business rules, and cloud-flow families still stay explicit boundaries unless new evidence clears them

Current outcome:
- the workflow source lane now reads export-backed `*.xaml.data.xml` metadata instead of only the earlier synthetic `Workflows/*.json` shell format
- the supported subset now covers three workflow kinds under the same owner family: `workflow`, `customAction`, and `businessProcessFlow`
- single-table BPF definitions now survive source parsing, tracked-source, reverse-generated `sourceBackedArtifacts[]`, package-input emission, live `workflow` plus `processstage` readback, and stable-overlap drift through the new `seed-workflow-bpf` fixture
- environment-gated publish or execution proof scaffolding now exists for classic workflow, custom action, and BPF runtime navigation, but real Dataverse execution evidence is still pending and therefore broader workflow runtime parity is not yet claimed

## Next Priority

- finish `B-018` by running the new environment-gated workflow, custom-action, and BPF proof harness against a real Dataverse environment
- keep future authoring expansion on the same proof bar: `export zip -> intent-spec -> package-inputs -> pack -> import/publish`, plus honest omission typing and explicit boundary classification
- reopen broader workflow or BPF scope only when new neutral evidence clears the same bar
