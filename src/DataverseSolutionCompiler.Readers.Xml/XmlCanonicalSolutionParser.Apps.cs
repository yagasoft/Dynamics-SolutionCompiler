using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using DataverseSolutionCompiler.Domain.Model;

namespace DataverseSolutionCompiler.Readers.Xml;

internal sealed partial class XmlCanonicalSolutionParser
{
    private void ParseAppModules()
    {
        var appModulesDirectory = Path.Combine(_root, "AppModules");
        if (!Directory.Exists(appModulesDirectory))
        {
            return;
        }

        foreach (var directory in Directory.GetDirectories(appModulesDirectory).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var appModulePath = Path.Combine(directory, "AppModule.xml");
            if (!File.Exists(appModulePath))
            {
                continue;
            }

            var root = LoadRoot(appModulePath);
            var uniqueName = NormalizeLogicalName(Text(root.ElementLocal("UniqueName")) ?? Path.GetFileName(directory));
            var displayName = LocalizedDescription(root.ElementLocal("LocalizedNames")) ?? uniqueName;
            var description = LocalizedDescription(root.ElementLocal("Descriptions"));
            var componentTypes = root
                .ElementLocal("AppModuleComponents")
                ?.Elements()
                .Where(element => element.Name.LocalName.Equals("AppModuleComponent", StringComparison.OrdinalIgnoreCase))
                .Select(element => element.AttributeValue("type") ?? string.Empty)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray()
                ?? [];
            var roleIds = root
                .ElementLocal("AppModuleRoleMaps")
                ?.Elements()
                .Where(element => element.Name.LocalName.Equals("Role", StringComparison.OrdinalIgnoreCase))
                .Select(element => NormalizeGuid(element.AttributeValue("id")))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray()
                ?? [];
            var appSettings = root
                .ElementLocal("appsettings")
                ?.Elements()
                .Where(element => element.Name.LocalName.Equals("appsetting", StringComparison.OrdinalIgnoreCase))
                .ToArray()
                ?? [];
            var summaryJson = SerializeJson(new
            {
                componentTypes,
                roleIds,
                roleMapCount = roleIds.Length,
                appSettingCount = appSettings.Length
            });

            AddArtifact(
                ComponentFamily.AppModule,
                uniqueName!,
                displayName,
                appModulePath,
                CreateProperties(
                    (ArtifactPropertyKeys.Description, description),
                    (ArtifactPropertyKeys.MetadataSourcePath, RelativePath(appModulePath)),
                    (ArtifactPropertyKeys.ComponentTypesJson, SerializeJson(componentTypes)),
                    (ArtifactPropertyKeys.RoleIdsJson, SerializeJson(roleIds)),
                    (ArtifactPropertyKeys.RoleMapCount, roleIds.Length.ToString(CultureInfo.InvariantCulture)),
                    (ArtifactPropertyKeys.AppSettingCount, appSettings.Length.ToString(CultureInfo.InvariantCulture)),
                    (ArtifactPropertyKeys.SummaryJson, summaryJson),
                    (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(summaryJson))));

            foreach (var appSetting in appSettings)
            {
                var settingDefinitionUniqueName = appSetting.AttributeValue("settingdefinitionid.uniquename") ?? string.Empty;
                AddArtifact(
                    ComponentFamily.AppSetting,
                    $"{uniqueName}|{settingDefinitionUniqueName}",
                    settingDefinitionUniqueName,
                    appModulePath,
                    CreateProperties(
                        (ArtifactPropertyKeys.MetadataSourcePath, RelativePath(appModulePath)),
                        (ArtifactPropertyKeys.ParentAppModuleUniqueName, uniqueName),
                        (ArtifactPropertyKeys.SettingDefinitionUniqueName, settingDefinitionUniqueName),
                        (ArtifactPropertyKeys.Value, Text(appSetting.ElementLocal("value")))));
            }
        }
    }

