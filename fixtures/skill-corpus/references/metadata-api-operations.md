# Metadata API Operations

Use this reference when mapping intended Dataverse changes into API calls or when explaining what artifacts a change requires.

## Core Principle

Create and update metadata from the intended app model first, then verify the emitted Dataverse artifacts through export and readback.

Use [component-catalog.md](component-catalog.md) when the requested change touches non-schema families such as app shell, security, plugins, web resources, or environment configuration.

## Tables

Typical create path:
- `POST EntityDefinitions`
- payload: `EntityMetadata`
- include primary name attribute metadata
- include `MSCRM.SolutionUniqueName` when the table should be created inside a specific unmanaged solution

Typical readback path:
- `GET EntityDefinitions(LogicalName='<logical-name>')`
- capture schema, entity set name, primary attributes, and object type code

## Columns

Typical create path:
- `POST EntityDefinitions(LogicalName='<entity>')/Attributes`
- payload type depends on the column kind:
  - `StringAttributeMetadata`
  - `MemoAttributeMetadata`
  - `IntegerAttributeMetadata`
  - `DecimalAttributeMetadata`
  - `BooleanAttributeMetadata`
  - `PicklistAttributeMetadata`
  - `DateTimeAttributeMetadata`
  - `LookupAttributeMetadata`

Typical readback path:
- `GET EntityDefinitions(LogicalName='<entity>')/Attributes`

## Choice Columns And Alternate Keys

Choice and key operations need narrower metadata surfaces than the base attribute list.

Practical readback paths:
- local or global choice columns:
  - `GET EntityDefinitions(LogicalName='<entity>')/Attributes/Microsoft.Dynamics.CRM.PicklistAttributeMetadata?...`
- shared global choices:
  - `POST GlobalOptionSetDefinitions`
  - then bind the consuming picklist attribute through `GlobalOptionSet@odata.bind`
- boolean labels:
  - `GET EntityDefinitions(LogicalName='<entity>')/Attributes/Microsoft.Dynamics.CRM.BooleanAttributeMetadata?...`
- alternate keys:
  - `GET EntityDefinitions(LogicalName='<entity>')?$expand=Keys(...)`

Practical rule:
- use typed metadata casts for choice readback so you get `OptionSet`, `GlobalOptionSet`, `TrueOption`, and `FalseOption`
- if a choice is intended to be shared across columns, treat the global choice contract and the consuming attribute as two linked artifacts, not one
- compare alternate keys by schema plus `KeyAttributes`
- treat key-index status as operational detail rather than durable authoring intent

Current best-effort note:
- the current skill uses live key readback, but unattended Web API key creation can still be environment-dependent
- when the create path is unavailable, keep key reasoning source-first or readback-first instead of faking full round-trip parity

## Relationships

Typical one-to-many path:
- `POST RelationshipDefinitions`
- payload: `OneToManyRelationshipMetadata`
- include the lookup attribute definition when creating the relationship

Typical readback path:
- `GET EntityDefinitions(LogicalName='<entity>')/OneToManyRelationships`
- also inspect `ManyToOneRelationships` and `ManyToManyRelationships` as needed

## Forms And Views

Forms and views are standard Dataverse rows, not only metadata-definition payloads.

Typical operations:
- forms:
  - create or update `systemforms`
  - important fields: `formid`, `name`, `type`, `formxml`, `formjson`, `objecttypecode`
- views:
  - create or update `savedqueries`
  - important fields: `savedqueryid`, `name`, `fetchxml`, `layoutxml`, `returnedtypecode`, `querytype`

## Publish

After schema, form, or view mutation:
- publish before trusting readback or exports
- `pac solution publish --environment <url>` is the simplest repeatable path

Direct platform messages such as `PublishAllXml` or targeted publish requests are also valid, but the PAC command is usually the cleanest shared workflow.

## Solution Registration

Two important rules:
- `MSCRM.SolutionUniqueName` helps register newly created metadata into an unmanaged solution at creation time
- existing components often still need explicit solution-component registration if they were created outside the target solution or materialized as dependents later

Representative registration paths:
- `pac solution add-solution-component --solutionUniqueName <solution> --component <id-or-schema> --componentType <type>`
- Dataverse `AddSolutionComponent` request if you are operating directly through the API surface

This matters especially for:
- system forms
- saved queries
- components created as follow-on side effects after table creation

## Readback Strategy

Good readback captures:
- entity metadata
- attributes
- relationships
- `systemform`
- `savedquery`

If the task is drift-focused, try to scope readback to the solution and entity set you actually care about, then explain any fallback if strict solution scoping is unavailable.

## Practical Decision Rule

Direct Dev metadata mutation is appropriate when:
- you need proof fast
- you will export, normalize, and inspect the emitted artifacts

Tracked source emission and PAC packaging are appropriate when:
- the change must become a durable release artifact
- the solution will move beyond `Dev`
- import order, settings, or solution check matter
