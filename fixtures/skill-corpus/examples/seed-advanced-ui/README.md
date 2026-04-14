# Seed Advanced UI

This folder is a neutral unmanaged example corpus for advanced app-shell reasoning.

What it covers:
- one model-driven app module
- one app-aware sitemap shell
- one app setting
- one saved-query visualization carried through an entity shell
- one standalone `customcontrol` in live solution scope, captured through readback
- supporting web resources
- one environment variable definition and value pair

How to use it:
- inspect `export/` for the packed solution
- inspect `unpacked/` for the tracked solution-source shape
- inspect `normalized/` for stable summaries used by drift comparison
- inspect `readback/` for live Dataverse metadata snapshots

What to look for:
- how app settings live under the owning app module in source
- how live readback can rejoin `appsetting` rows back to the parent app module
- how a chart can materialize under `Entities/Account/Visualizations/<id>.xml` while the solution root only shows an entity shell
- how a standalone `customcontrol` can exist in solution scope and readback while the unmanaged export still emits no matching source artifact
- how app-shell drift should compare readback-compatible fields rather than packaging-only details

Boundaries:
- this seed is intentionally compact and project-agnostic
- it is a good proof point for app settings, app modules, and one saved-query visualization round-trip behavior
- it now also includes `custom-control-summary.md` and `custom-control-summary.json` as a readback-only proof point for standalone custom controls
- the toolchain now also supports standalone `complexcontrol` and `customcontroldefaultconfig` parsing, normalization, drift, and helper summaries when future seeds or project examples emit non-empty rows
- the chart currently reuses a neutral standard account visualization, so the entity shell is packaging context rather than the primary authoring surface
- embedded FormXML `controlDescriptions` are analyzed elsewhere in the skill, but richer ribbon and standalone `defaultconfig` coverage still need separate follow-up work
