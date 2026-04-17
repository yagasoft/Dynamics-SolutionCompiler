# Example Corpus

This skill ships with a compact example corpus intended for reusable Dataverse metadata reasoning.

## Seed Core

Path:
- `references/examples/seed-core/`

Purpose:
- neutral unmanaged seed focused on table, column, relationship, representative choice metadata, and representative UI artifacts
- good for inventory, normalization, and drift examples

Key files:
- `inventory.md`
- `normalized/`
- `readback/`
- `drift.md`

Notes:
- this seed now proves local picklist and boolean readback through per-entity `option-sets.json`
- it now also proves one true shared global choice through `OptionSets/cdxmeta_priorityband.xml`, per-entity attribute binding, and top-level `global-option-sets/` normalization
- it also carries an external-code column that is reused by the dedicated alternate-key seed

## Seed Forms

Path:
- `references/examples/seed-forms/`

Purpose:
- neutral unmanaged seed focused on richer model-driven form structure
- demonstrates multiple tabs, header controls, quick form usage, and a related-record subgrid

Key files:
- `inventory.md`
- `normalized/`
- `readback/`
- `drift.md`
- `ribbon-summary.md`
- `ribbon-summary.json`
- `work-item-seed-main-summary.md`
- `work-item-seed-main-summary.json`

## Seed App Shell

Path:
- `references/examples/seed-app-shell/`

Purpose:
- neutral unmanaged seed focused on app module, site map, web resources, and environment variables
- useful for app-shell/config inventory, normalization, and readback analysis

Key files and folders:
- `export/`
- `unpacked/`
- `normalized/`
- `readback/`

Notes:
- the example is intentionally compact and project-agnostic
- use it to reason about app-shell packaging and drift, not as a DBM template

## Seed Advanced UI

Path:
- `references/examples/seed-advanced-ui/`

Purpose:
- neutral unmanaged solution focused on advanced app-shell reasoning
- proves app module, app setting, site map, one real saved-query visualization, one standalone custom-control readback-only asymmetry, web resource, and environment-variable round-trip for the advanced-UI slice

Key files and folders:
- `export/`
- `unpacked/`
- `normalized/`
- `readback/`
- `inventory.md`
- `drift.md`
- `custom-control-summary.md`
- `custom-control-summary.json`
- `visualization-summary.md`
- `visualization-summary.json`

Notes:
- this is still a compact, project-agnostic seed rather than a full UX showcase
- it now proves app-setting and app-module comparison behavior plus one neutral saved-query visualization round-trip
- it also now proves one real standalone `customcontrol` in live solution scope, even though the unmanaged export still omits a matching source artifact
- the toolchain now also recognizes standalone `complexcontrol` and `customcontroldefaultconfig` families, but this neutral seed still has zero live rows for those families
- the visualization export currently pulls an `account` entity shell into the solution; treat that shell as packaging context for the chart rather than as intended full-table source
- embedded FormXML `controlDescriptions` are analyzable today, but ribbon and standalone `customcontrol` or `defaultconfig` surfaces still need richer follow-up coverage

## Seed Environment

Path:
- `references/examples/seed-environment/`

Purpose:
- neutral unmanaged solution focused on one real packaged `canvasapp`
- useful for environment/config inventory, normalization, live readback, and stable-overlap drift

Key files and folders:
- `export/`
- `unpacked/`
- `normalized/`
- `readback/`
- `inventory.md`
- `drift.md`
- `canvas-app-summary.md`
- `canvas-app-summary.json`

Notes:
- this seed now proves root component type `300` plus unpacked `CanvasApps/*.meta.xml` parsing
- it preserves the packaged `.msapp` asset and background asset in normalized output
- live readback now compares the honest overlap Dataverse exposes for `canvasapps`, including tags, database references, and CDS dependencies
- the current neutral example carries a managed-solution missing dependency in `solution.xml`; treat that as packaging context for the app, not as source drift

## Seed Import Map

Path:
- `references/examples/seed-import-map/`

Purpose:
- compact neutral unmanaged solution focused on `importmap` plus child data-source mappings
- useful for source-first environment/config inventory, deterministic tracked-source emission, and source-backed package-input copying

Key files and folders:
- `unpacked/`
- `readback/`

