# Drift And Readback

Use this reference when comparing exported or tracked solution source against live Dataverse metadata.

## Why Readback Matters

Dataverse can materialize more than you explicitly authored:
- companion name columns
- default views
- default forms
- platform relationships
- generated metadata differences after publish

That means export-only reasoning is not enough for synthesis workflows. Capture live readback and compare it against normalized source.

## Recommended Sequence

1. Normalize the source export or unpacked solution.
2. Capture readback for the relevant solution and entities.
3. Compare source and readback.
4. Classify differences before calling them drift.

## What To Normalize

Good normalization usually includes:
- XML whitespace reduction
- GUID casing normalization
- stable JSON formatting
- explicit separation of forms, views, attributes, and relationships

The bundled normalizer strips volatile XML formatting but intentionally keeps the semantic structure.

## Expected Noise Categories

Common expected extras in readback:
- lookup companion name fields such as `<lookupfield>name`
- platform ownership and lifecycle fields
- platform relationships tied to ownership, async operations, bulk delete, sync, or process/session behavior
- default forms and views introduced because an entity is present in the solution artifact family

These are usually not business drift by themselves.

## Useful Diff Categories

The bundled comparer reports:
- missing in readback
- extra in readback
- mismatched form structure
- mismatched view structure

Interpret them carefully:
- missing often means the source names a thing that was never applied or was filtered out
- extra often means platform expansion or broader solution emission
- mismatched form or view structure usually means the live artifact diverged in shape, not just formatting

## Best-Effort Solution Scoping

Readback should prefer solution-aware filtering when possible, especially for forms and views.

Still, some solutions surface forms or views indirectly through entity components or required-component expansion. In those cases:
- strict solution-component scoping may under-report
- entity-scoped fallback may be more truthful than an empty result

Call out which mode you are effectively using.

## Family-Specific Comparison Rules

Some Dataverse families need narrower comparison rules than raw source would suggest.

- App settings:
  - compare through the owning app module plus the `settingdefinition` unique name and value
  - live readback may expose the row separately from the app module, so rejoin by `parentappmoduleid`
- App role maps:
  - keep them in source inventory and full-signature summaries
  - treat them as best-effort for drift unless a stable live association surface is available
- Canvas apps:
  - compare the stable overlap between unpacked `CanvasApps/*.meta.xml` and live `canvasapps` rows
  - stable overlap currently includes name, display name, app version, status, client-version constraints, tags, authorization or connection references, database references, `CanConsumeAppPass`, app type, introduced version, CDS dependencies, and customizable state
  - treat the `.msapp` binary and background asset as packaged source artifacts unless live readback exposes a comparable payload
  - if the export records a missing dependency on another managed solution, treat that as packaging context rather than source drift
- Field security permissions:
  - compare only after the attribute is actually secured and the owning field security profile is in scope
  - once those prerequisites are present, field-permission rows compare cleanly by profile, entity, attribute, and access flags
  - if the source only carries the secured attribute shell, entity-level readback may still look broad because Dataverse returns the surrounding table family
- Duplicate rules:
  - compare the rule row and its child conditions separately
  - normalize boolean flags because source often stores `1/0` while readback returns `true/false`
  - if the unpacked source lives under `duplicaterules/<hash>/duplicaterule.xml`, treat that file as the source of truth even when `Customizations.xml` and `RootComponents` look sparse
- Choice metadata and alternate keys:
  - compare authored local or boolean choice columns through typed metadata readback, not through the base attribute list alone
  - compare shared global choices twice when the source proves them: once as reusable contracts by option-set name, and again as bound attribute usage by entity plus attribute
  - if the unpacked source emits `OptionSets/<name>.xml`, treat that file as the durable source shell for the shared choice contract rather than trying to reconstruct it only from attribute metadata
  - inventory state and status, but do not treat missing source-side option labels as drift when the export only emits the thin companion shell
  - compare alternate keys by entity plus key attributes and treat `EntityKeyIndexStatus` as operational state rather than release drift
  - if unattended key creation is unavailable in the current environment, keep the family readback-first or source-first and record that boundary explicitly
- Routing rules:
  - compare the routing-rule shell and each item's condition XML separately
  - if the unpacked source lives under `RoutingRules/<name>.meta.xml`, treat that file as the source of truth even when `Customizations.xml` only shows a thin routing-rule shell
  - treat workflow links and queue targets as best-effort drift dimensions when the unpacked source omits them but live readback still returns them
- Mobile offline profiles:
  - compare the profile shell and nested profile-item shell from `MobileOfflineProfiles/<name>.xml`
  - align child items by owning profile plus entity, then compare the stable overlap such as item name, distribution criteria, and ownership flags
  - treat richer live toggles such as visibility or related-record behavior as best-effort when the export omits them
- Similarity rules:
  - compare stable targeting fields such as base entity, matching entity, inactive-record handling, `maxkeywords`, and `ngramsize`
  - if the Web API surface does not expose normal create or list operations for `similarityrule`, keep the family source-first or record-id-based best-effort instead of forcing a round-trip claim
- SLAs:
  - compare the SLA shell separately from its SLA items and prefer stable fields such as applicable-from, default flag, pause or resume allowance, applicable entity, and action-flow unique name
  - if a neutral live seed is blocked by `IsSLAEnabled` or other service-table prerequisites, keep the family source-first and use the bundled `source-only-sla` fixture to reason about parser shape and normalization
- Saved query visualizations:
  - compare the owning entity plus the stored `datadescription` and `presentationdescription` payloads
  - if the export pulls an entity shell into the solution only to carry `Visualizations/<id>.xml`, do not treat missing full entity metadata as chart drift
- Embedded custom controls:
  - treat FormXML `controlDescriptions` as part of the form signature and compare them through the owning form
  - compare host control, embedded control family, bind attribute, datasets, quick forms, and default views before assuming standalone control drift
- Ribbon and standalone custom controls:
  - prefer source-first reasoning and direct `Dev` proof
  - compare only the parts the platform actually returns cleanly, and do not infer standalone `customcontrol` or `defaultconfig` components from embedded form control descriptions alone
  - if a standalone `customcontrol` is present in live solution scope but the unmanaged export emits no source artifact, record that as a source-asymmetric platform boundary rather than as a packaging mistake
  - if a `customcontroldefaultconfig` or `complexcontrol` row is present, compare only stable payload presence and normalized XML or JSON overlap until a stronger neutral round-trip proof exists

## Practical Rule

Do not say "drift" until you can answer:
- is this a platform-generated companion artifact
- is this a solution-layer side effect
- is this a volatile formatting difference
- or is this a real semantic mismatch between intended source and live state

## Example Outputs

See:
- `references/examples/seed-core/drift.md`
- `references/examples/seed-forms/drift.md`
- `references/examples/seed-advanced-ui/drift.md`
- `references/examples/seed-environment/drift.md`
- `references/examples/seed-process-security/drift.md`
- `references/examples/seed-process-policy/drift.md`

Those examples intentionally show both:
- expected readback noise
- meaningful family-specific comparison for forms, saved-query visualizations, field security, and process-policy artifacts
