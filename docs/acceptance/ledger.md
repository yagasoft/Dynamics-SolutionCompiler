# Acceptance Ledger

This ledger records what is currently proven in the `.NET 10` repository, what is still partial, and what remains missing.

## Evidence Scale

- `done` - proven and documented.
- `partial` - useful proof exists, but boundaries remain.
- `missing` - not yet proven in a durable way.

## Ledger

| Area | Status | Evidence | Notes |
| --- | --- | --- | --- |
| Docs spine | done | `README.md`, `docs/roadmap.md`, `docs/architecture.md`, `docs/backlog/backlog.md`, `docs/acceptance/ledger.md`, `docs/threads/current.md` | Repository now has a stable planning and handoff surface. |
| .NET 10 solution skeleton | done | `DataverseSolutionCompiler.sln`, `global.json`, `Directory.Build.props`, `Directory.Packages.props`, `src/`, `tests/` | The repo builds on `net10.0` and matches the approved product shape. |
| Typed compiler contracts | done | `src/DataverseSolutionCompiler.Domain` | Canonical IR, diagnostics, requests/results, and public interfaces now exist in C#. |
| Bootstrap compiler core | done | `src/DataverseSolutionCompiler.Compiler`, `src/DataverseSolutionCompiler.Cli` | Capability registry, kernel, planner, explanation service, and CLI command surface are implemented. |
| Structural source reader proof | partial | `src/DataverseSolutionCompiler.Readers.Xml`, unit tests against `fixtures/skill-corpus/examples/seed-core/unpacked` | The XML reader inventories known families structurally, but does not yet parse each family into rich typed payloads. |
| Tracked source and package emitters | partial | `src/DataverseSolutionCompiler.Emitters.*` | Emitters return deterministic bootstrap artifact plans, but do not yet write full tracked source or package trees. |
| Stable-overlap drift | partial | `src/DataverseSolutionCompiler.Diff` and unit tests | Comparison currently works at family/logical-name overlap and still needs richer family semantics. |
| Live readback | partial | `src/DataverseSolutionCompiler.Readers.Live` | The live readback contract exists, but the Web API adapter is still a placeholder. |
| PAC packaging/import | partial | `src/DataverseSolutionCompiler.Packaging.Pac` | The wrapper contract exists, but real PAC invocation is deferred. |
| Agent orchestration | partial | `src/DataverseSolutionCompiler.Agent` | Compiler-backed orchestration exists as a bootstrap entry point, not yet a full autonomous workflow layer. |
| Seed fixture corpus | done | `fixtures/skill-corpus/manifest.json` and copied examples/references | The current Dataverse skill corpus is preserved locally as the initial regression and reference surface. |

## Acceptance Notes

- Add a new row whenever a roadmap slice becomes evidence-backed.
- Keep the ledger short enough that a new thread can read it in one pass.
- If a family is still missing proof, say so plainly rather than inferring completion.
