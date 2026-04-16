using System.Globalization;
using DataverseSolutionCompiler.Domain.Model;

namespace DataverseSolutionCompiler.Readers.Xml;

internal sealed partial class XmlCanonicalSolutionParser
{
    private void ParseEntities()
    {
        var entitiesRoot = Path.Combine(_root, "Entities");
        if (!Directory.Exists(entitiesRoot))
        {
            return;
        }

        foreach (var entityDirectory in Directory.GetDirectories(entitiesRoot).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var entityPath = Path.Combine(entityDirectory, "Entity.xml");
            if (!File.Exists(entityPath))
            {
                continue;
            }

            var entityRoot = LoadRoot(entityPath);
            var entity = entityRoot.ElementLocal("EntityInfo")?.ElementLocal("entity");
            if (entity is null)
            {
                continue;
            }

            var schemaName = Text(entityRoot.ElementLocal("Name")) ?? Path.GetFileName(entityDirectory);
            var logicalName = NormalizeLogicalName(entity.AttributeValue("Name") ?? schemaName);
            var displayName = entityRoot.ElementLocal("Name")?.AttributeValue("LocalizedName")
                ?? LocalizedDescription(entity.ElementLocal("LocalizedNames"))
                ?? schemaName;
            var description = LocalizedDescription(entity.ElementLocal("Descriptions"));
            var entitySetName = Text(entity.ElementLocal("EntitySetName"));
            var ownershipTypeMask = Text(entity.ElementLocal("OwnershipTypeMask"));
            var attributes = entity.ElementLocal("attributes")
                ?.Elements()
                .Where(element => element.Name.LocalName.Equals("attribute", StringComparison.OrdinalIgnoreCase))
                .ToArray()
                ?? [];
            var primaryIdAttribute = attributes
                .FirstOrDefault(attribute => Text(attribute.ElementLocal("Type"))?.Equals("primarykey", StringComparison.OrdinalIgnoreCase) == true)
                ?.ElementLocal("LogicalName")
                .Let(Text);
            var primaryNameAttribute = attributes
                .FirstOrDefault(attribute => (Text(attribute.ElementLocal("DisplayMask")) ?? string.Empty).Contains("PrimaryName", StringComparison.OrdinalIgnoreCase))
                ?.ElementLocal("LogicalName")
                .Let(Text);
            var hasForms = Directory.Exists(Path.Combine(entityDirectory, "FormXml"))
                && Directory.EnumerateFiles(Path.Combine(entityDirectory, "FormXml"), "*.xml", SearchOption.AllDirectories).Any();
            var hasViews = Directory.Exists(Path.Combine(entityDirectory, "SavedQueries"))
                && Directory.EnumerateFiles(Path.Combine(entityDirectory, "SavedQueries"), "*.xml", SearchOption.AllDirectories).Any();
            var hasVisualizations = Directory.Exists(Path.Combine(entityDirectory, "Visualizations"))
                && Directory.EnumerateFiles(Path.Combine(entityDirectory, "Visualizations"), "*.xml", SearchOption.AllDirectories).Any();

            var authoredAttributes = attributes
                .Where(attribute => IsAuthoredAttribute(attribute, primaryIdAttribute, primaryNameAttribute))
                .ToArray();

            AddArtifact(
                ComponentFamily.Table,
                logicalName!,
                displayName,
                entityPath,
                CreateProperties(
                    (ArtifactPropertyKeys.MetadataSourcePath, RelativePath(entityPath)),
                    (ArtifactPropertyKeys.EntityLogicalName, logicalName),
                    (ArtifactPropertyKeys.SchemaName, schemaName),
                    (ArtifactPropertyKeys.Description, description),
                    (ArtifactPropertyKeys.EntitySetName, entitySetName),
                    (ArtifactPropertyKeys.OwnershipTypeMask, ownershipTypeMask),
                    (ArtifactPropertyKeys.PrimaryIdAttribute, NormalizeLogicalName(primaryIdAttribute)),
                    (ArtifactPropertyKeys.PrimaryNameAttribute, NormalizeLogicalName(primaryNameAttribute)),
                    (ArtifactPropertyKeys.IsCustomizable, NormalizeBoolean(Text(entity.ElementLocal("IsCustomizable")))),
                    (ArtifactPropertyKeys.ShellOnly, (!authoredAttributes.Any() && !hasForms && !hasViews && !hasVisualizations) ? "true" : "false")));

            foreach (var attribute in authoredAttributes)
            {
                var attributeLogicalName = NormalizeLogicalName(Text(attribute.ElementLocal("LogicalName")) ?? Text(attribute.ElementLocal("Name")));
                var attributeSchemaName = Text(attribute.ElementLocal("Name")) ?? attributeLogicalName;
                var attributeType = Text(attribute.ElementLocal("Type")) ?? "unknown";
                var attributeDisplayName = LocalizedDescription(attribute.ElementLocal("displaynames")) ?? attributeSchemaName;
                var optionSet = attribute.ElementLocal("optionset");
                var exportOptionSetName = NormalizeLogicalName(Text(attribute.ElementLocal("OptionSetName")));
                var optionSetType = optionSet is null
                    ? (!string.IsNullOrWhiteSpace(exportOptionSetName) ? attributeType : null)
                    : (Text(optionSet.ElementLocal("OptionSetType")) ?? attributeType);
                var isGlobalOptionSet = optionSet is null
                    ? (!string.IsNullOrWhiteSpace(exportOptionSetName) ? "true" : null)
                    : NormalizeBoolean(Text(optionSet.ElementLocal("IsGlobal")));
                var optionSetName = optionSet is null
                    ? exportOptionSetName
                    : optionSet.AttributeValue("Name")
                        ?? (string.Equals(isGlobalOptionSet, "true", StringComparison.OrdinalIgnoreCase)
                            ? null
                            : attributeLogicalName);

                AddArtifact(
                    ComponentFamily.Column,
                    $"{logicalName}|{attributeLogicalName}",
                    attributeDisplayName,
                    entityPath,
                    CreateProperties(
                        (ArtifactPropertyKeys.EntityLogicalName, logicalName),
                        (ArtifactPropertyKeys.SchemaName, attributeSchemaName),
                        (ArtifactPropertyKeys.Description, LocalizedDescription(attribute.ElementLocal("Descriptions"))),
                        (ArtifactPropertyKeys.AttributeType, attributeType),
                        (ArtifactPropertyKeys.IsSecured, NormalizeBoolean(Text(attribute.ElementLocal("IsSecured")))),
                        (ArtifactPropertyKeys.IsCustomField, NormalizeBoolean(Text(attribute.ElementLocal("IsCustomField")))),
                        (ArtifactPropertyKeys.IsPrimaryKey, primaryIdAttribute is not null && attributeLogicalName!.Equals(primaryIdAttribute, StringComparison.OrdinalIgnoreCase) ? "true" : "false"),
                        (ArtifactPropertyKeys.IsPrimaryName, primaryNameAttribute is not null && attributeLogicalName!.Equals(primaryNameAttribute, StringComparison.OrdinalIgnoreCase) ? "true" : "false"),
                        (ArtifactPropertyKeys.IsLogical, NormalizeBoolean(Text(attribute.ElementLocal("IsLogical")))),
                        (ArtifactPropertyKeys.IsCustomizable, NormalizeBoolean(Text(attribute.ElementLocal("IsCustomizable")))),
                        (ArtifactPropertyKeys.CanStoreFullImage, NormalizeBoolean(Text(attribute.ElementLocal("CanStoreFullImage")))),
                        (ArtifactPropertyKeys.IsPrimaryImage, NormalizeBoolean(Text(attribute.ElementLocal("IsPrimaryImage")))),
                        (ArtifactPropertyKeys.OptionSetName, optionSetName),
                        (ArtifactPropertyKeys.OptionSetType, optionSetType),
                        (ArtifactPropertyKeys.IsGlobal, isGlobalOptionSet)));
            }

            foreach (var optionArtifact in ParseAttributeOptionSets(logicalName!, entityPath, attributes))
            {
                _artifacts.Add(optionArtifact);
            }

            foreach (var keyArtifact in ParseEntityKeys(logicalName!, entityPath, entity))
            {
                _artifacts.Add(keyArtifact);
            }

            foreach (var imageConfigurationArtifact in ParseImageConfigurations(logicalName!, entityPath, entity, attributes))
            {
                _artifacts.Add(imageConfigurationArtifact);
            }

            ParseForms(entityDirectory, logicalName!);
            ParseViews(entityDirectory, logicalName!);
            ParseVisualizations(entityDirectory, logicalName!);
            ParseRibbons(entityDirectory, logicalName!);
        }
    }

