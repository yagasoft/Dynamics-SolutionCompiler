using System.Text.Json;
using System.Text.Json.Nodes;
using DataverseSolutionCompiler.Domain.Abstractions;
using DataverseSolutionCompiler.Domain.Diagnostics;
using DataverseSolutionCompiler.Domain.Model;
using DataverseSolutionCompiler.Domain.Read;

namespace DataverseSolutionCompiler.Readers.TrackedSource;

public sealed class TrackedSourceReader : ISolutionReader
{
    private static readonly StringComparer PathComparer = StringComparer.OrdinalIgnoreCase;

    public CanonicalSolution Read(ReadRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SourcePath);

        if (!Directory.Exists(request.SourcePath))
        {
            throw new DirectoryNotFoundException($"Tracked source folder not found: {request.SourcePath}");
        }

        var rootPath = Path.GetFullPath(request.SourcePath);
        var rootManifestPath = Path.Combine(rootPath, "manifest.json");
        var solutionManifestPath = Path.Combine(rootPath, "solution", "manifest.json");
        if (!File.Exists(rootManifestPath) || !File.Exists(solutionManifestPath))
        {
            throw new FileNotFoundException("Tracked source input requires manifest.json and solution/manifest.json.", rootPath);
        }

        var solutionManifest = ReadObject(solutionManifestPath);
        var publisherObject = GetObject(solutionManifest, "publisher");

        var identity = new SolutionIdentity(
            GetRequiredString(solutionManifest, "UniqueName") ?? "tracked-source",
            GetRequiredString(solutionManifest, "DisplayName") ?? "Tracked Source",
            GetRequiredString(solutionManifest, "Version") ?? "0.1.0",
            ParseLayeringIntent(GetRequiredString(solutionManifest, "layeringIntent")));
        var publisher = new PublisherDefinition(
            GetRequiredString(publisherObject, "UniqueName") ?? "dsc",
            GetRequiredString(publisherObject, "Prefix") ?? "dsc",
            GetRequiredString(publisherObject, "CustomizationPrefix") ?? GetRequiredString(publisherObject, "Prefix") ?? "dsc",
            GetRequiredString(publisherObject, "DisplayName") ?? "Dataverse Solution Compiler");

        var artifacts = new List<FamilyArtifact>
        {
            new(
                ComponentFamily.SolutionShell,
                identity.UniqueName,
                identity.DisplayName,
                solutionManifestPath,
                EvidenceKind.Source,
                CreateProperties(
                    (ArtifactPropertyKeys.MetadataSourcePath, GetRequiredString(solutionManifest, "metadataSourcePath")),
                    (ArtifactPropertyKeys.Version, identity.Version),
                    (ArtifactPropertyKeys.Managed, identity.LayeringIntent == LayeringIntent.ManagedRelease ? "true" : "false"),
                    (ArtifactPropertyKeys.PublisherUniqueName, publisher.UniqueName),
                    (ArtifactPropertyKeys.PublisherPrefix, publisher.Prefix),
                    (ArtifactPropertyKeys.PublisherDisplayName, publisher.DisplayName))),
            new(
                ComponentFamily.Publisher,
                publisher.UniqueName,
                publisher.DisplayName,
                solutionManifestPath,
                EvidenceKind.Source,
                CreateProperties(
                    (ArtifactPropertyKeys.MetadataSourcePath, GetRequiredString(publisherObject, "metadataSourcePath")),
                    (ArtifactPropertyKeys.PublisherPrefix, publisher.Prefix),
                    (ArtifactPropertyKeys.PublisherDisplayName, publisher.DisplayName)))
        };

        var diagnostics = new List<CompilerDiagnostic>();
        var manifestFiles = ReadManifestFiles(rootManifestPath, rootPath);
        var parsedFiles = new HashSet<string>(PathComparer)
        {
            "manifest.json",
            "solution/manifest.json"
        };