Notes:
- this seed currently proves typed source parsing, tracked-source emission, and package-input copying for `ImportMap` and `DataSourceMapping`
- neutral live parity is now an explicit permanent best-effort boundary for this corpus, so drift suppresses false blocking findings rather than pretending a missing unattended live proof is still an open defect

## Seed Entity Analytics

Path:
- `references/examples/seed-entity-analytics/`

Purpose:
- compact neutral unmanaged solution focused on `entityanalyticsconfig`
- useful for source/live overlap on compare-safe environment analytics settings

Key files and folders:
- `unpacked/`
- `readback/`

Notes:
- this seed proves typed source parsing, deterministic tracked/package emission, real `entityanalyticsconfigs` live readback, and stable-overlap drift
- compare only the durable overlap: parent entity, data source, ADLS enablement, and time-series enablement

## Seed AI Families

Path:
- `references/examples/seed-ai-families/`

Purpose:
- compact neutral unmanaged solution focused on `AI Project Type`, `AI Project`, and `AI Configuration`
- useful for source/live overlap on compact AI environment/configuration families

Key files and folders:
- `unpacked/`
- `readback/`

Notes:
- this seed proves typed source parsing, deterministic tracked/package emission, live readback, and stable-overlap drift for all three compact AI families
- `AI Configuration` is anchored to the official `msdyn_aiconfiguration` surface, while the neutral seed remains the live-shape authority for `AI Project Type` and `AI Project`

## Seed Plugin Registration

Path:
- `references/examples/seed-plugin-registration/`

Purpose:
- compact neutral unmanaged solution focused on plugin assembly, plugin type, sdk message processing step, and sdk message processing step image
- useful for source/live overlap on neutral Dataverse plugin-registration metadata without pulling in project-specific DBM assumptions

Key files and folders:
- `unpacked/`
- `readback/`

Notes:
- this seed proves typed source parsing from `Other/Customizations.xml` plus packaged assembly payload preservation under `PluginAssemblies/`
- it also proves deterministic tracked-source emission, source-backed package-input copying, live Web API readback, and stable-overlap drift for the four neutral plugin-registration families
- the newer code-first plug-in seeds prove the supported raw C# ingestion path separately, so this seed stays focused on solution-row plugin metadata rather than source code

## Seed Code Plugin Classic

Path:
- `references/examples/seed-code-plugin-classic/`

Purpose:
- compact neutral code-first seed for the supported DBM-style raw C# registration pattern
- useful for read, tracked-source, reverse-generation, staged-build, and `apply-dev` proof for a signed classic plug-in assembly

Key files and folders:
- `plugins/`
- `readback/`

Notes:
- this seed proves the narrow `CodeFirstSdkRegistration` reader for `PluginAssembly`, `PluginType`, `PluginStep`, and `PluginStepImage`
- it also proves staged `.dll` build output outside the source tree and deployment through the existing plug-in apply path
- the proof contract is a `PreOperation Create` step on `account` that stamps `description` with the deterministic marker `B014-CLASSIC-PROOF`

## Seed Code Plugin Package

Path:
- `references/examples/seed-code-plugin-package/`

Purpose:
- compact neutral code-first seed for the supported DBM-style raw C# registration pattern with plug-in package deployment
- useful for read, tracked-source, reverse-generation, staged-build, and `publish` finalize-apply proof for a `.nupkg` plus dependent assembly

Key files and folders:
- `plugins/`
- `readback/`

Notes:
- this seed proves staged `.nupkg` build output outside the source tree and live deployment through `pac plugin push --type Nuget`
- the dependent assembly stays explicit package content, and the final proof marker comes from that dependency
- the proof contract is a `PreOperation Create` step on `account` that stamps `description` with the deterministic marker `B014-PACKAGE-PROOF-FROM-DEPENDENCY`
- this seed does not claim that `.nupkg` payloads are embedded in `solution.zip`; plug-in package deployment stays a live finalize-apply step until a stable package-bearing solution export shape is proven

## Seed Code Plugin Imperative

Path:
- `references/examples/seed-code-plugin-imperative/`

Purpose:
- compact neutral code-first seed for the supported imperative DBM helper registration shape
- useful for proving the widened parser without leaving the existing plug-in families

Key files and folders:
- `plugins/`
- `readback/`

