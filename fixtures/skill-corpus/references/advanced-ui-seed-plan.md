# Advanced UI Seed Plan

This reference describes the neutral advanced-UI seed for Dataverse metadata work. It is intentionally project-agnostic and should be read as a reusable baseline, not as a template for any specific solution.

## Current V1 Seed

The current v1 seed proves a compact advanced app-shell slice:

- one neutral model-driven app module
- one app-aware sitemap shell
- one app setting
- one standard saved-query visualization carried through a neutral solution
- one standalone `customcontrol` captured through live solution readback
- supporting web resources
- one environment variable definition and value pair

The v1 seed is intentionally small. It currently proves app-module, app-setting, shell, and one visualization round-trip behavior without pulling in project-specific assumptions.

## Why It Exists

The seed exists to give us a compact baseline for:

- inventorying how the app shell is packaged
- checking which advanced-UI artifacts are source-first versus best-effort readback
- normalizing exported or unpacked source before drift comparisons
- proving that app-module and app-setting artifacts still resolve correctly after export, unpack, and live readback
- proving that a saved-query visualization can be exported, unpacked, normalized, and compared without treating the surrounding entity shell as the primary authoring surface
- proving that a standalone `customcontrol` can appear in live solution scope even when the unmanaged export still omits a matching source artifact

In practice, this seed helps separate durable metadata from packaging noise and makes it easier to tell whether a difference is a real drift issue or just a round-trip artifact.

## Expected Artifacts

### Export

Expect a solution export that includes the shell and the UI dependencies needed by the seed:

- `solution.xml`
- `customizations.xml`
- app/module and sitemap metadata
- advanced UI assets where applicable, such as app-setting artifacts
- visualization artifacts when a seeded chart is present
- any packaged web resources or companion files referenced by the UI surface

### Unpacked

Expect the export to expand into a solution source tree that makes each family easier to inspect:

- `Other/` for solution-level and app-shell artifacts where applicable
- explicit files for app modules, site maps, web resources, app settings, and other advanced UI assets when the solution format exposes them separately
- entity-scoped `Visualizations/<id>.xml` files when a chart is present, even if the root component surface only shows an entity shell

The unpacked tree is the main source inspection surface. It should preserve enough structure to explain how the UI was assembled, even when one logical feature is distributed across multiple files.

### Readback

Expect live readback to confirm the runtime shape of the same seed, but treat some surfaces as partial or best-effort:

- app module and sitemap presence
- environment-variable or app-setting values when the readback path supports them
- saved-query visualizations when the chart is in solution scope
- command-bar or ribbon surfaces only when the environment exposes them consistently

Readback should be used to validate what Dataverse actually materialized, not to replace the source tree as the authoring record.

## Next Enrichments

The next enrichments for this seed are:

- richer sitemap navigation instead of the default shell stub
- neutral ribbon or command-bar examples
- neutral standalone `customcontrol` and `defaultconfig` examples, distinct from the embedded FormXML `controlDescriptions` that are already analyzable today
- standalone `RibbonDiff.xml` helper coverage through `python scripts/summarize_ribbon_diff.py <ribbondiff-file>` so command-bar analysis stays easy even before richer seeded command surfaces land
- broader visualization coverage beyond the current single chart, including additional chart shapes or user-owned variants

## Practical Rule

If a UI artifact is required for the seed to function, keep it in source and verify it in readback. If a surface is known to round-trip imperfectly, document it as best-effort rather than forcing false symmetry.