        foreach (var relativePath in manifestFiles.OrderBy(path => path, PathComparer))
        {
            if (TryReadSupportedFile(rootPath, relativePath, artifacts))
            {
                parsedFiles.Add(relativePath);
            }
        }

        foreach (var relativePath in manifestFiles
                     .Where(path => !parsedFiles.Contains(path))
                     .OrderBy(path => path, PathComparer))
        {
            if (TryClassifyUnsupportedTrackedSource(relativePath, out var family))
            {
                diagnostics.Add(new CompilerDiagnostic(
                    $"tracked-source-intent-unsupported-{family}",
                    DiagnosticSeverity.Warning,
                    $"Tracked-source reverse generation does not yet project family '{family}' into intent-spec JSON, so '{relativePath}' was omitted from the authoring surface.",
                    Path.Combine(rootPath, relativePath.Replace('/', Path.DirectorySeparatorChar))));
            }
        }

        diagnostics.Add(new CompilerDiagnostic(
            "tracked-source-reader-subset",
            DiagnosticSeverity.Info,
            "Tracked-source reader reconstructed the supported reverse-generation subset and reported omitted tracked-source families separately.",
            rootPath));

        return new CanonicalSolution(
            identity,
            publisher,
            artifacts
                .OrderBy(artifact => artifact.Family)
                .ThenBy(artifact => artifact.LogicalName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(artifact => artifact.SourcePath, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            [],
            [],
            diagnostics);
    }

    private static IReadOnlyList<string> ReadManifestFiles(string manifestPath, string rootPath)
    {
        var manifest = ReadObject(manifestPath);
        var files = GetArray(manifest, "files")
            .Select(item => item?.GetValue<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => NormalizeTrackedSourceRelativePath(value!))
            .Distinct(PathComparer)
            .ToArray();

        if (files.Length > 0)
        {
            return files;
        }

        return Directory.EnumerateFiles(rootPath, "*.json", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(rootPath, path).Replace('\\', '/'))
            .Distinct(PathComparer)
            .ToArray();
    }

    private static bool TryReadSupportedFile(string rootPath, string relativePath, ICollection<FamilyArtifact> artifacts)
    {
        var fullPath = Path.Combine(rootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath))
        {
            return false;
        }

        var segments = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return false;
        }

