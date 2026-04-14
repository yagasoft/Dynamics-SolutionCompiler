# Roadmap

This roadmap follows a compiler and release-pipeline-first strategy for Dataverse solution work on `.NET 10`.

## Phase 1: Canonical Compiler Spine

Goal:
- define the canonical intermediate model
- establish source, readback, compare, and release boundaries

Exit criteria:
- solution, entity, form, view, app shell, extensibility, and configuration families all map into the same model
- the roadmap and acceptance docs can explain what is proven versus partial

## Phase 2: Proven Families

Goal:
- deepen the families already supported by the neutral corpus and current skill coverage

Priority order:
- schema core
- schema detail
- forms and views
- app shell and configuration
- code and extensibility
- process and service policy

Exit criteria:
- each of the above has a clean source/readback path and a family-aware compare story

## Phase 3: Release Pipeline

Goal:
- make the compiler usable in a governed release flow

Pipeline:
1. normalize source
2. capture live readback when available
3. compare source versus readback
4. emit tracked source
5. package release artifacts
6. publish only after validation passes

Exit criteria:
- release output is deterministic and evidence-backed
- direct Dev proof is optional for synthesis, not required for shipping

## Phase 4: Environment And Configuration

Goal:
- add the next honest environment/config slice without overstating parity

Priority order:
- canvas app
- import map
- data source mapping
- entity analytics

Exit criteria:
- each family has a documented source shape, a compare-safe field set, and a clear boundary for what remains best-effort

## Phase 5: Expansion And Hardening

Goal:
- expand breadth while keeping the compiler trustworthy
- harden the reverse-authoring loop so supported tracked-source JSON can be edited and rebuilt through compiler-native intent

Current hardening baseline:
- `emit --layout intent-spec` now reverse-generates the supported tracked-source subset into one editable compiler-native intent-spec JSON document plus a machine-readable omission report
- preserved form and view IDs now flow through the intent surface when reverse generation needs them for rebuild fidelity
- tracked-source remains the primary reverse-authoring path, while XML/ZIP and existing intent input can also emit normalized intent-spec output
- unsupported families and unsupported shapes stay out of the authoring surface and are reported explicitly rather than silently dropped

Exit criteria:
- family coverage grows without weakening the difference between source evidence, readback evidence, and package evidence
- reverse-generation remains canonical-first and partial-intent safe as family coverage grows
- the docs spine remains short enough for a fresh thread to use in one pass
