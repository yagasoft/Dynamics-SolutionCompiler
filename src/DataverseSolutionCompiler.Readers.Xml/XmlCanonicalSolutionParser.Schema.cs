using System.Globalization;
using DataverseSolutionCompiler.Domain.Model;

namespace DataverseSolutionCompiler.Readers.Xml;

internal sealed partial class XmlCanonicalSolutionParser
{
    private void ParseRelationships()
    {
        var relationshipsDirectory = Path.Combine(_root, "Other", "Relationships");
        if (!Directory.Exists(relationshipsDirectory))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(relationshipsDirectory, "*.xml", SearchOption.TopDirectoryOnly).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var root = LoadRoot(file);
            foreach (var relationship in root.Elements().Where(element => element.Name.LocalName.Equals("EntityRelationship", StringComparison.OrdinalIgnoreCase)))
            {
                var logicalName = NormalizeLogicalName(relationship.AttributeValue("Name"));
                if (string.IsNullOrWhiteSpace(logicalName))
                {
                    continue;
                }

                var referencedEntity = NormalizeLogicalName(Text(relationship.ElementLocal("ReferencedEntityName")));
                var referencingEntity = NormalizeLogicalName(Text(relationship.ElementLocal("ReferencingEntityName")));

                AddArtifact(
                    ComponentFamily.Relationship,
                    logicalName,
                    logicalName,
                    file,
                    CreateProperties(
                        (ArtifactPropertyKeys.RelationshipType, Text(relationship.ElementLocal("EntityRelationshipType"))),
                        (ArtifactPropertyKeys.ReferencedEntity, referencedEntity),
                        (ArtifactPropertyKeys.ReferencingEntity, referencingEntity),
                        (ArtifactPropertyKeys.ReferencingAttribute, NormalizeLogicalName(Text(relationship.ElementLocal("ReferencingAttributeName")))),
                        (ArtifactPropertyKeys.OwningEntityLogicalName, referencedEntity),
                        (ArtifactPropertyKeys.Description, LocalizedDescription(relationship.ElementLocal("RelationshipDescription")?.ElementLocal("Descriptions")))));
            }
        }
    }

    private void ParseGlobalOptionSets()
    {
        var optionSetsDirectory = Path.Combine(_root, "OptionSets");
        if (!Directory.Exists(optionSetsDirectory))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(optionSetsDirectory, "*.xml", SearchOption.TopDirectoryOnly).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var root = LoadRoot(file);
            var logicalName = NormalizeLogicalName(root.AttributeValue("Name") ?? Path.GetFileNameWithoutExtension(file));
            var displayName = root.AttributeValue("localizedName") ?? LocalizedDescription(root.ElementLocal("displaynames")) ?? logicalName;
            var optionSetType = Text(root.ElementLocal("OptionSetType")) ?? "picklist";
            var optionsJson = BuildOptionEntriesJson(root, optionSetType);
            var summaryJson = SerializeJson(new
            {
                optionSetName = logicalName,
                optionSetType,
                isGlobal = NormalizeBoolean(Text(root.ElementLocal("IsGlobal"))) == "true",
                optionCount = CountOptionEntries(root, optionSetType),
                options = System.Text.Json.Nodes.JsonNode.Parse(optionsJson)
            });

            AddArtifact(
                ComponentFamily.OptionSet,
                logicalName!,
                displayName,
                file,
                CreateProperties(
                    (ArtifactPropertyKeys.OptionSetName, logicalName),
                    (ArtifactPropertyKeys.OptionSetType, optionSetType),
                    (ArtifactPropertyKeys.Description, LocalizedDescription(root.ElementLocal("Descriptions")) ?? root.AttributeValue("description")),
                    (ArtifactPropertyKeys.IsGlobal, NormalizeBoolean(Text(root.ElementLocal("IsGlobal")))),
                    (ArtifactPropertyKeys.OptionCount, CountOptionEntries(root, optionSetType).ToString(CultureInfo.InvariantCulture)),
                    (ArtifactPropertyKeys.OptionsJson, optionsJson),
                    (ArtifactPropertyKeys.SummaryJson, summaryJson),
                    (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(summaryJson))));
        }
    }
}
