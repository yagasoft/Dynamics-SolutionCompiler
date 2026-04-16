# Component Catalog

Use this reference when you need a practical view of Dataverse solution components: what family a component belongs to, how it is usually authored, what should be read back, and where drift is meaningful.

Do not plan work from raw component numbers alone. Start from the intended app model, classify the work by component family, then decide whether the right move is direct `Dev` mutation, tracked source emission, PAC packaging, or a hybrid path.

## Quick Triage

| Family | Typical component types | Fast `Dev` mutation? | Durable release path | Readback and drift focus |
| --- | --- | --- | --- | --- |
| Schema and metadata | `1`, `2`, `3`, `9`, `13`, `14`, `18` | Yes | Yes | `EntityDefinitions`, attributes, relationships, platform-generated companions |
| Model-driven UI | `24`, `26`, `55`, `59`, `60`, `62`, `64`, `66`, `68`, `80` | Yes | Yes | `systemform`, `savedquery`, unpacked FormXml, app shell artifacts, control config |
| Code and extensibility | `61`, `90`, `91`, `92`, `93`, `95`, `201-207`, `371`, `372` | Only for rapid proof or debugging | Yes | assemblies, steps, images, endpoints, web resources, environment bindings |
| Process and service policy | `29`, `44`, `45`, `150-155`, `161`, `162`, `165` | Sometimes | Yes | activation state, runtime ids, policy ownership |
| Security and access | `16`, `17`, `20`, `21`, `63`, `70`, `71` | Sometimes | Usually | privileges, profile scope, environment-specific assignments |
| Environment and configuration | `166`, `208`, `300`, `380`, `381`, `400-402`, `430-432` | Sometimes | Yes | environment variable separation, app/config references, generated metadata |
| Reporting, templates, legacy assets | `22`, `23`, `31-39`, `210` | Rarely | Usually | template payloads, report dependencies, legacy packaging |

## Important Rule About Subcomponents

Many component types are not primary authoring surfaces. They are dependents or subcomponents emitted by Dataverse after you change an owning artifact.

Common examples:
- `4` Attribute Picklist Value
- `5` Attribute Lookup Value
- `6` View Attribute
- `7` Localized Label
- `8` Relationship Extra Condition
- `10`, `11`, `12` relationship companion records
- `17` privilege object type code
- `21` role privilege
- `22`, `23` display-string companions
- `32`, `33`, `34` report companions
- `45`, `151`, `153`, `155`, `162` rule-item companions
- `205`, `207` SDK message request or response fields

Treat these as evidence of a broader component family. Usually you should change the owning table, view, form, app, process, or plugin registration and let Dataverse materialize the subordinate components.

## Authoritative Component Universe Accounting

The repo now keeps one checked-in authoritative inventory for `solutioncomponent.componenttype` at [solutioncomponent-componenttype-inventory.json](C:\Git\Dataverse-Solution-KB\fixtures\skill-corpus\references\solutioncomponent-componenttype-inventory.json).

That inventory is the source of truth for exhaustive coverage accounting. It is built from:
- the official current Microsoft Learn `solutioncomponent.componenttype` choice list
- a local-observed supplement when the official list is incomplete

Current important exception:
- the official current Learn page still omits component type `80` `App Module`
- the repo counts `80` explicitly from local observed exports instead of pretending the official list is exhaustive on its own

Classification rules used by the inventory:
- `owner`: owner-level family that must appear in the coverage matrix and must land as either evidence-backed support or an explicit boundary
- `subordinate`: dependent companion or child type that rides with an owning family and is not backlog-tracked separately by default
- `internal-only`: lookup context, platform catalog, or bookkeeping surface that is intentionally not treated as a standalone authoring lane
- `unknown`: unresolved only until there is enough evidence to classify it honestly; do not silently omit it

## Current Exhaustive Outcomes

The exhaustive owner-family pass now makes these outcomes explicit:
- No owner-family lanes remain planned in the current audited backlog.
- Explicit owner-level boundaries rather than silent omissions: `Managed property`, `Organization settings`, `Workflow`, `Entity map`, `Hierarchy rule`, `Convert rule`, `Complex control`, and `Custom control default config`
- Supported-subset owner boundaries rather than active backlog: richer or user-owned `Visualization` breadth beyond the proven saved-query subset
- Internal-only or lookup-context surfaces rather than standalone backlog work: `Privilege`, `PrivilegeObjectTypeCode`, localized labels, report/display-string companions, duplicate or routing or SLA or convert or mobile-offline child items, and the `SdkMessage*` request or response context family

