using System.Xml.Linq;
using DataverseSolutionCompiler.Domain.Model;

namespace DataverseSolutionCompiler.Readers.Xml;

internal sealed partial class XmlCanonicalSolutionParser
{
    private IEnumerable<FamilyArtifact> ParseImageConfigurations(
        string entityLogicalName,
        string sourcePath,
        XElement entity,
        IEnumerable<XElement> attributes)
    {
        var imageAttributes = attributes
            .Where(IsImageAttribute)
            .OrderBy(attribute => Text(attribute.ElementLocal("LogicalName")) ?? Text(attribute.ElementLocal("Name")), StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (imageAttributes.Length == 0)
        {
            yield break;
        }

        var primaryImageAttribute = NormalizeLogicalName(Text(entity.ElementLocal("PrimaryImageAttribute")));
        if (!string.IsNullOrWhiteSpace(primaryImageAttribute))
        {
            var primaryImage = imageAttributes.FirstOrDefault(attribute =>
                string.Equals(
                    NormalizeLogicalName(Text(attribute.ElementLocal("LogicalName")) ?? Text(attribute.ElementLocal("Name"))),
                    primaryImageAttribute,
                    StringComparison.OrdinalIgnoreCase));
            var displayName = primaryImage is null
                ? primaryImageAttribute
                : LocalizedDescription(primaryImage.ElementLocal("displaynames")) ?? primaryImageAttribute;

            yield return new FamilyArtifact(
                ComponentFamily.ImageConfiguration,
                $"{entityLogicalName}|entity-image",
                displayName,
                sourcePath,
                EvidenceKind.Source,
                CreateProperties(
                    (ArtifactPropertyKeys.EntityLogicalName, entityLogicalName),
                    (ArtifactPropertyKeys.ImageConfigurationScope, "entity"),
                    (ArtifactPropertyKeys.PrimaryImageAttribute, primaryImageAttribute),
                    (ArtifactPropertyKeys.ImageAttributeLogicalName, primaryImageAttribute),
                    (ArtifactPropertyKeys.CanStoreFullImage, NormalizeBoolean(Text(primaryImage?.ElementLocal("CanStoreFullImage")))),
                    (ArtifactPropertyKeys.IsPrimaryImage, "true")));
        }

        foreach (var attribute in imageAttributes)
        {
            var attributeLogicalName = NormalizeLogicalName(Text(attribute.ElementLocal("LogicalName")) ?? Text(attribute.ElementLocal("Name")));
            if (string.IsNullOrWhiteSpace(attributeLogicalName))
            {
                continue;
            }

            yield return new FamilyArtifact(
                ComponentFamily.ImageConfiguration,
                $"{entityLogicalName}|{attributeLogicalName}|attribute-image",
                LocalizedDescription(attribute.ElementLocal("displaynames")) ?? attributeLogicalName,
                sourcePath,
                EvidenceKind.Source,
                CreateProperties(
                    (ArtifactPropertyKeys.EntityLogicalName, entityLogicalName),
                    (ArtifactPropertyKeys.ImageConfigurationScope, "attribute"),
                    (ArtifactPropertyKeys.PrimaryImageAttribute, primaryImageAttribute),
                    (ArtifactPropertyKeys.ImageAttributeLogicalName, attributeLogicalName),
                    (ArtifactPropertyKeys.CanStoreFullImage, NormalizeBoolean(Text(attribute.ElementLocal("CanStoreFullImage")))),
                    (ArtifactPropertyKeys.IsPrimaryImage, NormalizeBoolean(Text(attribute.ElementLocal("IsPrimaryImage"))))));
        }
    }

    private static bool IsImageAttribute(XElement attribute)
    {
        var attributeType = NormalizeLogicalName(Text(attribute.ElementLocal("Type")));
        if (string.Equals(attributeType, "image", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return attribute.ElementLocal("CanStoreFullImage") is not null
            || attribute.ElementLocal("IsPrimaryImage") is not null;
    }
}