    private IEnumerable<FamilyArtifact> ParseAttributeOptionSets(string entityLogicalName, string sourcePath, IEnumerable<System.Xml.Linq.XElement> attributes)
    {
        foreach (var attribute in attributes)
        {
            var optionset = attribute.ElementLocal("optionset");
            if (optionset is null)
            {
                continue;
            }

            if (NormalizeBoolean(Text(optionset.ElementLocal("IsGlobal"))) == "true")
            {
                continue;
            }

            var attributeLogicalName = NormalizeLogicalName(Text(attribute.ElementLocal("LogicalName")) ?? Text(attribute.ElementLocal("Name")));
            var displayName = LocalizedDescription(attribute.ElementLocal("displaynames")) ?? attributeLogicalName;
            var description = LocalizedDescription(attribute.ElementLocal("Descriptions"));
            var optionSetType = Text(optionset.ElementLocal("OptionSetType")) ?? Text(attribute.ElementLocal("Type")) ?? "picklist";
            var optionsJson = BuildOptionEntriesJson(optionset, optionSetType);
            var summaryJson = SerializeJson(new
            {
                entityLogicalName,
                attributeLogicalName,
                optionSetType,
                isGlobal = false,
                optionCount = CountOptionEntries(optionset, optionSetType),
                options = System.Text.Json.Nodes.JsonNode.Parse(optionsJson)
            });

            yield return new FamilyArtifact(
                ComponentFamily.OptionSet,
                $"{entityLogicalName}|{attributeLogicalName}",
                displayName,
                sourcePath,
                EvidenceKind.Source,
                CreateProperties(
                    (ArtifactPropertyKeys.EntityLogicalName, entityLogicalName),
                    (ArtifactPropertyKeys.OptionSetName, optionset.AttributeValue("Name") ?? attributeLogicalName),
                    (ArtifactPropertyKeys.OptionSetType, optionSetType),
                    (ArtifactPropertyKeys.Description, description),
                    (ArtifactPropertyKeys.IsGlobal, "false"),
                    (ArtifactPropertyKeys.OptionCount, CountOptionEntries(optionset, optionSetType).ToString(CultureInfo.InvariantCulture)),
                    (ArtifactPropertyKeys.OptionsJson, optionsJson),
                    (ArtifactPropertyKeys.SummaryJson, summaryJson),
                    (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(summaryJson))));
        }
    }

