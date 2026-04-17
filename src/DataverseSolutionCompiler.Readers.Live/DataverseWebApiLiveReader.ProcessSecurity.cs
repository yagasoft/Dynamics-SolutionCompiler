using System.Text.Json.Nodes;
using DataverseSolutionCompiler.Domain.Diagnostics;
using DataverseSolutionCompiler.Domain.Model;

namespace DataverseSolutionCompiler.Readers.Live;

internal sealed partial class DataverseWebApiLiveReader
{
    private async Task ReadProcessPolicyFamiliesAsync(
        SolutionComponentScope scope,
        IReadOnlySet<ComponentFamily> requestedFamilies,
        ICollection<FamilyArtifact> artifacts,
        ICollection<CompilerDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        if (requestedFamilies.Contains(ComponentFamily.Workflow) && scope.WorkflowIds.Count > 0)
        {
            try
            {
                var rows = await GetCollectionAsync(
                    $"workflows?$select=workflowid,uniquename,name,description,category,mode,scope,ondemand,primaryentity,primaryentitylogicalname,trigger_message,messagename,workflowkind,clientdata,xaml,actionmetadata,businessprocesstype,processorder&$filter={BuildGuidFilter("workflowid", scope.WorkflowIds)}",
                    cancellationToken).ConfigureAwait(false);

                var workflowRows = rows.OfType<JsonObject>().ToArray();
                var stageRowsByWorkflowId = new Dictionary<Guid, List<JsonObject>>();
                var businessProcessFlowIds = workflowRows
                    .Where(IsLiveBusinessProcessFlowRow)
                    .Select(row => GetGuid(row, "workflowid"))
                    .Where(id => id.HasValue)
                    .Select(id => id!.Value)
                    .Distinct()
                    .ToArray();
                if (businessProcessFlowIds.Length > 0)
                {
                    try
                    {
                        var processStageFilter = string.Join(
                            " or ",
                            businessProcessFlowIds.Select(id => $"processid/workflowid eq {FormatGuid(id)}"));
                        var stageRows = await GetCollectionAsync(
                            $"processstages?$select=processstageid,stagename,_processid_value&$filter={processStageFilter}",
                            cancellationToken).ConfigureAwait(false);
                        foreach (var stageRow in stageRows.OfType<JsonObject>())
                        {
                            var workflowId = GetGuid(stageRow, "_processid_value", "processid");
                            if (!workflowId.HasValue)
                            {
                                continue;
                            }

                            if (!stageRowsByWorkflowId.TryGetValue(workflowId.Value, out var entries))
                            {
                                entries = [];
                                stageRowsByWorkflowId[workflowId.Value] = entries;
                            }

                            entries.Add(stageRow);
                        }
                    }
                    catch (Exception exception) when (exception is DataverseWebApiException or Azure.Identity.AuthenticationFailedException)
                    {
                        diagnostics.Add(CreateFamilyFailureDiagnostic(ComponentFamily.Workflow, "processstages", exception));
                    }
                }

                foreach (var row in workflowRows)
                {
                    var workflowId = GetGuid(row, "workflowid");
                    var artifact = CreateWorkflowArtifact(
                        row,
                        workflowId.HasValue && stageRowsByWorkflowId.TryGetValue(workflowId.Value, out var stageRows)
                            ? stageRows
                            : null);
                    if (artifact is not null)
                    {
                        artifacts.Add(artifact);
                    }
                }
            }
            catch (Exception exception) when (exception is DataverseWebApiException or Azure.Identity.AuthenticationFailedException)
            {
                diagnostics.Add(CreateFamilyFailureDiagnostic(ComponentFamily.Workflow, "workflows", exception));
            }
        }

        var duplicateRulesById = new Dictionary<Guid, DuplicateRuleContext>();
        if (ShouldReadAny(requestedFamilies, ComponentFamily.DuplicateRule, ComponentFamily.DuplicateRuleCondition)
            && scope.DuplicateRuleIds.Count > 0)
        {
            try
            {
                var rows = await GetCollectionAsync(
                    $"duplicaterules?$select=duplicateruleid,uniquename,name,description,baseentityname,matchingentityname,iscasesensitive,excludeinactiverecords,statecode,statuscode,componentstate&$filter={BuildGuidFilter("duplicateruleid", scope.DuplicateRuleIds)}",
                    cancellationToken).ConfigureAwait(false);

                foreach (var row in rows.OfType<JsonObject>())
                {
                    var context = CreateDuplicateRuleContext(row);
                    if (context is null)
                    {
                        continue;
                    }

                    duplicateRulesById[context.Id] = context;
                    if (requestedFamilies.Contains(ComponentFamily.DuplicateRule))
                    {
                        artifacts.Add(CreateDuplicateRuleArtifact(context));
                    }
                }
            }
            catch (Exception exception) when (exception is DataverseWebApiException or Azure.Identity.AuthenticationFailedException)
            {
                diagnostics.Add(CreateFamilyFailureDiagnostic(ComponentFamily.DuplicateRule, "duplicaterules", exception));
            }
        }

        if (requestedFamilies.Contains(ComponentFamily.DuplicateRuleCondition) && duplicateRulesById.Count > 0)
        {
            try
            {
                var rows = await GetCollectionAsync(
                    $"duplicateruleconditions?$select=duplicateruleconditionid,baseattributename,matchingattributename,operatorcode,ignoreblankvalues,_duplicateruleid_value&$filter={BuildGuidFilter("_duplicateruleid_value", duplicateRulesById.Keys)}",
                    cancellationToken).ConfigureAwait(false);

                foreach (var row in rows.OfType<JsonObject>())
                {
                    var artifact = CreateDuplicateRuleConditionArtifact(row, duplicateRulesById);
                    if (artifact is not null)
                    {
                        artifacts.Add(artifact);
                    }
                }
            }
            catch (Exception exception) when (exception is DataverseWebApiException or Azure.Identity.AuthenticationFailedException)
            {
                diagnostics.Add(CreateFamilyFailureDiagnostic(ComponentFamily.DuplicateRuleCondition, "duplicateruleconditions", exception));
            }
        }

        var routingRulesById = new Dictionary<Guid, RoutingRuleContext>();
        if (ShouldReadAny(requestedFamilies, ComponentFamily.RoutingRule, ComponentFamily.RoutingRuleItem)
            && scope.RoutingRuleIds.Count > 0)
        {
            try
            {
                var rows = await GetCollectionAsync(
                    $"routingrules?$select=routingruleid,name,description,_workflowid_value,workflow_id,statecode,statuscode,componentstate&$filter={BuildGuidFilter("routingruleid", scope.RoutingRuleIds)}",
                    cancellationToken).ConfigureAwait(false);

                foreach (var row in rows.OfType<JsonObject>())
                {
                    var context = CreateRoutingRuleContext(row);
                    if (context is null)
                    {
                        continue;
                    }

                    routingRulesById[context.Id] = context;
                    if (requestedFamilies.Contains(ComponentFamily.RoutingRule))
                    {
                        artifacts.Add(CreateRoutingRuleArtifact(context));
                    }
                }
            }
            catch (Exception exception) when (exception is DataverseWebApiException or Azure.Identity.AuthenticationFailedException)
            {
                diagnostics.Add(CreateFamilyFailureDiagnostic(ComponentFamily.RoutingRule, "routingrules", exception));
            }
        }

        if (requestedFamilies.Contains(ComponentFamily.RoutingRuleItem) && routingRulesById.Count > 0)
        {
            try
            {
                var rows = await GetCollectionAsync(
                    $"routingruleitems?$select=routingruleitemid,name,description,conditionxml,sequencenumber,_routingruleid_value,_routedqueueid_value,_assignobjectid_value&$filter={BuildGuidFilter("_routingruleid_value", routingRulesById.Keys)}",
                    cancellationToken).ConfigureAwait(false);

                foreach (var row in rows.OfType<JsonObject>())
                {
                    var artifact = CreateRoutingRuleItemArtifact(row, routingRulesById);
                    if (artifact is not null)
                    {
                        artifacts.Add(artifact);
                    }
                }
            }
            catch (Exception exception) when (exception is DataverseWebApiException or Azure.Identity.AuthenticationFailedException)
            {
                diagnostics.Add(CreateFamilyFailureDiagnostic(ComponentFamily.RoutingRuleItem, "routingruleitems", exception));
            }
        }

        var mobileOfflineProfilesById = new Dictionary<Guid, MobileOfflineProfileContext>();
        if (ShouldReadAny(requestedFamilies, ComponentFamily.MobileOfflineProfile, ComponentFamily.MobileOfflineProfileItem)
            && scope.MobileOfflineProfileIds.Count > 0)
        {
            try
            {
                var rows = await GetCollectionAsync(
                    $"mobileofflineprofiles?$select=mobileofflineprofileid,name,description,isvalidated,componentstate,ismanaged&$filter={BuildGuidFilter("mobileofflineprofileid", scope.MobileOfflineProfileIds)}",
                    cancellationToken).ConfigureAwait(false);

                foreach (var row in rows.OfType<JsonObject>())
                {
                    var context = CreateMobileOfflineProfileContext(row);
                    if (context is null)
                    {
                        continue;
                    }

                    mobileOfflineProfilesById[context.Id] = context;
                    if (requestedFamilies.Contains(ComponentFamily.MobileOfflineProfile))
                    {
                        artifacts.Add(CreateMobileOfflineProfileArtifact(context));
                    }
                }
            }
            catch (Exception exception) when (exception is DataverseWebApiException or Azure.Identity.AuthenticationFailedException)
            {
                diagnostics.Add(CreateFamilyFailureDiagnostic(ComponentFamily.MobileOfflineProfile, "mobileofflineprofiles", exception));
            }
        }

        if (requestedFamilies.Contains(ComponentFamily.MobileOfflineProfileItem) && mobileOfflineProfilesById.Count > 0)
        {
            try
            {
                var rows = await GetCollectionAsync(
                    $"mobileofflineprofileitems?$select=mobileofflineprofileitemid,name,selectedentitytypecode,recorddistributioncriteria,recordsownedbyme,recordsownedbymyteam,recordsownedbymybusinessunit,profileitementityfilter,_mobileofflineprofileid_value&$filter={BuildGuidFilter("_mobileofflineprofileid_value", mobileOfflineProfilesById.Keys)}",
                    cancellationToken).ConfigureAwait(false);

                foreach (var row in rows.OfType<JsonObject>())
                {
                    var artifact = CreateMobileOfflineProfileItemArtifact(row, mobileOfflineProfilesById);
                    if (artifact is not null)
                    {
                        artifacts.Add(artifact);
                    }
                }
            }
            catch (Exception exception) when (exception is DataverseWebApiException or Azure.Identity.AuthenticationFailedException)
            {
                diagnostics.Add(CreateFamilyFailureDiagnostic(ComponentFamily.MobileOfflineProfileItem, "mobileofflineprofileitems", exception));
            }
        }
    }

