# Architecture

## Purpose

This repository documents a release-pipeline-first Dataverse solution compiler aligned to the approved `.NET 10` direction.

The compiler should not treat raw solution XML as the design surface. Instead, it should translate between:

- shipped Dataverse artifacts
- a canonical intermediate model
- tracked solution source
- readback evidence from live environments
- release-ready package output

## Core Principles

1. Canonical model first.
   All family-specific readers and emitters must map into the same internal model before compare or release.

2. Release-pipeline-first.
   Validation, drift comparison, packaging, and packaging-proof should be designed before any family is considered complete.

3. Source and readback are different proof surfaces.
   Source proves authored intent. Readback proves platform materialization. The compiler must keep them separate and comparable.

4. Family-specific handling matters.
   Dataverse does not round-trip every component family the same way, so each family needs an explicit contract and compare strategy.

5. Prefer evidence over inference.
   If a family is not proven by a neutral corpus or a supported readback path, it stays partial or best-effort.

## Compiler Layers

- Source readers
  Parse unpacked solution source, normalized source JSON, and family-specific files into the canonical model.

- Live readback adapters
  Capture Dataverse metadata from Dev or another target environment and normalize it into the same model.

- Drift engine
  Compare source and readback by family, not by raw file shape.

- Release emitters
  Convert the canonical model into tracked source and solution-package output.

- Policy layer
  Enforce release rules, proof boundaries, and family readiness gates.

## Current Family Order

The strongest proven families should be deepened first:

- schema core
- schema detail
- forms and views
- app shell and configuration
- code and extensibility
- process and service policy

Environment and configuration should be expanded cautiously, starting with the most honest neutral foothold already present in the corpus. Canvas app is the best next candidate there; import map, data source mapping, and entity analytics remain weaker.

## Risks

- Readback asymmetry may make a family look incomplete even when source is valid.
- Tenant-dependent artifacts can make neutral proof hard to reproduce.
- Overlapping family contracts can blur source intent if the canonical model is too loose.
- A release pipeline built before drift rules are stable will produce brittle output.

