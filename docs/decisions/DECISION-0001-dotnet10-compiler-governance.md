# DECISION-0001: .NET 10 Compiler Governance

## Status

Accepted

## Context

The solution knowledge base needs a durable governance model for a Dataverse compiler that can:

- read authoritative source artifacts
- compare against live readback
- emit release-ready solution source
- stay aligned with the approved `.NET 10` roadmap

The repository already separates planning, evidence, and handoff notes. The compiler governance needs to preserve that separation.

## Decision

We will govern the compiler using a release-pipeline-first model:

1. Canonical model first.
   Every supported family maps into a single internal representation before comparison or release.

2. Proof before expansion.
   New families are admitted only when the corpus or readback path provides honest evidence.

3. Source and readback stay distinct.
   Source documents authored intent. Readback documents platform materialization. Neither one alone is treated as complete proof when the family is known to be asymmetric.

4. Shipping happens through the release pipeline.
   Direct Dev apply is allowed for validation and synthesis, but durable delivery must flow through tracked source and package output.

5. Partial families stay partial.
   Canvas app, import map, data source mapping, and entity analytics may be tracked as best-effort until their source and readback shapes are proven.

## Consequences

- The compiler can grow safely without overclaiming parity.
- Roadmap decisions stay tied to evidence, not optimism.
- Documentation remains the contract for how families are added, compared, and shipped.
- The team can use direct Dev proof where it helps, while still converging on release-safe tracked source.

## Notes

- This decision does not change implementation code.
- It establishes the governance rule for future compiler phases, family expansion, and release orchestration.