    private void ParseSiteMaps()
    {
        var siteMapsDirectory = Path.Combine(_root, "AppModuleSiteMaps");
        if (!Directory.Exists(siteMapsDirectory))
        {
            return;
        }

        foreach (var directory in Directory.GetDirectories(siteMapsDirectory).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var siteMapPath = Path.Combine(directory, "AppModuleSiteMap.xml");
            if (!File.Exists(siteMapPath))
            {
                continue;
            }

            var root = LoadRoot(siteMapPath);
            var uniqueName = NormalizeLogicalName(Text(root.ElementLocal("SiteMapUniqueName")) ?? Path.GetFileName(directory));
            var displayName = LocalizedDescription(root.ElementLocal("LocalizedNames")) ?? uniqueName;
            var siteMap = root.ElementLocal("SiteMap");
            var areaCount = siteMap?.Descendants().Count(element => element.Name.LocalName.Equals("Area", StringComparison.OrdinalIgnoreCase)) ?? 0;
            var groupCount = siteMap?.Descendants().Count(element => element.Name.LocalName.Equals("Group", StringComparison.OrdinalIgnoreCase)) ?? 0;
            var subAreas = siteMap?.Descendants().Where(element => element.Name.LocalName.Equals("SubArea", StringComparison.OrdinalIgnoreCase)).ToArray() ?? [];
            var webResourceSubAreaCount = subAreas.Count(subArea => (subArea.AttributeValue("Url") ?? string.Empty).StartsWith("$webresource:", StringComparison.OrdinalIgnoreCase));
            var siteMapDefinitionJson = SerializeJson(BuildSiteMapDefinition(siteMap));
            var summaryJson = SerializeJson(new
            {
                areaCount,
                groupCount,
                subAreaCount = subAreas.Length,
                webResourceSubAreaCount
            });

            AddArtifact(
                ComponentFamily.SiteMap,
                uniqueName!,
                displayName,
                siteMapPath,
                CreateProperties(
                    (ArtifactPropertyKeys.MetadataSourcePath, RelativePath(siteMapPath)),
                    (ArtifactPropertyKeys.AreaCount, areaCount.ToString(CultureInfo.InvariantCulture)),
                    (ArtifactPropertyKeys.GroupCount, groupCount.ToString(CultureInfo.InvariantCulture)),
                    (ArtifactPropertyKeys.SubAreaCount, subAreas.Length.ToString(CultureInfo.InvariantCulture)),
                    (ArtifactPropertyKeys.WebResourceSubAreaCount, webResourceSubAreaCount.ToString(CultureInfo.InvariantCulture)),
                    (ArtifactPropertyKeys.SiteMapDefinitionJson, siteMapDefinitionJson),
                    (ArtifactPropertyKeys.SummaryJson, summaryJson),
                    (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(siteMapDefinitionJson))));
        }
    }

