using System.Globalization;
using DataverseSolutionCompiler.Domain.Model;

namespace DataverseSolutionCompiler.Readers.Xml;

internal sealed partial class XmlCanonicalSolutionParser
{
    private void ParseAiFamilies()
    {
        ParseAiProjectTypes();
        ParseAiProjects();
        ParseAiConfigurations();
    }

    private void ParseAiProjectTypes()
    {
        var rootDirectory = Path.Combine(_root, "AIProjectTypes");
        if (!Directory.Exists(rootDirectory))
        {
            return;
        }

        foreach (var directory in Directory.GetDirectories(rootDirectory).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var metadataPath = Path.Combine(directory, "AIProjectType.xml");
            if (!File.Exists(metadataPath))
            {
                continue;
            }

            var root = LoadRoot(metadataPath);
            var logicalName = NormalizeLogicalName(Text(root.ElementLocal("LogicalName")) ?? Path.GetFileName(directory));
            if (string.IsNullOrWhiteSpace(logicalName))
            {
                continue;
            }

            var displayName = Text(root.ElementLocal("Name")) ?? logicalName;
            var description = Text(root.ElementLocal("Description"));
            var summaryJson = SerializeJson(new
            {
                logicalName,
                description
            });

            AddArtifact(
                ComponentFamily.AiProjectType,
                logicalName!,
                displayName,
                metadataPath,
                CreateProperties(
                    (ArtifactPropertyKeys.MetadataSourcePath, RelativePath(metadataPath)),
                    (ArtifactPropertyKeys.Description, description),
                    (ArtifactPropertyKeys.SummaryJson, summaryJson),
                    (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(summaryJson))));
        }
    }

    private void ParseAiProjects()
    {
        var rootDirectory = Path.Combine(_root, "AIProjects");
        if (!Directory.Exists(rootDirectory))
        {
            return;
        }

        foreach (var directory in Directory.GetDirectories(rootDirectory).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var metadataPath = Path.Combine(directory, "AIProject.xml");
            if (!File.Exists(metadataPath))
            {
                continue;
            }

            var root = LoadRoot(metadataPath);
            var logicalName = NormalizeLogicalName(Text(root.ElementLocal("LogicalName")) ?? Path.GetFileName(directory));
            if (string.IsNullOrWhiteSpace(logicalName))
            {
                continue;
            }

            var displayName = Text(root.ElementLocal("Name")) ?? logicalName;
            var description = Text(root.ElementLocal("Description"));
            var parentProjectTypeLogicalName = NormalizeLogicalName(Text(root.ElementLocal("ProjectTypeLogicalName")));
            var targetEntity = NormalizeLogicalName(Text(root.ElementLocal("TargetEntity")));
            var summaryJson = SerializeJson(new
            {
                logicalName,
                parentProjectTypeLogicalName,
                targetEntity,
                description
            });

            AddArtifact(
                ComponentFamily.AiProject,
                logicalName!,
                displayName,
                metadataPath,
                CreateProperties(
                    (ArtifactPropertyKeys.MetadataSourcePath, RelativePath(metadataPath)),
                    (ArtifactPropertyKeys.ParentAiProjectTypeLogicalName, parentProjectTypeLogicalName),
                    (ArtifactPropertyKeys.TargetEntity, targetEntity),
                    (ArtifactPropertyKeys.Description, description),
                    (ArtifactPropertyKeys.SummaryJson, summaryJson),
                    (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(summaryJson))));
        }
    }

