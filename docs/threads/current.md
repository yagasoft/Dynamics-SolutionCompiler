# Current Thread

## Thread State

- Purpose: turn the approved `.NET 10` roadmap into a buildable, testable compiler baseline.
- Scope: docs, fixtures, solution skeleton, typed contracts, bootstrap adapters, CLI, and tests.
- Roadmap boundary: approved `.NET 10` direction.

## What Is Ready

- The repository has a real `.NET 10` solution at `DataverseSolutionCompiler.sln`.
- `dotnet build` passes for the full solution.
- `dotnet test` passes for unit, golden, integration, and end-to-end suites.
- The compiler baseline includes:
  - typed domain contracts and canonical IR
  - capability registry and bootstrap planner/kernel
  - CLI command surface
  - structural XML/ZIP readers
  - bootstrap tracked-source and package emitters
  - bootstrap apply, live readback, diff, PAC, and agent orchestration adapters
- The copied `dataverse-metadata-synthesis` corpus lives under `fixtures/skill-corpus` and is now flattened cleanly for future tests.

## What Still Needs Attention

- Deepen `Readers.Xml` from structural inventory into real typed family parsing for the strongest proven families first.
- Turn the tracked-source and package emitters into real file writers with deterministic outputs.
- Upgrade the drift comparer from family/logical-name overlap into family-aware semantic comparison.
- Replace placeholder live readback and PAC execution with real adapters once the core source/emit path is stable.
- Keep the skill corpus fixture manifest in sync if the source skill evolves.

## Handoff Rule

- Future updates should preserve the distinction between planning, evidence, and acceptance.
- Do not mix schema-proof, packaging-proof, and project-specific examples in the same note unless the thread explicitly calls for it.
- When implementing new capability slices, update `docs/acceptance/ledger.md` and this file in the same change.