    private async Task ReadSecurityFamiliesAsync(
        SolutionComponentScope scope,
        IReadOnlySet<ComponentFamily> requestedFamilies,
        ICollection<FamilyArtifact> artifacts,
        ICollection<CompilerDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        var rolesById = new Dictionary<Guid, RoleContext>();
        if (requestedFamilies.Contains(ComponentFamily.Role) && scope.RoleIds.Count > 0)
        {
            try
            {
                var rows = await GetCollectionAsync(
                    $"roles?$select=roleid,name,isinherited,componentstate,_businessunitid_value&$filter={BuildGuidFilter("roleid", scope.RoleIds)}",
                    cancellationToken).ConfigureAwait(false);

                foreach (var row in rows.OfType<JsonObject>())
                {
                    var context = CreateRoleContext(row);
                    if (context is null)
                    {
                        continue;
                    }

                    rolesById[context.Id] = context;
                    artifacts.Add(CreateRoleArtifact(context));
                }
            }
            catch (Exception exception) when (exception is DataverseWebApiException or Azure.Identity.AuthenticationFailedException)
            {
                diagnostics.Add(CreateFamilyFailureDiagnostic(ComponentFamily.Role, "roles", exception));
            }
        }

        var fieldSecurityProfilesById = new Dictionary<Guid, FieldSecurityProfileContext>();
        if (ShouldReadAny(requestedFamilies, ComponentFamily.FieldSecurityProfile, ComponentFamily.FieldPermission)
            && scope.FieldSecurityProfileIds.Count > 0)
        {
            try
            {
                var rows = await GetCollectionAsync(
                    $"fieldsecurityprofiles?$select=fieldsecurityprofileid,name,description,componentstate&$filter={BuildGuidFilter("fieldsecurityprofileid", scope.FieldSecurityProfileIds)}",
                    cancellationToken).ConfigureAwait(false);

                foreach (var row in rows.OfType<JsonObject>())
                {
                    var context = CreateFieldSecurityProfileContext(row);
                    if (context is null)
                    {
                        continue;
                    }

                    fieldSecurityProfilesById[context.Id] = context;
                    if (requestedFamilies.Contains(ComponentFamily.FieldSecurityProfile))
                    {
                        artifacts.Add(CreateFieldSecurityProfileArtifact(context));
                    }
                }
            }
            catch (Exception exception) when (exception is DataverseWebApiException or Azure.Identity.AuthenticationFailedException)
            {
                diagnostics.Add(CreateFamilyFailureDiagnostic(ComponentFamily.FieldSecurityProfile, "fieldsecurityprofiles", exception));
            }
        }

        if (requestedFamilies.Contains(ComponentFamily.FieldPermission) && fieldSecurityProfilesById.Count > 0)
        {
            try
            {
                var rows = await GetCollectionAsync(
                    $"fieldpermissions?$select=fieldpermissionid,entityname,attributelogicalname,canread,cancreate,canupdate,canreadunmasked,_fieldsecurityprofileid_value&$filter={BuildGuidFilter("_fieldsecurityprofileid_value", fieldSecurityProfilesById.Keys)}",
                    cancellationToken).ConfigureAwait(false);

                foreach (var row in rows.OfType<JsonObject>())
                {
                    var artifact = CreateFieldPermissionArtifact(row, fieldSecurityProfilesById);
                    if (artifact is not null)
                    {
                        artifacts.Add(artifact);
                    }
                }
            }
            catch (Exception exception) when (exception is DataverseWebApiException or Azure.Identity.AuthenticationFailedException)
            {
                diagnostics.Add(CreateFamilyFailureDiagnostic(ComponentFamily.FieldPermission, "fieldpermissions", exception));
            }
        }

        if (requestedFamilies.Contains(ComponentFamily.ConnectionRole) && scope.ConnectionRoleIds.Count > 0)
        {
            try
            {
                var rows = await GetCollectionAsync(
                    $"connectionroles?$select=connectionroleid,name,description,category,componentstate,statecode,statuscode&$filter={BuildGuidFilter("connectionroleid", scope.ConnectionRoleIds)}",
                    cancellationToken).ConfigureAwait(false);

                foreach (var row in rows.OfType<JsonObject>())
                {
                    var artifact = CreateConnectionRoleArtifact(row);
                    if (artifact is not null)
                    {
                        artifacts.Add(artifact);
                    }
                }
            }
            catch (Exception exception) when (exception is DataverseWebApiException or Azure.Identity.AuthenticationFailedException)
            {
                diagnostics.Add(CreateFamilyFailureDiagnostic(ComponentFamily.ConnectionRole, "connectionroles", exception));
            }
        }

        if (requestedFamilies.Contains(ComponentFamily.RolePrivilege))
        {
            diagnostics.Add(new CompilerDiagnostic(
                "live-readback-role-privilege-best-effort",
                DiagnosticSeverity.Info,
                "RolePrivilege live readback remains best-effort in the neutral security slice because stable privilege rows are not consistently exposed in unattended readback.",
                "roleprivileges"));
        }
    }