Notes:
- this seed proves `new Entity("sdkmessageprocessingstep")` and `new Entity("sdkmessageprocessingstepimage")` payload construction through the supported helper pattern
- it also proves nearby constants, enum casts, and the current narrow ternary handling inside that imperative registration shape
- the proof contract is a `PreOperation Create` step on `account` that stamps `description` with the deterministic marker `B015-IMPERATIVE-PROOF`

## Seed Code Plugin Helper

Path:
- `references/examples/seed-code-plugin-helper/`

Purpose:
- compact neutral code-first seed for helper-returned registration collections
- useful for proving zero-argument helper methods for `Types`, `Steps`, and `Images` without leaving the existing plug-in families

Key files and folders:
- `plugins/`
- `readback/`

Notes:
- this seed proves helper-returned registration collections for the supported direct helper shape
- it also proves a mixed plug-in plus custom workflow activity type catalog in one classic assembly without reopening the owner-level `Workflow` family
- the proof contract is a `PreOperation Create` step on `account` that stamps `description` with the deterministic marker `B016-HELPER-PROOF`

## Seed Code Plugin Imperative Service

Path:
- `references/examples/seed-code-plugin-imperative-service/`

Purpose:
- compact neutral code-first seed for the more realistic DBM service-aware imperative lookup shape
- useful for proving `GetMessage(service, entity, message, handler)` without broadening into arbitrary helper or control-flow inference

Key files and folders:
- `plugins/`
- `readback/`

Notes:
- this seed keeps the imperative `sdkmessageprocessingstep` and `sdkmessageprocessingstepimage` entity payload pattern from phase A
- it widens only the message-context lookup to the supported service-leading DBM shape
- the proof contract is a `PreOperation Create` step on `account` that stamps `description` with the deterministic marker `B016-IMPERATIVE-SERVICE-PROOF`

## Seed Code Workflow Activity Classic

Path:
- `references/examples/seed-code-workflow-activity-classic/`

Purpose:
- compact neutral code-first seed for custom workflow activity registration through the existing code and extensibility lane
- useful for proving classic assembly staging, deploy, readback, reverse generation, and drift for `pluginTypeKind=customWorkflowActivity`

Key files and folders:
- `plugins/`
- `readback/`

Notes:
- this seed keeps custom workflow activities under `PluginAssembly` and `PluginType`; it does not reopen the owner-level `Workflow` family
- the proof in this phase is registration and readback proof, not workflow-definition or runtime execution proof

## Seed Code Workflow Activity Package

Path:
- `references/examples/seed-code-workflow-activity-package/`

Purpose:
- compact neutral negative seed for the custom workflow activity package boundary
- useful for proving that workflow activities stay classic-assembly only in the current code-first lane

Key files and folders:
- `plugins/`

Notes:
- this seed is expected to fail before deploy with an explicit compiler diagnostic
- it exists to prove that the compiler does not silently downgrade or overclaim plug-in package support for workflow activities

## Seed Workflow Classic

Path:
- `references/examples/seed-workflow-classic/`

Purpose:
- compact neutral source-backed seed for the reopened owner `Workflow` lane
- useful for classic workflow shell metadata, XAML fidelity, live readback, reverse generation, package-input emission, and stable-overlap on the current curated subset

Key files and folders:
- `unpacked/`
- `readback/`

Notes:
- this seed keeps the workflow lane source-backed through export-backed `Workflows/*.xaml.data.xml` plus the paired `.xaml`
- it proves source parsing, tracked-source summaries, reverse-generated `sourceBackedArtifacts[]`, package-input emission with root component `29`, live workflow readback, and stable-overlap for a deterministic classic workflow shell
- direct live mutation still stays on the package or import path; this seed does not reopen broader workflow execution parity by itself

## Seed Workflow Action

Path:
- `references/examples/seed-workflow-action/`

Purpose:
- compact neutral source-backed seed for the reopened owner `Workflow` lane
- useful for custom process action shell metadata, explicit action argument metadata, live readback, reverse generation, package-input emission, and stable-overlap on the current curated subset

Key files and folders:
- `unpacked/`
- `readback/`

Notes:
- this seed keeps custom actions under the owner `Workflow` lane, not under code-extensibility
- it proves action shell metadata, argument metadata, XAML fidelity, tracked-source summaries, reverse-generated `sourceBackedArtifacts[]`, package-input emission with root component `29`, live workflow readback, and stable-overlap for a deterministic custom action shell
- broader workflow families such as dialogs and cloud flows remain outside the current reopened subset

