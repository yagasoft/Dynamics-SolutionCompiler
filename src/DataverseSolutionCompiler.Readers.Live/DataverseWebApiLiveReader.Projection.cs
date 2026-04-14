using System.Globalization;
using System.Text.Json.Nodes;
using DataverseSolutionCompiler.Domain.Diagnostics;
using DataverseSolutionCompiler.Domain.Model;

namespace DataverseSolutionCompiler.Readers.Live;

internal sealed partial class DataverseWebApiLiveReader
{
    private async Task<IReadOnlyList<FamilyArtifact>> ReadFormArtifactsAsync(
        string entityLogicalName,
        IReadOnlyCollection<Guid> scopedFormIds,
        ICollection<CompilerDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        var strictScopeRows = new List<JsonObject>();
        if (scopedFormIds.Count > 0)
        {
            strictScopeRows.AddRange((await GetCollectionAsync(
                $"systemforms?$select=formid,name,type,description,isdefault,objecttypecode,formxml,formjson&$filter={BuildGuidFilter("formid", scopedFormIds)}",
                cancellationToken).ConfigureAwait(false)).OfType<JsonObject>());
        }

        IReadOnlyList<JsonObject> rows = strictScopeRows;
        var scope = SolutionScope;
        if (strictScopeRows.Count == 0 && _options.EnableEntityScopedUiFallback)
        {
            diagnostics.Add(new CompilerDiagnostic(
                "live-readback-form-fallback",
                DiagnosticSeverity.Warning,
                $"Falling back to entity-scoped form readback for '{entityLogicalName}' because strict solution scoping under-reported the form surface.",
                entityLogicalName));

            rows = (await GetCollectionAsync(
                $"systemforms?$select=formid,name,type,description,isdefault,objecttypecode,formxml,formjson&$filter=objecttypecode eq '{EscapeODataLiteral(entityLogicalName)}'",
                cancellationToken).ConfigureAwait(false)).OfType<JsonObject>().ToArray();
            scope = EntityFallbackScope;
        }

        return rows
            .Select(row => CreateFormArtifact(entityLogicalName, row, scope))
            .Where(artifact => artifact is not null)
            .Cast<FamilyArtifact>()
            .ToArray();
    }

    private async Task<IReadOnlyList<FamilyArtifact>> ReadViewArtifactsAsync(
        string entityLogicalName,
        IReadOnlyCollection<Guid> scopedViewIds,
        ICollection<CompilerDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        var strictScopeRows = new List<JsonObject>();
        if (scopedViewIds.Count > 0)
        {
            strictScopeRows.AddRange((await GetCollectionAsync(
                $"savedqueries?$select=savedqueryid,name,returnedtypecode,querytype,fetchxml,layoutxml&$filter={BuildGuidFilter("savedqueryid", scopedViewIds)}",
                cancellationToken).ConfigureAwait(false)).OfType<JsonObject>());
        }

        IReadOnlyList<JsonObject> rows = strictScopeRows;
        var scope = SolutionScope;
        if (strictScopeRows.Count == 0 && _options.EnableEntityScopedUiFallback)
        {
            diagnostics.Add(new CompilerDiagnostic(
                "live-readback-view-fallback",
                DiagnosticSeverity.Warning,
                $"Falling back to entity-scoped view readback for '{entityLogicalName}' because strict solution scoping under-reported the saved-query surface.",
                entityLogicalName));

            rows = (await GetCollectionAsync(
                $"savedqueries?$select=savedqueryid,name,returnedtypecode,querytype,fetchxml,layoutxml&$filter=returnedtypecode eq '{EscapeODataLiteral(entityLogicalName)}'",
                cancellationToken).ConfigureAwait(false)).OfType<JsonObject>().ToArray();
            scope = EntityFallbackScope;
        }

        return rows
            .Select(row => CreateViewArtifact(entityLogicalName, row, scope))
            .Where(artifact => artifact is not null)
            .Cast<FamilyArtifact>()
            .ToArray();
    }

    private async Task ReadAppShellFamiliesAsync(
        SolutionComponentScope scope,
        IReadOnlySet<ComponentFamily> requestedFamilies,
        ICollection<FamilyArtifact> artifacts,
        ICollection<CompilerDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        var appModulesById = new Dictionary<Guid, AppModuleContext>();

        if (ShouldReadAny(requestedFamilies, ComponentFamily.AppModule, ComponentFamily.AppSetting) && scope.AppModuleIds.Count > 0)
        {
            try
            {
                var rows = await GetCollectionAsync(
                    $"appmodules?$select=appmoduleid,uniquename,name,description,components,role_ids,app_settings,webresourceid,clienttype,formfactor,navigationtype,statecode,statuscode&$filter={BuildGuidFilter("appmoduleid", scope.AppModuleIds)}",
                    cancellationToken).ConfigureAwait(false);

                foreach (var row in rows.OfType<JsonObject>())
                {
                    var context = CreateAppModuleContext(row);
                    if (context is null)
                    {
                        continue;
                    }

                    appModulesById[context.Id] = context;
                    if (requestedFamilies.Contains(ComponentFamily.AppModule))
                    {
                        artifacts.Add(CreateAppModuleArtifact(context));
                    }
                }
            }
            catch (Exception exception) when (exception is DataverseWebApiException or Azure.Identity.AuthenticationFailedException)
            {
                diagnostics.Add(CreateFamilyFailureDiagnostic(ComponentFamily.AppModule, "appmodules", exception));
            }
        }

        if (requestedFamilies.Contains(ComponentFamily.AppSetting) && appModulesById.Count > 0)
        {
            try
            {
                var rows = await GetCollectionAsync(
                    $"appsettings?$select=appsettingid,displayname,uniquename,value,description,_parentappmoduleid_value,_settingdefinitionid_value,componentidunique&$filter={BuildGuidFilter("_parentappmoduleid_value", appModulesById.Keys)}",
                    cancellationToken).ConfigureAwait(false);

                var emittedSettings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var row in rows.OfType<JsonObject>())
                {
                    var artifact = CreateAppSettingArtifact(row, appModulesById, diagnostics);
                    if (artifact is null)
                    {
                        continue;
                    }

                    artifacts.Add(artifact);
                    emittedSettings.Add(artifact.LogicalName);
                }

                foreach (var context in appModulesById.Values)
                {
                    foreach (var nestedSetting in context.NestedSettings)
                    {
                        var logicalName = $"{context.UniqueName}|{nestedSetting.SettingDefinitionUniqueName}";
                        if (!emittedSettings.Add(logicalName))
                        {
                            continue;
                        }

                        artifacts.Add(new FamilyArtifact(
                            ComponentFamily.AppSetting,
                            logicalName,
                            nestedSetting.SettingDefinitionUniqueName,
                            $"appmodules/{context.UniqueName}/appsettings/{nestedSetting.SettingDefinitionUniqueName}",
                            EvidenceKind.Readback,
                            CreateProperties(
                                (ArtifactPropertyKeys.ParentAppModuleUniqueName, context.UniqueName),
                                (ArtifactPropertyKeys.SettingDefinitionUniqueName, nestedSetting.SettingDefinitionUniqueName),
                                (ArtifactPropertyKeys.Value, nestedSetting.Value))));
                    }
                }
            }
            catch (Exception exception) when (exception is DataverseWebApiException or Azure.Identity.AuthenticationFailedException)
            {
                diagnostics.Add(CreateFamilyFailureDiagnostic(ComponentFamily.AppSetting, "appsettings", exception));
            }
        }