    private static DuplicateRuleContext? CreateDuplicateRuleContext(JsonObject row)
    {
        var id = GetGuid(row, "duplicateruleid");
        var displayName = GetString(row, "name");
        if (!id.HasValue || string.IsNullOrWhiteSpace(displayName))
        {
            return null;
        }

        var logicalName = NormalizeLogicalName(GetString(row, "uniquename")) ?? NormalizeLogicalName(displayName);
        if (string.IsNullOrWhiteSpace(logicalName))
        {
            return null;
        }

        return new DuplicateRuleContext(
            id.Value,
            logicalName!,
            displayName,
            GetString(row, "description"),
            NormalizeLogicalName(GetString(row, "baseentityname")),
            NormalizeLogicalName(GetString(row, "matchingentityname")),
            NormalizeBoolean(GetString(row, "iscasesensitive")),
            NormalizeBoolean(GetString(row, "excludeinactiverecords")));
    }

    private static FamilyArtifact CreateDuplicateRuleArtifact(DuplicateRuleContext context)
    {
        var summaryJson = SerializeJson(new
        {
            context.LogicalName,
            context.BaseEntityName,
            context.MatchingEntityName,
            context.IsCaseSensitive,
            context.ExcludeInactiveRecords
        });

        return new FamilyArtifact(
            ComponentFamily.DuplicateRule,
            context.LogicalName,
            context.DisplayName,
            $"duplicaterules/{context.LogicalName}",
            EvidenceKind.Readback,
            CreateProperties(
                (ArtifactPropertyKeys.Description, context.Description),
                (ArtifactPropertyKeys.BaseEntityName, context.BaseEntityName),
                (ArtifactPropertyKeys.MatchingEntityName, context.MatchingEntityName),
                (ArtifactPropertyKeys.IsCaseSensitive, context.IsCaseSensitive),
                (ArtifactPropertyKeys.ExcludeInactiveRecords, context.ExcludeInactiveRecords),
                (ArtifactPropertyKeys.SummaryJson, summaryJson),
                (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(summaryJson))));
    }

