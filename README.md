# Dataverse Solution KB

This repository is the persistent knowledge base and implementation workspace for the `.NET 10` Dataverse Solution Compiler.

## Scope

- Follow the approved `.NET 10` roadmap.
- Keep release guidance separate from implementation details.
- Capture what is proven, what is planned, and what still needs evidence.
- Treat Dataverse source, readback, and packaged artifacts as distinct proof surfaces.
- Keep the current `dataverse-metadata-synthesis` skill corpus available as the seed fixture set, not as a runtime dependency.

## What Exists Now

- A buildable `.NET 10` solution at [C:\Git\Dataverse-Solution-KB\DataverseSolutionCompiler.sln](C:\Git\Dataverse-Solution-KB\DataverseSolutionCompiler.sln).
- A typed domain model and compiler contracts in [C:\Git\Dataverse-Solution-KB\src\DataverseSolutionCompiler.Domain](C:\Git\Dataverse-Solution-KB\src\DataverseSolutionCompiler.Domain).
- A bootstrap compiler core and capability registry in [C:\Git\Dataverse-Solution-KB\src\DataverseSolutionCompiler.Compiler](C:\Git\Dataverse-Solution-KB\src\DataverseSolutionCompiler.Compiler).
- Stubbed readers, emitters, apply, diff, PAC, and agent orchestration projects under [C:\Git\Dataverse-Solution-KB\src](C:\Git\Dataverse-Solution-KB\src).
- Unit, golden, integration, and end-to-end tests under [C:\Git\Dataverse-Solution-KB\tests](C:\Git\Dataverse-Solution-KB\tests).
- A copied seed corpus under [C:\Git\Dataverse-Solution-KB\fixtures\skill-corpus](C:\Git\Dataverse-Solution-KB\fixtures\skill-corpus).

## Project Map

- `src/DataverseSolutionCompiler.Domain`: canonical IR, diagnostics, requests/results, and public interfaces.
- `src/DataverseSolutionCompiler.Compiler`: capability registry, bootstrap planner, explanation service, and kernel orchestration.
- `src/DataverseSolutionCompiler.Readers.*`: unpacked XML, tracked-source, and live readback entry points.
- `src/DataverseSolutionCompiler.Emitters.*`: tracked-source and packaging output entry points.
- `src/DataverseSolutionCompiler.Apply`: direct `Dev` apply placeholder.
- `src/DataverseSolutionCompiler.Diff`: stable-overlap drift comparison bootstrap.
- `src/DataverseSolutionCompiler.Packaging.Pac`: PAC wrapper bootstrap.
- `src/DataverseSolutionCompiler.Agent`: compiler-backed orchestration entry point.
- `src/DataverseSolutionCompiler.Cli`: CLI surface with `read`, `plan`, `emit`, `apply-dev`, `readback`, `diff`, `pack`, `import`, `publish`, `check`, `doctor`, and `explain`.

## Working Commands

- `dotnet build C:\Git\Dataverse-Solution-KB\DataverseSolutionCompiler.sln`
- `dotnet test C:\Git\Dataverse-Solution-KB\DataverseSolutionCompiler.sln`

## Doc Map

- [docs/roadmap.md](C:\Git\Dataverse-Solution-KB\docs\roadmap.md) - the phase plan.
- [docs/architecture.md](C:\Git\Dataverse-Solution-KB\docs\architecture.md) - compiler shape, IR, and boundaries.
- [docs/backlog/backlog.md](C:\Git\Dataverse-Solution-KB\docs\backlog\backlog.md) - prioritized implementation work.
- [docs/acceptance/ledger.md](C:\Git\Dataverse-Solution-KB\docs\acceptance\ledger.md) - current proven status by surface.
- [docs/threads/current.md](C:\Git\Dataverse-Solution-KB\docs\threads\current.md) - live baton for the next Codex thread.
- [fixtures/skill-corpus/manifest.json](C:\Git\Dataverse-Solution-KB\fixtures\skill-corpus\manifest.json) - copied skill corpus inventory.

## Working Rules

- Prefer short, evidence-based notes over long narrative.
- Separate design intent from artifact proof.
- Record gaps honestly when a family is still unproven.
- Keep each roadmap slice release-safe for .NET 10.
