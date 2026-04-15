using System.Globalization;
using System.Text.Json.Nodes;
using DataverseSolutionCompiler.Domain.Diagnostics;
using DataverseSolutionCompiler.Domain.Model;

namespace DataverseSolutionCompiler.Readers.Live;

internal sealed partial class DataverseWebApiLiveReader
{
    private async Task<SolutionRecord?> ReadSolutionAsync(string solutionUniqueName, CancellationToken cancellationToken)
    {
        var filter = $"uniquename eq '{EscapeODataLiteral(solutionUniqueName)}'";
        JsonArray rows;
        try
        {
            rows = await GetCollectionAsync(
                $"solutions?$select=solutionid,friendlyname,uniquename,version,ismanaged,publisheruniquename,publishercustomizationprefix,publisherfriendlyname&$filter={filter}",
                cancellationToken).ConfigureAwait(false);
        }
        catch (DataverseWebApiException exception) when (RequiresLegacySolutionProjectionFallback(exception))
        {
            rows = await GetCollectionAsync(
                $"solutions?$select=solutionid,friendlyname,uniquename,version,ismanaged&$filter={filter}",
                cancellationToken).ConfigureAwait(false);
        }

        var row = rows.OfType<JsonObject>().FirstOrDefault();
        if (row is null)
        {
            return null;
        }

        return new SolutionRecord(
            GetGuid(row, "solutionid") ?? Guid.Empty,
            GetString(row, "uniquename") ?? solutionUniqueName,
            GetString(row, "friendlyname") ?? solutionUniqueName,
            GetString(row, "version") ?? "0.1.0",
            NormalizeBoolean(GetString(row, "ismanaged")),
            GetString(row, "publisheruniquename"),
            GetString(row, "publishercustomizationprefix"),
            GetString(row, "publisherfriendlyname"));
    }

    private static bool RequiresLegacySolutionProjectionFallback(DataverseWebApiException exception) =>
        exception.StatusCode == System.Net.HttpStatusCode.BadRequest
        && exception.Location.Contains("solutions?$select=", StringComparison.OrdinalIgnoreCase)
        && exception.Message.Contains("publisheruniquename", StringComparison.OrdinalIgnoreCase);

