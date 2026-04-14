# Seed App Shell

This folder is a neutral unmanaged example corpus for app-shell and configuration reasoning.

What it covers:
- one model-driven app shell
- one sitemap attached to the app
- web resources used by the shell
- an environment variable definition and value pair

How to use it:
- inspect `export/` when you want the packed Dataverse solution
- inspect `unpacked/` when you want the unpacked solution-source shape
- inspect `normalized/` when you want stable, machine-friendly summaries
- inspect `readback/` when you want live Dataverse metadata snapshots

What to look for:
- app module anatomy and app-aware sitemap structure
- web-resource packaging and registration
- environment variable definition versus value behavior
- drift between live metadata and tracked solution source

Boundaries:
- this is project-agnostic example material
- do not treat it as a DBM-specific rule set
- if a task is DBM-specific, use DBM references only as optional project context
