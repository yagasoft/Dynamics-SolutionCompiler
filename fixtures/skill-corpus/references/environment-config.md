# Environment And Configuration

Use this reference when a Dataverse task is really about tenant-scoped or deployment-scoped artifacts rather than table or form metadata.

## Main Families

- `300` Canvas App
- `380` Environment Variable Definition
- `381` Environment Variable Value
- `166` Data Source Mapping
- `208` Import Map
- `400` AI Project Type
- `401` AI Project
- `402` AI Configuration
- `430` Entity Analytics Configuration

## Canvas Apps

Treat canvas apps as packaged environment assets with three related surfaces:
- solution component registration through type `300`
- unpacked source under `CanvasApps/<name>.meta.xml`
- binary and companion assets such as `CanvasApps/<name>_DocumentUri.msapp` and `CanvasApps/<name>_BackgroundImageUri`

The neutral proof point is:
- `references/examples/seed-environment/`

What the current skill can do:
- inventory root component type `300`
- parse `CanvasApps/*.meta.xml`
- summarize display name, app version, status, client-version constraints, tags, database references, connection references, CDS dependencies, and asset presence
- normalize the meta XML plus JSON payloads and preserved `.msapp` or background asset files
- capture best-effort live `canvasapps` readback
- compare the stable overlap between source and live metadata

Practical authoring rule:
- do not treat the `.meta.xml` as the primary authoring surface
- use it as emitted evidence of a packaged canvas app
- keep the real app artifact plus tracked solution source for durable release outputs

Practical drift rule:
- compare stable fields such as name, display name, app version, status, client versions, tags, connection references, database references, CDS dependencies, and `CanConsumeAppPass`
- treat the binary `.msapp` and background asset as source-side packaging artifacts unless live readback exposes a comparable payload
- if the export carries a missing dependency on another managed solution, treat that as packaging context, not immediate drift

## Environment Variables

Treat definitions and values separately:
- definition is the reusable contract
- value is the environment-local override

Use source and readback to compare:
- schema name
- display name
- type
- default value
- secret-store mode
- current value set

Do not confuse:
- a missing current value with a broken definition
- a different environment-local value with source drift unless the release actually intends that value to be fixed

## Import Maps And Data Source Mappings

These families remain thinner-proof than canvas apps and environment variables in the current skill.

Current rule:
- inventory and normalize source when present
- prefer source-first reasoning
- treat live readback as best-effort unless the environment exposes real neutral rows

What to look for:
- source file or unpacked folder presence
- connector or external-system bindings
- tenant-local dependencies that should not be over-normalized into release drift

## AI And Analytics Families

`AI Project Type`, `AI Project`, `AI Configuration`, and `Entity Analytics Configuration` are currently cataloged but not strongly seeded.

Current rule:
- classify them correctly
- preserve source evidence
- treat readback and drift as best-effort until a compact neutral seed exists

## Direct Dev Versus Packaged Release

Use direct `Dev` apply when:
- you need quick proof that a canvas app, environment variable, or config artifact materializes correctly
- you need readback to understand how Dataverse stores the artifact family

Use tracked source plus packaging when:
- the artifact must be released across environments
- tenant-local bindings need controlled substitution
- the binary or packaged asset needs durable reviewable storage

The current hybrid recommendation is:
- validate in `Dev`
- read back the real Dataverse shape
- keep durable release artifacts in tracked solution source and packaged outputs