    private async Task<SolutionComponentScope> ReadSolutionComponentScopeAsync(
        SolutionRecord solution,
        IReadOnlySet<ComponentFamily> requestedFamilies,
        CancellationToken cancellationToken)
    {
        var rows = await GetCollectionAsync(
            $"solutioncomponents?$select=componenttype,objectid&$filter=_solutionid_value eq {FormatGuid(solution.Id)}&$top={_options.PageSize.ToString(CultureInfo.InvariantCulture)}",
            cancellationToken).ConfigureAwait(false);

        var scope = new SolutionComponentScope();
        foreach (var row in rows.OfType<JsonObject>())
        {
            var componentType = GetInt32(row, "componenttype");
            if (componentType is null)
            {
                continue;
            }

            switch (componentType.Value)
            {
                case 1:
                    scope.EntityMetadataIds.AddIfNotEmpty(GetGuid(row, "objectid"));
                    scope.EntityLogicalNames.AddIfNotEmpty(GetString(row, "logicalname", "name", "schemaname"));
                    break;
                case 14:
                    scope.KeyMetadataIds.AddIfNotEmpty(GetGuid(row, "objectid"));
                    break;
                case 431:
                    scope.AttributeImageConfigurationIds.AddIfNotEmpty(GetGuid(row, "objectid"));
                    scope.AttributeImageConfigurationLogicalNames.AddIfNotEmpty(
                        NormalizeLogicalName(GetString(row, "logical_name", "logicalname"))
                        ?? BuildAttributeImageConfigurationLogicalName(
                            NormalizeLogicalName(GetString(row, "parententitylogicalname")),
                            NormalizeLogicalName(GetString(row, "attributelogicalname"))));
                    break;
                case 432:
                    scope.EntityImageConfigurationIds.AddIfNotEmpty(GetGuid(row, "objectid"));
                    scope.EntityImageConfigurationEntities.AddIfNotEmpty(
                        NormalizeLogicalName(GetString(row, "parententitylogicalname"))
                        ?? BuildEntityImageConfigurationEntityName(NormalizeLogicalName(GetString(row, "logical_name", "logicalname"))));
                    break;
                case 9:
                    scope.GlobalOptionSetMetadataIds.AddIfNotEmpty(GetGuid(row, "objectid"));
                    scope.GlobalOptionSetNames.AddIfNotEmpty(GetString(row, "name", "schemaname", "uniquename"));
                    break;
                case 26:
                    scope.ViewIds.AddIfNotEmpty(GetGuid(row, "objectid"));
                    break;
                case 60:
                    scope.FormIds.AddIfNotEmpty(GetGuid(row, "objectid"));
                    break;
                case 62:
                    scope.SiteMapIds.AddIfNotEmpty(GetGuid(row, "objectid"));
                    break;
                case 80:
                    scope.AppModuleIds.AddIfNotEmpty(GetGuid(row, "objectid"));
                    break;
                case 90:
                    scope.PluginTypeIds.AddIfNotEmpty(GetGuid(row, "objectid"));
                    break;
                case 91:
                    scope.PluginAssemblyIds.AddIfNotEmpty(GetGuid(row, "objectid"));
                    break;
                case 92:
                    scope.PluginStepIds.AddIfNotEmpty(GetGuid(row, "objectid"));
                    break;
                case 93:
                    scope.PluginStepImageIds.AddIfNotEmpty(GetGuid(row, "objectid"));
                    break;
                case 95:
                    scope.ServiceEndpointIds.AddIfNotEmpty(GetGuid(row, "objectid"));
                    break;
                case 20:
                    scope.RoleIds.AddIfNotEmpty(GetGuid(row, "objectid"));
                    break;
                case 44:
                    scope.DuplicateRuleIds.AddIfNotEmpty(GetGuid(row, "objectid"));
                    break;
                case 63:
                    scope.ConnectionRoleIds.AddIfNotEmpty(GetGuid(row, "objectid"));
                    break;
                case 70:
                    scope.FieldSecurityProfileIds.AddIfNotEmpty(GetGuid(row, "objectid"));
                    break;
                case 150:
                    scope.RoutingRuleIds.AddIfNotEmpty(GetGuid(row, "objectid"));
                    break;
                case 161:
                    scope.MobileOfflineProfileIds.AddIfNotEmpty(GetGuid(row, "objectid"));
                    break;
                case 300:
                    scope.CanvasAppIds.AddIfNotEmpty(GetGuid(row, "objectid"));
                    break;
                case 371:
                case 372:
                    scope.ConnectorIds.AddIfNotEmpty(GetGuid(row, "objectid"));
                    break;
                case 400:
                    scope.AiProjectTypeIds.AddIfNotEmpty(GetGuid(row, "objectid"));
                    break;
                case 401:
                    scope.AiProjectIds.AddIfNotEmpty(GetGuid(row, "objectid"));
                    break;
                case 402:
                    scope.AiConfigurationIds.AddIfNotEmpty(GetGuid(row, "objectid"));
                    break;
                case 430:
                    scope.EntityAnalyticsConfigurationIds.AddIfNotEmpty(GetGuid(row, "objectid"));
                    break;
                case 380:
                    scope.EnvironmentVariableDefinitionIds.AddIfNotEmpty(GetGuid(row, "objectid"));
                    break;
                case 381:
                    scope.EnvironmentVariableValueIds.AddIfNotEmpty(GetGuid(row, "objectid"));
                    break;
            }
        }

        if (scope.EntityLogicalNames.Count == 0 && scope.EntityMetadataIds.Count > 0 && ShouldReadAny(requestedFamilies, ComponentFamily.Table, ComponentFamily.Column, ComponentFamily.Relationship, ComponentFamily.OptionSet, ComponentFamily.Key, ComponentFamily.ImageConfiguration, ComponentFamily.Form, ComponentFamily.View))
        {
            foreach (var logicalName in await ResolveEntityLogicalNamesAsync(scope.EntityMetadataIds, cancellationToken).ConfigureAwait(false))
            {
                scope.EntityLogicalNames.Add(logicalName);
            }
        }

        if (scope.GlobalOptionSetNames.Count == 0 && scope.GlobalOptionSetMetadataIds.Count > 0 && requestedFamilies.Contains(ComponentFamily.OptionSet))
        {
            foreach (var name in await ResolveGlobalOptionSetNamesAsync(scope.GlobalOptionSetMetadataIds, cancellationToken).ConfigureAwait(false))
            {
                scope.GlobalOptionSetNames.Add(name);
            }
        }

        return scope;
    }