Practical rule:
- plan backlog work from owner families, not from raw component numbers
- keep subordinate and internal-only rows documented in the inventory, but do not promote them into standalone compiler slices unless new source, live, or package evidence proves they deserve that treatment

## Schema And Metadata

Typical types:
- `1` Entity
- `2` Attribute
- `3` Relationship
- `9` Option Set
- `13` Managed Property
- `14` Entity Key
- `18` Index
- `46` Entity Map
- `47` Attribute Map
- `431` Attribute Image Configuration
- `432` Entity Image Configuration

How to deal with them:
- Prefer the canonical app model as the source of truth.
- Direct metadata apply in `Dev` is appropriate for fast synthesis, proof, and readback.
- Durable delivery should still end as tracked solution source plus PAC-packed artifacts.
- Read back tables, attributes, relationships, keys, and option sets before trusting the export.
- Distinguish authored choice metadata from platform-generated state or status companions. The former are durable source surfaces; the latter are often readback-only expansions.
- Compare alternate keys by key shape and owning entity, not by transient index-build status.
- The current neutral proof is strongest for local picklist, boolean, and one true global choice contract. Alternate-key readback plumbing is supported, but unattended key creation still remains best-effort in the seed environment.

Drift signals that matter:
- missing or extra columns
- relationship shape mismatch
- option-set values diverging
- alternate-key shape diverging
- platform-generated attributes or companion relationships that are expected side effects rather than true drift

## Model-Driven UI And App Shell

Typical types:
- `24` Form
- `26` Saved Query
- `48`, `49`, `50`, `52`, `53`, `55` ribbon assets
- `59` Saved Query Visualization
- `60` System Form
- `62` Site Map
- `64` Complex Control
- `66` Custom Control
- `68` Custom Control Default Config
- `80` App Module in commonly observed exports

How to deal with them:
- Direct `Dev` mutation is often the fastest way to validate form layout, view composition, navigation, and control wiring.
- Normalize and summarize `FormXml` or unpacked UI artifacts before drawing conclusions from raw XML.
- Distinguish embedded custom controls in form `controlDescriptions` from standalone `64` or `66` or `68` component families; the former ride with the form, while the latter are separate control-definition artifacts.
- When the UI change is meant to ship, keep the durable artifact in tracked source and PAC packaging.
- Do not author ribbon or FormXML by hand unless there is no higher-level generator or source representation available.

Readback focus:
- `systemform` rows and `formxml`
- `savedquery` rows with `fetchxml` and `layoutxml`
- unpacked `FormXml` and `SavedQueries` files
- site map and app module payloads when the solution is app-shell heavy

Common drift patterns:
- control ordering noise
- default platform forms or views included because an entity root component was exported
- generated control metadata or client layout details that differ from the authored intent without changing behavior

## Code And Extensibility

Typical types:
- `61` Web Resource
- `90` Plugin Type
- `91` Plugin Assembly
- `92` SDK Message Processing Step
- `93` SDK Message Processing Step Image
- `95` Service Endpoint
- `201` SDKMessage
- `202` SDKMessageFilter
- `203` SdkMessagePair
- `204` SdkMessageRequest
- `205` SdkMessageRequestField
- `206` SdkMessageResponse
- `207` SdkMessageResponseField
- `371`, `372` Connector

How to deal with them:
- Prefer source-controlled code, binaries, manifests, or generated package assets as the authoring surface.
- Use direct `Dev` registration only for debugging, discovery, or quick proof.
- Package and register the final output through the governed solution path.
- Treat solution XML as packaging evidence, not as the place to design plugin or web-resource behavior.

Readback focus:
- assembly version and identity
- step stage, mode, message, entity filter, and secure or unsecure config
- image registration
- connector or endpoint references
- referenced web-resource paths and names

Common drift patterns:
- assembly version mismatch
- step configuration drift
- environment-specific endpoint or connector differences
- missing generated solution registration for assets created outside the target solution

## Process And Service Policy

Typical types:
- `29` Workflow
- `44` Duplicate Rule
- `45` Duplicate Rule Condition
- `150` Routing Rule
- `151` Routing Rule Item
- `152` SLA
- `153` SLA Item
- `154` Convert Rule
- `155` Convert Rule Item
- `161` Mobile Offline Profile
- `162` Mobile Offline Profile Item
- `165` Similarity Rule

