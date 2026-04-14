# Form XML Summary

- Tabs: `3`
- Sections: `6`
- Controls: `11`
- Quick forms: `1`
- Subgrids: `1`
- Web resources: `0`

## Header
- `cdxmeta_stage` (optionset)
- `cdxmeta_duedate` (date-or-time)
- `cdxmeta_categoryid` (lookup)

## summary
- Section `overview`
  - `cdxmeta_workitemname` [single-line-text]
  - `cdxmeta_summary` [single-line-text]
  - `cdxmeta_categoryid` [lookup]
- Section `details`
  - `cdxmeta_details` [multiline-text]

## planning
- Section `schedule`
  - `cdxmeta_duedate` [date-or-time]
  - `cdxmeta_efforthours` [number]
  - `cdxmeta_priority` [number]
- Section `status`
  - `cdxmeta_stage` [optionset]
  - `cdxmeta_isblocked` [two-options]

## related
- Section `category_snapshot`
  - `cdxmeta_categoryid` [quickform] (quick forms `cdxmeta_category:5978624f-3b37-f111-88b3-0022489b9600`)
- Section `checkpoints`
  - `cdxmeta_checkpoints` [subgrid] (relationship `cdxmeta_workitem_checkpoint`; target `cdxmeta_checkpoint`)