    private void ParseWebResources()
    {
        var webResourcesDirectory = Path.Combine(_root, "WebResources");
        if (!Directory.Exists(webResourcesDirectory))
        {
            return;
        }

        foreach (var metadataFile in Directory.EnumerateFiles(webResourcesDirectory, "*.data.xml", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var root = LoadRoot(metadataFile);
            var logicalName = Text(root.ElementLocal("Name")) ?? Path.GetFileNameWithoutExtension(metadataFile);
            var assetFile = metadataFile[..^".data.xml".Length];
            var assetRelativePath = File.Exists(assetFile) ? RelativePath(assetFile) : null;

            AddArtifact(
                ComponentFamily.WebResource,
                logicalName,
                Text(root.ElementLocal("DisplayName")) ?? logicalName,
                metadataFile,
                CreateProperties(
                    (ArtifactPropertyKeys.Description, Text(root.ElementLocal("Description"))),
                    (ArtifactPropertyKeys.MetadataSourcePath, RelativePath(metadataFile)),
                    (ArtifactPropertyKeys.AssetSourcePath, assetRelativePath),
                    (ArtifactPropertyKeys.WebResourceType, Text(root.ElementLocal("WebResourceType"))),
                    (ArtifactPropertyKeys.WebResourceTypeLabel, DescribeWebResourceType(Text(root.ElementLocal("WebResourceType")))),
                    (ArtifactPropertyKeys.ByteLength, File.Exists(assetFile) ? new FileInfo(assetFile).Length.ToString(CultureInfo.InvariantCulture) : "0"),
                    (ArtifactPropertyKeys.ContentHash, File.Exists(assetFile) ? ComputeFileHash(assetFile) : null)));
        }
    }

    private void ParseEnvironmentVariables()
    {
        var environmentVariablesDirectory = Path.Combine(_root, "environmentvariabledefinitions");
        if (!Directory.Exists(environmentVariablesDirectory))
        {
            return;
        }

        foreach (var directory in Directory.GetDirectories(environmentVariablesDirectory).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var definitionPath = Path.Combine(directory, "environmentvariabledefinition.xml");
            if (!File.Exists(definitionPath))
            {
                continue;
            }

            var definitionRoot = LoadRoot(definitionPath);
            var schemaName = definitionRoot.AttributeValue("schemaname") ?? Path.GetFileName(directory);
            var displayName = definitionRoot.ElementLocal("displayname")?.AttributeValue("default")
                ?? LocalizedDescription(definitionRoot.ElementLocal("displayname"))
                ?? schemaName;

            AddArtifact(
                ComponentFamily.EnvironmentVariableDefinition,
                schemaName,
                displayName,
                definitionPath,
                CreateProperties(
                    (ArtifactPropertyKeys.MetadataSourcePath, RelativePath(definitionPath)),
                    (ArtifactPropertyKeys.DefaultValue, Text(definitionRoot.ElementLocal("defaultvalue"))),
                    (ArtifactPropertyKeys.SecretStore, Text(definitionRoot.ElementLocal("secretstore"))),
                    (ArtifactPropertyKeys.ValueSchema, Text(definitionRoot.ElementLocal("valueschema"))),
                    (ArtifactPropertyKeys.AttributeType, Text(definitionRoot.ElementLocal("type")))));

            var valuePath = Path.Combine(directory, "environmentvariablevalues.json");
            if (!File.Exists(valuePath))
            {
                continue;
            }

            AddArtifact(
                ComponentFamily.EnvironmentVariableValue,
                schemaName,
                schemaName,
                valuePath,
                CreateProperties(
                    (ArtifactPropertyKeys.DefinitionSchemaName, schemaName),
                    (ArtifactPropertyKeys.MetadataSourcePath, RelativePath(valuePath)),
                    (ArtifactPropertyKeys.Value, ParseEnvironmentVariableValue(valuePath))));
        }
    }

    private void ParseCanvasApps()
    {
        var canvasAppsDirectory = Path.Combine(_root, "CanvasApps");
        if (!Directory.Exists(canvasAppsDirectory))
        {
            return;
        }

        foreach (var metadataFile in Directory.EnumerateFiles(canvasAppsDirectory, "*.meta.xml", SearchOption.TopDirectoryOnly).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var root = LoadRoot(metadataFile);
            var logicalName = Text(root.ElementLocal("Name")) ?? Path.GetFileNameWithoutExtension(metadataFile);
            var tagsJson = NormalizeJson(Text(root.ElementLocal("Tags")));
            var authorizationReferencesJson = NormalizeJson(Text(root.ElementLocal("AuthorizationReferences")));
            var connectionReferencesJson = NormalizeJson(Text(root.ElementLocal("ConnectionReferences")));
            var databaseReferencesJson = NormalizeJson(Text(root.ElementLocal("DatabaseReferences")));
            var cdsDependenciesJson = NormalizeJson(Text(root.ElementLocal("CdsDependencies")));
            var documentUri = Text(root.ElementLocal("DocumentUri"));
            var backgroundImageUri = Text(root.ElementLocal("BackgroundImageUri"));
            var documentPath = ResolveCanvasAssetPath(metadataFile, documentUri);
            var backgroundPath = ResolveCanvasAssetPath(metadataFile, backgroundImageUri);
            var summaryJson = SerializeJson(new
            {
                tags = JsonNode.Parse(tagsJson ?? "{}"),
                authorizationReferences = JsonNode.Parse(authorizationReferencesJson ?? "[]"),
                connectionReferences = JsonNode.Parse(connectionReferencesJson ?? "{}"),
                databaseReferences = JsonNode.Parse(databaseReferencesJson ?? "{}"),
                cdsDependencies = JsonNode.Parse(cdsDependenciesJson ?? "{}"),
                canConsumeAppPass = NormalizeBoolean(Text(root.ElementLocal("CanConsumeAppPass"))),
                canvasAppType = Text(root.ElementLocal("CanvasAppType")),
                introducedVersion = Text(root.ElementLocal("IntroducedVersion")),
                isCustomizable = NormalizeBoolean(Text(root.ElementLocal("IsCustomizable"))),
                backgroundColor = Text(root.ElementLocal("BackgroundColor")),
                backgroundImageUri,
                documentUri
            });

            AddArtifact(
                ComponentFamily.CanvasApp,
                logicalName,
                Text(root.ElementLocal("DisplayName")) ?? logicalName,
                metadataFile,
                CreateProperties(
                    (ArtifactPropertyKeys.AppVersion, Text(root.ElementLocal("AppVersion"))),
                    (ArtifactPropertyKeys.Status, Text(root.ElementLocal("Status"))),
                    (ArtifactPropertyKeys.CreatedByClientVersion, Text(root.ElementLocal("CreatedByClientVersion"))),
                    (ArtifactPropertyKeys.MinClientVersion, Text(root.ElementLocal("MinClientVersion"))),
                    (ArtifactPropertyKeys.Description, Text(root.ElementLocal("Description"))),
                    (ArtifactPropertyKeys.MetadataSourcePath, RelativePath(metadataFile)),
                    (ArtifactPropertyKeys.TagsJson, tagsJson),
                    (ArtifactPropertyKeys.AuthorizationReferencesJson, authorizationReferencesJson),
                    (ArtifactPropertyKeys.ConnectionReferencesJson, connectionReferencesJson),
                    (ArtifactPropertyKeys.DatabaseReferencesJson, databaseReferencesJson),
                    (ArtifactPropertyKeys.CanConsumeAppPass, NormalizeBoolean(Text(root.ElementLocal("CanConsumeAppPass")))),
                    (ArtifactPropertyKeys.CanvasAppType, Text(root.ElementLocal("CanvasAppType"))),
                    (ArtifactPropertyKeys.IntroducedVersion, Text(root.ElementLocal("IntroducedVersion"))),
                    (ArtifactPropertyKeys.CdsDependenciesJson, cdsDependenciesJson),
                    (ArtifactPropertyKeys.IsCustomizable, NormalizeBoolean(Text(root.ElementLocal("IsCustomizable")))),
                    (ArtifactPropertyKeys.IsManaged, NormalizeBoolean(Text(root.ElementLocal("IsManaged")))),
                    (ArtifactPropertyKeys.BackgroundColor, Text(root.ElementLocal("BackgroundColor"))),
                    (ArtifactPropertyKeys.BackgroundImageUri, backgroundImageUri),
                    (ArtifactPropertyKeys.DocumentUri, documentUri),
                    (ArtifactPropertyKeys.BackgroundSourcePath, backgroundPath is null ? null : RelativePath(backgroundPath)),
                    (ArtifactPropertyKeys.DocumentSourcePath, documentPath is null ? null : RelativePath(documentPath)),
                    (ArtifactPropertyKeys.SummaryJson, summaryJson),
                    (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(summaryJson))));
        }
    }

    private static string? ParseEnvironmentVariableValue(string valuePath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(valuePath));
        if (!document.RootElement.TryGetProperty("environmentvariablevalues", out var valuesElement))
        {
            return null;
        }

        if (!valuesElement.TryGetProperty("environmentvariablevalue", out var valueElement))
        {
            return null;
        }

        if (valueElement.ValueKind == JsonValueKind.Object && valueElement.TryGetProperty("value", out var valueProperty))
        {
            return valueProperty.GetString();
        }

        if (valueElement.ValueKind == JsonValueKind.Array)
        {
            var first = valueElement.EnumerateArray().FirstOrDefault();
            if (first.ValueKind == JsonValueKind.Object && first.TryGetProperty("value", out var arrayValueProperty))
            {
                return arrayValueProperty.GetString();
            }
        }

        return null;
    }