    private async Task<IReadOnlyList<string>> ResolveEntityLogicalNamesAsync(IReadOnlyCollection<Guid> metadataIds, CancellationToken cancellationToken)
    {
        if (metadataIds.Count == 0)
        {
            return [];
        }

        var rows = await GetCollectionAsync(
            $"EntityDefinitions?$select=LogicalName,MetadataId&$filter={BuildGuidFilter("MetadataId", metadataIds)}",
            cancellationToken).ConfigureAwait(false);

        return rows
            .OfType<JsonObject>()
            .Select(row => GetString(row, "LogicalName"))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private async Task<IReadOnlyList<string>> ResolveGlobalOptionSetNamesAsync(IReadOnlyCollection<Guid> metadataIds, CancellationToken cancellationToken)
    {
        if (metadataIds.Count == 0)
        {
            return [];
        }

        var rows = await GetCollectionAsync(
            $"GlobalOptionSetDefinitions?$select=Name,MetadataId&$filter={BuildGuidFilter("MetadataId", metadataIds)}",
            cancellationToken).ConfigureAwait(false);

        return rows
            .OfType<JsonObject>()
            .Select(row => GetString(row, "Name", "name", "option_set_name"))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private async Task ReadSchemaFamiliesAsync(
        SolutionRecord solution,
        SolutionComponentScope scope,
        IReadOnlySet<ComponentFamily> requestedFamilies,
        ICollection<FamilyArtifact> artifacts,
        ICollection<CompilerDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        foreach (var entityLogicalName in scope.EntityLogicalNames.OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
        {
            JsonObject? entity;
            try
            {
                entity = await ReadEntityDefinitionAsync(entityLogicalName, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is DataverseWebApiException or Azure.Identity.AuthenticationFailedException)
            {
                diagnostics.Add(CreateFamilyFailureDiagnostic(ComponentFamily.Table, entityLogicalName, exception));
                continue;
            }

            if (entity is null)
            {
                continue;
            }

            var primaryIdAttribute = NormalizeLogicalName(GetString(entity, "PrimaryIdAttribute"));
            var primaryNameAttribute = NormalizeLogicalName(GetString(entity, "PrimaryNameAttribute"));

            if (requestedFamilies.Contains(ComponentFamily.Table))
            {
                artifacts.Add(CreateTableArtifact(solution, entityLogicalName, entity, primaryIdAttribute, primaryNameAttribute));
            }

            IReadOnlyList<JsonObject> attributeRows = [];
            if (ShouldReadAny(requestedFamilies, ComponentFamily.Column, ComponentFamily.OptionSet, ComponentFamily.ImageConfiguration))
            {
                try
                {
                    attributeRows = await ReadEntityAttributesAsync(entityLogicalName, entity, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception) when (exception is DataverseWebApiException or Azure.Identity.AuthenticationFailedException)
                {
                    diagnostics.Add(CreateFamilyFailureDiagnostic(ComponentFamily.Column, entityLogicalName, exception));
                }
            }

            if (requestedFamilies.Contains(ComponentFamily.Column))
            {
                foreach (var attributeRow in attributeRows)
                {
                    var artifact = CreateColumnArtifact(entityLogicalName, primaryIdAttribute, primaryNameAttribute, attributeRow);
                    if (artifact is not null)
                    {
                        artifacts.Add(artifact);
                    }
                }
            }

            if (requestedFamilies.Contains(ComponentFamily.ImageConfiguration))
            {
                try
                {
                    foreach (var artifact in await ReadImageConfigurationArtifactsAsync(entityLogicalName, entity, scope, diagnostics, cancellationToken).ConfigureAwait(false))
                    {
                        artifacts.Add(artifact);
                    }
                }
                catch (Exception exception) when (exception is DataverseWebApiException or Azure.Identity.AuthenticationFailedException)
                {
                    diagnostics.Add(CreateFamilyFailureDiagnostic(ComponentFamily.ImageConfiguration, entityLogicalName, exception));
                }
            }

            if (requestedFamilies.Contains(ComponentFamily.OptionSet))
            {
                try
                {
                    foreach (var optionArtifact in await ReadEntityOptionSetArtifactsAsync(entityLogicalName, attributeRows, entity, cancellationToken).ConfigureAwait(false))
                    {
                        artifacts.Add(optionArtifact);
                    }
                }
                catch (Exception exception) when (exception is DataverseWebApiException or Azure.Identity.AuthenticationFailedException)
                {
                    diagnostics.Add(CreateFamilyFailureDiagnostic(ComponentFamily.OptionSet, entityLogicalName, exception));
                }
            }

            if (requestedFamilies.Contains(ComponentFamily.Key))
            {
                try
                {
                    foreach (var keyArtifact in await ReadEntityKeyArtifactsAsync(entityLogicalName, entity, scope, cancellationToken).ConfigureAwait(false))
                    {
                        artifacts.Add(keyArtifact);
                    }
                }
                catch (Exception exception) when (exception is DataverseWebApiException or Azure.Identity.AuthenticationFailedException)
                {
                    diagnostics.Add(CreateFamilyFailureDiagnostic(ComponentFamily.Key, entityLogicalName, exception));
                }
            }

            if (requestedFamilies.Contains(ComponentFamily.Relationship))
            {
                try
                {
                    foreach (var relationship in await ReadRelationshipArtifactsAsync(entityLogicalName, cancellationToken).ConfigureAwait(false))
                    {
                        artifacts.Add(relationship);
                    }
                }
                catch (Exception exception) when (exception is DataverseWebApiException or Azure.Identity.AuthenticationFailedException)
                {
                    diagnostics.Add(CreateFamilyFailureDiagnostic(ComponentFamily.Relationship, entityLogicalName, exception));
                }
            }

            if (requestedFamilies.Contains(ComponentFamily.Form))
            {
                try
                {
                    foreach (var form in await ReadFormArtifactsAsync(entityLogicalName, scope.FormIds, diagnostics, cancellationToken).ConfigureAwait(false))
                    {
                        artifacts.Add(form);
                    }
                }
                catch (Exception exception) when (exception is DataverseWebApiException or Azure.Identity.AuthenticationFailedException)
                {
                    diagnostics.Add(CreateFamilyFailureDiagnostic(ComponentFamily.Form, entityLogicalName, exception));
                }
            }

            if (requestedFamilies.Contains(ComponentFamily.View))
            {
                try
                {
                    foreach (var view in await ReadViewArtifactsAsync(entityLogicalName, scope.ViewIds, diagnostics, cancellationToken).ConfigureAwait(false))
                    {
                        artifacts.Add(view);
                    }
                }
                catch (Exception exception) when (exception is DataverseWebApiException or Azure.Identity.AuthenticationFailedException)
                {
                    diagnostics.Add(CreateFamilyFailureDiagnostic(ComponentFamily.View, entityLogicalName, exception));
                }
            }
        }

        if (requestedFamilies.Contains(ComponentFamily.OptionSet) && (scope.GlobalOptionSetNames.Count > 0 || scope.GlobalOptionSetMetadataIds.Count > 0))
        {
            try
            {
                foreach (var artifact in await ReadGlobalOptionSetArtifactsAsync(scope, cancellationToken).ConfigureAwait(false))
                {
                    artifacts.Add(artifact);
                }
            }
            catch (Exception exception) when (exception is DataverseWebApiException or Azure.Identity.AuthenticationFailedException)
            {
                diagnostics.Add(CreateFamilyFailureDiagnostic(ComponentFamily.OptionSet, solution.UniqueName, exception));
            }
        }
    }

    private async Task<JsonObject?> ReadEntityDefinitionAsync(string entityLogicalName, CancellationToken cancellationToken) =>
        await GetSingleObjectAsync(
            $"EntityDefinitions(LogicalName='{EscapeODataLiteral(entityLogicalName)}')?$select=LogicalName,SchemaName,EntitySetName,PrimaryIdAttribute,PrimaryNameAttribute,PrimaryImageAttribute,ObjectTypeCode,DisplayName,OwnershipType,IsCustomizable",
            cancellationToken).ConfigureAwait(false);

    private async Task<IReadOnlyList<JsonObject>> ReadEntityAttributesAsync(string entityLogicalName, JsonObject entity, CancellationToken cancellationToken)
    {
        var rows = await GetCollectionAsync(
            $"EntityDefinitions(LogicalName='{EscapeODataLiteral(entityLogicalName)}')/Attributes?$select=LogicalName,SchemaName,AttributeType,AttributeTypeName,IsSecured,IsPrimaryName,DisplayName,IsLogical,IsCustomAttribute,IsCustomizable",
            cancellationToken).ConfigureAwait(false);

        return rows.Count > 0 ? rows.OfType<JsonObject>().ToArray() : ReadObjects(entity, "Attributes").ToArray();
    }

    private async Task<IReadOnlyList<FamilyArtifact>> ReadEntityOptionSetArtifactsAsync(
        string entityLogicalName,
        IReadOnlyList<JsonObject> attributeRows,
        JsonObject entity,
        CancellationToken cancellationToken)
    {
        var optionRows = new List<JsonObject>();
        foreach (var typeCast in new[]
                 {
                     "Microsoft.Dynamics.CRM.PicklistAttributeMetadata",
                     "Microsoft.Dynamics.CRM.BooleanAttributeMetadata",
                     "Microsoft.Dynamics.CRM.StateAttributeMetadata",
                     "Microsoft.Dynamics.CRM.StatusAttributeMetadata"
                 })
        {
            var rows = await GetCollectionAsync(
                $"EntityDefinitions(LogicalName='{EscapeODataLiteral(entityLogicalName)}')/Attributes/{typeCast}?$select=LogicalName,SchemaName,AttributeType,DisplayName,OptionSet",
                cancellationToken).ConfigureAwait(false);
            optionRows.AddRange(rows.OfType<JsonObject>());
        }

        if (optionRows.Count == 0)
        {
            optionRows.AddRange(ReadObjects(entity, "OptionSets"));
        }

        if (optionRows.Count == 0)
        {
            optionRows.AddRange(attributeRows.Where(row => GetProperty(row, "OptionSet") is JsonObject));
        }

        var artifacts = new List<FamilyArtifact>();
        foreach (var row in optionRows)
        {
            var optionSet = (GetProperty(row, "OptionSet") as JsonObject ?? row).DeepClone() as JsonObject;
            if (optionSet is null)
            {
                continue;
            }

            var attributeLogicalName = NormalizeLogicalName(GetString(optionSet, "attribute_logical_name") ?? GetString(row, "LogicalName"));
            if (string.IsNullOrWhiteSpace(attributeLogicalName))
            {
                continue;
            }

            var optionSetType = NormalizeLogicalName(GetString(optionSet, "option_set_type")) ?? NormalizeLogicalName(GetString(row, "AttributeType")) ?? "picklist";
            var options = ReadArray(optionSet, "options");
            var optionsJson = SerializeJson(NormalizeOptionEntries(options));
            var summaryJson = SerializeJson(new
            {
                entityLogicalName,
                attributeLogicalName,
                optionSetType,
                isGlobal = false,
                optionCount = options.Count,
                options = JsonNode.Parse(optionsJson)
            });

            artifacts.Add(new FamilyArtifact(
                ComponentFamily.OptionSet,
                $"{entityLogicalName}|{attributeLogicalName}",
                GetString(row, "DisplayName") ?? attributeLogicalName,
                $"EntityDefinitions(LogicalName='{entityLogicalName}')/Attributes/{attributeLogicalName}/OptionSet",
                EvidenceKind.Readback,
                CreateProperties(
                    (ArtifactPropertyKeys.EntityLogicalName, entityLogicalName),
                    (ArtifactPropertyKeys.OptionSetName, GetString(optionSet, "option_set_name", "Name") ?? attributeLogicalName),
                    (ArtifactPropertyKeys.OptionSetType, optionSetType),
                    (ArtifactPropertyKeys.IsGlobal, "false"),
                    (ArtifactPropertyKeys.OptionCount, options.Count.ToString(CultureInfo.InvariantCulture)),
                    (ArtifactPropertyKeys.OptionsJson, optionsJson),
                    (ArtifactPropertyKeys.SummaryJson, summaryJson),
                    (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(summaryJson)))));
        }

        return artifacts;
    }

    private async Task<IReadOnlyList<FamilyArtifact>> ReadEntityKeyArtifactsAsync(
        string entityLogicalName,
        JsonObject entity,
        SolutionComponentScope scope,
        CancellationToken cancellationToken)
    {
        var entityWithKeys = await GetSingleObjectAsync(
            $"EntityDefinitions(LogicalName='{EscapeODataLiteral(entityLogicalName)}')?$select=LogicalName&$expand=Keys($select=MetadataId,LogicalName,SchemaName,KeyAttributes,EntityKeyIndexStatus)",
            cancellationToken).ConfigureAwait(false);
        var keyRows = ReadObjects(entityWithKeys ?? entity, "Keys").ToArray();
        if (keyRows.Length == 0)
        {
            return [];
        }

        return keyRows
            .Where(row => scope.KeyMetadataIds.Count == 0 || !GetGuid(row, "MetadataId").HasValue || scope.KeyMetadataIds.Contains(GetGuid(row, "MetadataId")!.Value))
            .Select(row => CreateKeyArtifact(entityLogicalName, row))
            .Where(artifact => artifact is not null)
            .Cast<FamilyArtifact>()
            .ToArray();
    }

    private async Task<IReadOnlyList<FamilyArtifact>> ReadGlobalOptionSetArtifactsAsync(SolutionComponentScope scope, CancellationToken cancellationToken)
    {
        string relativePath;
        if (scope.GlobalOptionSetNames.Count > 0)
        {
            relativePath =
                $"GlobalOptionSetDefinitions?$select=Name,Options,OptionSetType,IsGlobal&$filter={string.Join(" or ", scope.GlobalOptionSetNames.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).Select(name => $"Name eq '{EscapeODataLiteral(name)}'"))}";
        }
        else
        {
            relativePath =
                $"GlobalOptionSetDefinitions?$select=Name,Options,OptionSetType,IsGlobal,MetadataId&$filter={BuildGuidFilter("MetadataId", scope.GlobalOptionSetMetadataIds)}";
        }

        var rows = await GetCollectionAsync(relativePath, cancellationToken).ConfigureAwait(false);
        var artifacts = new List<FamilyArtifact>();
        foreach (var row in rows.OfType<JsonObject>())
        {
            var optionSetName = NormalizeLogicalName(GetString(row, "Name", "name", "option_set_name"));
            if (string.IsNullOrWhiteSpace(optionSetName))
            {
                continue;
            }

            var optionSetType = NormalizeLogicalName(GetString(row, "OptionSetType", "option_set_type")) ?? "picklist";
            var options = ReadArray(row, "Options", "options");
            var optionsJson = SerializeJson(NormalizeOptionEntries(options));
            var summaryJson = SerializeJson(new
            {
                optionSetName,
                optionSetType,
                isGlobal = true,
                optionCount = options.Count,
                options = JsonNode.Parse(optionsJson)
            });

            artifacts.Add(new FamilyArtifact(
                ComponentFamily.OptionSet,
                optionSetName,
                optionSetName,
                $"GlobalOptionSetDefinitions({optionSetName})",
                EvidenceKind.Readback,
                CreateProperties(
                    (ArtifactPropertyKeys.OptionSetName, optionSetName),
                    (ArtifactPropertyKeys.OptionSetType, optionSetType),
                    (ArtifactPropertyKeys.IsGlobal, "true"),
                    (ArtifactPropertyKeys.OptionCount, options.Count.ToString(CultureInfo.InvariantCulture)),
                    (ArtifactPropertyKeys.OptionsJson, optionsJson),
                    (ArtifactPropertyKeys.SummaryJson, summaryJson),
                    (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(summaryJson)))));
        }

        return artifacts;
    }

    private async Task<IReadOnlyList<FamilyArtifact>> ReadRelationshipArtifactsAsync(string entityLogicalName, CancellationToken cancellationToken)
    {
        var artifacts = new List<FamilyArtifact>();
        foreach (var (segment, relationshipType) in new[]
                 {
                     ("ManyToOneRelationships", "ManyToOne"),
                     ("OneToManyRelationships", "OneToMany"),
                     ("ManyToManyRelationships", "ManyToMany")
                 })
        {
            var rows = await GetCollectionAsync(
                $"EntityDefinitions(LogicalName='{EscapeODataLiteral(entityLogicalName)}')/{segment}?$select=SchemaName,ReferencedEntity,ReferencingEntity,ReferencingAttribute,Entity1LogicalName,Entity2LogicalName",
                cancellationToken).ConfigureAwait(false);

            foreach (var row in rows.OfType<JsonObject>())
            {
                var schemaName = NormalizeLogicalName(GetString(row, "SchemaName"));
                if (string.IsNullOrWhiteSpace(schemaName))
                {
                    continue;
                }

                var referencedEntity = NormalizeLogicalName(GetString(row, "ReferencedEntity", "Entity1LogicalName"));
                var referencingEntity = NormalizeLogicalName(GetString(row, "ReferencingEntity", "Entity2LogicalName"));

                artifacts.Add(new FamilyArtifact(
                    ComponentFamily.Relationship,
                    schemaName,
                    schemaName,
                    $"EntityDefinitions(LogicalName='{entityLogicalName}')/{segment}/{schemaName}",
                    EvidenceKind.Readback,
                    CreateProperties(
                        (ArtifactPropertyKeys.RelationshipType, relationshipType),
                        (ArtifactPropertyKeys.ReferencedEntity, referencedEntity),
                        (ArtifactPropertyKeys.ReferencingEntity, referencingEntity),
                        (ArtifactPropertyKeys.ReferencingAttribute, NormalizeLogicalName(GetString(row, "ReferencingAttribute"))),
                        (ArtifactPropertyKeys.OwningEntityLogicalName, referencedEntity))));
            }
        }

        return artifacts;
    }
}
