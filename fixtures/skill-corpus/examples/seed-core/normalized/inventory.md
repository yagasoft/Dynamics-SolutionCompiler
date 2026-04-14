# CodexMetadataSeedCore

- Layout: `xml`
- Version: `1.0.0.0`
- Publisher prefix: `cdxmeta`
- Entities: `2`
- Option sets: `7`
- Global option sets: `1`
- Entity keys: `0`
- Root components: `3`
- App modules: `0`
- Site maps: `0`
- Saved query visualizations: `0`
- Complex controls: `0`
- Web resources: `0`
- Custom controls: `0`
- Custom control default configs: `0`
- Environment variables: `0`
- Plugin assemblies: `0`
- Plugin steps: `0`
- Plugin step images: `0`
- Workflows: `0`
- Duplicate rules: `0`
- Routing rules: `0`
- SLAs: `0`
- Similarity rules: `0`
- Mobile offline profiles: `0`
- Roles: `0`
- Field security profiles: `0`
- Connection roles: `0`

## Root Component Families
- Schema and Metadata: `3`

## Root Components
- Type `1` (Entity): `2`
- Type `9` (Option Set): `1`

## Entities
- `cdxmeta_category`: `19` attributes, `2` option sets, `0` keys, `4` forms, `7` views, `0` visualizations, ribbon actions `0`, ribbon commands `0`, ribbon buttons `0`
  Option set `statecode`: type `state`, global `None`, options `0`
  Option set `statuscode`: type `status`, global `None`, options `0`
  Form `Information`: tabs `1`, sections `4`, controls `4`, embedded hosts `0`, custom controls `0`
  Form `Information`: tabs `1`, sections `1`, controls `2`, embedded hosts `0`, custom controls `0`
  Form `Category Snapshot`: tabs `1`, sections `1`, controls `2`, embedded hosts `0`, custom controls `0`
  Form `Information`: tabs `1`, sections `1`, controls `2`, embedded hosts `0`, custom controls `0`
  View `My Categories`: columns `0`
  View `Active Categories`: columns `2`
  View `Quick Find Active Categories`: columns `2`
  View `Inactive Categories`: columns `2`
  View `Category Advanced Find View`: columns `2`
  View `Category Lookup View`: columns `2`
  View `Category Associated View`: columns `2`
- `cdxmeta_workitem`: `28` attributes, `5` option sets, `0` keys, `3` forms, `7` views, `0` visualizations, ribbon actions `0`, ribbon commands `0`, ribbon buttons `0`
  Option set `cdxmeta_isblocked`: type `boolean`, global `None`, options `2`, labels `Yes, No`
  Option set `cdxmeta_priorityband`: type `picklist`, global `True`, options `3`, labels `Low, Medium, High`
  Option set `cdxmeta_stage`: type `picklist`, global `None`, options `3`, labels `Planned, Active, Done`
  Option set `statecode`: type `state`, global `None`, options `0`
  Option set `statuscode`: type `status`, global `None`, options `0`
  Form `Information`: tabs `1`, sections `4`, controls `4`, embedded hosts `0`, custom controls `0`
  Form `Work Item Seed Main`: tabs `3`, sections `6`, controls `11`, embedded hosts `0`, custom controls `0`
  Form `Information`: tabs `1`, sections `1`, controls `2`, embedded hosts `0`, custom controls `0`
  View `Inactive Work Items`: columns `2`
  View `My Work Items`: columns `0`
  View `Active Work Items`: columns `2`
  View `Quick Find Active Work Items`: columns `2`
  View `Work Item Advanced Find View`: columns `2`
  View `Work Item Lookup View`: columns `2`
  View `Work Item Associated View`: columns `2`

## Option Sets
- `cdxmeta_category.statecode`: type `state`, global `None`, options `0`
- `cdxmeta_category.statuscode`: type `status`, global `None`, options `0`
- `cdxmeta_workitem.cdxmeta_isblocked`: type `boolean`, global `None`, options `2`, labels `Yes, No`
- `cdxmeta_workitem.cdxmeta_priorityband`: type `picklist`, global `True`, options `3`, labels `Low, Medium, High`
- `cdxmeta_workitem.cdxmeta_stage`: type `picklist`, global `None`, options `3`, labels `Planned, Active, Done`
- `cdxmeta_workitem.statecode`: type `state`, global `None`, options `0`
- `cdxmeta_workitem.statuscode`: type `status`, global `None`, options `0`

## Global Option Sets
- `cdxmeta_priorityband`: type `picklist`, options `3`, labels `Low, Medium, High`