    private static string? ResolveCanvasAssetPath(string metadataFile, string? uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
        {
            return null;
        }

        var fileName = Path.GetFileName(uri.Replace('/', Path.DirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        var candidate = Path.Combine(Path.GetDirectoryName(metadataFile) ?? string.Empty, fileName);
        return File.Exists(candidate) ? candidate : null;
    }

    private static object BuildSiteMapDefinition(System.Xml.Linq.XElement? siteMap)
    {
        var areas = siteMap?
            .Elements()
            .Where(element => element.Name.LocalName.Equals("Area", StringComparison.OrdinalIgnoreCase))
            .Select((area, areaIndex) => new
            {
                id = area.AttributeValue("Id") ?? area.AttributeValue("id") ?? $"area_{areaIndex + 1}",
                title = ReadSiteMapTitle(area) ?? area.AttributeValue("Title") ?? area.AttributeValue("title") ?? $"Area {areaIndex + 1}",
                groups = area
                    .Elements()
                    .Where(element => element.Name.LocalName.Equals("Group", StringComparison.OrdinalIgnoreCase))
                    .Select((group, groupIndex) => new
                    {
                        id = group.AttributeValue("Id") ?? group.AttributeValue("id") ?? $"group_{groupIndex + 1}",
                        title = ReadSiteMapTitle(group) ?? group.AttributeValue("Title") ?? group.AttributeValue("title") ?? $"Group {groupIndex + 1}",
                        subAreas = group
                            .Elements()
                            .Where(element => element.Name.LocalName.Equals("SubArea", StringComparison.OrdinalIgnoreCase))
                            .Select((subArea, subAreaIndex) =>
                            {
                                var rawUrl = subArea.AttributeValue("Url") ?? subArea.AttributeValue("url");
                                var entityList = TryParseEntityListTarget(rawUrl);
                                var entityRecord = TryParseEntityRecordTarget(rawUrl);
                                var dashboard = TryParseDashboardTarget(rawUrl);
                                var customPage = TryParseCustomPageTarget(rawUrl);
                                return new
                                {
                                    id = subArea.AttributeValue("Id") ?? subArea.AttributeValue("id") ?? $"subarea_{subAreaIndex + 1}",
                                    title = ReadSiteMapTitle(subArea) ?? subArea.AttributeValue("Title") ?? subArea.AttributeValue("title") ?? $"Sub Area {subAreaIndex + 1}",
                                    entity = entityList?.LogicalName
                                        ?? entityRecord?.LogicalName
                                        ?? NormalizeLogicalName(subArea.AttributeValue("Entity") ?? subArea.AttributeValue("entity")),
                                    viewId = entityList?.ViewId,
                                    viewType = entityList?.ViewType,
                                    recordId = entityRecord?.RecordId,
                                    formId = entityRecord?.FormId,
                                    url = rawUrl is { Length: > 0 } url && !url.StartsWith("$webresource:", StringComparison.OrdinalIgnoreCase)
                                        && entityList is null
                                        && entityRecord is null
                                        && dashboard is null
                                        && customPage is null
                                        ? NormalizeSiteMapRawUrl(url)
                                        : null,
                                    webResource = rawUrl is { Length: > 0 } encodedUrl && encodedUrl.StartsWith("$webresource:", StringComparison.OrdinalIgnoreCase)
                                        ? encodedUrl["$webresource:".Length..]
                                        : null,
                                    dashboard = dashboard?.DashboardId,
                                    customPage = customPage?.LogicalName,
                                    customPageEntityName = customPage?.ContextEntityName,
                                    customPageRecordId = customPage?.ContextRecordId,
                                    appId = dashboard?.AppId ?? customPage?.AppId ?? entityList?.AppId ?? entityRecord?.AppId,
                                    client = subArea.AttributeValue("Client") ?? subArea.AttributeValue("client"),
                                    passParams = ParseOptionalBoolean(subArea.AttributeValue("PassParams") ?? subArea.AttributeValue("passparams")),
                                    availableOffline = ParseOptionalBoolean(subArea.AttributeValue("AvailableOffline") ?? subArea.AttributeValue("availableoffline")),
                                    icon = subArea.AttributeValue("Icon") ?? subArea.AttributeValue("icon"),
                                    vectorIcon = subArea.AttributeValue("VectorIcon") ?? subArea.AttributeValue("vectoricon")
                                };
                            })
                            .ToArray()
                    })
                    .ToArray()
            })
            .ToArray()
            ?? [];

        return new
        {
            areas
        };
    }

    private static string? ReadSiteMapTitle(System.Xml.Linq.XElement element) =>
        element
            .Elements()
            .FirstOrDefault(child => child.Name.LocalName.Equals("Titles", StringComparison.OrdinalIgnoreCase))?
            .Elements()
            .FirstOrDefault(child => child.Name.LocalName.Equals("Title", StringComparison.OrdinalIgnoreCase))?
            .AttributeValue("Title")
        ?? element
            .Elements()
            .FirstOrDefault(child => child.Name.LocalName.Equals("Titles", StringComparison.OrdinalIgnoreCase))?
            .Elements()
            .FirstOrDefault(child => child.Name.LocalName.Equals("Title", StringComparison.OrdinalIgnoreCase))?
            .AttributeValue("title");

    private static bool? ParseOptionalBoolean(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : string.Equals(NormalizeBoolean(value), "true", StringComparison.OrdinalIgnoreCase);

    private readonly record struct SiteMapDashboardTarget(string DashboardId, string? AppId);

    private readonly record struct SiteMapEntityListTarget(string LogicalName, string ViewId, string? ViewType, string? AppId);

    private readonly record struct SiteMapEntityRecordTarget(string LogicalName, string RecordId, string? FormId, string? AppId);

    private readonly record struct SiteMapCustomPageTarget(string LogicalName, string? AppId, string? ContextEntityName, string? ContextRecordId);

    private static SiteMapDashboardTarget? TryParseDashboardTarget(string? rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl)
            || rawUrl.StartsWith("$webresource:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var query = ExtractQueryString(rawUrl);
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        var parameters = ParseQueryString(query);
        if (parameters.Keys.Any(key =>
                !key.Equals("appid", StringComparison.OrdinalIgnoreCase)
                && !key.Equals("pagetype", StringComparison.OrdinalIgnoreCase)
                && !key.Equals("id", StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        if (!parameters.TryGetValue("pagetype", out var pageType)
            || !pageType.Equals("dashboard", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!parameters.TryGetValue("id", out var dashboardId))
        {
            return null;
        }

        var normalizedDashboardId = NormalizeGuid(dashboardId);
        if (string.IsNullOrWhiteSpace(normalizedDashboardId))
        {
            return null;
        }

        string? normalizedAppId = null;
        if (parameters.TryGetValue("appid", out var appId)
            && TryNormalizeGuid(appId) is not { Length: > 0 } parsedAppId)
        {
            return null;
        }
        else if (!string.IsNullOrWhiteSpace(appId))
        {
            normalizedAppId = TryNormalizeGuid(appId);
        }

        return new SiteMapDashboardTarget(normalizedDashboardId, normalizedAppId);
    }

    private static SiteMapEntityListTarget? TryParseEntityListTarget(string? rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl)
            || rawUrl.StartsWith("$webresource:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var query = ExtractQueryString(rawUrl);
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        var parameters = ParseQueryString(query);
        if (parameters.Keys.Any(key =>
                !key.Equals("appid", StringComparison.OrdinalIgnoreCase)
                && !key.Equals("pagetype", StringComparison.OrdinalIgnoreCase)
                && !key.Equals("etn", StringComparison.OrdinalIgnoreCase)
                && !key.Equals("viewid", StringComparison.OrdinalIgnoreCase)
                && !key.Equals("viewtype", StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        if (!parameters.TryGetValue("pagetype", out var pageType)
            || !pageType.Equals("entitylist", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!parameters.TryGetValue("etn", out var entityLogicalName)
            || NormalizeLogicalName(entityLogicalName) is not { Length: > 0 } normalizedEntityLogicalName)
        {
            return null;
        }

        if (!parameters.TryGetValue("viewid", out var viewId)
            || TryNormalizeGuid(viewId) is not { Length: > 0 } normalizedViewId)
        {
            return null;
        }

        string? normalizedViewType = null;
        if (parameters.TryGetValue("viewtype", out var viewType)
            && NormalizeSiteMapViewType(viewType) is not { Length: > 0 } parsedViewType)
        {
            return null;
        }
        else if (!string.IsNullOrWhiteSpace(viewType))
        {
            normalizedViewType = NormalizeSiteMapViewType(viewType);
        }

        string? normalizedAppId = null;
        if (parameters.TryGetValue("appid", out var appId)
            && TryNormalizeGuid(appId) is not { Length: > 0 } parsedAppId)
        {
            return null;
        }
        else if (!string.IsNullOrWhiteSpace(appId))
        {
            normalizedAppId = TryNormalizeGuid(appId);
        }

        return new SiteMapEntityListTarget(normalizedEntityLogicalName, normalizedViewId, normalizedViewType, normalizedAppId);
    }

    private static SiteMapEntityRecordTarget? TryParseEntityRecordTarget(string? rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl)
            || rawUrl.StartsWith("$webresource:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var query = ExtractQueryString(rawUrl);
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        var parameters = ParseQueryString(query);
        if (parameters.Keys.Any(key =>
                !key.Equals("appid", StringComparison.OrdinalIgnoreCase)
                && !key.Equals("pagetype", StringComparison.OrdinalIgnoreCase)
                && !key.Equals("etn", StringComparison.OrdinalIgnoreCase)
                && !key.Equals("id", StringComparison.OrdinalIgnoreCase)
                && !key.Equals("extraqs", StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        if (!parameters.TryGetValue("pagetype", out var pageType)
            || !pageType.Equals("entityrecord", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!parameters.TryGetValue("etn", out var entityLogicalName)
            || NormalizeLogicalName(entityLogicalName) is not { Length: > 0 } normalizedEntityLogicalName)
        {
            return null;
        }

        if (!parameters.TryGetValue("id", out var recordId)
            || TryNormalizeGuid(recordId) is not { Length: > 0 } normalizedRecordId)
        {
            return null;
        }

        string? normalizedFormId = null;
        if (parameters.TryGetValue("extraqs", out var extraQueryString))
        {
            normalizedFormId = TryParseSiteMapFormId(extraQueryString);
            if (string.IsNullOrWhiteSpace(normalizedFormId))
            {
                return null;
            }
        }

        string? normalizedAppId = null;
        if (parameters.TryGetValue("appid", out var appId)
            && TryNormalizeGuid(appId) is not { Length: > 0 } parsedAppId)
        {
            return null;
        }
        else if (!string.IsNullOrWhiteSpace(appId))
        {
            normalizedAppId = TryNormalizeGuid(appId);
        }

        return new SiteMapEntityRecordTarget(normalizedEntityLogicalName, normalizedRecordId, normalizedFormId, normalizedAppId);
    }

    private static SiteMapCustomPageTarget? TryParseCustomPageTarget(string? rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl)
            || rawUrl.StartsWith("$webresource:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var query = ExtractQueryString(rawUrl);
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        var parameters = ParseQueryString(query);
        if (parameters.Keys.Any(key =>
                !key.Equals("appid", StringComparison.OrdinalIgnoreCase)
                && !key.Equals("pagetype", StringComparison.OrdinalIgnoreCase)
                && !key.Equals("name", StringComparison.OrdinalIgnoreCase)
                && !key.Equals("entityname", StringComparison.OrdinalIgnoreCase)
                && !key.Equals("recordid", StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        if (!parameters.TryGetValue("pagetype", out var pageType)
            || !pageType.Equals("custom", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!parameters.TryGetValue("name", out var logicalName))
        {
            return null;
        }

        var normalizedLogicalName = NormalizeLogicalName(logicalName);
        if (string.IsNullOrWhiteSpace(normalizedLogicalName))
        {
            return null;
        }

        string? normalizedContextEntityName = null;
        if (parameters.TryGetValue("entityname", out var contextEntityName)
            && NormalizeLogicalName(contextEntityName) is not { Length: > 0 } parsedContextEntityName)
        {
            return null;
        }
        else if (!string.IsNullOrWhiteSpace(contextEntityName))
        {
            normalizedContextEntityName = NormalizeLogicalName(contextEntityName);
        }

        string? normalizedContextRecordId = null;
        if (parameters.TryGetValue("recordid", out var contextRecordId)
            && TryNormalizeGuid(contextRecordId) is not { Length: > 0 } parsedContextRecordId)
        {
            return null;
        }
        else if (!string.IsNullOrWhiteSpace(contextRecordId))
        {
            normalizedContextRecordId = TryNormalizeGuid(contextRecordId);
        }

        if (!parameters.TryGetValue("appid", out var appId))
        {
            return new SiteMapCustomPageTarget(normalizedLogicalName, null, normalizedContextEntityName, normalizedContextRecordId);
        }

        var normalizedAppId = TryNormalizeGuid(appId);
        return string.IsNullOrWhiteSpace(normalizedAppId)
            ? null
            : new SiteMapCustomPageTarget(normalizedLogicalName, normalizedAppId, normalizedContextEntityName, normalizedContextRecordId);
    }

    private static string? ExtractQueryString(string rawUrl)
    {
        var separatorIndex = rawUrl.IndexOf('?', StringComparison.Ordinal);
        if (separatorIndex < 0 || separatorIndex == rawUrl.Length - 1)
        {
            return null;
        }

        return rawUrl[(separatorIndex + 1)..];
    }

    private static string NormalizeSiteMapRawUrl(string rawUrl)
    {
        var trimmed = rawUrl.Trim();
        var separatorIndex = trimmed.IndexOf('?', StringComparison.Ordinal);
        if (separatorIndex < 0 || separatorIndex == trimmed.Length - 1)
        {
            return trimmed;
        }

        var path = trimmed[..separatorIndex];
        var parameters = ParseQueryString(trimmed[(separatorIndex + 1)..]);
        if (parameters.Count == 0)
        {
            return path;
        }

        var normalizedQuery = string.Join("&", parameters
            .Select(pair => new KeyValuePair<string, string>(pair.Key.Trim(), NormalizeSiteMapRawQueryValue(pair.Key, pair.Value)))
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .ThenBy(pair => pair.Value, StringComparer.OrdinalIgnoreCase)
            .Select(FormatSiteMapRawQueryPair));

        return $"{path}?{normalizedQuery}";
    }

    private static string NormalizeSiteMapRawQueryValue(string key, string value)
    {
        var trimmed = value.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return string.Empty;
        }

        return key.Trim().ToLowerInvariant() switch
        {
            "appid" or "id" or "recordid" or "viewid" or "formid" => TryNormalizeGuid(trimmed) ?? trimmed,
            "etn" or "entityname" or "name" or "pagetype" => trimmed.ToLowerInvariant(),
            "extraqs" => NormalizeSiteMapEmbeddedRawQuery(trimmed),
            _ => NormalizeSiteMapRawBoolean(trimmed) ?? trimmed
        };
    }

    private static string NormalizeSiteMapEmbeddedRawQuery(string value)
    {
        var parameters = ParseQueryString(Uri.UnescapeDataString(value));
        if (parameters.Count == 0)
        {
            return value.Trim();
        }

        return string.Join("&", parameters
            .Select(pair => new KeyValuePair<string, string>(pair.Key.Trim(), NormalizeSiteMapRawQueryValue(pair.Key, pair.Value)))
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .ThenBy(pair => pair.Value, StringComparer.OrdinalIgnoreCase)
            .Select(pair => string.IsNullOrEmpty(pair.Value)
                ? Uri.EscapeDataString(pair.Key)
                : $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
    }

    private static string? NormalizeSiteMapRawBoolean(string value) =>
        value.Trim() switch
        {
            "1" => "true",
            "0" => "false",
            var text when text.Equals("true", StringComparison.OrdinalIgnoreCase) => "true",
            var text when text.Equals("false", StringComparison.OrdinalIgnoreCase) => "false",
            _ => null
        };

    private static string FormatSiteMapRawQueryPair(KeyValuePair<string, string> pair) =>
        string.IsNullOrEmpty(pair.Value)
            ? Uri.EscapeDataString(pair.Key)
            : $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}";

    private static Dictionary<string, string> ParseQueryString(string query)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var segment in query.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separatorIndex = segment.IndexOf('=', StringComparison.Ordinal);
            var key = separatorIndex >= 0 ? segment[..separatorIndex] : segment;
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            var value = separatorIndex >= 0 ? segment[(separatorIndex + 1)..] : string.Empty;
            values[Uri.UnescapeDataString(key)] = Uri.UnescapeDataString(value);
        }

        return values;
    }

    private static string? NormalizeSiteMapViewType(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "1039" => "savedquery",
            "4230" => "userquery",
            "savedquery" => "savedquery",
            "userquery" => "userquery",
            _ => null
        };

    private static string? TryParseSiteMapFormId(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        var extraParameters = ParseEmbeddedQueryString(Uri.UnescapeDataString(rawValue));
        if (extraParameters.Count != 1
            || !extraParameters.TryGetValue("formid", out var formId))
        {
            return null;
        }

        return TryNormalizeGuid(formId);
    }

    private static Dictionary<string, string> ParseEmbeddedQueryString(string query)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var segment in query.Split(['&', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separatorIndex = segment.IndexOf('=', StringComparison.Ordinal);
            var key = separatorIndex >= 0 ? segment[..separatorIndex] : segment;
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            var value = separatorIndex >= 0 ? segment[(separatorIndex + 1)..] : string.Empty;
            values[Uri.UnescapeDataString(key)] = Uri.UnescapeDataString(value);
        }

        return values;
    }

    private static string? TryNormalizeGuid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim().Trim('{', '}');
        return Guid.TryParse(trimmed, out var guid)
            ? guid.ToString("D")
            : null;
    }
}