    private static bool IsAuthoredAttribute(System.Xml.Linq.XElement attribute, string? primaryIdAttribute, string? primaryNameAttribute)
    {
        var logicalName = NormalizeLogicalName(Text(attribute.ElementLocal("LogicalName")) ?? Text(attribute.ElementLocal("Name")));
        if (string.IsNullOrWhiteSpace(logicalName))
        {
            return false;
        }

        if (NormalizeBoolean(Text(attribute.ElementLocal("IsCustomField"))) == "true")
        {
            return true;
        }

        if (logicalName.Equals(NormalizeLogicalName(primaryIdAttribute), StringComparison.OrdinalIgnoreCase)
            || logicalName.Equals(NormalizeLogicalName(primaryNameAttribute), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static string BuildOptionEntriesJson(System.Xml.Linq.XElement optionSet, string optionSetType)
    {
        IEnumerable<object> options = optionSetType.ToLowerInvariant() switch
        {
            "state" => optionSet.ElementLocal("states")
                ?.Elements()
                .Where(element => element.Name.LocalName.Equals("state", StringComparison.OrdinalIgnoreCase))
                .Select(element => new
                {
                    value = element.AttributeValue("value") ?? string.Empty,
                    label = LocalizedDescription(element.ElementLocal("labels")) ?? string.Empty,
                    defaultStatus = element.AttributeValue("defaultstatus") ?? string.Empty,
                    invariantName = element.AttributeValue("invariantname") ?? string.Empty
                })
                ?? [],
            "status" => optionSet.ElementLocal("statuses")
                ?.Elements()
                .Where(element => element.Name.LocalName.Equals("status", StringComparison.OrdinalIgnoreCase))
                .Select(element => new
                {
                    value = element.AttributeValue("value") ?? string.Empty,
                    label = LocalizedDescription(element.ElementLocal("labels")) ?? string.Empty,
                    state = element.AttributeValue("state") ?? string.Empty
                })
                ?? [],
            _ => optionSet.ElementLocal("options")
                ?.Elements()
                .Where(element => element.Name.LocalName.Equals("option", StringComparison.OrdinalIgnoreCase))
                .Select(element => new
                {
                    value = element.AttributeValue("value") ?? string.Empty,
                    label = LocalizedDescription(element.ElementLocal("labels")) ?? string.Empty,
                    isHidden = NormalizeBoolean(element.AttributeValue("IsHidden"))
                })
                ?? []
        };

        return SerializeJson(options.ToArray());
    }

    private static int CountOptionEntries(System.Xml.Linq.XElement optionSet, string optionSetType) =>
        optionSetType.ToLowerInvariant() switch
        {
            "state" => optionSet.ElementLocal("states")?.Elements().Count(element => element.Name.LocalName.Equals("state", StringComparison.OrdinalIgnoreCase)) ?? 0,
            "status" => optionSet.ElementLocal("statuses")?.Elements().Count(element => element.Name.LocalName.Equals("status", StringComparison.OrdinalIgnoreCase)) ?? 0,
            _ => optionSet.ElementLocal("options")?.Elements().Count(element => element.Name.LocalName.Equals("option", StringComparison.OrdinalIgnoreCase)) ?? 0
        };
}
