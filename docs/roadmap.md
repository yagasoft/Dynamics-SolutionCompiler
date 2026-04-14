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

## Phase 5: Rebuild Fidelity And Controlled Expansion

Goal:
- make the already-supported authoring subset deeply trustworthy from real exports before adding more breadth
- expand breadth only after the reverse-authoring and rebuild loop stays honest about omissions and authorable scope

Current hardening baseline:
- `emit --layout intent-spec` now reverse-generates the supported tracked-source subset into one editable compiler-native intent-spec JSON document plus a machine-readable omission report
- classic exported solution zips are now first-class reverse-authoring input; the compiler normalizes them internally through PAC unpack before typed parsing and intent emission
- preserved form and view IDs now flow through the intent surface when reverse generation needs them for rebuild fidelity
- tracked-source remains the primary reverse-authoring path, while classic export ZIP, unpacked XML, and existing intent input can also emit normalized intent-spec output
- omission reporting now distinguishes unsupported families, unsupported shapes, platform-generated or non-authorable artifacts, and missing source fidelity instead of collapsing them into a generic partial warning

Immediate priority:
- keep new breadth paused until the supported authoring subset can reliably complete `export zip -> intent-spec -> package-inputs -> pack -> import`
- continue hardening rebuild-safe authored savedquery views, app modules, entity-only site maps, and environment variables using real export-backed proof instead of assuming every touched family is generatable

Exit criteria:
- a compact real exported solution can go straight from classic ZIP to editable intent-spec JSON and back to a successful rebuild without hidden manual unpack steps
- family coverage grows without weakening the difference between source evidence, readback evidence, and package evidence
- reverse-generation remains canonical-first, omission-typed, and partial-intent safe as family coverage grows
- the docs spine remains short enough for a fresh thread to use in one pass
