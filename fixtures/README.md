# Fixtures

This folder is the executable and documentary seed corpus for the Dataverse Solution Compiler.

## Structure

- `skill-corpus/references/`
  - copied reference material from the current global `dataverse-metadata-synthesis` skill
- `skill-corpus/examples/`
  - copied neutral and project-specific example corpora used as the initial regression and acceptance seed

## Ground Rules

- Treat these fixtures as imported evidence, not as the compiler runtime.
- Python in the source skill is allowed as historical/spec evidence here, but the new compiler runtime must remain .NET-only.
- Future compiler tests should gradually replace skill-era derived outputs with compiler-generated golden outputs while preserving lineage.

## Immediate Purpose

These fixtures exist to support:

- initial architecture and roadmap grounding
- family-by-family acceptance planning
- source-reader and diff golden tests
- future cross-thread continuity without depending on the live skill folder
