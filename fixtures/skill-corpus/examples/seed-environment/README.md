# Seed Environment

This neutral unmanaged seed focuses on one real packaged Dataverse `canvasapp`.

What it proves:
- solution component type `300`
- unpacked source under `CanvasApps/*.meta.xml`
- preserved packaged `.msapp` and background asset files
- normalized source output for the canvas-app family
- best-effort live `canvasapps` readback
- stable-overlap drift comparison with `No drift`

Key outputs:
- `inventory.md`
- `normalized/`
- `readback/`
- `drift.md`
- `canvas-app-summary.md`
- `canvas-app-summary.json`

Important interpretation rules:
- treat the canvas-app meta XML as emitted package evidence, not the primary authoring surface
- treat the `.msapp` binary as part of the durable packaged artifact family
- treat any missing dependency in `solution.xml` as packaging context rather than immediate drift unless the release task is specifically about dependency closure