    private static FamilyArtifact? CreateDuplicateRuleConditionArtifact(JsonObject row, IReadOnlyDictionary<Guid, DuplicateRuleContext> rulesById)
    {
        var parentRuleId = GetGuid(row, "_duplicateruleid_value", "rule_id");
        if (!parentRuleId.HasValue || !rulesById.TryGetValue(parentRuleId.Value, out var parentRule))
        {
            return null;
        }

        var baseAttributeName = NormalizeLogicalName(GetString(row, "baseattributename"));
        var matchingAttributeName = NormalizeLogicalName(GetString(row, "matchingattributename"));
        var operatorCode = GetString(row, "operatorcode");
        var logicalName = $"{parentRule.LogicalName}|{baseAttributeName ?? "base"}|{matchingAttributeName ?? "matching"}|{operatorCode ?? "0"}";
        var summaryJson = SerializeJson(new
        {
            logicalName,
            parentDuplicateRule = parentRule.LogicalName,
            baseAttributeName,
            matchingAttributeName,
            operatorCode,
            ignoreBlankValues = NormalizeBoolean(GetString(row, "ignoreblankvalues"))
        });

        return new FamilyArtifact(
            ComponentFamily.DuplicateRuleCondition,
            logicalName,
            $"{baseAttributeName ?? "source"} -> {matchingAttributeName ?? "target"}",
            $"duplicateruleconditions/{logicalName}",
            EvidenceKind.Readback,
            CreateProperties(
                (ArtifactPropertyKeys.ParentDuplicateRuleLogicalName, parentRule.LogicalName),
                (ArtifactPropertyKeys.BaseAttributeName, baseAttributeName),
                (ArtifactPropertyKeys.MatchingAttributeName, matchingAttributeName),
                (ArtifactPropertyKeys.OperatorCode, operatorCode),
                (ArtifactPropertyKeys.IgnoreBlankValues, NormalizeBoolean(GetString(row, "ignoreblankvalues"))),
                (ArtifactPropertyKeys.SummaryJson, summaryJson),
                (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(summaryJson))));
    }

