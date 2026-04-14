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
- it also carries an external-code column as the neutral business-key shell for alternate-key work, while unattended live key creation remains best-effort in this environment

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

## Seed Process Security

Path:
- `references/examples/seed-process-security/`

Purpose:
- neutral unmanaged seed focused on security-role shell, secured attribute plus field permission, field security profile, and connection role artifacts
- useful for security/access inventory, normalization, and best-effort readback drift

Key files and folders:
- `export/`
- `unpacked/`
- `normalized/`
- `readback/`
- `inventory.md`
- `drift.md`

Notes:
- the current seed proves the security-definition side of the family with a real secured attribute and field permission example plus honest best-effort readback
- role privileges and effective access still need runtime proof and should not be inferred from source alone
- entity-level drift in this seed is still broader than the secured-field change itself because Dataverse readback expands the full table family once the table is in scope

## Seed Process Policy

Path:
- `references/examples/seed-process-policy/`

Purpose:
- neutral unmanaged seed focused on duplicate-rule, routing-rule, and mobile-offline definition handling
- useful for process-policy inventory, normalization, and readback drift

Key files and folders:
- `export/`
- `unpacked/`
- `normalized/`
- `readback/`
- `inventory.md`
- `drift.md`

Notes:
- the current seed proves duplicate-rule and duplicate-rule-condition round-trip against a neutral standard table
- it also proves routing-rule and routing-rule-item round-trip, with workflow and queue links treated as best-effort live associations
- it also proves mobile-offline profile export plus best-effort profile-item readback and drift, with the dedicated `MobileOfflineProfiles/<name>.xml` export treated as the durable source shell
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
- it exists to prove the supported source shape and normalization path for `SimilarityRule`
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
- it exists to prove the supported source shape and normalization path for `SLA` and `SLAItem`
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
- use it to understand source-first registration patterns, not to import DBM assumptions into general Dataverse guidance

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
