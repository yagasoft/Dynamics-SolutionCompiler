using DataverseSolutionCompiler.Domain.Abstractions;
using DataverseSolutionCompiler.Domain.Diff;
using DataverseSolutionCompiler.Domain.Diagnostics;
using DataverseSolutionCompiler.Domain.Live;
using DataverseSolutionCompiler.Domain.Model;
using System.Text.Json;

namespace DataverseSolutionCompiler.Diff;

public sealed class StableOverlapDriftComparer : IDriftComparer
{
    private static readonly HashSet<string> CaseInsensitivePropertyKeys = new(StringComparer.Ordinal)
    {
        ArtifactPropertyKeys.DefinitionSchemaName,
        ArtifactPropertyKeys.EntityLogicalName,
        ArtifactPropertyKeys.EntityAlias,
        ArtifactPropertyKeys.AssemblyFullName,
        ArtifactPropertyKeys.AssemblyQualifiedName,
        ArtifactPropertyKeys.Name,
        ArtifactPropertyKeys.HandlerPluginTypeName,
        ArtifactPropertyKeys.MessageName,
        ArtifactPropertyKeys.MessagePropertyName,
        ArtifactPropertyKeys.TriggerMessageName,
        ArtifactPropertyKeys.ParentEntityLogicalName,
        ArtifactPropertyKeys.ParentAiProjectLogicalName,
        ArtifactPropertyKeys.ParentAiProjectTypeLogicalName,
        ArtifactPropertyKeys.ParentPluginStepLogicalName,
        ArtifactPropertyKeys.ImportTargetEntity,
        ArtifactPropertyKeys.ParentAppModuleUniqueName,
        ArtifactPropertyKeys.ParentImportMapLogicalName,
        ArtifactPropertyKeys.PrimaryEntity,
        ArtifactPropertyKeys.PrimaryImageAttribute,
        ArtifactPropertyKeys.ConnectorInternalId,
        ArtifactPropertyKeys.ReferencedEntity,
        ArtifactPropertyKeys.ReferencingAttribute,
        ArtifactPropertyKeys.ReferencingEntity,
        ArtifactPropertyKeys.SchemaName,
        ArtifactPropertyKeys.SettingDefinitionUniqueName,
        ArtifactPropertyKeys.SourceAttributeName,
        ArtifactPropertyKeys.SourceEntityName,
        ArtifactPropertyKeys.TargetEntity,
        ArtifactPropertyKeys.TargetEntityName,
        ArtifactPropertyKeys.TargetAttributeName,
        ArtifactPropertyKeys.ImageAttributeLogicalName,
        ArtifactPropertyKeys.BaseEntityName,
        ArtifactPropertyKeys.MatchingEntityName,
        ArtifactPropertyKeys.BaseAttributeName,
        ArtifactPropertyKeys.MatchingAttributeName,
        ArtifactPropertyKeys.ParentDuplicateRuleLogicalName,
        ArtifactPropertyKeys.ParentRoutingRuleLogicalName,
        ArtifactPropertyKeys.ParentMobileOfflineProfileLogicalName,
        ArtifactPropertyKeys.ParentRoleLogicalName,
        ArtifactPropertyKeys.ParentFieldSecurityProfileLogicalName,
        ArtifactPropertyKeys.PrivilegeName,
        ArtifactPropertyKeys.ApplicableFrom,
        ArtifactPropertyKeys.ApplicableEntity,
        ArtifactPropertyKeys.ParentSlaLogicalName,
        ArtifactPropertyKeys.ActionFlowUniqueName
    };