## Seed Workflow BPF

Path:
- `references/examples/seed-workflow-bpf/`

Purpose:
- compact neutral source-backed seed for the reopened owner `Workflow` lane
- useful for one single-table business process flow definition through export-backed workflow metadata, normalized stage definitions, live readback, reverse generation, package-input emission, and stable-overlap

Key files and folders:
- `unpacked/`
- `readback/`

Notes:
- this seed keeps BPF under `Workflow` category `4`, not as a new owner family
- it uses export-backed `Workflows/*.xaml.data.xml` plus `.xaml`, with stable BPF stage metadata preserved as normalized JSON instead of a new standalone stage authoring family
- it proves source parsing, tracked-source summaries, reverse-generated `sourceBackedArtifacts[]`, package-input emission with root component `29`, live `workflow` plus `processstage` readback, and stable-overlap for a deterministic single-table BPF definition
- BPF runtime instances and stage movement are still proof-only operational state; they are not part of tracked-source, reverse generation, or package rebuild

## Seed Service Endpoint Connector

Path:
- `references/examples/seed-service-endpoint-connector/`

Purpose:
- compact neutral unmanaged solution focused on `serviceendpoint` and `connector`
- useful for neutral source/live overlap on integration-oriented extensibility metadata after the plugin-registration slice

Key files and folders:
- `unpacked/`
- `readback/`

Notes:
- this seed proves typed source parsing, deterministic tracked-source emission, source-backed package-input copying, live Web API readback, and stable-overlap drift for both families
- drift intentionally compares only durable overlap fields such as contract, connection mode, auth type, namespace/path/url, connector internal id, normalized capabilities, introduced version, and stable `isCustomizable`
- connection references, custom-code payloads, and secret-bearing values remain outside this neutral seed

## Seed Alternate Key

Path:
- `references/examples/seed-alternate-key/`

Purpose:
- compact neutral unmanaged solution focused on one real authored alternate key over a business-key column
- useful for schema-detail proof across source, live readback, drift, tracked-source, package-inputs, and JSON-intent generation

Key files and folders:
- `unpacked/`
- `readback/`

Notes:
- this seed proves keys embedded in `Entities/<entity>/Entity.xml` rather than a separate unpacked root
- live proof comes from `EntityDefinitions(LogicalName='<entity>')?$expand=Keys(...)` plus solution-component type `14`
- stable-overlap drift compares entity, stable key name, and normalized key-attribute set while ignoring `EntityKeyIndexStatus`

## Seed Image Config

Path:
- `references/examples/seed-image-config/`

Purpose:
- compact neutral unmanaged solution focused on `ImageConfiguration` plus narrow managed-flag proof on the owning table and columns
- useful for schema-detail source/live overlap on primary-image metadata and stable `IsCustomizable` drift

Key files and folders:
- `unpacked/`
- `readback/`

Notes:
- this seed proves both entity-level and attribute-level image configuration through `PrimaryImageAttribute` plus image-attribute metadata
- it also proves the intentionally narrow managed-property stance in this repo: stable `IsCustomizable` on table and column artifacts, not a new standalone public family
- broader component-type `13` managed-property proof remains follow-up work

## Seed Process Security

Path:
- `references/examples/seed-process-security/`

Purpose:
- neutral unmanaged seed focused on security-role shell, secured attribute plus field permission, field security profile, and connection role artifacts
- useful for security-definition inventory, normalization, live readback, and stable-overlap drift

Key files and folders:
- `export/`
- `unpacked/`
- `normalized/`
- `readback/`
- `inventory.md`
- `drift.md`

Notes:
- the current seed proves the security-definition side of the family with a real secured attribute and field permission example plus live readback and stable-overlap drift for role shell, field security profile, field permission, and connection role
- role privileges remain definition-adjacent best effort, and effective access still needs runtime proof and should not be inferred from source alone
- entity-level drift in this seed is still broader than the secured-field change itself because Dataverse readback expands the full table family once the table is in scope

## Seed Process Policy

Path:
- `references/examples/seed-process-policy/`

Purpose:
- neutral unmanaged seed focused on duplicate-rule, routing-rule, and mobile-offline definition handling
- useful for process-policy inventory, normalization, live readback, and stable-overlap drift

