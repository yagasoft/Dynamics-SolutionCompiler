# Metadata Drift Report

- Source solution: `CodexMetadataSeedProcessSecurity` `1.0.0.0`
- Readback solution: `CodexMetadataSeedProcessSecurity` `1.0.0.0`

## App Modules
- No drift

## Site Maps
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
- Missing in readback: `Codex Metadata Seed Role|prvCreateSharePointData|Global, Codex Metadata Seed Role|prvReadPluginAssembly|Global, Codex Metadata Seed Role|prvReadPluginType|Global, Codex Metadata Seed Role|prvReadSdkMessageProcessingStepImage|Global, Codex Metadata Seed Role|prvReadSdkMessageProcessingStep|Global, Codex Metadata Seed Role|prvReadSdkMessage|Global, Codex Metadata Seed Role|prvReadSharePointData|Global, Codex Metadata Seed Role|prvReadSharePointDocument|Global, Codex Metadata Seed Role|prvWriteSharePointData|Global`

## Field Security Profiles
- No drift

## Field Permissions
- No drift

## Connection Roles
- No drift

## cdxmeta_workitem
- Attributes:
  - Extra in readback: `cdxmeta_categoryid, cdxmeta_categoryidname, cdxmeta_duedate, cdxmeta_efforthours, cdxmeta_isblocked, cdxmeta_isblockedname, cdxmeta_priority, cdxmeta_stage, cdxmeta_stagename, cdxmeta_summary, cdxmeta_workitemid, cdxmeta_workitemname, createdby, createdbyname, createdbyyominame, createdon, createdonbehalfby, createdonbehalfbyname, createdonbehalfbyyominame, importsequencenumber, modifiedby, modifiedbyname, modifiedbyyominame, modifiedon, modifiedonbehalfby, modifiedonbehalfbyname, modifiedonbehalfbyyominame, overriddencreatedon, ownerid, owneridname, owneridtype, owneridyominame, owningbusinessunit, owningbusinessunitname, owningteam, owninguser, statecode, statecodename, statuscode, statuscodename, timezoneruleversionnumber, utcconversiontimezonecode, versionnumber`
- Relationships:
  - Extra in readback: `business_unit_cdxmeta_workitem, cdxmeta_category_workitem, cdxmeta_workitem_AsyncOperations, cdxmeta_workitem_BulkDeleteFailures, cdxmeta_workitem_MailboxTrackingFolders, cdxmeta_workitem_PrincipalObjectAttributeAccesses, cdxmeta_workitem_ProcessSession, cdxmeta_workitem_SyncErrors, cdxmeta_workitem_UserEntityInstanceDatas, cdxmeta_workitem_checkpoint, lk_cdxmeta_workitem_createdby, lk_cdxmeta_workitem_createdonbehalfby, lk_cdxmeta_workitem_modifiedby, lk_cdxmeta_workitem_modifiedonbehalfby, owner_cdxmeta_workitem, team_cdxmeta_workitem, user_cdxmeta_workitem`
- Forms:
  - Extra in readback: `Information [type 11], Information [type 6], Work Item Seed Main [type 2]`
- Views:
  - Extra in readback: `Active Work Items, Inactive Work Items, My Work Items, Quick Find Active Work Items, Work Item Advanced Find View, Work Item Associated View, Work Item Lookup View`
