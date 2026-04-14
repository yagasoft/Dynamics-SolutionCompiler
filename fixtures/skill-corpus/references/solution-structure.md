# Solution Structure

Use this reference when you need to interpret exported or unpacked Dataverse solution metadata.

## Core Files

- `solution.xml`
  - solution manifest
  - unique name, version, publisher, managed flag
  - root components and high-level package metadata
- `customizations.xml`
  - entity metadata
  - inline `FormXml` and `SavedQueries` in packed exports
  - relationship, attribute, and other metadata payloads
- PAC-unpacked XML
  - `Other/Solution.xml`
  - `Other/Customizations.xml`
  - `Entities/<SchemaName>/Entity.xml`
  - `Entities/<SchemaName>/FormXml/<kind>/*.xml`
  - `Entities/<SchemaName>/SavedQueries/*.xml`

## Root Components

Root components tell you what the solution explicitly anchors, not always the full emitted artifact family.

Use [component-catalog.md](component-catalog.md) when you need the practical handling guidance behind a component type rather than just its number.

Common component types you will often care about:
- `1`: entity
- `2`: attribute
- `26`: saved query or system view
- `60`: system form
- `61`: web resource
- `62`: site map
- `80`: app module in many observed solution exports

Relationship component types exist too, but the exact type varies by relationship kind. Do not assume every relationship uses one shared component type in every export.
The analyzer scripts map official `solutioncomponent.componenttype` labels where available and preserve unknown values instead of inventing ownership or semantics.

Important behavior:
- an entity-root solution may still emit dependent forms, views, attributes, and relationships in the exported metadata
- unpacked source may split those artifacts into separate files even when the entity manifest shows placeholder nodes

## Layering

Think in layers, not one monolith:
- baseline solution metadata
- generated or synthesized metadata layer
- app-specific assets such as web resources or plugins
- environment-local readback and proof artifacts

In unmanaged `Dev`, layers can coexist and be iterated quickly. In governed release paths, layered solutions should become tracked source and packaged artifacts with an intentional import order.

## Reading Heuristics

When analyzing a solution:
1. Start with the manifest in `solution.xml`.
2. Count root components and identify which entities are anchored.
3. Inspect entity metadata, forms, and views together.
4. Check whether forms and views are inline in `customizations.xml` or split into unpacked files.
5. Do not confuse default platform-generated artifacts with requested business changes.

## Practical Signals

- If only entities appear as root components, the export can still contain more UI surface than the original request named.
- If unpacked `Entity.xml` shows empty `FormXml` or `SavedQueries` nodes, check sibling folders before assuming the entity has no forms or views.
- If a solution contains little business metadata but many non-entity root components, it may be acting as a packaging or application shell rather than a schema-heavy solution.
