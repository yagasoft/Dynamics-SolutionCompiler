# Schema Detail

Use this reference when the task goes beyond basic tables and columns into choice metadata, alternate keys, managed-property-like flags, or image/key companion artifacts.

## What Counts As Schema Detail

Typical subfamilies:
- local choice columns and their emitted option-set payloads
- boolean true or false labels
- state and status companion choices
- alternate keys
- managed-property-like flags on attributes and entities
- image and mapping companion metadata

These are part of the schema family, but they are not all equally strong authoring surfaces.

## Choice Metadata

Practical split:
- local choice or boolean columns are real authored metadata
- state and status choices are usually platform companions
- global choices may be shared contracts, but the owning attribute still matters for synthesis

Best source surfaces:
- unpacked `Entities/<entity>/Entity.xml`
- local `optionset` blocks under the owning attribute
- shared `OptionSets/<name>.xml` files for true global choices
- tracked source emitted from the real attribute definition

Best readback surfaces:
- `PicklistAttributeMetadata` casts with `OptionSet` or `GlobalOptionSet`
- `BooleanAttributeMetadata` casts with `TrueOption` and `FalseOption`
- `StateAttributeMetadata` and `StatusAttributeMetadata` only when you need to understand companion state shells

Practical rule:
- compare authored local or boolean choice columns directly
- compare shared global choices both as reusable contracts by option-set name and as bound attribute usage by entity plus attribute
- inventory state and status, but do not over-call drift when the export only carries a thin shell and live readback expands the full option list

## Alternate Keys

Keys are real schema artifacts, but they behave differently from ordinary columns.

Best readback surface:
- `EntityDefinitions(LogicalName='<entity>')?$expand=Keys(...)`

Compare keys by:
- entity
- key schema or logical name
- key attribute set

Treat these as weaker drift dimensions:
- async index job references
- transient key-index status while the platform is still building the index

Current skill stance:
- key readback and normalization are supported
- unattended key creation can still be environment-dependent, so seed provisioning treats live key creation as best-effort instead of pretending symmetry

## Managed Properties And Image Flags

The current skill has partial coverage here.

Today it is safest to:
- surface embedded flags such as `IsSecured` from attribute readback
- inventory image or map companion component types when exports prove them
- avoid claiming full round-trip parity until a neutral seed proves the family

## Seed Proof

The strongest neutral proof point today is `references/examples/seed-core/`.

It now proves:
- local picklist readback through `cdxmeta_stage`
- global picklist source and readback through `cdxmeta_priorityband` plus `OptionSets/cdxmeta_priorityband.xml`
- boolean label readback through `cdxmeta_isblocked`
- source and readback normalization into per-entity `option-sets.json`
- top-level `global-option-sets/` normalization for reusable shared choices
- drift logic that compares authored choice columns while excluding state or status expansion noise
- an external-code schema shell for future alternate-key coverage rather than a finished live key proof

## Decision Rule

Use direct `Dev` apply when:
- you need to confirm how Dataverse materializes a choice column
- you want readback proof of emitted options or boolean labels

Use tracked source plus PAC packaging when:
- the choice or key change must ship as a durable release artifact
- downstream environments will receive imports instead of direct metadata mutation