        if (string.Equals(segments[0], "source-backed", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (segments.Length == 2
            && SourceBackedSummaryFamilies.ContainsKey(segments[0])
            && !relativePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (segments.Length == 2 && string.Equals(segments[0], "entities", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (segments.Length == 3
            && string.Equals(segments[0], "entities", StringComparison.OrdinalIgnoreCase)
            && string.Equals(segments[2], "entity.json", StringComparison.OrdinalIgnoreCase))
        {
            artifacts.Add(ParseTableArtifact(segments[1], fullPath));
            return true;
        }

        if (segments.Length == 3
            && string.Equals(segments[0], "entities", StringComparison.OrdinalIgnoreCase)
            && string.Equals(segments[2], "attributes.json", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var artifact in ParseAttributeArtifacts(segments[1], fullPath))
            {
                artifacts.Add(artifact);
            }

            return true;
        }

        if (segments.Length == 3
            && string.Equals(segments[0], "entities", StringComparison.OrdinalIgnoreCase)
            && string.Equals(segments[2], "relationships.json", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var artifact in ParseRelationshipArtifacts(fullPath))
            {
                artifacts.Add(artifact);
            }

            return true;
        }

        if (segments.Length == 3
            && string.Equals(segments[0], "entities", StringComparison.OrdinalIgnoreCase)
            && string.Equals(segments[2], "keys.json", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var artifact in ParseKeyArtifacts(segments[1], fullPath))
            {
                artifacts.Add(artifact);
            }

            return true;
        }

        if (segments.Length == 3
            && string.Equals(segments[0], "entities", StringComparison.OrdinalIgnoreCase)
            && string.Equals(segments[2], "image-configurations.json", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var artifact in ParseImageConfigurationArtifacts(segments[1], fullPath))
            {
                artifacts.Add(artifact);
            }

            return true;
        }

        if (segments.Length >= 4
            && string.Equals(segments[0], "entities", StringComparison.OrdinalIgnoreCase)
            && string.Equals(segments[2], "forms", StringComparison.OrdinalIgnoreCase))
        {
            artifacts.Add(ParseSummaryArtifact(fullPath, ComponentFamily.Form));
            return true;
        }

        if (segments.Length >= 4
            && string.Equals(segments[0], "entities", StringComparison.OrdinalIgnoreCase)
            && string.Equals(segments[2], "views", StringComparison.OrdinalIgnoreCase))
        {
            artifacts.Add(ParseSummaryArtifact(fullPath, ComponentFamily.View));
            return true;
        }

        if (segments.Length == 2 && string.Equals(segments[0], "global-option-sets", StringComparison.OrdinalIgnoreCase))
        {
            artifacts.Add(ParseGlobalOptionSetArtifact(fullPath));
            return true;
        }

        if (segments.Length == 2 && string.Equals(segments[0], "app-modules", StringComparison.OrdinalIgnoreCase))
        {
            artifacts.Add(ParseSummaryArtifact(fullPath, ComponentFamily.AppModule));
            return true;
        }

        if (segments.Length == 2 && string.Equals(segments[0], "site-maps", StringComparison.OrdinalIgnoreCase))
        {
            artifacts.Add(ParseSummaryArtifact(fullPath, ComponentFamily.SiteMap));
            return true;
        }

        if (segments.Length == 2 && string.Equals(segments[0], "environment-variables", StringComparison.OrdinalIgnoreCase))
        {
            var family = relativePath.EndsWith(".definition.json", StringComparison.OrdinalIgnoreCase)
                ? ComponentFamily.EnvironmentVariableDefinition
                : relativePath.EndsWith(".value.json", StringComparison.OrdinalIgnoreCase)
                    ? ComponentFamily.EnvironmentVariableValue
                    : (ComponentFamily?)null;
            if (family is null)
            {
                return false;
            }

            artifacts.Add(ParseSummaryArtifact(fullPath, family.Value));
            return true;
        }

        if (segments.Length == 2
            && SourceBackedSummaryFamilies.TryGetValue(segments[0], out var expectedFamily))
        {
            artifacts.Add(ParseSummaryArtifact(fullPath, expectedFamily));
            return true;
        }

        return false;
    }

    private static FamilyArtifact ParseTableArtifact(string entityLogicalName, string path)
    {
        var json = ReadObject(path);
        return new FamilyArtifact(
            ComponentFamily.Table,
            GetRequiredString(json, "logicalName") ?? NormalizeLogicalName(entityLogicalName) ?? entityLogicalName,
            GetRequiredString(json, "DisplayName") ?? GetRequiredString(json, "displayName"),
            path,
            EvidenceKind.Source,
            CreateProperties(
                (ArtifactPropertyKeys.EntityLogicalName, GetRequiredString(json, "logicalName")),
                (ArtifactPropertyKeys.SchemaName, GetRequiredString(json, "schemaName")),
                (ArtifactPropertyKeys.Description, GetRequiredString(json, "description")),
                (ArtifactPropertyKeys.EntitySetName, GetRequiredString(json, "entitySetName")),
                (ArtifactPropertyKeys.OwnershipTypeMask, GetRequiredString(json, "ownershipTypeMask")),
                (ArtifactPropertyKeys.PrimaryIdAttribute, GetRequiredString(json, "primaryIdAttribute")),
                (ArtifactPropertyKeys.PrimaryNameAttribute, GetRequiredString(json, "primaryNameAttribute")),
                (ArtifactPropertyKeys.IsCustomizable, GetRequiredBooleanString(json, "isCustomizable")),
                (ArtifactPropertyKeys.ShellOnly, GetRequiredBooleanString(json, "shellOnly"))));
    }

    private static IEnumerable<FamilyArtifact> ParseAttributeArtifacts(string entityLogicalName, string path)
    {
        foreach (var node in ReadArray(path).OfType<JsonObject>())
        {
            var logicalName = NormalizeLogicalName(GetRequiredString(node, "logicalName"));
            if (string.IsNullOrWhiteSpace(logicalName))
            {
                continue;
            }

            yield return new FamilyArtifact(
                ComponentFamily.Column,
                $"{NormalizeLogicalName(entityLogicalName)}|{logicalName}",
                GetRequiredString(node, "DisplayName") ?? GetRequiredString(node, "displayName"),
                path,
                EvidenceKind.Source,
                CreateProperties(
                    (ArtifactPropertyKeys.EntityLogicalName, NormalizeLogicalName(entityLogicalName)),
                    (ArtifactPropertyKeys.SchemaName, GetRequiredString(node, "schemaName")),
                    (ArtifactPropertyKeys.Description, GetRequiredString(node, "description")),
                    (ArtifactPropertyKeys.AttributeType, GetRequiredString(node, "attributeType")),
                    (ArtifactPropertyKeys.IsSecured, GetRequiredBooleanString(node, "isSecured")),
                    (ArtifactPropertyKeys.IsCustomField, GetRequiredBooleanString(node, "isCustomField")),
                    (ArtifactPropertyKeys.IsCustomizable, GetRequiredBooleanString(node, "isCustomizable")),
                    (ArtifactPropertyKeys.IsPrimaryKey, GetRequiredBooleanString(node, "isPrimaryKey")),
                    (ArtifactPropertyKeys.IsPrimaryName, GetRequiredBooleanString(node, "isPrimaryName")),
                    (ArtifactPropertyKeys.IsLogical, GetRequiredBooleanString(node, "isLogical")),
                    (ArtifactPropertyKeys.CanStoreFullImage, GetRequiredBooleanString(node, "canStoreFullImage")),
                    (ArtifactPropertyKeys.IsPrimaryImage, GetRequiredBooleanString(node, "isPrimaryImage")),
                    (ArtifactPropertyKeys.OptionSetName, GetRequiredString(node, "optionSetName")),
                    (ArtifactPropertyKeys.OptionSetType, GetRequiredString(node, "optionSetType")),
                    (ArtifactPropertyKeys.IsGlobal, GetRequiredBooleanString(node, "isGlobal"))));

            var optionSet = GetObject(node, "optionSet");
            if (optionSet is null)
            {
                continue;
            }

            var optionsJson = SerializeNode(GetValue(optionSet, "options"));
            var optionSetType = GetRequiredString(optionSet, "optionSetType");
            var optionSetName = GetRequiredString(node, "optionSetName") ?? logicalName;
            var summaryJson = SerializeCompact(new
            {
                entityLogicalName = NormalizeLogicalName(entityLogicalName),
                attributeLogicalName = logicalName,
                optionSetType,
                isGlobal = false,
                optionCount = ParseInt(GetValue(optionSet, "optionCount")) ?? 0,
                options = JsonNode.Parse(optionsJson ?? "[]")
            });

            yield return new FamilyArtifact(
                ComponentFamily.OptionSet,
                $"{NormalizeLogicalName(entityLogicalName)}|{logicalName}",
                GetRequiredString(optionSet, "DisplayName") ?? GetRequiredString(node, "DisplayName"),
                path,
                EvidenceKind.Source,
                CreateProperties(
                    (ArtifactPropertyKeys.EntityLogicalName, NormalizeLogicalName(entityLogicalName)),
                    (ArtifactPropertyKeys.OptionSetName, optionSetName),
                    (ArtifactPropertyKeys.OptionSetType, optionSetType),
                    (ArtifactPropertyKeys.Description, GetRequiredString(node, "description")),
                    (ArtifactPropertyKeys.IsGlobal, "false"),
                    (ArtifactPropertyKeys.OptionCount, (ParseInt(GetValue(optionSet, "optionCount")) ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture)),
                    (ArtifactPropertyKeys.OptionsJson, optionsJson),
                    (ArtifactPropertyKeys.SummaryJson, summaryJson),
                    (ArtifactPropertyKeys.ComparisonSignature, GetRequiredString(optionSet, "comparisonSignature"))));
        }
    }

    private static IEnumerable<FamilyArtifact> ParseRelationshipArtifacts(string path)
    {
        foreach (var node in ReadArray(path).OfType<JsonObject>())
        {
            var logicalName = NormalizeLogicalName(GetRequiredString(node, "logicalName"));
            if (string.IsNullOrWhiteSpace(logicalName))
            {
                continue;
            }

            var referencedEntity = NormalizeLogicalName(GetRequiredString(node, "referencedEntity"));

            yield return new FamilyArtifact(
                ComponentFamily.Relationship,
                logicalName,
                GetRequiredString(node, "logicalName"),
                path,
                EvidenceKind.Source,
                CreateProperties(
                    (ArtifactPropertyKeys.RelationshipType, GetRequiredString(node, "relationshipType")),
                    (ArtifactPropertyKeys.ReferencedEntity, referencedEntity),
                    (ArtifactPropertyKeys.ReferencingEntity, NormalizeLogicalName(GetRequiredString(node, "referencingEntity"))),
                    (ArtifactPropertyKeys.ReferencingAttribute, NormalizeLogicalName(GetRequiredString(node, "referencingAttribute"))),
                    (ArtifactPropertyKeys.OwningEntityLogicalName, referencedEntity),
                    (ArtifactPropertyKeys.Description, GetRequiredString(node, "description"))));
        }
    }

    private static IEnumerable<FamilyArtifact> ParseKeyArtifacts(string entityLogicalName, string path)
    {
        foreach (var node in ReadArray(path).OfType<JsonObject>())
        {
            var logicalName = NormalizeLogicalName(GetRequiredString(node, "logicalName"));
            if (string.IsNullOrWhiteSpace(logicalName))
            {
                continue;
            }

            yield return new FamilyArtifact(
                ComponentFamily.Key,
                $"{NormalizeLogicalName(entityLogicalName)}|{logicalName}",
                GetRequiredString(node, "schemaName") ?? GetRequiredString(node, "DisplayName"),
                path,
                EvidenceKind.Source,
                CreateProperties(
                    (ArtifactPropertyKeys.EntityLogicalName, NormalizeLogicalName(entityLogicalName)),
                    (ArtifactPropertyKeys.SchemaName, GetRequiredString(node, "schemaName")),
                    (ArtifactPropertyKeys.Description, GetRequiredString(node, "description")),
                    (ArtifactPropertyKeys.KeyAttributesJson, SerializeNode(GetValue(node, "keyAttributes"))),
                    (ArtifactPropertyKeys.IndexStatus, GetRequiredString(node, "indexStatus"))));
        }
    }

    private static IEnumerable<FamilyArtifact> ParseImageConfigurationArtifacts(string entityLogicalName, string path)
    {
        foreach (var node in ReadArray(path).OfType<JsonObject>())
        {
            var logicalName = NormalizeLogicalName(GetRequiredString(node, "LogicalName") ?? GetRequiredString(node, "logicalName"));
            if (string.IsNullOrWhiteSpace(logicalName))
            {
                continue;
            }

            yield return new FamilyArtifact(
                ComponentFamily.ImageConfiguration,
                logicalName,
                GetRequiredString(node, "DisplayName") ?? GetRequiredString(node, "displayName"),
                path,
                EvidenceKind.Source,
                CreateProperties(
                    (ArtifactPropertyKeys.EntityLogicalName, NormalizeLogicalName(entityLogicalName)),
                    (ArtifactPropertyKeys.ImageConfigurationScope, GetRequiredString(node, "scope")),
                    (ArtifactPropertyKeys.PrimaryImageAttribute, GetRequiredString(node, "primaryImageAttribute")),
                    (ArtifactPropertyKeys.ImageAttributeLogicalName, GetRequiredString(node, "imageAttributeLogicalName")),
                    (ArtifactPropertyKeys.CanStoreFullImage, GetRequiredBooleanString(node, "canStoreFullImage")),
                    (ArtifactPropertyKeys.IsPrimaryImage, GetRequiredBooleanString(node, "isPrimaryImage"))));
        }
    }

    private static FamilyArtifact ParseGlobalOptionSetArtifact(string path)
    {
        var node = ReadObject(path);
        return new FamilyArtifact(
            ComponentFamily.OptionSet,
            NormalizeLogicalName(GetRequiredString(node, "LogicalName")) ?? Path.GetFileNameWithoutExtension(path),
            GetRequiredString(node, "DisplayName"),
            path,
            EvidenceKind.Source,
            CreateProperties(
                (ArtifactPropertyKeys.OptionSetName, NormalizeLogicalName(GetRequiredString(node, "LogicalName"))),
                (ArtifactPropertyKeys.OptionSetType, GetRequiredString(node, "optionSetType")),
                (ArtifactPropertyKeys.Description, GetRequiredString(node, "description")),
                (ArtifactPropertyKeys.IsGlobal, "true"),
                (ArtifactPropertyKeys.OptionCount, (ParseInt(GetValue(node, "optionCount")) ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture)),
                (ArtifactPropertyKeys.OptionsJson, SerializeNode(GetValue(node, "options"))),
                (ArtifactPropertyKeys.SummaryJson, BuildSummaryFromGlobalOptionSet(node)),
                (ArtifactPropertyKeys.ComparisonSignature, GetRequiredString(node, "comparisonSignature"))));
    }

    private static FamilyArtifact ParseSummaryArtifact(string path, ComponentFamily expectedFamily)
    {
        var node = ReadObject(path);
        var family = ParseFamily(GetValue(node, "Family")) ?? expectedFamily;
        var properties = ReadSummaryProperties(node);
        return new FamilyArtifact(
            family,
            GetRequiredString(node, "LogicalName") ?? Path.GetFileNameWithoutExtension(path),
            GetRequiredString(node, "DisplayName"),
            path,
            EvidenceKind.Source,
            properties);
    }

    private static IReadOnlyDictionary<string, string>? ReadSummaryProperties(JsonObject node)
    {
        var properties = new Dictionary<string, string>(StringComparer.Ordinal);
        if (GetValue(node, "metadataSourcePath") is JsonNode metadataPath)
        {
            var value = ReadScalarString(metadataPath);
            if (!string.IsNullOrWhiteSpace(value))
            {
                properties[ArtifactPropertyKeys.MetadataSourcePath] = value;
            }
        }

        if (GetValue(node, "assetSourcePaths") is JsonNode assetSourcePaths)
        {
            var value = SerializeNode(assetSourcePaths);
            if (!string.IsNullOrWhiteSpace(value) && !string.Equals(value, "null", StringComparison.OrdinalIgnoreCase))
            {
                properties[ArtifactPropertyKeys.AssetSourceMapJson] = value;
            }
        }

        if (GetObject(node, "properties") is not { } propertiesNode)
        {
            return properties.Count == 0 ? null : properties;
        }

        foreach (var property in propertiesNode)
        {
            if (property.Value is null)
            {
                continue;
            }

            var value = property.Value is JsonObject or JsonArray
                ? SerializeNode(property.Value)
                : ReadScalarString(property.Value);
            if (!string.IsNullOrWhiteSpace(value))
            {
                properties[property.Key] = value;
            }
        }

        return properties.Count == 0 ? null : properties;
    }

    private static bool TryClassifyUnsupportedTrackedSource(string relativePath, out ComponentFamily family)
    {
        family = default;
        var normalized = NormalizeTrackedSourceRelativePath(relativePath);
        if (normalized.StartsWith("entities/", StringComparison.OrdinalIgnoreCase)
            && normalized.EndsWith("/image-configurations.json", StringComparison.OrdinalIgnoreCase))
        {
            family = ComponentFamily.ImageConfiguration;
            return true;
        }

        return UnsupportedPathPrefixes.TryGetValue(
            normalized.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty,
            out family);
    }

    private static readonly IReadOnlyDictionary<string, ComponentFamily> UnsupportedPathPrefixes =
        new Dictionary<string, ComponentFamily>(StringComparer.OrdinalIgnoreCase)
        {
            ["import-maps"] = ComponentFamily.ImportMap,
            ["data-source-mappings"] = ComponentFamily.DataSourceMapping,
            ["similarity-rules"] = ComponentFamily.SimilarityRule,
            ["slas"] = ComponentFamily.Sla,
            ["sla-items"] = ComponentFamily.SlaItem,
            ["reports"] = ComponentFamily.Report,
            ["templates"] = ComponentFamily.Template,
            ["display-strings"] = ComponentFamily.DisplayString,
            ["attachments"] = ComponentFamily.Attachment,
            ["legacy-assets"] = ComponentFamily.LegacyAsset
        };

    private static readonly IReadOnlyDictionary<string, ComponentFamily> SourceBackedSummaryFamilies =
        new Dictionary<string, ComponentFamily>(StringComparer.OrdinalIgnoreCase)
        {
            ["saved-query-visualizations"] = ComponentFamily.Visualization,
            ["app-settings"] = ComponentFamily.AppSetting,
            ["web-resources"] = ComponentFamily.WebResource,
            ["canvas-apps"] = ComponentFamily.CanvasApp,
            ["entity-analytics-configurations"] = ComponentFamily.EntityAnalyticsConfiguration,
            ["ai-project-types"] = ComponentFamily.AiProjectType,
            ["ai-projects"] = ComponentFamily.AiProject,
            ["ai-configurations"] = ComponentFamily.AiConfiguration,
            ["plugin-assemblies"] = ComponentFamily.PluginAssembly,
            ["plugin-types"] = ComponentFamily.PluginType,
            ["plugin-steps"] = ComponentFamily.PluginStep,
            ["plugin-step-images"] = ComponentFamily.PluginStepImage,
            ["service-endpoints"] = ComponentFamily.ServiceEndpoint,
            ["connectors"] = ComponentFamily.Connector,
            ["workflows"] = ComponentFamily.Workflow,
            ["duplicate-rules"] = ComponentFamily.DuplicateRule,
            ["duplicate-rule-conditions"] = ComponentFamily.DuplicateRuleCondition,
            ["routing-rules"] = ComponentFamily.RoutingRule,
            ["routing-rule-items"] = ComponentFamily.RoutingRuleItem,
            ["mobile-offline-profiles"] = ComponentFamily.MobileOfflineProfile,
            ["mobile-offline-profile-items"] = ComponentFamily.MobileOfflineProfileItem,
            ["roles"] = ComponentFamily.Role,
            ["role-privileges"] = ComponentFamily.RolePrivilege,
            ["field-security-profiles"] = ComponentFamily.FieldSecurityProfile,
            ["field-permissions"] = ComponentFamily.FieldPermission,
            ["connection-roles"] = ComponentFamily.ConnectionRole,
            ["ribbons"] = ComponentFamily.Ribbon,
            ["reports"] = ComponentFamily.Report,
            ["templates"] = ComponentFamily.Template,
            ["display-strings"] = ComponentFamily.DisplayString,
            ["attachments"] = ComponentFamily.Attachment,
            ["legacy-assets"] = ComponentFamily.LegacyAsset
        };

    private static string NormalizeTrackedSourceRelativePath(string value)
    {
        var normalized = value.Replace('\\', '/').TrimStart('/');
        return normalized.StartsWith("tracked-source/", StringComparison.OrdinalIgnoreCase)
            ? normalized["tracked-source/".Length..]
            : normalized;
    }

    private static JsonObject ReadObject(string path) =>
        JsonNode.Parse(File.ReadAllText(path)) as JsonObject
        ?? throw new InvalidOperationException($"Expected a JSON object at {path}.");

    private static JsonArray ReadArray(string path) =>
        JsonNode.Parse(File.ReadAllText(path)) as JsonArray
        ?? throw new InvalidOperationException($"Expected a JSON array at {path}.");

    private static string? GetRequiredString(JsonObject? node, string propertyName) =>
        ReadScalarString(GetValue(node, propertyName));

    private static string? GetRequiredBooleanString(JsonObject? node, string propertyName)
    {
        var value = GetValue(node, propertyName);
        return value switch
        {
            JsonValue jsonValue when jsonValue.TryGetValue<bool>(out var boolValue) => boolValue ? "true" : "false",
            _ => ReadScalarString(value)
        };
    }

    private static JsonNode? GetValue(JsonObject? node, string propertyName)
    {
        if (node is null)
        {
            return null;
        }

        foreach (var property in node)
        {
            if (property.Key.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value;
            }
        }

        return null;
    }

    private static JsonObject? GetObject(JsonObject? node, string propertyName) =>
        GetValue(node, propertyName) as JsonObject;

    private static JsonArray GetArray(JsonObject? node, string propertyName) =>
        GetValue(node, propertyName) as JsonArray ?? [];

    private static string? ReadScalarString(JsonNode? node)
    {
        if (node is null)
        {
            return null;
        }

        if (node is JsonValue value)
        {
            if (value.TryGetValue<string>(out var stringValue))
            {
                return stringValue;
            }

            if (value.TryGetValue<bool>(out var boolValue))
            {
                return boolValue ? "true" : "false";
            }

            if (value.TryGetValue<int>(out var intValue))
            {
                return intValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            if (value.TryGetValue<long>(out var longValue))
            {
                return longValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            if (value.TryGetValue<double>(out var doubleValue))
            {
                return doubleValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        return node.ToJsonString();
    }

    private static int? ParseInt(JsonNode? node)
    {
        if (node is JsonValue value)
        {
            if (value.TryGetValue<int>(out var intValue))
            {
                return intValue;
            }

            if (value.TryGetValue<string>(out var stringValue)
                && int.TryParse(stringValue, out intValue))
            {
                return intValue;
            }
        }

        return null;
    }

    private static ComponentFamily? ParseFamily(JsonNode? node)
    {
        if (node is JsonValue value)
        {
            if (value.TryGetValue<int>(out var intValue) && Enum.IsDefined(typeof(ComponentFamily), intValue))
            {
                return (ComponentFamily)intValue;
            }

            if (value.TryGetValue<string>(out var stringValue)
                && Enum.TryParse<ComponentFamily>(stringValue, ignoreCase: true, out var family))
            {
                return family;
            }
        }

        return null;
    }

    private static string? NormalizeLogicalName(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();

    private static LayeringIntent ParseLayeringIntent(string? value) =>
        Enum.TryParse<LayeringIntent>(value, ignoreCase: true, out var intent)
            ? intent
            : LayeringIntent.Hybrid;

    private static string SerializeNode(JsonNode? node) =>
        node is null ? "null" : node.ToJsonString(new JsonSerializerOptions { WriteIndented = false });

    private static string SerializeCompact<T>(T value) =>
        JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = false });

    private static string BuildSummaryFromGlobalOptionSet(JsonObject node) =>
        SerializeCompact(new
        {
            optionSetName = NormalizeLogicalName(GetRequiredString(node, "LogicalName")),
            optionSetType = GetRequiredString(node, "optionSetType"),
            isGlobal = true,
            optionCount = ParseInt(GetValue(node, "optionCount")) ?? 0,
            options = JsonNode.Parse(SerializeNode(GetValue(node, "options")) ?? "[]")
        });

    private static IReadOnlyDictionary<string, string>? CreateProperties(params (string Key, string? Value)[] properties)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in properties)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                result[key] = value;
            }
        }

        return result.Count == 0 ? null : result;
    }
}
