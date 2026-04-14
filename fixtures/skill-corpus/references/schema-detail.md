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

Best source surface:
- the captured unpacked `Entities/<entity>/Entity.xml` key container, when the export keeps keys embedded with the owning table shell

Compare keys by:
- entity
- key schema or logical name
- key attribute set

Treat these as weaker drift dimensions:
- async index job references
- transient key-index status while the platform is still building the index

Current skill stance:
- key readback, normalization, tracked-source emission, package-input emission, and JSON-intent generation are now proven for one dedicated neutral alternate-key slice
- `EntityKeyIndexStatus` is treated as operational state, not release drift
- image configuration is now proven through a dedicated neutral seed covering both entity-level and attribute-level image surfaces
- managed-property proof is now intentionally narrow: compare stable `IsCustomizable` on owning table and column artifacts rather than promoting a new standalone public family

## Managed Properties And Image Flags

The current skill has partial coverage here.

Today it is safest to:
- surface embedded flags such as `IsSecured` from attribute readback
- inventory image or map companion component types when exports prove them
- avoid claiming full round-trip parity until a neutral seed proves the family

## Seed Proof

The strongest neutral proof points today are:
- `references/examples/seed-core/` for local, boolean, and shared global choice metadata
- `references/examples/seed-alternate-key/` for dedicated alternate-key proof
- `references/examples/seed-image-config/` for entity-image plus attribute-image proof and narrow managed-flag drift

`seed-core` now proves:
- local picklist readback through `cdxmeta_stage`
- global picklist source and readback through `cdxmeta_priorityband` plus `OptionSets/cdxmeta_priorityband.xml`
- boolean label readback through `cdxmeta_isblocked`
- source and readback normalization into per-entity `option-sets.json`
- top-level `global-option-sets/` normalization for reusable shared choices
- drift logic that compares authored choice columns while excluding state or status expansion noise
- an external-code schema shell that is reused by the dedicated alternate-key seed

`seed-alternate-key` now proves:
- one authored alternate key embedded in `Entities/cdxmeta_WorkItem/Entity.xml`
- solution-aware live key discovery through component type `14`
- `Keys(...)` metadata projection into the shared canonical IR
- deterministic `tracked-source/entities/<entity>/keys.json`
- source-backed package-input preservation and derived JSON-intent key synthesis
- stable-overlap drift on entity plus key name plus normalized attribute set, while ignoring transient index status

`seed-image-config` now proves:
- one entity-level image configuration through `PrimaryImageAttribute`
- one attribute-level image configuration through image-attribute metadata
- live solution-component discovery for types `431` and `432`, with entity-scoped metadata fallback when solution scope under-reports them
- deterministic `tracked-source/entities/<entity>/image-configurations.json`
- source-backed package-input preservation with image metadata embedded in `Entities/<entity>/Entity.xml`
- stable-overlap drift on entity, scope, image linkage, `CanStoreFullImage`, `IsPrimaryImage`, and narrow `IsCustomizable` flags on the owning table and columns

## Decision Rule

Use direct `Dev` apply when:
- you need to confirm how Dataverse materializes a choice column
- you want readback proof of emitted options or boolean labels

Use tracked source plus PAC packaging when:
- the choice or key change must ship as a durable release artifact
- downstream environments will receive imports instead of direct metadata mutation
