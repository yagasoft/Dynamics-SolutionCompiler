# Dataverse Solution Compiler

Dataverse Solution Compiler is an experimental `.NET 10` project for working with Dataverse solution metadata.

It can read source files, read live Dataverse metadata, compare the two, reverse-generate editable intent, and build package inputs for release work.

This repo keeps those paths separate on purpose. It tries to be clear about what is fully proven, what is best-effort, and what is outside the current scope.

## What It Does

- Reads unpacked solution folders and classic export ZIPs into one shared model.
- Reads a bounded common-idiom set of code-first C# plug-in registration shapes into the same plug-in families the XML reader already uses.
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
- The code-first plug-in lane now covers a bounded common-idiom parser lane: direct object initializers, imperative `Entity(\"sdkmessageprocessingstep\")` payloads, reducible constants and members, `nameof`, simple interpolation, switch or ternary reductions, helper-returned registration collections, direct `yield return` aggregators, and the service-aware DBM `GetMessage(service, entity, message, handler)` shape.
- Custom workflow activities now flow through the same `PluginAssembly` and `PluginType` lane as a `pluginTypeKind=customWorkflowActivity` subtype. This phase covers registration, readback, reverse-generation, drift, and classic assembly deployment only.
- Helper-based mixed assemblies are now supported too, as long as they stay in the proven lane above. A code-first project can carry both a normal plug-in type and a custom workflow activity type without reopening the owner-level `Workflow` family.
- Plug-in package deployment stays supported for regular plug-ins, but solution ZIP parity for those `.nupkg` payloads is still not claimed. The package payload remains a live finalize-apply boundary until a stable exported solution shape is proven.
- Custom workflow activities are an explicit classic-only boundary. The compiler now fails with the same clear diagnostic across build, `apply-dev`, and `publish` instead of pretending NuGet package deployment works for them.
- The owner `Workflow` lane is now reopened for a curated classic workflow and custom-action subset. The compiler can read source-backed workflow shells, keep XAML and client-data fidelity, reverse-generate them into `sourceBackedArtifacts[]`, rebuild package inputs with root component `29`, read them back live, and compare stable overlap. Direct live mutation still stays on the package or import path, and broader workflow families remain outside the current scope.
- Best-effort or source-first boundaries still include compact AI live create, import maps and data-source mappings, reporting/legacy rebuild parity, entity-map or hierarchy-rule or convert-rule owner lanes, platform-generated views, richer user-owned visualizations, broader workflow execution parity, and effective-access expansion.

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
dotnet run --project .\src\DataverseSolutionCompiler.Cli -- read .\fixtures\skill-corpus\examples\seed-code-plugin-classic
dotnet run --project .\src\DataverseSolutionCompiler.Cli -- read .\fixtures\skill-corpus\examples\seed-code-plugin-imperative
dotnet run --project .\src\DataverseSolutionCompiler.Cli -- read .\fixtures\skill-corpus\examples\seed-code-plugin-helper
dotnet run --project .\src\DataverseSolutionCompiler.Cli -- emit .\fixtures\skill-corpus\examples\seed-code-plugin-classic --layout intent-spec --output .\artifacts\seed-code-plugin-classic
```

Compare source with live Dataverse metadata:

```powershell
dotnet run --project .\src\DataverseSolutionCompiler.Cli -- readback .\fixtures\skill-corpus\examples\seed-plugin-registration --environment https://org.crm.dynamics.com --solution CodexPluginSeed
dotnet run --project .\src\DataverseSolutionCompiler.Cli -- diff .\fixtures\skill-corpus\examples\seed-plugin-registration --environment https://org.crm.dynamics.com --solution CodexPluginSeed
```

Run the verified Dev workflow:

```powershell
dotnet run --project .\src\DataverseSolutionCompiler.Cli -- apply-dev .\fixtures\skill-corpus\examples\seed-image-config --environment https://org.crm.dynamics.com --solution CodexImageConfig
dotnet run --project .\src\DataverseSolutionCompiler.Cli -- apply-dev .\fixtures\skill-corpus\examples\seed-code-plugin-classic --environment https://org.crm.dynamics.com --solution CodexMetadataSeedCodePluginClassic
dotnet run --project .\src\DataverseSolutionCompiler.Cli -- apply-dev .\fixtures\skill-corpus\examples\seed-code-plugin-imperative --environment https://org.crm.dynamics.com --solution CodexMetadataSeedCodePluginImperative
dotnet run --project .\src\DataverseSolutionCompiler.Cli -- apply-dev .\fixtures\skill-corpus\examples\seed-code-plugin-helper --environment https://org.crm.dynamics.com --solution CodexMetadataSeedCodePluginHelper
```

`apply-dev` always runs `compile -> apply -> readback -> diff`.
For the supported code-first plug-in lane, it also builds the staged `.dll` or `.nupkg` first and then deploys that artifact through the existing plug-in families. Custom workflow activities stay classic-assembly only in this phase.

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
dotnet run --project .\src\DataverseSolutionCompiler.Cli -- publish .\fixtures\skill-corpus\examples\seed-code-plugin-package --environment https://org.crm.dynamics.com --solution CodexMetadataSeedCodePluginPackage --output .\artifacts\publish
```

`publish` keeps the current release behavior: compile with dev-apply enabled, emit package inputs, pack, import when packageable root components exist, and run finalize apply for the currently supported live-mutation families.
For code-first plug-in packages, the staged `.nupkg` is pushed live in finalize apply. It is not written into `solution.zip`. Custom workflow activity package deployment is an explicit unsupported boundary and stops before finalize apply.
For the reopened workflow subset, workflow definitions still ship through package import rather than direct `apply-dev` mutation.

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
- The supported code-first plug-in reader now covers a bounded common-idiom lane, not arbitrary C# inference. Reflection, dynamic dispatch, external files, non-reducible helper frameworks, data-driven loops, and other non-static shapes still stay outside scope and emit explicit `unsupportedShape` diagnostics.
- Plug-in package deployment for regular plug-ins is still a live finalize-apply lane, not solution ZIP parity.
- Custom workflow activities remain classic-assembly only, in line with current Microsoft guidance for workflow extensions.
- Future work should reopen current boundaries only when new neutral evidence or a new approved workflow program supports it.

## Read Next

- [Architecture](docs/architecture.md)
- [Roadmap](docs/roadmap.md)
- [Backlog](docs/backlog/backlog.md)
- [Acceptance Ledger](docs/acceptance/ledger.md)
- [Current Thread Baton](docs/threads/current.md)
- [Coverage Matrix](fixtures/skill-corpus/references/component-coverage-matrix.md)
- [Component Catalog](fixtures/skill-corpus/references/component-catalog.md)
