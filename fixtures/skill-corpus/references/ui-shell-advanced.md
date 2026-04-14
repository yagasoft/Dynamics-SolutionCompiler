# UI Shell Advanced

Use this reference for Dataverse UI-shell artifacts that sit above basic forms and views but below app-specific business logic. Keep the guidance project-agnostic and treat unsupported readback as best-effort rather than as a defect in the source.

## Ribbon Assets

Ribbon assets include the command bar and related customization surfaces that control actions, visibility, enablement, and placement.

- Source: track the ribbon definition and any generated XML or asset files under solution source.
- Source analysis: prefer `RibbonDiff.xml` summaries that surface command ids, button ids, and rule ids before dropping to raw XML.
- Helper: `python scripts/summarize_ribbon_diff.py <ribbondiff-file>` gives a fast markdown or JSON summary of commands, buttons, and rules from a standalone ribbon-diff file.
- Direct `Dev` proof: validate the command surface in the UI by checking that the expected buttons appear, hide, enable, or route correctly.
- Readback: best-effort. Ribbon surfaces often round-trip imperfectly and may be represented through multiple solution artifacts or generated metadata.
- Guidance: reason from the owning app, table, or command surface first; do not treat raw ribbon XML as the whole story.

## Saved Query Visualizations

Saved query visualizations pair a view with chart or visualization metadata.

- Source: track the saved query or visualization definition separately from the base view where possible.
- Source analysis: unpacked solutions can materialize charts under `Entities/<entity>/Visualizations/<id>.xml`, even when the solution root only shows an entity shell. Treat that file as the durable source artifact for the chart itself.
- Helper: `python scripts/summarize_saved_query_visualization.py <visualization-xml-or-json>` gives a fast markdown or JSON summary of chart type, entities, group-by columns, measure aliases, and titles.
- Direct `Dev` proof: confirm the chart or visualization renders against the intended dataset and filters.
- Readback: stronger than ribbon when the chart is actually in the solution scope. Compare the stored `datadescription` and `presentationdescription` payloads plus the owning entity, but avoid over-claiming on fields the export does not preserve symmetrically.
- Guidance: compare the visualization intent, underlying query, and display shape instead of assuming one artifact captures everything.

## Custom Controls And Default Configs

Custom controls and their default configs define how model-driven UI renders specialized components.

- Embedded versus standalone rule:
  - embedded control descriptions live inside FormXML under `controlDescriptions` and travel with the owning form
  - standalone `customcontrol` and `customcontroldefaultconfig` component families are separate solution components and should not be inferred just because a form carries embedded control descriptions
- Source: track the control registration, parameterization, and any default configuration payloads.
- Source analysis: when controls are embedded in FormXML, inventory the `controlDescription` block separately from the surrounding form layout so control identity, host control, form factor, data binding, dataset wiring, quick forms, and default views stay visible.
- Helper: `python scripts/summarize_form_xml.py <formxml-file-or-readback-json>` now surfaces embedded control details from FormXML plus `CustomControlConfigurations` when a readback bundle includes `formjson`.
- Helper: `python scripts/summarize_custom_control.py <custom-control-json-or-manifest>` summarizes a standalone custom control manifest or readback row.
- Helper: `python scripts/summarize_custom_control_default_config.py <default-config-json-or-xml>` summarizes a standalone default-config row by primary entity plus control-description and event payload presence.
- Direct `Dev` proof: add the control to a form or view and verify that it loads, binds, and behaves as expected.
- Readback: best-effort. The runtime may normalize or expand control configuration, and not every detail is symmetric across export and readback. Embedded FormXML control descriptions are currently the stronger proof path, and `formjson` control configurations can provide a second clue when present. The current corpus now has one real standalone `customcontrol` readback example, but the unmanaged export still omits a corresponding source artifact, so treat that family as readback-proven but source-asymmetric. `complexcontrol` and `customcontroldefaultconfig` are now first-class parser, normalization, and drift families in the toolchain, but the neutral seed still lacks non-empty live rows for them.
- Guidance: keep the host form control, embedded control family, bound property, and default config distinct when comparing source to live metadata.

## App Settings

App settings represent app-scoped configuration and are often subordinate to the owning app module.

- Source: track app settings alongside the app module that owns them.
- Direct `Dev` proof: confirm the app behaves correctly after the setting is changed and published.
- Readback: stronger than ribbon, but still family-specific. The practical readback path is the `appsetting` row plus its `settingdefinition` join, then reattachment to the owning app module through `parentappmoduleid`.
- Guidance: compare app-setting intent through the owning app module plus the setting-definition unique name, not through raw row shape alone.

## App Role Maps

App role maps control which roles can see an app module.

- Source: trust the unpacked `AppModule.xml` role map block as the primary artifact.
- Readback: best-effort. Live app-module row surfaces do not reliably expose the role-map structure in the same shape as source.
- Guidance: keep role maps in inventory and full-signature summaries, but do not over-claim round-trip parity unless a stable association surface is available.

## Current Neutral Proof Points

Use the bundled examples as follows:

- `references/examples/seed-app-shell/` for the compact app-shell baseline.
- `references/examples/seed-advanced-ui/` for app-setting-aware app-module drift reasoning plus one real saved-query visualization round-trip and one real standalone custom-control readback-only example, including generated `visualization-summary.md`, `visualization-summary.json`, `custom-control-summary.md`, and `custom-control-summary.json`. The toolchain also now recognizes standalone `complexcontrol` and `customcontroldefaultconfig` folders and readback rows when future seeds or project examples emit them.
- `references/examples/seed-forms/` when you need neutral `RibbonDiff.xml` examples and generated `ribbon-summary.md` or `ribbon-summary.json` outputs while richer command-bar seeding is still growing.
- `references/examples/dbm-workflow-scratch/` when you need a project-specific example of richer embedded `controlDescriptions` that go beyond the neutral seed corpus.

## Practical Rules

- Prefer the canonical app and UX model over raw XML when deciding what should change.
- Use tracked solution source for durable release artifacts.
- Use direct `Dev` proof when the question is whether the shell behaves correctly for users.
- Use readback drift when the family has a proven source/readback slice, such as the seeded saved-query visualization.
- Use best-effort readback when the question is what Dataverse stored for families that still lack that proven slice, especially ribbon and standalone custom-control surfaces.
- Keep this reference neutral so it can support multiple projects and packaging styles.