        if (requestedFamilies.Contains(ComponentFamily.SiteMap) && scope.SiteMapIds.Count > 0)
        {
            try
            {
                var rows = await GetCollectionAsync(
                    $"sitemaps?$select=sitemapid,sitemapname,sitemapnameunique,sitemapxml,isappaware,showhome,showpinned,showrecents,enablecollapsiblegroups&$filter={BuildGuidFilter("sitemapid", scope.SiteMapIds)}",
                    cancellationToken).ConfigureAwait(false);

                foreach (var row in rows.OfType<JsonObject>())
                {
                    var artifact = CreateSiteMapArtifact(row);
                    if (artifact is not null)
                    {
                        artifacts.Add(artifact);
                    }
                }
            }
            catch (Exception exception) when (exception is DataverseWebApiException or Azure.Identity.AuthenticationFailedException)
            {
                diagnostics.Add(CreateFamilyFailureDiagnostic(ComponentFamily.SiteMap, "sitemaps", exception));
            }
        }

        var definitionSchemaById = new Dictionary<Guid, string>();
        if (requestedFamilies.Contains(ComponentFamily.EnvironmentVariableDefinition) && scope.EnvironmentVariableDefinitionIds.Count > 0)
        {
            try
            {
                var rows = await GetCollectionAsync(
                    $"environmentvariabledefinitions?$select=environmentvariabledefinitionid,schemaname,displayname,defaultvalue,type,secretstore,isrequired,valueschema,introducedversion&$filter={BuildGuidFilter("environmentvariabledefinitionid", scope.EnvironmentVariableDefinitionIds)}",
                    cancellationToken).ConfigureAwait(false);

                foreach (var row in rows.OfType<JsonObject>())
                {
                    var schemaName = NormalizeLogicalName(GetString(row, "schemaname"));
                    var id = GetGuid(row, "environmentvariabledefinitionid");
                    if (id.HasValue && !string.IsNullOrWhiteSpace(schemaName))
                    {
                        definitionSchemaById[id.Value] = schemaName!;
                    }

                    var artifact = CreateEnvironmentVariableDefinitionArtifact(row);
                    if (artifact is not null)
                    {
                        artifacts.Add(artifact);
                    }
                }
            }
            catch (Exception exception) when (exception is DataverseWebApiException or Azure.Identity.AuthenticationFailedException)
            {
                diagnostics.Add(CreateFamilyFailureDiagnostic(ComponentFamily.EnvironmentVariableDefinition, "environmentvariabledefinitions", exception));
            }
        }

        if (requestedFamilies.Contains(ComponentFamily.EnvironmentVariableValue) && scope.EnvironmentVariableValueIds.Count > 0)
        {
            try
            {
                var rows = await GetCollectionAsync(
                    $"environmentvariablevalues?$select=environmentvariablevalueid,schemaname,value,_environmentvariabledefinitionid_value,definition_schema_name&$filter={BuildGuidFilter("environmentvariablevalueid", scope.EnvironmentVariableValueIds)}",
                    cancellationToken).ConfigureAwait(false);

                foreach (var row in rows.OfType<JsonObject>())
                {
                    var artifact = CreateEnvironmentVariableValueArtifact(row, definitionSchemaById, diagnostics);
                    if (artifact is not null)
                    {
                        artifacts.Add(artifact);
                    }
                }
            }
            catch (Exception exception) when (exception is DataverseWebApiException or Azure.Identity.AuthenticationFailedException)
            {
                diagnostics.Add(CreateFamilyFailureDiagnostic(ComponentFamily.EnvironmentVariableValue, "environmentvariablevalues", exception));
            }
        }