    private void ParseAiConfigurations()
    {
        var rootDirectory = Path.Combine(_root, "AIConfigurations");
        if (!Directory.Exists(rootDirectory))
        {
            return;
        }

        foreach (var directory in Directory.GetDirectories(rootDirectory).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var metadataPath = Path.Combine(directory, "AIConfiguration.xml");
            if (!File.Exists(metadataPath))
            {
                continue;
            }

            var root = LoadRoot(metadataPath);
            var logicalName = NormalizeLogicalName(Text(root.ElementLocal("LogicalName")) ?? Path.GetFileName(directory));
            if (string.IsNullOrWhiteSpace(logicalName))
            {
                continue;
            }

            var displayName = Text(root.ElementLocal("Name")) ?? logicalName;
            var parentProjectLogicalName = NormalizeLogicalName(Text(root.ElementLocal("ProjectLogicalName")));
            var configurationKind = NormalizeLogicalName(Text(root.ElementLocal("ConfigurationKind")));
            var value = Text(root.ElementLocal("Value")) ?? Text(root.ElementLocal("ConfigurationValue"));
            var summaryJson = SerializeJson(new
            {
                logicalName,
                parentProjectLogicalName,
                configurationKind,
                value
            });

            AddArtifact(
                ComponentFamily.AiConfiguration,
                logicalName!,
                displayName,
                metadataPath,
                CreateProperties(
                    (ArtifactPropertyKeys.MetadataSourcePath, RelativePath(metadataPath)),
                    (ArtifactPropertyKeys.ParentAiProjectLogicalName, parentProjectLogicalName),
                    (ArtifactPropertyKeys.ConfigurationKind, configurationKind),
                    (ArtifactPropertyKeys.Value, value),
                    (ArtifactPropertyKeys.SummaryJson, summaryJson),
                    (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(summaryJson))));
        }
    }

    private void ParseEntityAnalyticsConfigurations()
    {
        var analyticsDirectory = Path.Combine(_root, "entityanalyticsconfigs");
        if (!Directory.Exists(analyticsDirectory))
        {
            return;
        }

        foreach (var directory in Directory.GetDirectories(analyticsDirectory).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var analyticsPath = Path.Combine(directory, "entityanalyticsconfig.xml");
            if (!File.Exists(analyticsPath))
            {
                continue;
            }

            var root = LoadRoot(analyticsPath);
            var parentEntityLogicalName = NormalizeLogicalName(Text(root.ElementLocal("ParentEntityLogicalName")) ?? Path.GetFileName(directory));
            if (string.IsNullOrWhiteSpace(parentEntityLogicalName))
            {
                continue;
            }

            var entityDataSource = Text(root.ElementLocal("EntityDataSource"));
            var isEnabledForAdls = NormalizeBoolean(Text(root.ElementLocal("IsEnabledForADLS")) ?? Text(root.ElementLocal("IsEnabledForAdls")));
            var isEnabledForTimeSeries = NormalizeBoolean(Text(root.ElementLocal("IsEnabledForTimeSeries")));
            var summaryJson = SerializeJson(new
            {
                parentEntityLogicalName,
                entityDataSource,
                isEnabledForAdls,
                isEnabledForTimeSeries
            });

            AddArtifact(
                ComponentFamily.EntityAnalyticsConfiguration,
                parentEntityLogicalName!,
                parentEntityLogicalName,
                analyticsPath,
                CreateProperties(
                    (ArtifactPropertyKeys.MetadataSourcePath, RelativePath(analyticsPath)),
                    (ArtifactPropertyKeys.ParentEntityLogicalName, parentEntityLogicalName),
                    (ArtifactPropertyKeys.EntityDataSource, entityDataSource),
                    (ArtifactPropertyKeys.IsEnabledForAdls, isEnabledForAdls),
                    (ArtifactPropertyKeys.IsEnabledForTimeSeries, isEnabledForTimeSeries),
                    (ArtifactPropertyKeys.SummaryJson, summaryJson),
                    (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(summaryJson))));
        }
    }