    private static readonly HashSet<string> IgnoredSystemColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "ownerid",
        "owningbusinessunit",
        "owningteam",
        "owninguser",
        "createdby",
        "createdbyexternalparty",
        "createdbyname",
        "createdon",
        "createdonbehalfby",
        "createdonbehalfbyname",
        "importsequencenumber",
        "modifiedby",
        "modifiedbyname",
        "modifiedon",
        "modifiedonbehalfby",
        "modifiedonbehalfbyname",
        "overriddencreatedon",
        "processid",
        "stageid",
        "statecode",
        "statuscode",
        "timezoneruleversionnumber",
        "transactioncurrencyid",
        "transactioncurrencyidname",
        "traversedpath",
        "utcconversiontimezonecode",
        "versionnumber"
    };

    private static readonly IReadOnlyDictionary<ComponentFamily, string[]> FamilyComparisonKeys =
        new Dictionary<ComponentFamily, string[]>
        {
            [ComponentFamily.Publisher] = [],
            [ComponentFamily.SolutionShell] = [],
            [ComponentFamily.Table] =
            [
                ArtifactPropertyKeys.SchemaName,
                ArtifactPropertyKeys.EntitySetName,
                ArtifactPropertyKeys.IsCustomizable
            ],
            [ComponentFamily.Column] =
            [
                ArtifactPropertyKeys.EntityLogicalName,
                ArtifactPropertyKeys.SchemaName,
                ArtifactPropertyKeys.AttributeType,
                ArtifactPropertyKeys.IsSecured,
                ArtifactPropertyKeys.IsCustomizable,
                ArtifactPropertyKeys.IsPrimaryKey,
                ArtifactPropertyKeys.IsPrimaryName
            ],
            [ComponentFamily.Relationship] =
            [
                ArtifactPropertyKeys.RelationshipType,
                ArtifactPropertyKeys.ReferencedEntity,
                ArtifactPropertyKeys.ReferencingEntity,
                ArtifactPropertyKeys.ReferencingAttribute
            ],
            [ComponentFamily.OptionSet] =
            [
                ArtifactPropertyKeys.OptionSetType,
                ArtifactPropertyKeys.IsGlobal,
                ArtifactPropertyKeys.OptionCount,
                ArtifactPropertyKeys.ComparisonSignature
            ],
            [ComponentFamily.Key] =
            [
                ArtifactPropertyKeys.EntityLogicalName,
                ArtifactPropertyKeys.SchemaName,
                ArtifactPropertyKeys.KeyAttributesJson
            ],
            [ComponentFamily.ImageConfiguration] =
            [
                ArtifactPropertyKeys.EntityLogicalName,
                ArtifactPropertyKeys.ImageConfigurationScope,
                ArtifactPropertyKeys.PrimaryImageAttribute,
                ArtifactPropertyKeys.ImageAttributeLogicalName,
                ArtifactPropertyKeys.CanStoreFullImage,
                ArtifactPropertyKeys.IsPrimaryImage
            ],
            [ComponentFamily.Form] =
            [
                ArtifactPropertyKeys.EntityLogicalName,
                ArtifactPropertyKeys.FormType,
                ArtifactPropertyKeys.FormId,
                ArtifactPropertyKeys.ComparisonSignature
            ],
            [ComponentFamily.View] =
            [
                ArtifactPropertyKeys.EntityLogicalName,
                ArtifactPropertyKeys.QueryType,
                ArtifactPropertyKeys.TargetEntity,
                ArtifactPropertyKeys.ComparisonSignature
            ],
            [ComponentFamily.Visualization] =
            [
                ArtifactPropertyKeys.TargetEntity,
                ArtifactPropertyKeys.ComparisonSignature
            ],
            [ComponentFamily.Ribbon] =
            [
                ArtifactPropertyKeys.EntityLogicalName,
                ArtifactPropertyKeys.ComparisonSignature
            ],
            [ComponentFamily.CustomControl] =
            [
                ArtifactPropertyKeys.Version,
                ArtifactPropertyKeys.ComparisonSignature
            ],
            [ComponentFamily.AppModule] =
            [
                ArtifactPropertyKeys.ComponentTypesJson
            ],
            [ComponentFamily.AppSetting] =
            [
                ArtifactPropertyKeys.ParentAppModuleUniqueName,
                ArtifactPropertyKeys.SettingDefinitionUniqueName,
                ArtifactPropertyKeys.Value
            ],
            [ComponentFamily.SiteMap] =
            [
                ArtifactPropertyKeys.AreaCount,
                ArtifactPropertyKeys.GroupCount,
                ArtifactPropertyKeys.SubAreaCount,
                ArtifactPropertyKeys.WebResourceSubAreaCount,
                ArtifactPropertyKeys.ComparisonSignature
            ],
            [ComponentFamily.WebResource] =
            [
                ArtifactPropertyKeys.WebResourceType,
                ArtifactPropertyKeys.ByteLength,
                ArtifactPropertyKeys.ContentHash
            ],
            [ComponentFamily.EnvironmentVariableDefinition] =
            [
                ArtifactPropertyKeys.DefaultValue,
                ArtifactPropertyKeys.SecretStore,
                ArtifactPropertyKeys.ValueSchema,
                ArtifactPropertyKeys.AttributeType
            ],
            [ComponentFamily.EnvironmentVariableValue] =
            [
                ArtifactPropertyKeys.DefinitionSchemaName,
                ArtifactPropertyKeys.Value
            ],
            [ComponentFamily.ImportMap] =
            [
                ArtifactPropertyKeys.ImportSource,
                ArtifactPropertyKeys.SourceFormat,
                ArtifactPropertyKeys.ImportTargetEntity,
                ArtifactPropertyKeys.FieldDelimiter,
                ArtifactPropertyKeys.MappingCount,
                ArtifactPropertyKeys.ComparisonSignature
            ],
            [ComponentFamily.DataSourceMapping] =
            [
                ArtifactPropertyKeys.ParentImportMapLogicalName,
                ArtifactPropertyKeys.SourceEntityName,
                ArtifactPropertyKeys.SourceAttributeName,
                ArtifactPropertyKeys.TargetEntityName,
                ArtifactPropertyKeys.TargetAttributeName,
                ArtifactPropertyKeys.ProcessCode,
                ArtifactPropertyKeys.ColumnIndex
            ],
            [ComponentFamily.EntityAnalyticsConfiguration] =
            [
                ArtifactPropertyKeys.ParentEntityLogicalName,
                ArtifactPropertyKeys.EntityDataSource,
                ArtifactPropertyKeys.IsEnabledForAdls,
                ArtifactPropertyKeys.IsEnabledForTimeSeries
            ],
            [ComponentFamily.AiProjectType] =
            [
                ArtifactPropertyKeys.Description
            ],
            [ComponentFamily.AiProject] =
            [
                ArtifactPropertyKeys.ParentAiProjectTypeLogicalName,
                ArtifactPropertyKeys.TargetEntity,
                ArtifactPropertyKeys.Description
            ],
            [ComponentFamily.AiConfiguration] =
            [
                ArtifactPropertyKeys.ParentAiProjectLogicalName,
                ArtifactPropertyKeys.ConfigurationKind,
                ArtifactPropertyKeys.Value
            ],
            [ComponentFamily.PluginAssembly] =
            [
                ArtifactPropertyKeys.AssemblyFileName,
                ArtifactPropertyKeys.IsolationMode,
                ArtifactPropertyKeys.SourceType,
                ArtifactPropertyKeys.IntroducedVersion
            ],
            [ComponentFamily.PluginType] =
            [
                ArtifactPropertyKeys.AssemblyFullName,
                ArtifactPropertyKeys.AssemblyQualifiedName,
                ArtifactPropertyKeys.PluginTypeKind,
                ArtifactPropertyKeys.WorkflowActivityGroupName
            ],
            [ComponentFamily.PluginStep] =
            [
                ArtifactPropertyKeys.Stage,
                ArtifactPropertyKeys.Mode,
                ArtifactPropertyKeys.Rank,
                ArtifactPropertyKeys.SupportedDeployment,
                ArtifactPropertyKeys.MessageName,
                ArtifactPropertyKeys.PrimaryEntity,
                ArtifactPropertyKeys.HandlerPluginTypeName,
                ArtifactPropertyKeys.FilteringAttributes
            ],
            [ComponentFamily.PluginStepImage] =
            [
                ArtifactPropertyKeys.ParentPluginStepLogicalName,
                ArtifactPropertyKeys.EntityAlias,
                ArtifactPropertyKeys.ImageType,
                ArtifactPropertyKeys.MessagePropertyName,
                ArtifactPropertyKeys.SelectedAttributes
            ],
            [ComponentFamily.ServiceEndpoint] =
            [
                ArtifactPropertyKeys.Name,
                ArtifactPropertyKeys.Contract,
                ArtifactPropertyKeys.ConnectionMode,
                ArtifactPropertyKeys.AuthType,
                ArtifactPropertyKeys.NamespaceAddress,
                ArtifactPropertyKeys.EndpointPath,
                ArtifactPropertyKeys.Url,
                ArtifactPropertyKeys.MessageFormat,
                ArtifactPropertyKeys.MessageCharset,
                ArtifactPropertyKeys.IntroducedVersion,
                ArtifactPropertyKeys.IsCustomizable
            ],
            [ComponentFamily.Connector] =
            [
                ArtifactPropertyKeys.Name,
                ArtifactPropertyKeys.Description,
                ArtifactPropertyKeys.ConnectorInternalId,
                ArtifactPropertyKeys.ConnectorType,
                ArtifactPropertyKeys.CapabilitiesJson,
                ArtifactPropertyKeys.IntroducedVersion,
                ArtifactPropertyKeys.IsCustomizable
            ],
            [ComponentFamily.DuplicateRule] =
            [
                ArtifactPropertyKeys.BaseEntityName,
                ArtifactPropertyKeys.MatchingEntityName,
                ArtifactPropertyKeys.IsCaseSensitive,
                ArtifactPropertyKeys.ExcludeInactiveRecords
            ],
            [ComponentFamily.DuplicateRuleCondition] =
            [
                ArtifactPropertyKeys.ParentDuplicateRuleLogicalName,
                ArtifactPropertyKeys.BaseAttributeName,
                ArtifactPropertyKeys.MatchingAttributeName,
                ArtifactPropertyKeys.OperatorCode,
                ArtifactPropertyKeys.IgnoreBlankValues
            ],
            [ComponentFamily.RoutingRule] =
            [
                ArtifactPropertyKeys.Description
            ],
            [ComponentFamily.RoutingRuleItem] =
            [
                ArtifactPropertyKeys.ParentRoutingRuleLogicalName,
                ArtifactPropertyKeys.ComparisonSignature
            ],
            [ComponentFamily.MobileOfflineProfile] =
            [
                ArtifactPropertyKeys.Description,
                ArtifactPropertyKeys.IsValidated
            ],
            [ComponentFamily.MobileOfflineProfileItem] =
            [
                ArtifactPropertyKeys.ParentMobileOfflineProfileLogicalName,
                ArtifactPropertyKeys.EntityLogicalName,
                ArtifactPropertyKeys.RecordDistributionCriteria,
                ArtifactPropertyKeys.RecordsOwnedByMe,
                ArtifactPropertyKeys.RecordsOwnedByMyTeam,
                ArtifactPropertyKeys.RecordsOwnedByMyBusinessUnit,
                ArtifactPropertyKeys.ProfileItemEntityFilter
            ],
            [ComponentFamily.SimilarityRule] =
            [
                ArtifactPropertyKeys.BaseEntityName,
                ArtifactPropertyKeys.MatchingEntityName,
                ArtifactPropertyKeys.ExcludeInactiveRecords,
                ArtifactPropertyKeys.MaxKeywords,
                ArtifactPropertyKeys.NgramSize
            ],
            [ComponentFamily.Sla] =
            [
                ArtifactPropertyKeys.ApplicableFrom,
                ArtifactPropertyKeys.AllowPauseResume,
                ArtifactPropertyKeys.IsDefault,
                ArtifactPropertyKeys.WorkflowId
            ],
            [ComponentFamily.SlaItem] =
            [
                ArtifactPropertyKeys.ParentSlaLogicalName,
                ArtifactPropertyKeys.ApplicableEntity,
                ArtifactPropertyKeys.AllowPauseResume,
                ArtifactPropertyKeys.ActionUrl,
                ArtifactPropertyKeys.ActionFlowUniqueName,
                ArtifactPropertyKeys.ApplicableWhenXml
            ],
            [ComponentFamily.Role] = [],
            [ComponentFamily.RolePrivilege] =
            [
                ArtifactPropertyKeys.ParentRoleLogicalName,
                ArtifactPropertyKeys.PrivilegeName,
                ArtifactPropertyKeys.AccessLevel,
                ArtifactPropertyKeys.ObjectTypeCode
            ],
            [ComponentFamily.FieldSecurityProfile] =
            [
                ArtifactPropertyKeys.Description
            ],
            [ComponentFamily.FieldPermission] =
            [
                ArtifactPropertyKeys.ParentFieldSecurityProfileLogicalName,
                ArtifactPropertyKeys.EntityLogicalName,
                ArtifactPropertyKeys.AttributeLogicalName,
                ArtifactPropertyKeys.CanRead,
                ArtifactPropertyKeys.CanCreate,
                ArtifactPropertyKeys.CanUpdate,
                ArtifactPropertyKeys.CanReadUnmasked
            ],
            [ComponentFamily.ConnectionRole] =
            [
                ArtifactPropertyKeys.Category,
                ArtifactPropertyKeys.Description
            ],
            [ComponentFamily.Workflow] =
            [
                ArtifactPropertyKeys.WorkflowId,
                ArtifactPropertyKeys.WorkflowKind,
                ArtifactPropertyKeys.Category,
                ArtifactPropertyKeys.Mode,
                ArtifactPropertyKeys.WorkflowScope,
                ArtifactPropertyKeys.OnDemand,
                ArtifactPropertyKeys.PrimaryEntity,
                ArtifactPropertyKeys.TriggerMessageName,
                ArtifactPropertyKeys.XamlHash,
                ArtifactPropertyKeys.ClientDataHash,
                ArtifactPropertyKeys.WorkflowActionMetadataJson
            ],
            [ComponentFamily.CanvasApp] =
            [
                ArtifactPropertyKeys.AppVersion,
                ArtifactPropertyKeys.Status,
                ArtifactPropertyKeys.CreatedByClientVersion,
                ArtifactPropertyKeys.MinClientVersion,
                ArtifactPropertyKeys.TagsJson,
                ArtifactPropertyKeys.AuthorizationReferencesJson,
                ArtifactPropertyKeys.ConnectionReferencesJson,
                ArtifactPropertyKeys.DatabaseReferencesJson,
                ArtifactPropertyKeys.CanConsumeAppPass,
                ArtifactPropertyKeys.CanvasAppType,
                ArtifactPropertyKeys.IntroducedVersion,
                ArtifactPropertyKeys.CdsDependenciesJson,
                ArtifactPropertyKeys.IsCustomizable
            ]
        };

    public DriftReport Compare(CanonicalSolution source, LiveSnapshot snapshot, CompareRequest request)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(request);

        var sourceArtifacts = FilterArtifacts(source.Artifacts, request);
        var liveArtifacts = FilterArtifacts(snapshot.Artifacts, request);

        var sourceKeys = sourceArtifacts.ToDictionary(KeyFor, artifact => artifact);
        var liveKeys = liveArtifacts.ToDictionary(KeyFor, artifact => artifact);
        var findings = new List<DriftFinding>();

        foreach (var (key, artifact) in sourceKeys)
        {
            if (!liveKeys.TryGetValue(key, out var liveArtifact))
            {
                if (ShouldIgnoreMissingInLiveArtifact(artifact))
                {
                    continue;
                }

                findings.Add(new DriftFinding(
                    "Missing in live snapshot",
                    DriftSeverity.Warning,
                    DriftCategory.MissingInLive,
                    artifact.Family,
                    $"{artifact.Family} '{artifact.LogicalName}' is present in source but not in live readback."));
                continue;
            }

            var mismatch = CompareArtifact(artifact, liveArtifact);
            if (mismatch is not null)
            {
                findings.Add(mismatch);
            }
        }

        foreach (var (key, artifact) in liveKeys)
        {
            if (!sourceKeys.ContainsKey(key) && !ShouldIgnoreExtraLiveArtifact(artifact))
            {
                findings.Add(new DriftFinding(
                    "Missing in source",
                    DriftSeverity.Warning,
                    DriftCategory.MissingInSource,
                    artifact.Family,
                    $"{artifact.Family} '{artifact.LogicalName}' is present in live readback but not in source."));
            }
        }

        var diagnostics = new List<CompilerDiagnostic>
        {
            new(
                "stable-overlap-family-semantic",
                DiagnosticSeverity.Info,
                "Drift comparison now uses stable family-aware fields for the strongest proven Dataverse families while later families remain overlap-only.")
        };
        diagnostics.AddRange(BuildAppModuleRoleMapBestEffortDiagnostics(sourceKeys, liveKeys));
        diagnostics.AddRange(BuildCustomControlSourceAsymmetryDiagnostics(sourceKeys, liveKeys));

        return new DriftReport(
            findings.Any(finding => finding.Severity == DriftSeverity.Error),
            findings,
            diagnostics);
    }

    private static IEnumerable<CompilerDiagnostic> BuildAppModuleRoleMapBestEffortDiagnostics(
        IReadOnlyDictionary<string, FamilyArtifact> sourceKeys,
        IReadOnlyDictionary<string, FamilyArtifact> liveKeys)
    {
        foreach (var (key, sourceArtifact) in sourceKeys)
        {
            if (sourceArtifact.Family != ComponentFamily.AppModule
                || !liveKeys.TryGetValue(key, out var liveArtifact))
            {
                continue;
            }

            var sourceRoleMapCount = ParseInt(GetProperty(sourceArtifact, ArtifactPropertyKeys.RoleMapCount));
            var liveRoleMapCount = ParseInt(GetProperty(liveArtifact, ArtifactPropertyKeys.RoleMapCount));
            if (sourceRoleMapCount <= 0 || liveRoleMapCount > 0)
            {
                continue;
            }

            yield return new CompilerDiagnostic(
                "stable-overlap-appmodule-rolemap-best-effort",
                DiagnosticSeverity.Info,
                $"AppModule '{sourceArtifact.LogicalName}' carries role-map source evidence, but live app-module readback currently underreports role_ids in the neutral corpus. Drift keeps role-map detail as explicit best-effort instead of blocking parity.",
                sourceArtifact.LogicalName);
        }
    }

    private static IEnumerable<CompilerDiagnostic> BuildCustomControlSourceAsymmetryDiagnostics(
        IReadOnlyDictionary<string, FamilyArtifact> sourceKeys,
        IReadOnlyDictionary<string, FamilyArtifact> liveKeys)
    {
        foreach (var (key, liveArtifact) in liveKeys)
        {
            if (liveArtifact.Family != ComponentFamily.CustomControl
                || sourceKeys.ContainsKey(key))
            {
                continue;
            }

            yield return new CompilerDiagnostic(
                "stable-overlap-customcontrol-source-asymmetry",
                DiagnosticSeverity.Info,
                $"CustomControl '{liveArtifact.LogicalName}' is present in live solution scope, but unmanaged source export does not currently emit a matching standalone source artifact in the neutral corpus. Drift treats that lane as an explicit source-asymmetric best-effort boundary.",
                liveArtifact.LogicalName);
        }
    }

    private static DriftFinding? CompareArtifact(FamilyArtifact source, FamilyArtifact live)
    {
        if (!FamilyComparisonKeys.TryGetValue(source.Family, out var keys))
        {
            return null;
        }

        var mismatchedKeys = new List<string>();

        if (ShouldCompareDisplayName(source)
            && !string.IsNullOrWhiteSpace(source.DisplayName)
            && !string.IsNullOrWhiteSpace(live.DisplayName)
            && !string.Equals(source.DisplayName, live.DisplayName, StringComparison.Ordinal))
        {
            mismatchedKeys.Add("displayName");
        }

        foreach (var key in keys)
        {
            if (string.Equals(key, ArtifactPropertyKeys.IsCustomizable, StringComparison.Ordinal)
                && (string.IsNullOrWhiteSpace(GetProperty(source, key)) || string.IsNullOrWhiteSpace(GetProperty(live, key))))
            {
                continue;
            }

            if (source.Family == ComponentFamily.Column
                && (string.Equals(GetProperty(source, ArtifactPropertyKeys.IsPrimaryKey), "true", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(GetProperty(source, ArtifactPropertyKeys.IsPrimaryName), "true", StringComparison.OrdinalIgnoreCase))
                && (string.Equals(key, ArtifactPropertyKeys.SchemaName, StringComparison.Ordinal)
                    || string.Equals(key, ArtifactPropertyKeys.AttributeType, StringComparison.Ordinal)))
            {
                continue;
            }

            var sourceValue = GetProperty(source, key);
            var liveValue = GetProperty(live, key);
            if (!PropertyValuesEqual(source, key, sourceValue, liveValue))
            {
                mismatchedKeys.Add(key);
            }
        }

        if (!mismatchedKeys.Any())
        {
            return null;
        }

        return new DriftFinding(
            "Stable property mismatch",
            IsBlockingMismatch(source, mismatchedKeys) ? DriftSeverity.Error : DriftSeverity.Warning,
            DriftCategory.Mismatch,
            source.Family,
            $"{source.Family} '{source.LogicalName}' differs on stable fields: {string.Join(", ", mismatchedKeys)}.");
    }

    private static bool IsBlockingMismatch(FamilyArtifact source, IReadOnlyCollection<string> mismatchedKeys)
    {
        if (source.Family == ComponentFamily.ImageConfiguration)
        {
            return true;
        }

        return (source.Family == ComponentFamily.Table || source.Family == ComponentFamily.Column)
            && mismatchedKeys.Contains(ArtifactPropertyKeys.IsCustomizable, StringComparer.Ordinal);
    }

    private static IReadOnlyList<FamilyArtifact> FilterArtifacts(IReadOnlyList<FamilyArtifact> artifacts, CompareRequest request) =>
        request.IncludeBestEffortFamilies
            ? artifacts.Where(artifact => !ShouldIgnoreArtifact(artifact)).ToArray()
            : artifacts.Where(artifact => artifact.Evidence != EvidenceKind.BestEffort && !ShouldIgnoreArtifact(artifact)).ToArray();

    private static string KeyFor(FamilyArtifact artifact) =>
        $"{artifact.Family}:{artifact.LogicalName}".ToLowerInvariant();

    private static string? GetProperty(FamilyArtifact artifact, string key)
    {
        if (artifact.Properties is not null && artifact.Properties.TryGetValue(key, out var value))
        {
            return value;
        }

        if (artifact.Family == ComponentFamily.PluginType
            && string.Equals(key, ArtifactPropertyKeys.PluginTypeKind, StringComparison.Ordinal))
        {
            return string.IsNullOrWhiteSpace(GetProperty(artifact, ArtifactPropertyKeys.WorkflowActivityGroupName))
                ? "plugin"
                : "customWorkflowActivity";
        }

        return null;
    }

    private static bool PropertyValuesEqual(FamilyArtifact artifact, string key, string? sourceValue, string? liveValue)
    {
        if (artifact.Family == ComponentFamily.Column
            && string.Equals(key, ArtifactPropertyKeys.AttributeType, StringComparison.Ordinal))
        {
            return string.Equals(
                CanonicalizeColumnAttributeType(sourceValue),
                CanonicalizeColumnAttributeType(liveValue),
                StringComparison.Ordinal);
        }

        if (artifact.Family == ComponentFamily.Relationship
            && string.Equals(key, ArtifactPropertyKeys.RelationshipType, StringComparison.Ordinal))
        {
            return string.Equals(
                CanonicalizeRelationshipType(sourceValue),
                CanonicalizeRelationshipType(liveValue),
                StringComparison.Ordinal);
        }

        if (artifact.Family == ComponentFamily.Key
            && string.Equals(key, ArtifactPropertyKeys.KeyAttributesJson, StringComparison.Ordinal))
        {
            return string.Equals(
                CanonicalizeKeyAttributesJson(sourceValue),
                CanonicalizeKeyAttributesJson(liveValue),
                StringComparison.Ordinal);
        }

        if (artifact.Family == ComponentFamily.Connector
            && string.Equals(key, ArtifactPropertyKeys.CapabilitiesJson, StringComparison.Ordinal))
        {
            return string.Equals(
                CanonicalizeCapabilitiesJson(sourceValue),
                CanonicalizeCapabilitiesJson(liveValue),
                StringComparison.Ordinal);
        }

        var comparison = CaseInsensitivePropertyKeys.Contains(key)
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return string.Equals(sourceValue ?? string.Empty, liveValue ?? string.Empty, comparison);
    }

    private static string CanonicalizeColumnAttributeType(string? value) =>
        (value ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "bit" => "boolean",
            "datetime" => "datetime",
            "decimal" => "decimal",
            "int" => "integer",
            "lookup" => "lookup",
            "memo" => "memo",
            "ntext" => "memo",
            "nvarchar" => "string",
            "owner" => "owner",
            "picklist" => "picklist",
            "primarykey" => "uniqueidentifier",
            "state" => "state",
            "status" => "status",
            "string" => "string",
            "uniqueidentifier" => "uniqueidentifier",
            var other => other
        };

    private static string CanonicalizeRelationshipType(string? value) =>
        (value ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "manytoone" => "one-to-many-family",
            "onetomany" => "one-to-many-family",
            "manytomany" => "many-to-many-family",
            var other => other
        };

    private static string CanonicalizeKeyAttributesJson(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "[]";
        }

        try
        {
            var values = JsonSerializer.Deserialize<string[]>(value) ?? [];
            return JsonSerializer.Serialize(values
                .Where(entry => !string.IsNullOrWhiteSpace(entry))
                .Select(entry => entry.Trim().ToLowerInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(entry => entry, StringComparer.OrdinalIgnoreCase)
                .ToArray());
        }
        catch (JsonException)
        {
            return value.Trim();
        }
    }

    private static string CanonicalizeCapabilitiesJson(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "[]";
        }

        try
        {
            var values = JsonSerializer.Deserialize<string[]>(value) ?? [];
            return JsonSerializer.Serialize(values
                .Where(entry => !string.IsNullOrWhiteSpace(entry))
                .Select(entry => entry.Trim().ToLowerInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(entry => entry, StringComparer.OrdinalIgnoreCase)
                .ToArray());
        }
        catch (JsonException)
        {
            return value.Trim();
        }
    }

    private static bool ShouldCompareDisplayName(FamilyArtifact artifact) =>
        artifact.Family is not ComponentFamily.EnvironmentVariableValue
            and not ComponentFamily.Key
            and not ComponentFamily.ImageConfiguration
            and not ComponentFamily.OptionSet;

    private static bool ShouldIgnoreArtifact(FamilyArtifact artifact) =>
        artifact.Family switch
        {
            ComponentFamily.AppSetting => true,
            ComponentFamily.Column => ShouldIgnoreColumn(artifact),
            ComponentFamily.LegacyAsset => true,
            ComponentFamily.OptionSet => ShouldIgnoreOptionSet(artifact),
            ComponentFamily.Relationship => ShouldIgnoreRelationship(artifact),
            _ => false
        };

    private static bool ShouldIgnoreMissingInLiveArtifact(FamilyArtifact artifact) =>
        artifact.Family == ComponentFamily.Publisher
        || artifact.Family == ComponentFamily.ImportMap
        || artifact.Family == ComponentFamily.DataSourceMapping
        || artifact.Family == ComponentFamily.Ribbon
        || artifact.Family == ComponentFamily.Report
        || artifact.Family == ComponentFamily.Template
        || artifact.Family == ComponentFamily.DisplayString
        || artifact.Family == ComponentFamily.Attachment
        || artifact.Family == ComponentFamily.LegacyAsset
        || artifact.Family == ComponentFamily.RolePrivilege
        || artifact.Family == ComponentFamily.SimilarityRule
        || artifact.Family == ComponentFamily.Sla
        || artifact.Family == ComponentFamily.SlaItem
        || (artifact.Family == ComponentFamily.OptionSet
            && ShouldIgnoreOptionSet(artifact));

    private static bool ShouldIgnoreExtraLiveArtifact(FamilyArtifact artifact)
    {
        if (artifact.Evidence != EvidenceKind.Readback)
        {
            return false;
        }

        return artifact.Family switch
        {
            ComponentFamily.CustomControl => true,
            ComponentFamily.Form => string.Equals(
                GetProperty(artifact, ArtifactPropertyKeys.IsDefault),
                "true",
                StringComparison.OrdinalIgnoreCase),
            ComponentFamily.Column => string.Equals(
                GetProperty(artifact, ArtifactPropertyKeys.IsPrimaryKey),
                "true",
                StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    GetProperty(artifact, ArtifactPropertyKeys.IsPrimaryName),
                    "true",
                    StringComparison.OrdinalIgnoreCase),
            ComponentFamily.View => string.Equals(
                GetProperty(artifact, ArtifactPropertyKeys.ReadbackScope),
                "entity-fallback",
                StringComparison.OrdinalIgnoreCase)
                && !string.Equals(
                    GetProperty(artifact, ArtifactPropertyKeys.QueryType),
                    "0",
                    StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static bool ShouldIgnoreColumn(FamilyArtifact artifact)
    {
        if (string.Equals(GetProperty(artifact, ArtifactPropertyKeys.IsPrimaryKey), "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(GetProperty(artifact, ArtifactPropertyKeys.IsPrimaryName), "true", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(GetProperty(artifact, ArtifactPropertyKeys.IsCustomField), "true", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var logicalName = artifact.LogicalName.Split('|').LastOrDefault() ?? artifact.LogicalName;
        if (IgnoredSystemColumns.Contains(logicalName))
        {
            return true;
        }

        return logicalName.EndsWith("name", StringComparison.OrdinalIgnoreCase)
            || logicalName.EndsWith("yominame", StringComparison.OrdinalIgnoreCase)
            || logicalName.EndsWith("type", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldIgnoreRelationship(FamilyArtifact artifact)
    {
        var logicalName = artifact.LogicalName;
        return logicalName.Contains("asyncoperation", StringComparison.OrdinalIgnoreCase)
            || logicalName.Contains("business_unit", StringComparison.OrdinalIgnoreCase)
            || logicalName.Contains("bulkdelete", StringComparison.OrdinalIgnoreCase)
            || logicalName.Contains("mailbox", StringComparison.OrdinalIgnoreCase)
            || logicalName.Contains("owner", StringComparison.OrdinalIgnoreCase)
            || logicalName.Contains("principalobjectattributeaccess", StringComparison.OrdinalIgnoreCase)
            || logicalName.Contains("processsession", StringComparison.OrdinalIgnoreCase)
            || logicalName.Contains("syncerror", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldIgnoreOptionSet(FamilyArtifact artifact)
    {
        var optionSetType = GetProperty(artifact, ArtifactPropertyKeys.OptionSetType);
        return string.Equals(optionSetType, "state", StringComparison.OrdinalIgnoreCase)
            || string.Equals(optionSetType, "status", StringComparison.OrdinalIgnoreCase);
    }

    private static int ParseInt(string? value) =>
        int.TryParse(value, out var parsed) ? parsed : 0;
}
