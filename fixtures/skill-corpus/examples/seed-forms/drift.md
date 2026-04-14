# Metadata Drift Report

- Source solution: `CodexMetadataSeedForms` `1.0.0.0`
- Readback solution: `CodexMetadataSeedForms` `1.0.0.0`

## Warnings
- Unhandled solution component types present in scope: 1 x3

## App Modules
- No drift

## Site Maps
- No drift

## Saved Query Visualizations
- No drift

## Web Resources
- No drift

## Environment Variable Definitions
- No drift

## Environment Variable Values
- No drift

## Plugin Assemblies
- No drift

## Plugin Steps
- No drift

## Plugin Step Images
- No drift

## Workflows
- No drift

## Duplicate Rules
- No drift

## Duplicate Rule Conditions
- No drift

## Routing Rules
- No drift

## Routing Rule Items
- No drift

## SLAs
- No drift

## SLA Items
- No drift

## Similarity Rules
- No drift

## Mobile Offline Profiles
- No drift

## Mobile Offline Profile Items
- No drift

## Roles
- No drift

## Role Privileges
- No drift

## Field Security Profiles
- No drift

## Field Permissions
- No drift

## Connection Roles
- No drift

## cdxmeta_category
- Attributes:
  - Extra in readback: `cdxmeta_categoryid, cdxmeta_categoryname, cdxmeta_description, createdby, createdbyname, createdbyyominame, createdon, createdonbehalfby, createdonbehalfbyname, createdonbehalfbyyominame, importsequencenumber, modifiedby, modifiedbyname, modifiedbyyominame, modifiedon, modifiedonbehalfby, modifiedonbehalfbyname, modifiedonbehalfbyyominame, overriddencreatedon, ownerid, owneridname, owneridtype, owneridyominame, owningbusinessunit, owningbusinessunitname, owningteam, owninguser, statecode, statecodename, statuscode, statuscodename, timezoneruleversionnumber, utcconversiontimezonecode, versionnumber`
- Relationships:
  - Extra in readback: `business_unit_cdxmeta_category, cdxmeta_category_AsyncOperations, cdxmeta_category_BulkDeleteFailures, cdxmeta_category_MailboxTrackingFolders, cdxmeta_category_PrincipalObjectAttributeAccesses, cdxmeta_category_ProcessSession, cdxmeta_category_SyncErrors, cdxmeta_category_UserEntityInstanceDatas, cdxmeta_category_workitem, lk_cdxmeta_category_createdby, lk_cdxmeta_category_createdonbehalfby, lk_cdxmeta_category_modifiedby, lk_cdxmeta_category_modifiedonbehalfby, owner_cdxmeta_category, team_cdxmeta_category, user_cdxmeta_category`
- Forms:
  - Missing in readback: `Category Snapshot [type quick]`
  - Extra in readback: `Category Snapshot [type 6]`
- Views:
  - Extra in readback: `Active Categories, Category Advanced Find View, Category Associated View, Category Lookup View, Inactive Categories, My Categories, Quick Find Active Categories`

## cdxmeta_checkpoint
- Attributes:
  - Extra in readback: `cdxmeta_workitemidname, createdbyname, createdbyyominame, createdonbehalfbyname, createdonbehalfbyyominame, modifiedbyname, modifiedbyyominame, modifiedonbehalfbyname, modifiedonbehalfbyyominame, owneridname, owneridtype, owneridyominame, owningbusinessunitname, statecodename, statuscodename, versionnumber`
- Relationships:
  - Extra in readback: `business_unit_cdxmeta_checkpoint, cdxmeta_checkpoint_AsyncOperations, cdxmeta_checkpoint_BulkDeleteFailures, cdxmeta_checkpoint_MailboxTrackingFolders, cdxmeta_checkpoint_PrincipalObjectAttributeAccesses, cdxmeta_checkpoint_ProcessSession, cdxmeta_checkpoint_SyncErrors, cdxmeta_checkpoint_UserEntityInstanceDatas, cdxmeta_workitem_checkpoint, lk_cdxmeta_checkpoint_createdby, lk_cdxmeta_checkpoint_createdonbehalfby, lk_cdxmeta_checkpoint_modifiedby, lk_cdxmeta_checkpoint_modifiedonbehalfby, owner_cdxmeta_checkpoint, team_cdxmeta_checkpoint, user_cdxmeta_checkpoint`
- Forms:
  - Missing in readback: `Information [type card], Information [type main], Information [type quick]`
  - Extra in readback: `Information [type 11], Information [type 2], Information [type 6]`
- Views: no drift

## cdxmeta_workitem
- Attributes:
  - Extra in readback: `cdxmeta_categoryidname, cdxmeta_isblockedname, cdxmeta_stagename, createdbyname, createdbyyominame, createdonbehalfbyname, createdonbehalfbyyominame, modifiedbyname, modifiedbyyominame, modifiedonbehalfbyname, modifiedonbehalfbyyominame, owneridname, owneridtype, owneridyominame, owningbusinessunitname, statecodename, statuscodename, versionnumber`
- Relationships:
  - Extra in readback: `business_unit_cdxmeta_workitem, cdxmeta_category_workitem, cdxmeta_workitem_AsyncOperations, cdxmeta_workitem_BulkDeleteFailures, cdxmeta_workitem_MailboxTrackingFolders, cdxmeta_workitem_PrincipalObjectAttributeAccesses, cdxmeta_workitem_ProcessSession, cdxmeta_workitem_SyncErrors, cdxmeta_workitem_UserEntityInstanceDatas, cdxmeta_workitem_checkpoint, lk_cdxmeta_workitem_createdby, lk_cdxmeta_workitem_createdonbehalfby, lk_cdxmeta_workitem_modifiedby, lk_cdxmeta_workitem_modifiedonbehalfby, owner_cdxmeta_workitem, team_cdxmeta_workitem, user_cdxmeta_workitem`
- Forms:
  - Missing in readback: `Information [type card], Information [type quick], Work Item Seed Main [type main]`
  - Extra in readback: `Information [type 11], Information [type 6], Work Item Seed Main [type 2]`
- Views: no drift