    private static RoutingRuleContext? CreateRoutingRuleContext(JsonObject row)
    {
        var id = GetGuid(row, "routingruleid");
        var displayName = GetString(row, "name");
        if (!id.HasValue || string.IsNullOrWhiteSpace(displayName))
        {
            return null;
        }

        var logicalName = NormalizeLogicalName(displayName);
        if (string.IsNullOrWhiteSpace(logicalName))
        {
            return null;
        }

        return new RoutingRuleContext(
            id.Value,
            logicalName!,
            displayName,
            GetString(row, "description"),
            NormalizeGuid(GetString(row, "_workflowid_value", "workflow_id")));
    }

    private static FamilyArtifact CreateRoutingRuleArtifact(RoutingRuleContext context)
    {
        var summaryJson = SerializeJson(new
        {
            context.LogicalName,
            context.Description
        });

        return new FamilyArtifact(
            ComponentFamily.RoutingRule,
            context.LogicalName,
            context.DisplayName,
            $"routingrules/{context.LogicalName}",
            EvidenceKind.Readback,
            CreateProperties(
                (ArtifactPropertyKeys.Description, context.Description),
                (ArtifactPropertyKeys.WorkflowId, context.WorkflowId),
                (ArtifactPropertyKeys.SummaryJson, summaryJson),
                (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(summaryJson))));
    }

    private static FamilyArtifact? CreateRoutingRuleItemArtifact(JsonObject row, IReadOnlyDictionary<Guid, RoutingRuleContext> rulesById)
    {
        var parentRuleId = GetGuid(row, "_routingruleid_value", "routing_rule_id");
        if (!parentRuleId.HasValue || !rulesById.TryGetValue(parentRuleId.Value, out var parentRule))
        {
            return null;
        }

        var stableSegment = NormalizeLogicalName(GetString(row, "description", "name")) ?? "item";
        var logicalName = $"{parentRule.LogicalName}|{stableSegment}";
        var conditionXml = NormalizeConditionXml(GetString(row, "conditionxml"));
        var summaryJson = SerializeJson(new
        {
            logicalName,
            parentRoutingRule = parentRule.LogicalName,
            description = GetString(row, "description"),
            conditionXml
        });

        return new FamilyArtifact(
            ComponentFamily.RoutingRuleItem,
            logicalName,
            GetString(row, "name", "description"),
            $"routingruleitems/{logicalName}",
            EvidenceKind.Readback,
            CreateProperties(
                (ArtifactPropertyKeys.ParentRoutingRuleLogicalName, parentRule.LogicalName),
                (ArtifactPropertyKeys.Description, GetString(row, "description")),
                (ArtifactPropertyKeys.ConditionXml, conditionXml),
                (ArtifactPropertyKeys.RoutedQueueId, NormalizeGuid(GetString(row, "_routedqueueid_value", "routed_queue_id"))),
                (ArtifactPropertyKeys.AssignObjectId, NormalizeGuid(GetString(row, "_assignobjectid_value", "assign_object_id"))),
                (ArtifactPropertyKeys.SequenceNumber, GetString(row, "sequencenumber")),
                (ArtifactPropertyKeys.SummaryJson, summaryJson),
                (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(summaryJson))));
    }

    private static MobileOfflineProfileContext? CreateMobileOfflineProfileContext(JsonObject row)
    {
        var id = GetGuid(row, "mobileofflineprofileid");
        var displayName = GetString(row, "name");
        if (!id.HasValue || string.IsNullOrWhiteSpace(displayName))
        {
            return null;
        }

        var logicalName = NormalizeLogicalName(displayName);
        if (string.IsNullOrWhiteSpace(logicalName))
        {
            return null;
        }

        return new MobileOfflineProfileContext(
            id.Value,
            logicalName!,
            displayName,
            GetString(row, "description"),
            NormalizeBoolean(GetString(row, "isvalidated")));
    }

    private static FamilyArtifact CreateMobileOfflineProfileArtifact(MobileOfflineProfileContext context)
    {
        var summaryJson = SerializeJson(new
        {
            context.LogicalName,
            context.Description,
            context.IsValidated
        });

        return new FamilyArtifact(
            ComponentFamily.MobileOfflineProfile,
            context.LogicalName,
            context.DisplayName,
            $"mobileofflineprofiles/{context.LogicalName}",
            EvidenceKind.Readback,
            CreateProperties(
                (ArtifactPropertyKeys.Description, context.Description),
                (ArtifactPropertyKeys.IsValidated, context.IsValidated),
                (ArtifactPropertyKeys.SummaryJson, summaryJson),
                (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(summaryJson))));
    }