        var aiProjectTypeLogicalNameById = new Dictionary<Guid, string>();
        if (requestedFamilies.Contains(ComponentFamily.AiProjectType) && scope.AiProjectTypeIds.Count > 0)
        {
            try
            {
                var rows = await GetCollectionAsync(
                    $"msdyn_aiprojecttypes?$select=msdyn_aiprojecttypeid,msdyn_uniquename,msdyn_name,description&$filter={BuildGuidFilter("msdyn_aiprojecttypeid", scope.AiProjectTypeIds)}",
                    cancellationToken).ConfigureAwait(false);

                foreach (var row in rows.OfType<JsonObject>())
                {
                    var logicalName = NormalizeLogicalName(GetString(row, "msdyn_uniquename", "uniquename", "msdyn_name"));
                    var id = GetGuid(row, "msdyn_aiprojecttypeid");
                    if (id.HasValue && !string.IsNullOrWhiteSpace(logicalName))
                    {
                        aiProjectTypeLogicalNameById[id.Value] = logicalName!;
                    }

                    var artifact = CreateAiProjectTypeArtifact(row);
                    if (artifact is not null)
                    {
                        artifacts.Add(artifact);
                    }
                }
            }
            catch (Exception exception) when (exception is DataverseWebApiException or Azure.Identity.AuthenticationFailedException)
            {
                diagnostics.Add(CreateFamilyFailureDiagnostic(ComponentFamily.AiProjectType, "msdyn_aiprojecttypes", exception));
            }
        }

        var aiProjectLogicalNameById = new Dictionary<Guid, string>();
        if (requestedFamilies.Contains(ComponentFamily.AiProject) && scope.AiProjectIds.Count > 0)
        {
            try
            {
                var rows = await GetCollectionAsync(
                    $"msdyn_aiprojects?$select=msdyn_aiprojectid,msdyn_uniquename,msdyn_name,description,msdyn_targetentity,msdyn_projecttypeuniquename,_msdyn_aiprojecttypeid_value&$filter={BuildGuidFilter("msdyn_aiprojectid", scope.AiProjectIds)}",
                    cancellationToken).ConfigureAwait(false);

                foreach (var row in rows.OfType<JsonObject>())
                {
                    var logicalName = NormalizeLogicalName(GetString(row, "msdyn_uniquename", "uniquename", "msdyn_name"));
                    var id = GetGuid(row, "msdyn_aiprojectid");
                    if (id.HasValue && !string.IsNullOrWhiteSpace(logicalName))
                    {
                        aiProjectLogicalNameById[id.Value] = logicalName!;
                    }

                    var artifact = CreateAiProjectArtifact(row, aiProjectTypeLogicalNameById);
                    if (artifact is not null)
                    {
                        artifacts.Add(artifact);
                    }
                }
            }
            catch (Exception exception) when (exception is DataverseWebApiException or Azure.Identity.AuthenticationFailedException)
            {
                diagnostics.Add(CreateFamilyFailureDiagnostic(ComponentFamily.AiProject, "msdyn_aiprojects", exception));
            }
        }

        if (requestedFamilies.Contains(ComponentFamily.AiConfiguration) && scope.AiConfigurationIds.Count > 0)
        {
            try
            {
                var rows = await GetCollectionAsync(
                    $"msdyn_aiconfigurations?$select=msdyn_aiconfigurationid,msdyn_uniquename,msdyn_name,msdyn_type,msdyn_runconfiguration,msdyn_customconfiguration,msdyn_projectuniquename,_msdyn_aiprojectid_value&$filter={BuildGuidFilter("msdyn_aiconfigurationid", scope.AiConfigurationIds)}",
                    cancellationToken).ConfigureAwait(false);

                foreach (var row in rows.OfType<JsonObject>())
                {
                    var artifact = CreateAiConfigurationArtifact(row, aiProjectLogicalNameById);
                    if (artifact is not null)
                    {
                        artifacts.Add(artifact);
                    }
                }
            }
            catch (Exception exception) when (exception is DataverseWebApiException or Azure.Identity.AuthenticationFailedException)
            {
                diagnostics.Add(CreateFamilyFailureDiagnostic(ComponentFamily.AiConfiguration, "msdyn_aiconfigurations", exception));
            }
        }

        if (requestedFamilies.Contains(ComponentFamily.EntityAnalyticsConfiguration) && scope.EntityAnalyticsConfigurationIds.Count > 0)
        {
            try
            {
                var rows = await GetCollectionAsync(
                    $"entityanalyticsconfigs?$select=entityanalyticsconfigid,parententitylogicalname,entitydatasource,isenabledforadls,isenabledfortimeseries&$filter={BuildGuidFilter("entityanalyticsconfigid", scope.EntityAnalyticsConfigurationIds)}",
                    cancellationToken).ConfigureAwait(false);

                foreach (var row in rows.OfType<JsonObject>())
                {
                    var artifact = CreateEntityAnalyticsConfigurationArtifact(row);
                    if (artifact is not null)
                    {
                        artifacts.Add(artifact);
                    }
                }
            }
            catch (Exception exception) when (exception is DataverseWebApiException or Azure.Identity.AuthenticationFailedException)
            {
                diagnostics.Add(CreateFamilyFailureDiagnostic(ComponentFamily.EntityAnalyticsConfiguration, "entityanalyticsconfigs", exception));
            }
        }