Key files and folders:
- `export/`
- `unpacked/`
- `normalized/`
- `readback/`
- `inventory.md`
- `drift.md`

Notes:
- the current seed proves duplicate-rule and duplicate-rule-condition round-trip against a neutral standard table
- it also proves routing-rule and routing-rule-item round-trip, with workflow and queue links treated as best-effort live associations rather than release-blocking drift
- it also proves mobile-offline profile and mobile-offline profile-item source/live overlap, with the dedicated `MobileOfflineProfiles/<name>.xml` export treated as the durable source shell
- Dataverse can emit this family into dedicated unpacked folders such as `duplicaterules/<hash>/duplicaterule.xml` even when classic manifest surfaces stay sparse
- routing rules can surface as `RoutingRules/<name>.meta.xml` while `Customizations.xml` still carries only a thin shell
- mobile offline profiles can surface as `MobileOfflineProfiles/<name>.xml` while `Customizations.xml` still carries only a placeholder shell
- duplicate-rule publish or activation state remains best-effort and should not be conflated with definition drift for the mixed process-policy seed

## Source-Only Similarity Rule

Path:
- `references/examples/source-only-similarity-rule/`

Purpose:
- compact source-only parser fixture for the `SimilarityRule` family
- useful when the environment does not expose clean unattended create or list operations for `similarityrule`

Key files and folders:
- `unpacked/`
- `normalized/`
- `inventory.md`
- `drift.md`

Notes:
- this is not a live neutral Dev seed
- it exists to prove the supported source shape, tracked-source output, and source-backed package-input path for `SimilarityRule`
- the family is intentionally source-first or best-effort in this corpus rather than an unfinished live-parity gap
- use it together with the process-policy reference when you need source-first reasoning for similarity rules

## Source-Only SLA

Path:
- `references/examples/source-only-sla/`

Purpose:
- compact source-only parser fixture for the `SLA` and `SLAItem` family
- useful when a neutral live seed is blocked by service-table prerequisites such as `IsSLAEnabled`

Key files and folders:
- `unpacked/`
- `normalized/`
- `inventory.md`
- `drift.md`

Notes:
- this is not a live neutral Dev seed
- it exists to prove the supported source shape, tracked-source output, and source-backed package-input path for `SLA` and `SLAItem`
- the family is intentionally source-first or best-effort in this corpus rather than an unfinished live-parity gap
- use it together with the process-policy reference when you need source-first reasoning for service-policy artifacts

## DBM Workflow Scratch

Path:
- `references/examples/dbm-workflow-scratch/`

Purpose:
- optional DBM-specific workflow example derived from real unpacked workflow source
- useful while the neutral process-policy seed is still growing beyond security-first coverage
- now also useful as a project-specific source example for richer embedded custom-control descriptions in real FormXML

Key files:
- `inventory.md`
- `account-form-summary.md`
- `account-form-summary.json`

Important:
- treat this as a project-specific example, not a global baseline
- use it to study workflow XAML/source shape and richer embedded control-description source, not to import DBM assumptions into general guidance

## DBM SDK Registration

Path:
- `references/examples/dbm-sdk-registration/`

Purpose:
- optional DBM-specific source-first example for SDK-message, filter, step, and image registration logic
- useful when a project creates Dataverse plugin steps from code rather than relying on exported solution rows alone

Key files:
- `inventory.md`
- `inventory.json`
- `README.md`

Important:
- this is a project-specific example, not a neutral seed
- use it to understand source-first registration patterns and unsupported local variants, not to import DBM assumptions into general Dataverse guidance
- the neutral `seed-code-plugin-classic` and `seed-code-plugin-package` fixtures now cover the supported runtime compiler path for the narrow DBM-style registration shape

## DBM Baseline

Path:
- `references/examples/dbm-baseline/`

Purpose:
- optional DBM-specific reference derived from the DBM tracked baseline
- useful when a task explicitly benefits from DBM packaging or layering context

Important:
- this is an example, not a global rule set
- do not import DBM assumptions into general Dataverse advice unless the task is actually DBM-specific

## Coverage Matrix

Path:
- `references/component-coverage-matrix.md`

Purpose:
- rolling ledger of which Dataverse component families are documented, seeded, read back, drift-checked, and forward-tested
- use this when choosing the next family to deepen
