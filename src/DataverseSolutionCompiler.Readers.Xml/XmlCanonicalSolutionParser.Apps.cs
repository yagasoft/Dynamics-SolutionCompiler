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
                    (ArtifactPropertyKeys.SummaryJson, summaryJson),
                    (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(summaryJson))));
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
}
