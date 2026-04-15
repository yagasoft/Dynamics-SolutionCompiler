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
- `intent-spec.json` now also supports `sourceBackedArtifacts[]`, allowing broader rebuildable families to round-trip through staged XML or asset evidence inside the same authoring document
- classic exported solution zips are now first-class reverse-authoring input; the compiler normalizes them internally through PAC unpack before typed parsing and intent emission
- preserved form and view IDs now flow through the intent surface when reverse generation needs them for rebuild fidelity
- structured `main`, `quick`, and `card` forms plus supported `field`, `quickView`, and `subgrid` controls now reverse-generate and rebuild through the intent surface for the supported subset, with unsupported form shapes falling back to explicit source-backed handling
- rebuild-safe authored saved-query visualizations now reverse-generate structurally from both unpacked source and classic export ZIPs when the chart metadata is complete
- supported app-shell detail such as `appSettings` plus `entity` / `url` / `webResource` site map subareas now also survives reverse generation in structured form, while staged web resources remain explicit source-backed artifacts
- tracked-source remains the primary reverse-authoring path, while classic export ZIP, unpacked XML, and existing intent input can also emit normalized intent-spec output
- omission reporting now distinguishes unsupported families, unsupported shapes, platform-generated or non-authorable artifacts, missing source fidelity, and staged source-backed artifacts instead of collapsing them into a generic partial warning
- the supported authoring subset now has a compact live export/delete/reimport proof that includes a custom table, custom column, main form, rebuild-safe authored savedquery view, entity-only app module plus site map, and environment variable definition plus current value
- broader touched families can now reverse-generate and rebuild through hybrid source-backed intent even when they are not yet fully structured-authoring or live-reimport proven end to end
- hybrid package rebuild now overlays staged web-resource payload assets as well as metadata files, closing a real app-shell rebuild gap for reverse-generated source-backed intent

Immediate priority:
- keep future authoring expansion on the same proof bar: `export zip -> intent-spec -> package-inputs -> pack -> import/publish` plus honest omission typing
- live-prove representative source-backed families next so the wider rebuildability program is not only package-level but import-level where the family surface is credible, starting with the newer advanced UI and broader app-shell subset
- resume controlled breadth only after new families can meet the same export-backed rebuild standard instead of falling back to “touched means generatable”

Exit criteria:
- a compact real exported solution can go straight from classic ZIP to editable intent-spec JSON and back to a successful rebuild without hidden manual unpack steps
- hybrid source-backed intent can carry broader rebuildable families through the same loop without silent loss
- family coverage grows without weakening the difference between source evidence, readback evidence, and package evidence
- reverse-generation remains canonical-first, omission-typed, and partial-intent safe as family coverage grows
- the docs spine remains short enough for a fresh thread to use in one pass
