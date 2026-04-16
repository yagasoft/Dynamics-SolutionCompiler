using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
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

        strictScopeRows = strictScopeRows
            .Where(row => string.Equals(
                NormalizeLogicalName(GetString(row, "objecttypecode")),
                entityLogicalName,
                StringComparison.OrdinalIgnoreCase))
            .ToList();

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

        strictScopeRows = strictScopeRows
            .Where(row => string.Equals(
                NormalizeLogicalName(GetString(row, "returnedtypecode")),
                entityLogicalName,
                StringComparison.OrdinalIgnoreCase))
            .ToList();

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

    private async Task<IReadOnlyList<FamilyArtifact>> ReadVisualizationArtifactsAsync(
        string entityLogicalName,
        IReadOnlyCollection<Guid> scopedVisualizationIds,
        CancellationToken cancellationToken)
    {
        if (scopedVisualizationIds.Count == 0)
        {
            return [];
        }

        var rows = await GetCollectionAsync(
            $"savedqueryvisualizations?$select=savedqueryvisualizationid,name,description,charttype,type,primaryentitytypecode,datadescription,presentationdescription&$filter={BuildGuidFilter("savedqueryvisualizationid", scopedVisualizationIds)}",
            cancellationToken).ConfigureAwait(false);

        return rows
            .OfType<JsonObject>()
            .Where(row => string.Equals(
                NormalizeLogicalName(GetString(row, "primaryentitytypecode")),
                entityLogicalName,
                StringComparison.OrdinalIgnoreCase))
            .Select(row => CreateVisualizationArtifact(entityLogicalName, row))
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

        if (requestedFamilies.Contains(ComponentFamily.CustomControl) && scope.CustomControlIds.Count > 0)
        {
            try
            {
                var rows = await GetCollectionAsync(
                    $"customcontrols?$select=customcontrolid,name,manifest,authoringmanifest,clientjson,compatibledatatypes,supportedplatform,version&$filter={BuildGuidFilter("customcontrolid", scope.CustomControlIds)}",
                    cancellationToken).ConfigureAwait(false);

                foreach (var row in rows.OfType<JsonObject>())
                {
                    var artifact = CreateCustomControlArtifact(row, diagnostics);
                    if (artifact is not null)
                    {
                        artifacts.Add(artifact);
                    }
                }
            }
            catch (Exception exception) when (exception is DataverseWebApiException or Azure.Identity.AuthenticationFailedException)
            {
                diagnostics.Add(CreateFamilyFailureDiagnostic(ComponentFamily.CustomControl, "customcontrols", exception));
            }
        }

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

        if (requestedFamilies.Contains(ComponentFamily.WebResource) && scope.WebResourceIds.Count > 0)
        {
            try
            {
                var rows = await GetCollectionAsync(
                    $"webresourceset?$select=webresourceid,name,displayname,description,webresourcetype,content&$filter={BuildGuidFilter("webresourceid", scope.WebResourceIds)}",
                    cancellationToken).ConfigureAwait(false);

                foreach (var row in rows.OfType<JsonObject>())
                {
                    var artifact = CreateWebResourceArtifact(row, diagnostics);
                    if (artifact is not null)
                    {
                        artifacts.Add(artifact);
                    }
                }
            }
            catch (Exception exception) when (exception is DataverseWebApiException or Azure.Identity.AuthenticationFailedException)
            {
                diagnostics.Add(CreateFamilyFailureDiagnostic(ComponentFamily.WebResource, "webresourceset", exception));
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
                    $"msdyn_aitemplates?$select=msdyn_aitemplateid,msdyn_uniquename,msdyn_resourceinfo,msdyn_templateversion,msdyn_istrainable&$filter={BuildGuidFilter("msdyn_aitemplateid", scope.AiProjectTypeIds)}",
                    cancellationToken).ConfigureAwait(false);

                foreach (var row in rows.OfType<JsonObject>())
                {
                    var logicalName = NormalizeLogicalName(GetString(row, "msdyn_uniquename", "uniquename"));
                    var id = GetGuid(row, "msdyn_aitemplateid");
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
                diagnostics.Add(CreateFamilyFailureDiagnostic(ComponentFamily.AiProjectType, "msdyn_aitemplates", exception));
            }
        }

        var aiProjectLogicalNameById = new Dictionary<Guid, string>();
        if (requestedFamilies.Contains(ComponentFamily.AiProject) && scope.AiProjectIds.Count > 0)
        {
            try
            {
                var rows = await GetCollectionAsync(
                    $"msdyn_aimodels?$select=msdyn_aimodelid,msdyn_name,msdyn_modelcreationcontext,_msdyn_templateid_value&$filter={BuildGuidFilter("msdyn_aimodelid", scope.AiProjectIds)}",
                    cancellationToken).ConfigureAwait(false);

                foreach (var row in rows.OfType<JsonObject>())
                {
                    var logicalName = NormalizeLogicalName(GetAiProjectContextValue(row, "logicalName"));
                    var id = GetGuid(row, "msdyn_aimodelid");
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
                diagnostics.Add(CreateFamilyFailureDiagnostic(ComponentFamily.AiProject, "msdyn_aimodels", exception));
            }
        }

        if (requestedFamilies.Contains(ComponentFamily.AiConfiguration) && scope.AiConfigurationIds.Count > 0)
        {
            try
            {
                var rows = await GetCollectionAsync(
                    $"msdyn_aiconfigurations?$select=msdyn_aiconfigurationid,msdyn_name,msdyn_type,msdyn_runconfiguration,msdyn_customconfiguration,msdyn_resourceinfo,_msdyn_aimodelid_value&$filter={BuildGuidFilter("msdyn_aiconfigurationid", scope.AiConfigurationIds)}",
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

    private static FamilyArtifact? CreateVisualizationArtifact(string entityLogicalName, JsonObject row)
    {
        var visualizationId = NormalizeGuid(GetString(row, "savedqueryvisualizationid"));
        var displayName = GetString(row, "name");
        if (string.IsNullOrWhiteSpace(visualizationId) || string.IsNullOrWhiteSpace(displayName))
        {
            return null;
        }

        var normalizedDataDescriptionXml = NormalizeVisualizationFragment(GetString(row, "datadescription"), "datadescription");
        var normalizedPresentationDescriptionXml = NormalizeVisualizationFragment(GetString(row, "presentationdescription"), "presentationdescription");
        var summary = SummarizeVisualizationXml(
            normalizedDataDescriptionXml,
            normalizedPresentationDescriptionXml,
            NormalizeLogicalName(GetString(row, "primaryentitytypecode")) ?? entityLogicalName);
        var summaryJson = SerializeJson(new
        {
            targetEntity = summary.TargetEntity,
            chartTypes = summary.ChartTypes,
            groupByColumns = summary.GroupByColumns,
            measureAliases = summary.MeasureAliases,
            titleNames = summary.TitleNames
        });

        return new FamilyArtifact(
            ComponentFamily.Visualization,
            $"{summary.TargetEntity}|{displayName}",
            displayName,
            $"savedqueryvisualizations/{visualizationId}",
            EvidenceKind.Readback,
            CreateProperties(
                (ArtifactPropertyKeys.EntityLogicalName, entityLogicalName),
                (ArtifactPropertyKeys.TargetEntity, summary.TargetEntity),
                (ArtifactPropertyKeys.VisualizationId, visualizationId),
                (ArtifactPropertyKeys.Description, GetString(row, "description")),
                (ArtifactPropertyKeys.DataDescriptionXml, normalizedDataDescriptionXml),
                (ArtifactPropertyKeys.PresentationDescriptionXml, normalizedPresentationDescriptionXml),
                (ArtifactPropertyKeys.ChartTypesJson, SerializeJson(summary.ChartTypes)),
                (ArtifactPropertyKeys.GroupByColumnsJson, SerializeJson(summary.GroupByColumns)),
                (ArtifactPropertyKeys.MeasureAliasesJson, SerializeJson(summary.MeasureAliases)),
                (ArtifactPropertyKeys.TitleNamesJson, SerializeJson(summary.TitleNames)),
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

    private static FamilyArtifact? CreateCustomControlArtifact(JsonObject row, ICollection<CompilerDiagnostic> diagnostics)
    {
        var logicalName = NormalizeLogicalName(GetString(row, "name"));
        if (string.IsNullOrWhiteSpace(logicalName))
        {
            return null;
        }

        var manifestXml = GetString(row, "manifest");
        var clientJson = GetString(row, "clientjson");
        var summary = SummarizeCustomControl(manifestXml, clientJson, diagnostics, logicalName);
        var supportedPlatforms = NormalizeSupportedPlatforms(GetString(row, "supportedplatform"), summary.SupportedPlatforms);
        var summaryJson = SerializeJson(new
        {
            version = GetString(row, "version"),
            supportedPlatforms = JsonNode.Parse(SerializeJson(supportedPlatforms)),
            namespaceName = summary.NamespaceName,
            constructorName = summary.ConstructorName,
            controlType = summary.ControlType,
            apiVersion = summary.ApiVersion,
            propertyNames = JsonNode.Parse(SerializeJson(summary.PropertyNames)),
            datasetNames = JsonNode.Parse(SerializeJson(summary.DatasetNames)),
            featureNames = JsonNode.Parse(SerializeJson(summary.FeatureNames)),
            resourcePaths = JsonNode.Parse(SerializeJson(summary.ResourcePaths)),
            platformLibraries = JsonNode.Parse(SerializeJson(summary.PlatformLibraries))
        });

        return new FamilyArtifact(
            ComponentFamily.CustomControl,
            logicalName,
            GetString(row, "name") ?? logicalName,
            $"customcontrols/{logicalName}",
            EvidenceKind.Readback,
            CreateProperties(
                (ArtifactPropertyKeys.Name, GetString(row, "name")),
                (ArtifactPropertyKeys.Version, GetString(row, "version")),
                (ArtifactPropertyKeys.SupportedPlatformsJson, SerializeJson(supportedPlatforms)),
                (ArtifactPropertyKeys.Namespace, summary.NamespaceName),
                (ArtifactPropertyKeys.ConstructorName, summary.ConstructorName),
                (ArtifactPropertyKeys.ControlType, summary.ControlType),
                (ArtifactPropertyKeys.ApiVersion, summary.ApiVersion),
                (ArtifactPropertyKeys.PropertyCount, summary.PropertyNames.Length.ToString(CultureInfo.InvariantCulture)),
                (ArtifactPropertyKeys.DatasetCount, summary.DatasetNames.Length.ToString(CultureInfo.InvariantCulture)),
                (ArtifactPropertyKeys.FeatureCount, summary.FeatureNames.Length.ToString(CultureInfo.InvariantCulture)),
                (ArtifactPropertyKeys.ResourceCount, (summary.ResourcePaths.Length + summary.PlatformLibraries.Length).ToString(CultureInfo.InvariantCulture)),
                (ArtifactPropertyKeys.PropertyNamesJson, SerializeJson(summary.PropertyNames)),
                (ArtifactPropertyKeys.DatasetNamesJson, SerializeJson(summary.DatasetNames)),
                (ArtifactPropertyKeys.FeatureNamesJson, SerializeJson(summary.FeatureNames)),
                (ArtifactPropertyKeys.ResourcePathsJson, SerializeJson(summary.ResourcePaths)),
                (ArtifactPropertyKeys.PlatformLibrariesJson, SerializeJson(summary.PlatformLibraries)),
                (ArtifactPropertyKeys.SummaryJson, summaryJson),
                (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(summaryJson))));
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
                (ArtifactPropertyKeys.SiteMapDefinitionJson, summary.DefinitionJson),
                (ArtifactPropertyKeys.SummaryJson, summaryJson),
                (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(summary.DefinitionJson))));
    }

    private sealed record CustomControlSummary(
        string? NamespaceName,
        string? ConstructorName,
        string? ControlType,
        string? ApiVersion,
        string[] SupportedPlatforms,
        string[] PropertyNames,
        string[] DatasetNames,
        string[] FeatureNames,
        string[] ResourcePaths,
        string[] PlatformLibraries);

    private static CustomControlSummary SummarizeCustomControl(
        string? manifestXml,
        string? clientJson,
        ICollection<CompilerDiagnostic> diagnostics,
        string logicalName)
    {
        string? namespaceName = null;
        string? constructorName = null;
        string? controlType = null;
        string? apiVersion = null;
        var propertyNames = Array.Empty<string>();
        var datasetNames = Array.Empty<string>();
        var featureNames = Array.Empty<string>();
        var resourcePaths = Array.Empty<string>();
        var platformLibraries = Array.Empty<string>();
        var supportedPlatforms = Array.Empty<string>();

        if (!string.IsNullOrWhiteSpace(manifestXml))
        {
            try
            {
                var root = XDocument.Parse(manifestXml).Root;
                var control = root?.Descendants().FirstOrDefault(element => element.Name.LocalName.Equals("control", StringComparison.OrdinalIgnoreCase));
                if (control is not null)
                {
                    namespaceName = control.AttributeValue("namespace");
                    constructorName = control.AttributeValue("constructor");
                    controlType = control.AttributeValue("control-type");
                    apiVersion = control.AttributeValue("api-version");
                    propertyNames = control
                        .Descendants()
                        .Where(element => element.Name.LocalName.Equals("property", StringComparison.OrdinalIgnoreCase))
                        .Select(element => element.AttributeValue("name") ?? string.Empty)
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                    datasetNames = control
                        .Descendants()
                        .Where(element =>
                            element.Name.LocalName.Equals("data-set", StringComparison.OrdinalIgnoreCase)
                            || element.Name.LocalName.Equals("dataset", StringComparison.OrdinalIgnoreCase))
                        .Select(element => element.AttributeValue("name") ?? string.Empty)
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                    featureNames = control
                        .Descendants()
                        .Where(element => element.Name.LocalName.Equals("uses-feature", StringComparison.OrdinalIgnoreCase))
                        .Select(element => element.AttributeValue("name") ?? string.Empty)
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                    resourcePaths = control
                        .Descendants()
                        .Where(element => element.Name.LocalName.Equals("code", StringComparison.OrdinalIgnoreCase))
                        .Select(element => element.AttributeValue("path") ?? string.Empty)
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                    platformLibraries = control
                        .Descendants()
                        .Where(element => element.Name.LocalName.Equals("platform-library", StringComparison.OrdinalIgnoreCase))
                        .Select(element =>
                        {
                            var name = element.AttributeValue("name");
                            var version = element.AttributeValue("version");
                            return string.IsNullOrWhiteSpace(name)
                                ? string.Empty
                                : string.IsNullOrWhiteSpace(version)
                                    ? name!
                                    : $"{name}:{version}";
                        })
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                }
            }
            catch (Exception exception) when (exception is InvalidOperationException or System.Xml.XmlException)
            {
                diagnostics.Add(new CompilerDiagnostic(
                    "live-readback-customcontrol-manifest-best-effort",
                    DiagnosticSeverity.Warning,
                    $"CustomControl '{logicalName}' returned a manifest that could not be parsed as XML. Keeping the artifact with JSON-backed summary only.",
                    logicalName));
            }
        }

        var clientObject = ParseJsonObjectSafe(clientJson);
        var clientProperties = GetProperty(clientObject, "Properties");
        namespaceName ??= GetString(clientObject, "Namespace");
        constructorName ??= GetString(clientObject, "ConstructorName");
        controlType ??= NormalizeBoolean(GetString(clientObject, "IsVirtual")) == "true" ? "virtual" : GetString(clientObject, "ControlMode");
        apiVersion ??= GetString(clientObject, "ApiVersion");
        supportedPlatforms = NormalizeSupportedPlatforms(null, supportedPlatforms);

        propertyNames = MergeNormalizedArrays(
            propertyNames,
            ReadArray(clientProperties, "Properties")
                .OfType<JsonObject>()
                .Select(property => GetString(property, "Name")));
        datasetNames = MergeNormalizedArrays(
            datasetNames,
            ReadArray(clientProperties, "DataSets")
                .OfType<JsonObject>()
                .Select(dataset => GetString(dataset, "Name")));
        featureNames = MergeNormalizedArrays(
            featureNames,
            ReadArray(clientProperties, "FeatureUsage")
                .OfType<JsonObject>()
                .Select(feature => GetString(feature, "Name")));
        if (resourcePaths.Length == 0)
        {
            resourcePaths = ReadArray(clientProperties, "Resources")
                .OfType<JsonObject>()
                .Where(resource => !IsClientCustomControlPlatformLibrary(resource))
                .Select(resource => GetString(resource, "Name"))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        if (platformLibraries.Length == 0)
        {
            platformLibraries = ReadArray(clientProperties, "Resources")
                .OfType<JsonObject>()
                .Where(IsClientCustomControlPlatformLibrary)
                .Select(resource => GetString(resource, "LibraryName") ?? GetString(resource, "Name"))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        return new CustomControlSummary(
            namespaceName,
            constructorName,
            controlType,
            apiVersion,
            supportedPlatforms,
            propertyNames,
            datasetNames,
            featureNames,
            resourcePaths,
            platformLibraries);
    }

    private static string[] NormalizeSupportedPlatforms(string? raw, IEnumerable<string>? fallback)
    {
        var values = new List<string>();
        if (!string.IsNullOrWhiteSpace(raw))
        {
            values.AddRange(raw
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => value.Trim().Trim('(', ')'))
                .Where(value => !string.IsNullOrWhiteSpace(value)));
        }

        if (fallback is not null)
        {
            values.AddRange(fallback.Where(value => !string.IsNullOrWhiteSpace(value)));
        }

        return values
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string[] MergeNormalizedArrays(IEnumerable<string> primary, IEnumerable<string?> secondary) =>
        primary
            .Concat(secondary.Where(value => !string.IsNullOrWhiteSpace(value))!)
            .Select(value => value!)
            .Select(value => value.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static bool IsClientCustomControlPlatformLibrary(JsonObject resource)
    {
        var resourceType = GetInt32(resource, "Type");
        return resourceType == 0 || !string.IsNullOrWhiteSpace(GetString(resource, "LibraryName"));
    }

    private static FamilyArtifact? CreateWebResourceArtifact(JsonObject row, ICollection<CompilerDiagnostic> diagnostics)
    {
        var logicalName = GetString(row, "name");
        if (string.IsNullOrWhiteSpace(logicalName))
        {
            return null;
        }

        var type = GetString(row, "webresourcetype");
        var content = GetString(row, "content");
        string? byteLength = null;
        string? contentHash = null;
        if (string.IsNullOrWhiteSpace(content))
        {
            diagnostics.Add(new CompilerDiagnostic(
                "live-readback-webresource-content-best-effort",
                DiagnosticSeverity.Warning,
                $"WebResource '{logicalName}' was returned without content bytes. Keeping the artifact without payload hash evidence.",
                GetString(row, "webresourceid") ?? logicalName));
        }
        else if (TryDecodeBase64Content(content, out var contentBytes))
        {
            byteLength = contentBytes.Length.ToString(CultureInfo.InvariantCulture);
            contentHash = ComputeByteHash(contentBytes);
        }
        else
        {
            diagnostics.Add(new CompilerDiagnostic(
                "live-readback-webresource-content-best-effort",
                DiagnosticSeverity.Warning,
                $"WebResource '{logicalName}' returned content that could not be decoded as base64. Keeping the artifact without payload hash evidence.",
                GetString(row, "webresourceid") ?? logicalName));
        }

        var summaryJson = SerializeJson(new
        {
            webResourceType = type,
            byteLength,
            contentHash
        });

        return new FamilyArtifact(
            ComponentFamily.WebResource,
            logicalName,
            GetString(row, "displayname") ?? logicalName,
            $"webresourceset/{logicalName}",
            EvidenceKind.Readback,
            CreateProperties(
                (ArtifactPropertyKeys.Description, GetString(row, "description")),
                (ArtifactPropertyKeys.WebResourceType, type),
                (ArtifactPropertyKeys.WebResourceTypeLabel, DescribeWebResourceType(type)),
                (ArtifactPropertyKeys.ByteLength, byteLength),
                (ArtifactPropertyKeys.ContentHash, contentHash),
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
        var logicalName = NormalizeLogicalName(GetString(row, "msdyn_uniquename", "uniquename"));
        if (string.IsNullOrWhiteSpace(logicalName))
        {
            return null;
        }

        var description = GetAiProjectTypeResourceInfoValue(row, "description");
        var displayName = GetAiProjectTypeResourceInfoValue(row, "displayName") ?? logicalName;
        var summaryJson = SerializeJson(new
        {
            logicalName,
            description
        });

        return new FamilyArtifact(
            ComponentFamily.AiProjectType,
            logicalName,
            displayName,
            $"msdyn_aitemplates/{logicalName}",
            EvidenceKind.Readback,
            CreateProperties(
                (ArtifactPropertyKeys.Description, description),
                (ArtifactPropertyKeys.SummaryJson, summaryJson),
                (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(summaryJson))));
    }

    private static FamilyArtifact? CreateAiProjectArtifact(JsonObject row, IReadOnlyDictionary<Guid, string> projectTypeLogicalNameById)
    {
        var logicalName = NormalizeLogicalName(GetAiProjectContextValue(row, "logicalName"));
        if (string.IsNullOrWhiteSpace(logicalName))
        {
            return null;
        }

        string? parentProjectTypeLogicalName = null;
        var projectTypeId = GetGuid(row, "_msdyn_templateid_value");
        if (projectTypeId.HasValue && projectTypeLogicalNameById.TryGetValue(projectTypeId.Value, out var mappedLogicalName))
        {
            parentProjectTypeLogicalName = mappedLogicalName;
        }

        var targetEntity = NormalizeLogicalName(GetAiProjectContextValue(row, "targetEntity"));
        var description = GetAiProjectContextValue(row, "description");
        var displayName = GetString(row, "msdyn_name", "name") ?? logicalName;
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
            displayName,
            $"msdyn_aimodels/{logicalName}",
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
        var logicalName = NormalizeLogicalName(GetAiConfigurationResourceInfoValue(row, "logicalName"))
            ?? NormalizeLogicalName(GetString(row, "msdyn_name"));
        if (string.IsNullOrWhiteSpace(logicalName))
        {
            return null;
        }

        var parentProjectLogicalName = NormalizeLogicalName(GetAiConfigurationResourceInfoValue(row, "parentProjectLogicalName"));
        if (string.IsNullOrWhiteSpace(parentProjectLogicalName))
        {
            var projectId = GetGuid(row, "_msdyn_aimodelid_value");
            if (projectId.HasValue && projectLogicalNameById.TryGetValue(projectId.Value, out var mappedLogicalName))
            {
                parentProjectLogicalName = mappedLogicalName;
            }
        }

        var configurationKind = NormalizeAiConfigurationKind(GetString(row, "msdyn_configurationkind"))
            ?? NormalizeAiConfigurationKind(GetString(row, "msdyn_type"));
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

    private static string? NormalizeAiConfigurationKind(string? value)
    {
        var normalized = NormalizeLogicalName(value);
        return normalized switch
        {
            null => null,
            "190690000" or "trainingconfiguration" or "training" => "training",
            "190690001" or "runconfiguration" or "run" => "run",
            _ => normalized
        };
    }

    private static string? GetAiProjectTypeResourceInfoValue(JsonNode? row, string propertyName) =>
        GetString(ParseJsonObjectSafe(GetString(row, "msdyn_resourceinfo")), propertyName);

    private static string? GetAiProjectContextValue(JsonNode? row, string propertyName) =>
        GetString(ParseJsonObjectSafe(GetString(row, "msdyn_modelcreationcontext")), propertyName);

    private static string? GetAiConfigurationResourceInfoValue(JsonNode? row, string propertyName) =>
        GetString(ParseJsonObjectSafe(GetString(row, "msdyn_resourceinfo")), propertyName);

    private static JsonObject? ParseJsonObjectSafe(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(value) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static FamilyArtifact? CreateEntityAnalyticsConfigurationArtifact(JsonObject row)
    {
        var parentEntityLogicalName = NormalizeLogicalName(GetString(row, "parententitylogicalname"));
        if (string.IsNullOrWhiteSpace(parentEntityLogicalName))
        {
            return null;
        }

        var entityDataSource = NormalizeEntityAnalyticsDataSource(GetString(row, "entitydatasource"));

        var summaryJson = SerializeJson(new
        {
            parentEntityLogicalName,
            entityDataSource,
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
                (ArtifactPropertyKeys.EntityDataSource, entityDataSource),
                (ArtifactPropertyKeys.IsEnabledForAdls, NormalizeBoolean(GetString(row, "isenabledforadls"))),
                (ArtifactPropertyKeys.IsEnabledForTimeSeries, NormalizeBoolean(GetString(row, "isenabledfortimeseries"))),
                (ArtifactPropertyKeys.SummaryJson, summaryJson),
                (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(summaryJson))));
    }

    private static string? NormalizeEntityAnalyticsDataSource(string? value)
    {
        var normalized = NormalizeLogicalName(value);
        return normalized switch
        {
            null => null,
            "0" or "none" => "none",
            "1" or "dataverse" => "dataverse",
            "2" or "fnotables" => "fnotables",
            _ => normalized
        };
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