    private void ParseImportMaps()
    {
        var importMapsDirectory = Path.Combine(_root, "ImportMaps");
        if (!Directory.Exists(importMapsDirectory))
        {
            return;
        }

        foreach (var directory in Directory.GetDirectories(importMapsDirectory).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var importMapPath = Path.Combine(directory, "ImportMap.xml");
            if (!File.Exists(importMapPath))
            {
                continue;
            }

            var root = LoadRoot(importMapPath);
            var logicalName = NormalizeLogicalName(Text(root.ElementLocal("LogicalName")) ?? Path.GetFileName(directory));
            if (string.IsNullOrWhiteSpace(logicalName))
            {
                continue;
            }

            var displayName = Text(root.ElementLocal("Name")) ?? logicalName;
            var targetEntity = NormalizeLogicalName(Text(root.ElementLocal("TargetEntity")) ?? Text(root.ElementLocal("TargetEntityName")));
            var importSource = Text(root.ElementLocal("Source")) ?? Text(root.ElementLocal("SourceType"));
            var sourceFormat = Text(root.ElementLocal("SourceFormat")) ?? Text(root.ElementLocal("Format"));
            var fieldDelimiter = Text(root.ElementLocal("FieldDelimiter")) ?? Text(root.ElementLocal("Delimiter"));
            var description = Text(root.ElementLocal("Description"));
            var mappingRows = root.ElementLocal("DataSourceMappings")
                ?.Elements()
                .Where(element => element.Name.LocalName.Equals("DataSourceMapping", StringComparison.OrdinalIgnoreCase))
                .Select(element => new ImportMapMapping(
                    NormalizeLogicalName(Text(element.ElementLocal("SourceEntityName"))),
                    NormalizeLogicalName(Text(element.ElementLocal("SourceAttributeName"))),
                    NormalizeLogicalName(Text(element.ElementLocal("TargetEntityName")) ?? targetEntity),
                    NormalizeLogicalName(Text(element.ElementLocal("TargetAttributeName"))),
                    Text(element.ElementLocal("ProcessCode")),
                    Text(element.ElementLocal("ColumnIndex"))))
                .Where(mapping => !string.IsNullOrWhiteSpace(mapping.SourceAttributeName) || !string.IsNullOrWhiteSpace(mapping.TargetAttributeName))
                .OrderBy(mapping => mapping.TargetAttributeName ?? mapping.SourceAttributeName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(mapping => mapping.SourceAttributeName, StringComparer.OrdinalIgnoreCase)
                .ToArray()
                ?? [];
            var summaryJson = SerializeJson(new
            {
                importSource,
                sourceFormat,
                targetEntity,
                fieldDelimiter,
                mappingCount = mappingRows.Length
            });

            AddArtifact(
                ComponentFamily.ImportMap,
                logicalName!,
                displayName,
                importMapPath,
                CreateProperties(
                    (ArtifactPropertyKeys.MetadataSourcePath, RelativePath(importMapPath)),
                    (ArtifactPropertyKeys.Description, description),
                    (ArtifactPropertyKeys.ImportSource, importSource),
                    (ArtifactPropertyKeys.SourceFormat, sourceFormat),
                    (ArtifactPropertyKeys.ImportTargetEntity, targetEntity),
                    (ArtifactPropertyKeys.FieldDelimiter, fieldDelimiter),
                    (ArtifactPropertyKeys.MappingCount, mappingRows.Length.ToString(CultureInfo.InvariantCulture)),
                    (ArtifactPropertyKeys.SummaryJson, summaryJson),
                    (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(summaryJson))));

            foreach (var mapping in mappingRows)
            {
                var mappingKey = BuildImportMapMappingLogicalName(logicalName, mapping);
                AddArtifact(
                    ComponentFamily.DataSourceMapping,
                    mappingKey,
                    $"{mapping.SourceAttributeName ?? "source"} -> {mapping.TargetAttributeName ?? "target"}",
                    importMapPath,
                    CreateProperties(
                        (ArtifactPropertyKeys.ParentImportMapLogicalName, logicalName),
                        (ArtifactPropertyKeys.MetadataSourcePath, RelativePath(importMapPath)),
                        (ArtifactPropertyKeys.SourceEntityName, mapping.SourceEntityName),
                        (ArtifactPropertyKeys.SourceAttributeName, mapping.SourceAttributeName),
                        (ArtifactPropertyKeys.TargetEntityName, mapping.TargetEntityName),
                        (ArtifactPropertyKeys.TargetAttributeName, mapping.TargetAttributeName),
                        (ArtifactPropertyKeys.ProcessCode, mapping.ProcessCode),
                        (ArtifactPropertyKeys.ColumnIndex, mapping.ColumnIndex)));
            }
        }
    }

    private static string BuildImportMapMappingLogicalName(string importMapLogicalName, ImportMapMapping mapping)
    {
        var sourceAttribute = mapping.SourceAttributeName ?? "source";
        var targetAttribute = mapping.TargetAttributeName ?? "target";
        var suffix = string.IsNullOrWhiteSpace(mapping.ColumnIndex) ? null : mapping.ColumnIndex;
        return suffix is null
            ? $"{importMapLogicalName}|{sourceAttribute}|{targetAttribute}"
            : $"{importMapLogicalName}|{sourceAttribute}|{targetAttribute}|{suffix}";
    }

    private sealed record ImportMapMapping(
        string? SourceEntityName,
        string? SourceAttributeName,
        string? TargetEntityName,
        string? TargetAttributeName,
        string? ProcessCode,
        string? ColumnIndex);
}