    private static FamilyArtifact? CreateMobileOfflineProfileItemArtifact(JsonObject row, IReadOnlyDictionary<Guid, MobileOfflineProfileContext> profilesById)
    {
        var parentId = GetGuid(row, "_mobileofflineprofileid_value", "profile_id");
        if (!parentId.HasValue || !profilesById.TryGetValue(parentId.Value, out var parentProfile))
        {
            return null;
        }

        var entityLogicalName = NormalizeLogicalName(GetString(row, "selectedentitytypecode", "entityschemaname"));
        if (string.IsNullOrWhiteSpace(entityLogicalName))
        {
            return null;
        }

        var logicalName = $"{parentProfile.LogicalName}|{entityLogicalName}";
        var summaryJson = SerializeJson(new
        {
            logicalName,
            parentMobileOfflineProfile = parentProfile.LogicalName,
            entityLogicalName,
            recordDistributionCriteria = GetString(row, "recorddistributioncriteria"),
            recordsOwnedByMe = NormalizeBoolean(GetString(row, "recordsownedbyme")),
            recordsOwnedByMyTeam = NormalizeBoolean(GetString(row, "recordsownedbymyteam")),
            recordsOwnedByMyBusinessUnit = NormalizeBoolean(GetString(row, "recordsownedbymybusinessunit"))
        });

        return new FamilyArtifact(
            ComponentFamily.MobileOfflineProfileItem,
            logicalName,
            GetString(row, "name") ?? entityLogicalName,
            $"mobileofflineprofileitems/{logicalName}",
            EvidenceKind.Readback,
            CreateProperties(
                (ArtifactPropertyKeys.ParentMobileOfflineProfileLogicalName, parentProfile.LogicalName),
                (ArtifactPropertyKeys.EntityLogicalName, entityLogicalName),
                (ArtifactPropertyKeys.RecordDistributionCriteria, GetString(row, "recorddistributioncriteria")),
                (ArtifactPropertyKeys.RecordsOwnedByMe, NormalizeBoolean(GetString(row, "recordsownedbyme"))),
                (ArtifactPropertyKeys.RecordsOwnedByMyTeam, NormalizeBoolean(GetString(row, "recordsownedbymyteam"))),
                (ArtifactPropertyKeys.RecordsOwnedByMyBusinessUnit, NormalizeBoolean(GetString(row, "recordsownedbymybusinessunit"))),
                (ArtifactPropertyKeys.ProfileItemEntityFilter, GetString(row, "profileitementityfilter")),
                (ArtifactPropertyKeys.SummaryJson, summaryJson),
                (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(summaryJson))));
    }

    private static RoleContext? CreateRoleContext(JsonObject row)
    {
        var id = GetGuid(row, "roleid");
        var displayName = GetString(row, "name");
        if (!id.HasValue || string.IsNullOrWhiteSpace(displayName))
        {
            return null;
        }

        var logicalName = NormalizeLogicalName(displayName);
        if (string.IsNullOrWhiteSpace(logicalName))
        {
            return null;
        }

        return new RoleContext(
            id.Value,
            logicalName!,
            displayName,
            GetString(row, "_businessunitid_value"),
            NormalizeBoolean(GetString(row, "isinherited")));
    }

    private static FamilyArtifact CreateRoleArtifact(RoleContext context)
    {
        var summaryJson = SerializeJson(new
        {
            context.LogicalName,
            context.BusinessUnitId,
            context.IsInherited
        });

        return new FamilyArtifact(
            ComponentFamily.Role,
            context.LogicalName,
            context.DisplayName,
            $"roles/{context.LogicalName}",
            EvidenceKind.Readback,
            CreateProperties(
                (ArtifactPropertyKeys.BusinessUnitId, context.BusinessUnitId),
                (ArtifactPropertyKeys.IsInherited, context.IsInherited),
                (ArtifactPropertyKeys.SummaryJson, summaryJson),
                (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(summaryJson))));
    }

    private static FieldSecurityProfileContext? CreateFieldSecurityProfileContext(JsonObject row)
    {
        var id = GetGuid(row, "fieldsecurityprofileid");
        var displayName = GetString(row, "name");
        if (!id.HasValue || string.IsNullOrWhiteSpace(displayName))
        {
            return null;
        }

        var logicalName = NormalizeLogicalName(displayName);
        if (string.IsNullOrWhiteSpace(logicalName))
        {
            return null;
        }

        return new FieldSecurityProfileContext(
            id.Value,
            logicalName!,
            displayName,
            GetString(row, "description"));
    }

    private static FamilyArtifact CreateFieldSecurityProfileArtifact(FieldSecurityProfileContext context)
    {
        var summaryJson = SerializeJson(new
        {
            context.LogicalName,
            context.Description
        });

        return new FamilyArtifact(
            ComponentFamily.FieldSecurityProfile,
            context.LogicalName,
            context.DisplayName,
            $"fieldsecurityprofiles/{context.LogicalName}",
            EvidenceKind.Readback,
            CreateProperties(
                (ArtifactPropertyKeys.Description, context.Description),
                (ArtifactPropertyKeys.SummaryJson, summaryJson),
                (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(summaryJson))));
    }

