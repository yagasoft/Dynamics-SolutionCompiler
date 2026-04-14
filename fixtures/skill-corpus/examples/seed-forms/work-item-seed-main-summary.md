# Form XML Summary

- Source: `C:\Users\Ahmed Elsawalhy\.codex\skills\dataverse-metadata-synthesis\references\examples\seed-forms\normalized\entities\cdxmeta-workitem\forms\work-item-seed-main.xml`
- Form: `work-item-seed-main`

- Tabs: `3`
- Sections: `6`
- Controls: `11`
- Quick forms: `1`
- Subgrids: `1`
- Web resources: `0`
- Custom control hosts: `0`
- Custom control instances: `0`

## Header
- `cdxmeta_stage` (optionset)
- `cdxmeta_duedate` (date-or-time)
- `cdxmeta_categoryid` (lookup)

## Summary
- Section `Overview`
  - `cdxmeta_workitemname` [single-line-text] (class `4273edbd-ac1d-40d3-9fb2-095c621b552d`)
  - `cdxmeta_summary` [single-line-text] (class `4273edbd-ac1d-40d3-9fb2-095c621b552d`)
  - `cdxmeta_categoryid` [lookup] (class `270bd3db-d9af-4782-9025-509e298dec0a`)
- Section `Details`
  - `cdxmeta_details` [multiline-text] (class `e0dece4b-6fc8-4a8f-a065-082708572369`)

## Planning
- Section `Schedule`
  - `cdxmeta_duedate` [date-or-time] (class `5b773807-9fb2-42db-97c3-7a91eff8adff`)
  - `cdxmeta_efforthours` [number] (class `c6d124ca-7eda-4a60-aea9-7fb8d318b68f`)
  - `cdxmeta_priority` [number] (class `c6d124ca-7eda-4a60-aea9-7fb8d318b68f`)
- Section `Status`
  - `cdxmeta_stage` [optionset] (class `3ef39988-22bb-4f0b-bbbe-64b5a3748aee`)
  - `cdxmeta_isblocked` [two-options] (class `67fac785-cd58-4f9f-abb3-4b7ddc6ed5ed`)

## Related
- Section `Category Snapshot`
  - `cdxmeta_categoryid` [quickform] (quick forms `cdxmeta_category:5978624f-3b37-f111-88b3-0022489b9600`; class `5c5600e0-1d6e-4205-a272-be80da87fd42`; parameters `QuickForms -> cdxmeta_category:5978624f-3b37-f111-88b3-0022489b9600; ControlMode=Edit`)
- Section `Checkpoints`
  - `cdxmeta_checkpoints` [subgrid] (relationship `cdxmeta_workitem_checkpoint`; target `cdxmeta_checkpoint`; class `e7a81278-8635-4d9e-8d4d-59480b391c5b`; parameters `ViewId={500a740d-e399-42c5-9f3a-0f9c203ef9cd}; IsUserView=false; RelationshipName=cdxmeta_workitem_checkpoint; TargetEntityType=cdxmeta_checkpoint; AutoExpand=Auto; EnableQuickFind=false; EnableViewPicker=false; ViewIds={500a740d-e399-42c5-9f3a-0f9c203ef9cd}; EnableJumpBar=false; ChartGridMode=Grid; EnableChartPicker=false; RecordsPerPage=5`)

## Embedded Control Details
- `cdxmeta_categoryid` (quickform; class `5c5600e0-1d6e-4205-a272-be80da87fd42`; parameters `QuickForms -> cdxmeta_category:5978624f-3b37-f111-88b3-0022489b9600; ControlMode=Edit`; path `Related / Category Snapshot`)
- `cdxmeta_checkpoints` (subgrid; class `e7a81278-8635-4d9e-8d4d-59480b391c5b`; parameters `ViewId={500a740d-e399-42c5-9f3a-0f9c203ef9cd}; IsUserView=false; RelationshipName=cdxmeta_workitem_checkpoint; TargetEntityType=cdxmeta_checkpoint; AutoExpand=Auto; EnableQuickFind=false; EnableViewPicker=false; ViewIds={500a740d-e399-42c5-9f3a-0f9c203ef9cd}; EnableJumpBar=false; ChartGridMode=Grid; EnableChartPicker=false; RecordsPerPage=5`; path `Related / Checkpoints`)
