# Dataverse Solution Compiler

Dataverse Solution Compiler is an experimental `.NET 10` project for working with Dataverse solution metadata.

It can read source files, read live Dataverse metadata, compare the two, reverse-generate editable intent, and build package inputs for release work.

This repo keeps those paths separate on purpose. It tries to be clear about what is fully proven, what is best-effort, and what is outside the current scope.

## What It Does

- Reads unpacked solution folders and classic export ZIPs into one shared model.
- Writes tracked-source output, reverse-generated `intent-spec.json`, and PAC-ready package-input folders.
- Reads live Dataverse metadata and compares it to source with family-aware drift rules.
- Supports a verified Dev workflow through `apply-dev`.
- Supports release workflows through `pack`, `check`, and `publish`.
- Keeps unsupported or partial areas explicit instead of pretending everything has full parity.

## Current Status

- `dotnet build` and `dotnet test` pass for the full solution.
- Every audited Dataverse owner family is now either evidence-backed or an explicit boundary.
- The CLI commands are working: `read`, `plan`, `emit`, `apply-dev`, `readback`, `diff`, `pack`, `import`, `publish`, `check`, `doctor`, and `explain`.
- Strongest structured rebuildable areas: schema core, supported schema detail, supported model-driven forms, supported authored views, and environment variables.
- Strongest hybrid rebuildable areas: canvas apps, entity analytics, code/extensibility, process-policy, and security-definition families.
- Best-effort or source-first boundaries still include compact AI live create, import maps and data-source mappings, reporting/legacy rebuild parity, workflow/entity-map/hierarchy-rule/convert-rule owner lanes, platform-generated views, richer user-owned visualizations, and effective-access expansion.

For the detailed breakdown, start with the [Acceptance Ledger](docs/acceptance/ledger.md) and the [Coverage Matrix](fixtures/skill-corpus/references/component-coverage-matrix.md).

## Getting Started

Prerequisites:

- .NET 10 SDK
- Power Platform CLI (`pac`) for pack/import/check work
- Azure-authenticated access to a Dataverse environment for live readback, apply, or publish commands

Build and test:

```powershell
dotnet build .\DataverseSolutionCompiler.sln
dotnet test .\DataverseSolutionCompiler.sln
```

## Core Workflows

Read source or reverse-generate intent:

```powershell
dotnet run --project .\src\DataverseSolutionCompiler.Cli -- read .\fixtures\skill-corpus\examples\seed-core
dotnet run --project .\src\DataverseSolutionCompiler.Cli -- emit .\fixtures\skill-corpus\examples\seed-core --layout intent-spec --output .\artifacts\seed-core
```

Compare source with live Dataverse metadata:

```powershell
dotnet run --project .\src\DataverseSolutionCompiler.Cli -- readback .\fixtures\skill-corpus\examples\seed-plugin-registration --environment https://org.crm.dynamics.com --solution CodexPluginSeed
dotnet run --project .\src\DataverseSolutionCompiler.Cli -- diff .\fixtures\skill-corpus\examples\seed-plugin-registration --environment https://org.crm.dynamics.com --solution CodexPluginSeed
```

Run the verified Dev workflow:

```powershell
dotnet run --project .\src\DataverseSolutionCompiler.Cli -- apply-dev .\fixtures\skill-corpus\examples\seed-image-config --environment https://org.crm.dynamics.com --solution CodexImageConfig
```

`apply-dev` always runs `compile -> apply -> readback -> diff`.

For now, direct live mutation is limited to:

- `ImageConfiguration`
- `EntityAnalyticsConfiguration`
- `PluginAssembly`
- `PluginType`
- `PluginStep`
- `PluginStepImage`
- `ServiceEndpoint`
- `Connector`
- `MobileOfflineProfile`
- `MobileOfflineProfileItem`
- `ConnectionRole`

Run the release path:

```powershell
dotnet run --project .\src\DataverseSolutionCompiler.Cli -- pack .\fixtures\skill-corpus\examples\seed-service-endpoint-connector --output .\artifacts\publish
dotnet run --project .\src\DataverseSolutionCompiler.Cli -- publish .\fixtures\skill-corpus\examples\seed-service-endpoint-connector --environment https://org.crm.dynamics.com --solution CodexEndpointSeed --output .\artifacts\publish
```

`publish` keeps the current release behavior: compile with dev-apply enabled, emit package inputs, pack, import when packageable root components exist, and run finalize apply for the currently supported live-mutation families.

It is not a post-publish verification command.

## Repository Map

- [src/](src/) - compiler code, readers, emitters, live adapters, apply executor, and CLI
- [tests/](tests/) - unit, integration, command, and end-to-end tests
- [fixtures/skill-corpus/](fixtures/skill-corpus/) - copied neutral seed corpus and reference material
- [docs/](docs/) - roadmap, architecture, backlog, acceptance ledger, and thread baton docs

## Design Rules

- Raw solution XML is evidence and packaging input, not the primary authoring surface.
- Source, live readback, reverse generation, and package rebuild are different proof surfaces.
- Unsupported families or shapes stay explicit as boundaries.
- Future work should reopen current boundaries only when new neutral evidence or a new approved workflow program supports it.

## Read Next

- [Architecture](docs/architecture.md)
- [Roadmap](docs/roadmap.md)
- [Backlog](docs/backlog/backlog.md)
- [Acceptance Ledger](docs/acceptance/ledger.md)
- [Current Thread Baton](docs/threads/current.md)
- [Coverage Matrix](fixtures/skill-corpus/references/component-coverage-matrix.md)
- [Component Catalog](fixtures/skill-corpus/references/component-catalog.md)