    private static FamilyArtifact? CreateFieldPermissionArtifact(JsonObject row, IReadOnlyDictionary<Guid, FieldSecurityProfileContext> profilesById)
    {
        var parentId = GetGuid(row, "_fieldsecurityprofileid_value", "profile_id");
        if (!parentId.HasValue || !profilesById.TryGetValue(parentId.Value, out var parentProfile))
        {
            return null;
        }

        var entityLogicalName = NormalizeLogicalName(GetString(row, "entityname"));
        var attributeLogicalName = NormalizeLogicalName(GetString(row, "attributelogicalname"));
        if (string.IsNullOrWhiteSpace(entityLogicalName) || string.IsNullOrWhiteSpace(attributeLogicalName))
        {
            return null;
        }

        var logicalName = $"{parentProfile.LogicalName}|{entityLogicalName}|{attributeLogicalName}";
        var summaryJson = SerializeJson(new
        {
            logicalName,
            parentFieldSecurityProfile = parentProfile.LogicalName,
            entityLogicalName,
            attributeLogicalName,
            canRead = GetString(row, "canread"),
            canCreate = GetString(row, "cancreate"),
            canUpdate = GetString(row, "canupdate"),
            canReadUnmasked = GetString(row, "canreadunmasked")
        });

        return new FamilyArtifact(
            ComponentFamily.FieldPermission,
            logicalName,
            $"{entityLogicalName}.{attributeLogicalName}",
            $"fieldpermissions/{logicalName}",
            EvidenceKind.Readback,
            CreateProperties(
                (ArtifactPropertyKeys.ParentFieldSecurityProfileLogicalName, parentProfile.LogicalName),
                (ArtifactPropertyKeys.EntityLogicalName, entityLogicalName),
                (ArtifactPropertyKeys.AttributeLogicalName, attributeLogicalName),
                (ArtifactPropertyKeys.CanRead, GetString(row, "canread")),
                (ArtifactPropertyKeys.CanCreate, GetString(row, "cancreate")),
                (ArtifactPropertyKeys.CanUpdate, GetString(row, "canupdate")),
                (ArtifactPropertyKeys.CanReadUnmasked, GetString(row, "canreadunmasked")),
                (ArtifactPropertyKeys.SummaryJson, summaryJson),
                (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(summaryJson))));
    }

    private static FamilyArtifact? CreateConnectionRoleArtifact(JsonObject row)
    {
        var displayName = GetString(row, "name");
        var logicalName = NormalizeLogicalName(displayName);
        if (string.IsNullOrWhiteSpace(logicalName))
        {
            return null;
        }

        var summaryJson = SerializeJson(new
        {
            logicalName,
            category = GetString(row, "category"),
            description = GetString(row, "description")
        });

        return new FamilyArtifact(
            ComponentFamily.ConnectionRole,
            logicalName!,
            displayName,
            $"connectionroles/{logicalName}",
            EvidenceKind.Readback,
            CreateProperties(
                (ArtifactPropertyKeys.Category, GetString(row, "category")),
                (ArtifactPropertyKeys.Description, GetString(row, "description")),
                (ArtifactPropertyKeys.SummaryJson, summaryJson),
                (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(summaryJson))));
    }

    private static FamilyArtifact? CreateWorkflowArtifact(JsonObject row, IReadOnlyCollection<JsonObject>? processStageRows = null)
    {
        var workflowId = NormalizeGuid(GetString(row, "workflowid"));
        var displayName = GetString(row, "name");
        var logicalName = NormalizeLogicalName(GetString(row, "uniquename"))
            ?? NormalizeLogicalName(displayName)
            ?? workflowId;
        if (string.IsNullOrWhiteSpace(logicalName))
        {
            return null;
        }

        var workflowKind = NormalizeLiveWorkflowKind(row);
        var primaryEntity = NormalizeLogicalName(GetString(row, "primaryentity", "primaryentitylogicalname", "primaryentitytypecode"));
        var triggerMessageName = string.Equals(workflowKind, "businessProcessFlow", StringComparison.OrdinalIgnoreCase)
            ? null
            : GetString(row, "trigger_message", "messagename");
        var clientData = NormalizeWorkflowJson(ReadWorkflowJsonValue(row, "clientdata"));
        var actionMetadataJson = NormalizeWorkflowJson(ReadWorkflowJsonValue(row, "actionmetadata"));
        var xamlHash = NormalizeWorkflowHash(GetString(row, "xamlhash"))
            ?? ComputeOptionalSignature(GetString(row, "xaml"));
        var clientDataHash = NormalizeWorkflowHash(GetString(row, "clientdatahash"))
            ?? ComputeOptionalSignature(clientData);
        var businessProcessType = GetString(row, "businessprocesstype");
        var processOrder = GetString(row, "processorder");
        var processStagesJson = BuildProcessStagesJson(processStageRows);
        var summaryJson = SerializeJson(new
        {
            logicalName,
            workflowId,
            workflowKind,
            category = GetString(row, "category"),
            mode = GetString(row, "mode"),
            scope = GetString(row, "scope"),
            onDemand = NormalizeBoolean(GetString(row, "ondemand")),
            primaryEntity,
            triggerMessageName,
            xamlHash,
            clientDataHash,
            actionMetadata = actionMetadataJson is null ? null : JsonNode.Parse(actionMetadataJson),
            businessProcessType,
            processOrder,
            processStages = processStagesJson is null ? null : JsonNode.Parse(processStagesJson)
        });

        return new FamilyArtifact(
            ComponentFamily.Workflow,
            logicalName!,
            displayName ?? logicalName,
            $"workflows/{logicalName}",
            EvidenceKind.Readback,
            CreateProperties(
                (ArtifactPropertyKeys.WorkflowId, workflowId),
                (ArtifactPropertyKeys.WorkflowKind, workflowKind),
                (ArtifactPropertyKeys.Description, GetString(row, "description")),
                (ArtifactPropertyKeys.Category, GetString(row, "category")),
                (ArtifactPropertyKeys.Mode, GetString(row, "mode")),
                (ArtifactPropertyKeys.WorkflowScope, GetString(row, "scope")),
                (ArtifactPropertyKeys.OnDemand, NormalizeBoolean(GetString(row, "ondemand"))),
                (ArtifactPropertyKeys.PrimaryEntity, primaryEntity),
                (ArtifactPropertyKeys.TriggerMessageName, triggerMessageName),
                (ArtifactPropertyKeys.XamlHash, xamlHash),
                (ArtifactPropertyKeys.ClientDataHash, clientDataHash),
                (ArtifactPropertyKeys.WorkflowActionMetadataJson, actionMetadataJson),
                (ArtifactPropertyKeys.BusinessProcessType, businessProcessType),
                (ArtifactPropertyKeys.ProcessOrder, processOrder),
                (ArtifactPropertyKeys.ProcessStagesJson, processStagesJson),
                (ArtifactPropertyKeys.SummaryJson, summaryJson),
                (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(summaryJson))));
    }