How to deal with them:
- Use direct `Dev` changes when you need to understand platform behavior or validate rule semantics.
- For shipping changes, keep the authoritative definition in tracked source or in the solution artifact that your release process actually imports.
- Distinguish between definition drift and state drift. Activation state is often operational rather than authoring intent.

Readback focus:
- rule definitions
- child rule items
- active versus draft state
- ownership and scope

Observed export nuance:
- duplicate rules can surface as dedicated unpacked folders such as `duplicaterules/<hash>/duplicaterule.xml`
- the live solutioncomponent table may report modern internal ids for that same family, so prefer the actual unpacked artifact plus family-aware readback over strict dependence on legacy root-component numbers

## Security And Access

Typical types:
- `16` Privilege
- `17` PrivilegeObjectTypeCode
- `20` Role
- `21` Role Privilege
- `63` Connection Role
- `70` Field Security Profile
- `71` Field Permission

How to deal with them:
- Be conservative with direct mutation because security changes are high impact.
- Prefer tracked solution artifacts for the definitional part of the model.
- Treat environment-local assignments and business-unit placement as deployment or admin concerns unless the task explicitly says otherwise.

Readback focus:
- privilege coverage
- field-level security behavior
- role or profile scope
- connection-role definitions

Common drift patterns:
- unmanaged `Dev` privilege changes not reflected in tracked source
- environment-specific role differences that are intentional and should not be normalized away

## Environment And Configuration

Typical types:
- `166` Data Source Mapping
- `208` Import Map
- `300` Canvas App
- `380` Environment Variable Definition
- `381` Environment Variable Value
- `400` AI Project Type
- `401` AI Project
- `402` AI Configuration
- `430` Entity Analytics Configuration

How to deal with them:
- Track definitions and structure in source.
- Treat values and bindings as environment-specific unless the task explicitly wants fixed shared defaults.
- Use deployment settings or release-time substitution for values that differ by environment.
- The current skill now has a real neutral `canvasapp` proof point through `references/examples/seed-environment/`, including unpacked `CanvasApps/*.meta.xml`, preserved `.msapp` asset packaging, live readback, and drift comparison over the stable overlap.
- The copied corpus now also has a compact neutral `importmap` source-first proof point through `references/examples/seed-import-map/`, including unpacked `ImportMaps/<name>/ImportMap.xml`, child data-source mapping projection, deterministic tracked/package emission, and explicit best-effort live handling.
- The copied corpus now also has a compact neutral `entityanalyticsconfig` proof point through `references/examples/seed-entity-analytics/`, including unpacked source under `entityanalyticsconfigs/<entity>/entityanalyticsconfig.xml`, deterministic tracked/package emission, real `entityanalyticsconfigs` live projection, and stable-overlap drift on the compare-safe fields.
- The copied corpus now also has a compact neutral AI-family proof point through `references/examples/seed-ai-families/`, including source-backed `AIProjectTypes/`, `AIProjects/`, and `AIConfigurations/` parsing, deterministic tracked/package emission, and live stable-overlap proof for `AI Project Type`, `AI Project`, and `AI Configuration`.
- `Import Map`, data-source mapping, `Entity Analytics Configuration`, and the new AI slice are still thinner-proof than environment variables or canvas apps overall, so keep their scope narrow and family-aware. `AI Configuration` is anchored to the official `msdyn_aiconfiguration` table surface, while `AI Project Type` and `AI Project` still rely on the captured neutral seed as the live-shape authority until a primary-source table reference is proven.

Readback focus:
- definitions versus current values
- references to connectors, apps, or AI configurations
- analytics or app-shell configuration that may expand during export

## Reporting, Templates, And Legacy Assets

Typical types:
- `22` Display String
- `23` Display String Map
- `31` Report
- `32` Report Entity
- `33` Report Category
- `34` Report Visibility
- `35` Attachment
- `36` Email Template
- `37` Contract Template
- `38` KB Article Template
- `39` Mail Merge Template
- `210` WebWizard

How to deal with them:
- Treat these as packaging assets more than iterative metadata synthesis surfaces.
- Change them only when the request clearly includes those legacy or reporting concerns.
- Preserve dependencies and payload fidelity; direct mutation is usually not the first choice unless you are validating a specific asset.

## Decision Heuristic

When a task names a component, ask four questions:
1. What family does it belong to?
2. What is the real owning artifact: app model, form, table, code asset, rule, or environment value?
3. Is the goal rapid `Dev` proof, durable release output, or both?
4. What readback would prove the change materialized correctly?

If you answer those four questions first, the component-type number becomes supporting context instead of the entire strategy.
