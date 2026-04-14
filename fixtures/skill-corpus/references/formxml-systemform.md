# FormXML And `systemform`

Use this reference when working with model-driven forms, unpacked `FormXml`, or `systemform` readback.

## Where Forms Appear

- Packed export:
  - `customizations.xml`
  - `Entity -> FormXml -> forms type="<kind>" -> systemform`
- PAC-unpacked XML:
  - `Entities/<SchemaName>/FormXml/main/*.xml`
  - `Entities/<SchemaName>/FormXml/quick/*.xml`
  - `Entities/<SchemaName>/FormXml/card/*.xml`
- Live Dataverse readback:
  - `systemforms`
  - `formxml`
  - `formjson`

## Useful Form Types

Common `systemform.type` values you will usually see:
- `2`: main form
- `6`: quick form or quick view style surface
- `11`: card form

Treat the type as the delivery surface, then read the actual XML structure for behavior.

## How To Read FormXML

Primary structure:
- `<form>`
- `<tabs>`
- `<tab>`
- `<columns>`
- `<sections>`
- `<rows>`
- `<cell>`
- `<control>`
- optional `<header>` and `<footer>`

Read in this order:
1. tabs and labels
2. sections and labels
3. controls in each section
4. header controls
5. footer controls
6. control parameters such as view IDs, quick-form IDs, and relationship names

## Control Patterns

Representative control class IDs from the neutral seed corpus:
- single-line text: `{4273EDBD-AC1D-40d3-9FB2-095C621B552D}`
- multiline text: `{E0DECE4B-6FC8-4a8f-A065-082708572369}`
- lookup: `{270BD3DB-D9AF-4782-9025-509E298DEC0A}`
- option set or choice: `{3EF39988-22BB-4F0B-BBBE-64B5A3748AEE}`
- two options: `{67FAC785-CD58-4f9f-ABB3-4B7DDC6ED5ED}`
- date or time: `{5B773807-9FB2-42db-97C3-7A91EFF8ADFF}`
- number: `{C6D124CA-7EDA-4a60-AEA9-7FB8D318B68F}`
- quick form: `{5C5600E0-1D6E-4205-A272-BE80DA87FD42}`
- alternate quick-form variant: `{B68B05F0-A46D-43F8-843B-917920AF806A}`
- subgrid: `{E7A81278-8635-4d9e-8D4D-59480B391C5B}`
- reference-panel subgrid variant: `{02D4264B-47E2-4B4C-AA95-F439F3F4D458}`

These IDs are useful for classification, not as the primary design model.

## Quick Forms And Subgrids

Quick forms usually encode referenced forms in parameters:
- `QuickForms`
- `QuickFormId entityname="..."`

Subgrids usually encode:
- `ViewId`
- `RelationshipName`
- `TargetEntityType`
- optional view-picker and paging settings

When a form contains a quick form or subgrid, reason about both:
- the control that hosts it
- the referenced form or view that it points to

## `formxml` Versus `formjson`

- `formxml` is still the clearest structural artifact for tabs, sections, cells, controls, and parameters.
- `formjson` is useful as readback evidence and can expose interpreted control metadata, but it should not replace structural reading of the XML.

## Practical Heuristics

- Duplicate default form names such as `Information` are common across different form types. Use the form ID and type when ambiguity matters.
- A form can look simple by label count but still have meaningful behavior encoded in control parameters.
- Header controls matter for user-facing behavior and should be included in drift or authoring analysis.

## Example

The neutral rich form example lives at:
- `references/examples/seed-forms/unpacked/Entities/cdxmeta_WorkItem/FormXml/main/{c67be8a4-c475-4041-90e6-78e3ed79b018}.xml`

That example includes:
- multiple tabs and sections
- header controls
- a quick-form reference
- a related-record subgrid