    private static string NormalizeLiveWorkflowKind(JsonObject row)
    {
        var category = GetString(row, "category");
        if (string.Equals(category, "4", StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrWhiteSpace(GetString(row, "businessprocesstype")))
        {
            return "businessProcessFlow";
        }

        var raw = GetString(row, "workflowkind", "workflow_kind", "kind");
        if (!string.IsNullOrWhiteSpace(raw))
        {
            return raw.Trim() switch
            {
                var value when value.Equals("businessProcessFlow", StringComparison.OrdinalIgnoreCase) => "businessProcessFlow",
                var value when value.Equals("bpf", StringComparison.OrdinalIgnoreCase) => "businessProcessFlow",
                var value when value.Equals("customAction", StringComparison.OrdinalIgnoreCase) => "customAction",
                var value when value.Equals("action", StringComparison.OrdinalIgnoreCase) => "customAction",
                _ => "workflow"
            };
        }

        return !string.IsNullOrWhiteSpace(ReadWorkflowJsonValue(row, "actionmetadata"))
            ? "customAction"
            : "workflow";
    }

    private static string? ReadWorkflowJsonValue(JsonObject row, params string[] names)
    {
        var node = GetProperty(row, names);
        return node switch
        {
            JsonObject or JsonArray => node.ToJsonString(),
            _ => StringValue(node)
        };
    }

    private static string? NormalizeWorkflowJson(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(raw)?.ToJsonString();
        }
        catch
        {
            return raw.Trim();
        }
    }

    private static string? NormalizeWorkflowHash(string? raw) =>
        string.IsNullOrWhiteSpace(raw) ? null : raw.Trim().ToLowerInvariant();

    private static string? ComputeOptionalSignature(string? raw) =>
        string.IsNullOrWhiteSpace(raw) ? null : ComputeSignature(raw.Replace("\r\n", "\n", StringComparison.Ordinal));

    private static bool IsLiveBusinessProcessFlowRow(JsonObject row) =>
        string.Equals(NormalizeLiveWorkflowKind(row), "businessProcessFlow", StringComparison.OrdinalIgnoreCase);

    private static string? BuildProcessStagesJson(IReadOnlyCollection<JsonObject>? processStageRows)
    {
        if (processStageRows is null || processStageRows.Count == 0)
        {
            return null;
        }

        var stages = processStageRows
            .Select(row => new
            {
                id = NormalizeGuid(GetString(row, "processstageid")),
                name = GetString(row, "stagename")
            })
            .Where(stage => !string.IsNullOrWhiteSpace(stage.id) || !string.IsNullOrWhiteSpace(stage.name))
            .OrderBy(stage => stage.name ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(stage => stage.id ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return stages.Length == 0 ? null : SerializeJson(stages);
    }

    private static string? NormalizeConditionXml(string? xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            return null;
        }

        try
        {
            return System.Xml.Linq.XElement.Parse(xml).ToString(System.Xml.Linq.SaveOptions.DisableFormatting);
        }
        catch
        {
            return xml.Trim();
        }
    }

    private sealed record DuplicateRuleContext(
        Guid Id,
        string LogicalName,
        string DisplayName,
        string? Description,
        string? BaseEntityName,
        string? MatchingEntityName,
        string IsCaseSensitive,
        string ExcludeInactiveRecords);

    private sealed record RoutingRuleContext(
        Guid Id,
        string LogicalName,
        string DisplayName,
        string? Description,
        string WorkflowId);

    private sealed record MobileOfflineProfileContext(
        Guid Id,
        string LogicalName,
        string DisplayName,
        string? Description,
        string IsValidated);

    private sealed record RoleContext(
        Guid Id,
        string LogicalName,
        string DisplayName,
        string? BusinessUnitId,
        string IsInherited);

    private sealed record FieldSecurityProfileContext(
        Guid Id,
        string LogicalName,
        string DisplayName,
        string? Description);
}