        if (requestedFamilies.Contains(ComponentFamily.CanvasApp) && scope.CanvasAppIds.Count > 0)
        {
            try
            {
                var rows = await GetCollectionAsync(
                    $"canvasapps?$select=canvasappid,name,displayname,description,appversion,status,createdbyclientversion,minclientversion,tags,authorizationreferences,connectionreferences,databasereferences,canconsumeapppass,canvasapptype,introducedversion,cdsdependencies,iscustomizable,ismanaged,backgroundimage_name,document_name&$filter={BuildGuidFilter("canvasappid", scope.CanvasAppIds)}",
                    cancellationToken).ConfigureAwait(false);

                foreach (var row in rows.OfType<JsonObject>())
                {
                    var artifact = CreateCanvasAppArtifact(row);
                    if (artifact is not null)
                    {
                        artifacts.Add(artifact);
                    }
                }
            }
            catch (Exception exception) when (exception is DataverseWebApiException or Azure.Identity.AuthenticationFailedException)
            {
                diagnostics.Add(CreateFamilyFailureDiagnostic(ComponentFamily.CanvasApp, "canvasapps", exception));
            }
        }
    }

    private static FamilyArtifact CreateSolutionArtifact(SolutionRecord solution) =>
        new(
            ComponentFamily.SolutionShell,
            solution.UniqueName,
            solution.DisplayName,
            $"solutions/{solution.UniqueName}",
            EvidenceKind.Readback,
            CreateProperties(
                (ArtifactPropertyKeys.Managed, solution.Managed),
                (ArtifactPropertyKeys.Version, solution.Version),
                (ArtifactPropertyKeys.PublisherUniqueName, solution.PublisherUniqueName),
                (ArtifactPropertyKeys.PublisherPrefix, solution.PublisherPrefix),
                (ArtifactPropertyKeys.PublisherDisplayName, solution.PublisherDisplayName)));

    private static FamilyArtifact CreateTableArtifact(
        SolutionRecord solution,
        string entityLogicalName,
        JsonObject row,
        string? primaryIdAttribute,
        string? primaryNameAttribute) =>
        new(
            ComponentFamily.Table,
            entityLogicalName,
            GetString(row, "DisplayName"),
            $"EntityDefinitions(LogicalName='{entityLogicalName}')",
            EvidenceKind.Readback,
            CreateProperties(
                (ArtifactPropertyKeys.EntityLogicalName, entityLogicalName),
                (ArtifactPropertyKeys.SchemaName, GetString(row, "SchemaName")),
                (ArtifactPropertyKeys.EntitySetName, GetString(row, "EntitySetName")),
                (ArtifactPropertyKeys.PrimaryIdAttribute, primaryIdAttribute),
                (ArtifactPropertyKeys.PrimaryNameAttribute, primaryNameAttribute),
                (ArtifactPropertyKeys.OwnershipTypeMask, GetString(row, "OwnershipTypeMask", "OwnershipType")),
                (ArtifactPropertyKeys.IsCustomizable, GetString(row, "IsCustomizable") is { } tableIsCustomizable ? NormalizeBoolean(tableIsCustomizable) : null),
                (ArtifactPropertyKeys.ShellOnly, "false"),
                (ArtifactPropertyKeys.PublisherPrefix, solution.PublisherPrefix)));

    private static FamilyArtifact? CreateColumnArtifact(
        string entityLogicalName,
        string? primaryIdAttribute,
        string? primaryNameAttribute,
        JsonObject row)
    {
        var logicalName = NormalizeLogicalName(GetString(row, "LogicalName"));
        if (string.IsNullOrWhiteSpace(logicalName))
        {
            return null;
        }

        return new FamilyArtifact(
            ComponentFamily.Column,
            $"{entityLogicalName}|{logicalName}",
            GetString(row, "DisplayName"),
            $"EntityDefinitions(LogicalName='{entityLogicalName}')/Attributes/{logicalName}",
            EvidenceKind.Readback,
            CreateProperties(
                (ArtifactPropertyKeys.EntityLogicalName, entityLogicalName),
                (ArtifactPropertyKeys.SchemaName, GetString(row, "SchemaName")),
                (ArtifactPropertyKeys.AttributeType, NormalizeLogicalName(GetString(row, "AttributeType")) ?? "unknown"),
                (ArtifactPropertyKeys.IsSecured, NormalizeBoolean(GetString(row, "IsSecured"))),
                (ArtifactPropertyKeys.IsCustomField, NormalizeBoolean(GetString(row, "IsCustomAttribute"))),
                (ArtifactPropertyKeys.IsCustomizable, GetString(row, "IsCustomizable") is { } columnIsCustomizable ? NormalizeBoolean(columnIsCustomizable) : null),
                (ArtifactPropertyKeys.IsPrimaryKey, string.Equals(logicalName, primaryIdAttribute, StringComparison.OrdinalIgnoreCase) ? "true" : "false"),
                (ArtifactPropertyKeys.IsPrimaryName, NormalizeBoolean(GetString(row, "IsPrimaryName"))),
                (ArtifactPropertyKeys.IsLogical, NormalizeBoolean(GetString(row, "IsLogical")))));
    }

    private static FamilyArtifact? CreateKeyArtifact(string entityLogicalName, JsonObject row)
    {
        var logicalName = NormalizeLogicalName(GetString(row, "LogicalName"));
        var schemaName = GetString(row, "SchemaName");
        var keyName = logicalName ?? NormalizeLogicalName(schemaName);
        if (string.IsNullOrWhiteSpace(keyName))
        {
            return null;
        }

        var keyAttributesJson = NormalizeStringArrayJson(ReadArray(row, "KeyAttributes"));
        if (keyAttributesJson == "[]")
        {
            return null;
        }

        return new FamilyArtifact(
            ComponentFamily.Key,
            $"{entityLogicalName}|{keyName}",
            schemaName ?? keyName,
            $"EntityDefinitions(LogicalName='{entityLogicalName}')/Keys/{keyName}",
            EvidenceKind.Readback,
            CreateProperties(
                (ArtifactPropertyKeys.EntityLogicalName, entityLogicalName),
                (ArtifactPropertyKeys.SchemaName, schemaName),
                (ArtifactPropertyKeys.KeyAttributesJson, keyAttributesJson),
                (ArtifactPropertyKeys.IndexStatus, GetString(row, "EntityKeyIndexStatus"))));
    }

    private static FamilyArtifact? CreateFormArtifact(string entityLogicalName, JsonObject row, string scope)
    {
        var formId = NormalizeGuid(GetString(row, "formid"));
        if (string.IsNullOrWhiteSpace(formId))
        {
            return null;
        }

        var formType = MapFormType(GetString(row, "type"));
        var summary = SummarizeFormXml(GetString(row, "formxml"), formType, formId);
        var summaryJson = SerializeJson(new
        {
            formType = summary.FormType,
            formId = summary.FormId,
            tabCount = summary.TabCount,
            sectionCount = summary.SectionCount,
            controlCount = summary.ControlCount,
            quickFormCount = summary.QuickFormCount,
            subgridCount = summary.SubgridCount,
            headerControlCount = summary.HeaderControlCount,
            footerControlCount = summary.FooterControlCount,
            controlDescriptions = summary.ControlDescriptions.Select(control => new
            {
                id = control.Id,
                dataFieldName = control.DataFieldName,
                role = control.Role
            }).ToArray()
        });

        return new FamilyArtifact(
            ComponentFamily.Form,
            $"{entityLogicalName}|{formType}|{formId}",
            GetString(row, "name") ?? formId,
            $"systemforms/{formId}",
            EvidenceKind.Readback,
            CreateProperties(
                (ArtifactPropertyKeys.EntityLogicalName, entityLogicalName),
                (ArtifactPropertyKeys.FormType, formType),
                (ArtifactPropertyKeys.FormTypeCode, GetString(row, "type")),
                (ArtifactPropertyKeys.FormId, formId),
                (ArtifactPropertyKeys.Description, GetString(row, "description")),
                (ArtifactPropertyKeys.IsDefault, NormalizeBoolean(GetString(row, "isdefault"))),
                (ArtifactPropertyKeys.ReadbackScope, scope),
                (ArtifactPropertyKeys.TabCount, summary.TabCount.ToString(CultureInfo.InvariantCulture)),
                (ArtifactPropertyKeys.SectionCount, summary.SectionCount.ToString(CultureInfo.InvariantCulture)),
                (ArtifactPropertyKeys.ControlCount, summary.ControlCount.ToString(CultureInfo.InvariantCulture)),
                (ArtifactPropertyKeys.QuickFormCount, summary.QuickFormCount.ToString(CultureInfo.InvariantCulture)),
                (ArtifactPropertyKeys.SubgridCount, summary.SubgridCount.ToString(CultureInfo.InvariantCulture)),
                (ArtifactPropertyKeys.HeaderControlCount, summary.HeaderControlCount.ToString(CultureInfo.InvariantCulture)),
                (ArtifactPropertyKeys.FooterControlCount, summary.FooterControlCount.ToString(CultureInfo.InvariantCulture)),
                (ArtifactPropertyKeys.ControlDescriptionCount, summary.ControlDescriptions.Count.ToString(CultureInfo.InvariantCulture)),
                (ArtifactPropertyKeys.SummaryJson, summaryJson),
                (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(summaryJson))));
    }

    private static FamilyArtifact? CreateViewArtifact(string entityLogicalName, JsonObject row, string scope)
    {
        var viewId = NormalizeGuid(GetString(row, "savedqueryid"));
        var displayName = GetString(row, "name");
        if (string.IsNullOrWhiteSpace(viewId) || string.IsNullOrWhiteSpace(displayName))
        {
            return null;
        }

        var targetEntity = NormalizeLogicalName(GetString(row, "returnedtypecode")) ?? entityLogicalName;
        var summary = SummarizeViewXml(GetString(row, "fetchxml"), GetString(row, "layoutxml"), targetEntity);
        var summaryJson = SerializeJson(new
        {
            targetEntity = summary.TargetEntity,
            layoutColumns = summary.LayoutColumns,
            fetchAttributes = summary.FetchAttributes,
            filters = summary.Filters.Select(filter => new
            {
                attribute = filter.Attribute,
                @operator = filter.Operator,
                value = filter.Value
            }).ToArray(),
            orders = summary.Orders.Select(order => new
            {
                attribute = order.Attribute,
                descending = order.Descending
            }).ToArray()
        });

        return new FamilyArtifact(
            ComponentFamily.View,
            $"{entityLogicalName}|{displayName}",
            displayName,
            $"savedqueries/{viewId}",
            EvidenceKind.Readback,
            CreateProperties(
                (ArtifactPropertyKeys.EntityLogicalName, entityLogicalName),
                (ArtifactPropertyKeys.ViewId, viewId),
                (ArtifactPropertyKeys.QueryType, GetString(row, "querytype")),
                (ArtifactPropertyKeys.TargetEntity, targetEntity),
                (ArtifactPropertyKeys.ReadbackScope, scope),
                (ArtifactPropertyKeys.LayoutColumnsJson, SerializeJson(summary.LayoutColumns)),
                (ArtifactPropertyKeys.FetchAttributesJson, SerializeJson(summary.FetchAttributes)),
                (ArtifactPropertyKeys.FiltersJson, SerializeJson(summary.Filters)),
                (ArtifactPropertyKeys.OrdersJson, SerializeJson(summary.Orders)),
                (ArtifactPropertyKeys.SummaryJson, summaryJson),
                (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(summaryJson))));
    }

    private static string NormalizeStringArrayJson(JsonArray values) =>
        SerializeJson(values
            .Select(StringValue)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => NormalizeLogicalName(value)!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray());

    private static AppModuleContext? CreateAppModuleContext(JsonObject row)
    {
        var id = GetGuid(row, "appmoduleid");
        var uniqueName = NormalizeLogicalName(GetString(row, "uniquename"));
        if (!id.HasValue || string.IsNullOrWhiteSpace(uniqueName))
        {
            return null;
        }

        var componentTypes = ReadArray(row, "components")
            .OfType<JsonObject>()
            .Select(component => GetString(component, "componenttype"))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .Cast<string>()
            .ToArray();

        var roleIds = ReadArray(row, "role_ids")
            .Select(StringValue)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .Cast<string>()
            .ToArray();

        var nestedSettings = ReadArray(row, "app_settings")
            .OfType<JsonObject>()
            .Select(setting => new AppModuleSetting(
                GetString(setting, "setting_definition_unique_name") ?? string.Empty,
                GetString(setting, "value")))
            .Where(setting => !string.IsNullOrWhiteSpace(setting.SettingDefinitionUniqueName))
            .ToArray();

        return new AppModuleContext(
            id.Value,
            uniqueName!,
            GetString(row, "name") ?? uniqueName,
            GetString(row, "description"),
            componentTypes,
            roleIds,
            nestedSettings);
    }

    private static FamilyArtifact CreateAppModuleArtifact(AppModuleContext context)
    {
        var summaryJson = SerializeJson(new
        {
            componentTypes = context.ComponentTypes,
            roleIds = context.RoleIds,
            roleMapCount = context.RoleIds.Length,
            appSettingCount = context.NestedSettings.Length
        });

        return new FamilyArtifact(
            ComponentFamily.AppModule,
            context.UniqueName,
            context.DisplayName,
            $"appmodules/{context.UniqueName}",
            EvidenceKind.Readback,
            CreateProperties(
                (ArtifactPropertyKeys.Description, context.Description),
                (ArtifactPropertyKeys.ComponentTypesJson, SerializeJson(context.ComponentTypes)),
                (ArtifactPropertyKeys.RoleIdsJson, SerializeJson(context.RoleIds)),
                (ArtifactPropertyKeys.RoleMapCount, context.RoleIds.Length.ToString(CultureInfo.InvariantCulture)),
                (ArtifactPropertyKeys.AppSettingCount, context.NestedSettings.Length.ToString(CultureInfo.InvariantCulture)),
                (ArtifactPropertyKeys.SummaryJson, summaryJson),
                (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(summaryJson))));
    }

    private static FamilyArtifact? CreateAppSettingArtifact(
        JsonObject row,
        IReadOnlyDictionary<Guid, AppModuleContext> appModulesById,
        ICollection<CompilerDiagnostic> diagnostics)
    {
        var parentAppModuleId = GetGuid(row, "_parentappmoduleid_value");
        if (!parentAppModuleId.HasValue || !appModulesById.TryGetValue(parentAppModuleId.Value, out var appModule))
        {
            diagnostics.Add(new CompilerDiagnostic(
                "live-readback-appsetting-parent-missing",
                DiagnosticSeverity.Warning,
                "App setting readback returned a row whose parent app module was not available for stable rejoin.",
                GetString(row, "appsettingid")));
            return null;
        }

        var rowValue = GetString(row, "value");
        var uniqueName = GetString(row, "uniquename");
        if (string.IsNullOrWhiteSpace(uniqueName))
        {
            uniqueName = appModule.NestedSettings
                .Where(setting => string.Equals(setting.Value ?? string.Empty, rowValue ?? string.Empty, StringComparison.Ordinal))
                .Select(setting => setting.SettingDefinitionUniqueName)
                .DefaultIfEmpty(appModule.NestedSettings.Length == 1 ? appModule.NestedSettings[0].SettingDefinitionUniqueName : string.Empty)
                .FirstOrDefault();
        }

        if (string.IsNullOrWhiteSpace(uniqueName))
        {
            uniqueName = NormalizeLogicalName(GetString(row, "_settingdefinitionid_value")) ?? "unknown-setting";
            diagnostics.Add(new CompilerDiagnostic(
                "live-readback-appsetting-definition-fallback",
                DiagnosticSeverity.Warning,
                "App setting readback required a definition-id fallback because a stable setting definition unique name was not available.",
                GetString(row, "appsettingid")));
        }

        return new FamilyArtifact(
            ComponentFamily.AppSetting,
            $"{appModule.UniqueName}|{uniqueName}",
            uniqueName,
            $"appsettings/{GetString(row, "appsettingid")}",
            EvidenceKind.Readback,
            CreateProperties(
                (ArtifactPropertyKeys.ParentAppModuleUniqueName, appModule.UniqueName),
                (ArtifactPropertyKeys.SettingDefinitionUniqueName, uniqueName),
                (ArtifactPropertyKeys.Value, rowValue)));
    }

    private static FamilyArtifact? CreateSiteMapArtifact(JsonObject row)
    {
        var uniqueName = NormalizeLogicalName(GetString(row, "sitemapnameunique"));
        if (string.IsNullOrWhiteSpace(uniqueName))
        {
            return null;
        }

        var summary = SummarizeSiteMapXml(GetString(row, "sitemapxml"));
        var summaryJson = SerializeJson(new
        {
            areaCount = summary.AreaCount,
            groupCount = summary.GroupCount,
            subAreaCount = summary.SubAreaCount,
            webResourceSubAreaCount = summary.WebResourceSubAreaCount
        });

        return new FamilyArtifact(
            ComponentFamily.SiteMap,
            uniqueName,
            GetString(row, "sitemapname") ?? uniqueName,
            $"sitemaps/{uniqueName}",
            EvidenceKind.Readback,
            CreateProperties(
                (ArtifactPropertyKeys.AreaCount, summary.AreaCount.ToString(CultureInfo.InvariantCulture)),
                (ArtifactPropertyKeys.GroupCount, summary.GroupCount.ToString(CultureInfo.InvariantCulture)),
                (ArtifactPropertyKeys.SubAreaCount, summary.SubAreaCount.ToString(CultureInfo.InvariantCulture)),
                (ArtifactPropertyKeys.WebResourceSubAreaCount, summary.WebResourceSubAreaCount.ToString(CultureInfo.InvariantCulture)),
                (ArtifactPropertyKeys.SummaryJson, summaryJson),
                (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(summaryJson))));
    }

    private static FamilyArtifact? CreateEnvironmentVariableDefinitionArtifact(JsonObject row)
    {
        var schemaName = NormalizeLogicalName(GetString(row, "schemaname"));
        if (string.IsNullOrWhiteSpace(schemaName))
        {
            return null;
        }

        return new FamilyArtifact(
            ComponentFamily.EnvironmentVariableDefinition,
            schemaName,
            GetString(row, "displayname") ?? schemaName,
            $"environmentvariabledefinitions/{schemaName}",
            EvidenceKind.Readback,
            CreateProperties(
                (ArtifactPropertyKeys.DefaultValue, GetString(row, "defaultvalue")),
                (ArtifactPropertyKeys.SecretStore, GetString(row, "secretstore")),
                (ArtifactPropertyKeys.ValueSchema, GetString(row, "valueschema")),
                (ArtifactPropertyKeys.AttributeType, GetString(row, "type"))));
    }

    private static FamilyArtifact? CreateEnvironmentVariableValueArtifact(
        JsonObject row,
        IReadOnlyDictionary<Guid, string> definitionSchemaById,
        ICollection<CompilerDiagnostic> diagnostics)
    {
        var schemaName = NormalizeLogicalName(GetString(row, "schemaname"));
        if (string.IsNullOrWhiteSpace(schemaName))
        {
            return null;
        }

        var definitionSchemaName = NormalizeLogicalName(GetString(row, "definition_schema_name"));
        if (string.IsNullOrWhiteSpace(definitionSchemaName))
        {
            var definitionId = GetGuid(row, "_environmentvariabledefinitionid_value");
            if (definitionId.HasValue && definitionSchemaById.TryGetValue(definitionId.Value, out var mappedSchemaName))
            {
                definitionSchemaName = mappedSchemaName;
            }
            else
            {
                diagnostics.Add(new CompilerDiagnostic(
                    "live-readback-env-value-definition-missing",
                    DiagnosticSeverity.Warning,
                    $"Environment variable value '{schemaName}' was captured without a matching definition row. Keeping the value artifact as readback evidence.",
                    schemaName));
                definitionSchemaName = schemaName;
            }
        }

        return new FamilyArtifact(
            ComponentFamily.EnvironmentVariableValue,
            schemaName,
            schemaName,
            $"environmentvariablevalues/{schemaName}",
            EvidenceKind.Readback,
            CreateProperties(
                (ArtifactPropertyKeys.DefinitionSchemaName, definitionSchemaName),
                (ArtifactPropertyKeys.Value, GetString(row, "value"))));
    }

    private static FamilyArtifact? CreateAiProjectTypeArtifact(JsonObject row)
    {
        var logicalName = NormalizeLogicalName(GetString(row, "msdyn_uniquename", "uniquename", "msdyn_name"));
        if (string.IsNullOrWhiteSpace(logicalName))
        {
            return null;
        }

        var description = GetString(row, "description");
        var summaryJson = SerializeJson(new
        {
            logicalName,
            description
        });

        return new FamilyArtifact(
            ComponentFamily.AiProjectType,
            logicalName,
            GetString(row, "msdyn_name", "name") ?? logicalName,
            $"msdyn_aiprojecttypes/{logicalName}",
            EvidenceKind.Readback,
            CreateProperties(
                (ArtifactPropertyKeys.Description, description),
                (ArtifactPropertyKeys.SummaryJson, summaryJson),
                (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(summaryJson))));
    }

    private static FamilyArtifact? CreateAiProjectArtifact(JsonObject row, IReadOnlyDictionary<Guid, string> projectTypeLogicalNameById)
    {
        var logicalName = NormalizeLogicalName(GetString(row, "msdyn_uniquename", "uniquename", "msdyn_name"));
        if (string.IsNullOrWhiteSpace(logicalName))
        {
            return null;
        }

        var parentProjectTypeLogicalName = NormalizeLogicalName(GetString(row, "msdyn_projecttypeuniquename"));
        if (string.IsNullOrWhiteSpace(parentProjectTypeLogicalName))
        {
            var projectTypeId = GetGuid(row, "_msdyn_aiprojecttypeid_value");
            if (projectTypeId.HasValue && projectTypeLogicalNameById.TryGetValue(projectTypeId.Value, out var mappedLogicalName))
            {
                parentProjectTypeLogicalName = mappedLogicalName;
            }
        }

        var targetEntity = NormalizeLogicalName(GetString(row, "msdyn_targetentity", "targetentity"));
        var description = GetString(row, "description");
        var summaryJson = SerializeJson(new
        {
            logicalName,
            parentProjectTypeLogicalName,
            targetEntity,
            description
        });

        return new FamilyArtifact(
            ComponentFamily.AiProject,
            logicalName,
            GetString(row, "msdyn_name", "name") ?? logicalName,
            $"msdyn_aiprojects/{logicalName}",
            EvidenceKind.Readback,
            CreateProperties(
                (ArtifactPropertyKeys.ParentAiProjectTypeLogicalName, parentProjectTypeLogicalName),
                (ArtifactPropertyKeys.TargetEntity, targetEntity),
                (ArtifactPropertyKeys.Description, description),
                (ArtifactPropertyKeys.SummaryJson, summaryJson),
                (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(summaryJson))));
    }

    private static FamilyArtifact? CreateAiConfigurationArtifact(JsonObject row, IReadOnlyDictionary<Guid, string> projectLogicalNameById)
    {
        var logicalName = NormalizeLogicalName(GetString(row, "msdyn_uniquename", "uniquename", "msdyn_name"));
        if (string.IsNullOrWhiteSpace(logicalName))
        {
            return null;
        }

        var parentProjectLogicalName = NormalizeLogicalName(GetString(row, "msdyn_projectuniquename"));
        if (string.IsNullOrWhiteSpace(parentProjectLogicalName))
        {
            var projectId = GetGuid(row, "_msdyn_aiprojectid_value");
            if (projectId.HasValue && projectLogicalNameById.TryGetValue(projectId.Value, out var mappedLogicalName))
            {
                parentProjectLogicalName = mappedLogicalName;
            }
        }

        var configurationKind = NormalizeLogicalName(GetString(row, "msdyn_configurationkind"))
            ?? NormalizeLogicalName(GetString(row, "msdyn_type"));
        var value = GetString(row, "msdyn_configurationvalue", "msdyn_runconfiguration", "msdyn_customconfiguration");
        var summaryJson = SerializeJson(new
        {
            logicalName,
            parentProjectLogicalName,
            configurationKind,
            value
        });

        return new FamilyArtifact(
            ComponentFamily.AiConfiguration,
            logicalName,
            GetString(row, "msdyn_name") ?? logicalName,
            $"msdyn_aiconfigurations/{logicalName}",
            EvidenceKind.Readback,
            CreateProperties(
                (ArtifactPropertyKeys.ParentAiProjectLogicalName, parentProjectLogicalName),
                (ArtifactPropertyKeys.ConfigurationKind, configurationKind),
                (ArtifactPropertyKeys.Value, value),
                (ArtifactPropertyKeys.SummaryJson, summaryJson),
                (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(summaryJson))));
    }

    private static FamilyArtifact? CreateEntityAnalyticsConfigurationArtifact(JsonObject row)
    {
        var parentEntityLogicalName = NormalizeLogicalName(GetString(row, "parententitylogicalname"));
        if (string.IsNullOrWhiteSpace(parentEntityLogicalName))
        {
            return null;
        }

        var summaryJson = SerializeJson(new
        {
            parentEntityLogicalName,
            entityDataSource = GetString(row, "entitydatasource"),
            isEnabledForAdls = NormalizeBoolean(GetString(row, "isenabledforadls")),
            isEnabledForTimeSeries = NormalizeBoolean(GetString(row, "isenabledfortimeseries"))
        });

        return new FamilyArtifact(
            ComponentFamily.EntityAnalyticsConfiguration,
            parentEntityLogicalName,
            parentEntityLogicalName,
            $"entityanalyticsconfigs/{parentEntityLogicalName}",
            EvidenceKind.Readback,
            CreateProperties(
                (ArtifactPropertyKeys.ParentEntityLogicalName, parentEntityLogicalName),
                (ArtifactPropertyKeys.EntityDataSource, GetString(row, "entitydatasource")),
                (ArtifactPropertyKeys.IsEnabledForAdls, NormalizeBoolean(GetString(row, "isenabledforadls"))),
                (ArtifactPropertyKeys.IsEnabledForTimeSeries, NormalizeBoolean(GetString(row, "isenabledfortimeseries"))),
                (ArtifactPropertyKeys.SummaryJson, summaryJson),
                (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(summaryJson))));
    }

    private static FamilyArtifact? CreateCanvasAppArtifact(JsonObject row)
    {
        var logicalName = NormalizeLogicalName(GetString(row, "name"));
        if (string.IsNullOrWhiteSpace(logicalName))
        {
            return null;
        }

        var tagsJson = NormalizeJson(GetString(row, "tags"));
        var authorizationReferencesJson = NormalizeJson(GetString(row, "authorizationreferences")) ?? "[]";
        var connectionReferencesJson = NormalizeJson(GetString(row, "connectionreferences")) ?? "{}";
        var databaseReferencesJson = NormalizeJson(GetString(row, "databasereferences"));
        var cdsDependenciesJson = NormalizeJson(GetString(row, "cdsdependencies"));
        var summaryJson = SerializeJson(new
        {
            appVersion = GetString(row, "appversion"),
            status = GetString(row, "status"),
            createdByClientVersion = GetString(row, "createdbyclientversion"),
            minClientVersion = GetString(row, "minclientversion"),
            tags = JsonNode.Parse(tagsJson ?? "{}"),
            authorizationReferences = JsonNode.Parse(authorizationReferencesJson),
            connectionReferences = JsonNode.Parse(connectionReferencesJson),
            databaseReferences = JsonNode.Parse(databaseReferencesJson ?? "{}"),
            canConsumeAppPass = NormalizeBoolean(GetString(row, "canconsumeapppass")),
            canvasAppType = GetString(row, "canvasapptype"),
            introducedVersion = GetString(row, "introducedversion"),
            cdsDependencies = JsonNode.Parse(cdsDependenciesJson ?? "{}"),
            isCustomizable = NormalizeBoolean(StringValue(GetProperty(row, "iscustomizable")))
        });

        return new FamilyArtifact(
            ComponentFamily.CanvasApp,
            logicalName,
            GetString(row, "displayname") ?? logicalName,
            $"canvasapps/{logicalName}",
            EvidenceKind.Readback,
            CreateProperties(
                (ArtifactPropertyKeys.AppVersion, GetString(row, "appversion")),
                (ArtifactPropertyKeys.Status, GetString(row, "status")),
                (ArtifactPropertyKeys.CreatedByClientVersion, GetString(row, "createdbyclientversion")),
                (ArtifactPropertyKeys.MinClientVersion, GetString(row, "minclientversion")),
                (ArtifactPropertyKeys.Description, GetString(row, "description")),
                (ArtifactPropertyKeys.TagsJson, tagsJson),
                (ArtifactPropertyKeys.AuthorizationReferencesJson, authorizationReferencesJson),
                (ArtifactPropertyKeys.ConnectionReferencesJson, connectionReferencesJson),
                (ArtifactPropertyKeys.DatabaseReferencesJson, databaseReferencesJson),
                (ArtifactPropertyKeys.CanConsumeAppPass, NormalizeBoolean(GetString(row, "canconsumeapppass"))),
                (ArtifactPropertyKeys.CanvasAppType, GetString(row, "canvasapptype")),
                (ArtifactPropertyKeys.IntroducedVersion, GetString(row, "introducedversion")),
                (ArtifactPropertyKeys.CdsDependenciesJson, cdsDependenciesJson),
                (ArtifactPropertyKeys.IsCustomizable, NormalizeBoolean(StringValue(GetProperty(row, "iscustomizable")))),
                (ArtifactPropertyKeys.BackgroundImageUri, GetString(row, "backgroundimage_name")),
                (ArtifactPropertyKeys.DocumentUri, GetString(row, "document_name")),
                (ArtifactPropertyKeys.SummaryJson, summaryJson),
                (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(summaryJson))));
    }
}
