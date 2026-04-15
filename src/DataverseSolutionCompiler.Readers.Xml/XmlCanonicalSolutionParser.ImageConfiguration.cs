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
        var customizationsRoot = LoadCustomizationsRoot();
        var customizationsPath = Path.Combine(_root, "Other", "Customizations.xml");
        var imageAttributes = attributes
            .Where(IsImageAttribute)
            .OrderBy(attribute => Text(attribute.ElementLocal("LogicalName")) ?? Text(attribute.ElementLocal("Name")), StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (imageAttributes.Length == 0)
        {
            yield break;
        }

        var entityImageConfiguration = FindEntityImageConfiguration(customizationsRoot, entityLogicalName);
        var attributeImageConfigurations = FindAttributeImageConfigurations(customizationsRoot, entityLogicalName);
        var primaryImageAttribute = NormalizeLogicalName(Text(entity.ElementLocal("PrimaryImageAttribute")))
            ?? NormalizeLogicalName(Text(entityImageConfiguration?.ElementLocal("primaryimageattribute")));
        if (!string.IsNullOrWhiteSpace(primaryImageAttribute))
        {
            var primaryImage = imageAttributes.FirstOrDefault(attribute =>
                string.Equals(
                    NormalizeLogicalName(Text(attribute.ElementLocal("LogicalName")) ?? Text(attribute.ElementLocal("Name"))),
                    primaryImageAttribute,
                    StringComparison.OrdinalIgnoreCase));
            attributeImageConfigurations.TryGetValue(primaryImageAttribute, out var primaryImageConfiguration);
            var displayName = primaryImage is null
                ? primaryImageAttribute
                : LocalizedDescription(primaryImage.ElementLocal("displaynames")) ?? primaryImageAttribute;
            var canStoreFullImage = NormalizeBoolean(
                Text(primaryImageConfiguration?.ElementLocal("canstorefullimage"))
                ?? Text(primaryImage?.ElementLocal("CanStoreFullImage")));
            var entityImageSourcePath = entityImageConfiguration is null && primaryImageConfiguration is null
                ? sourcePath
                : customizationsPath;

            yield return new FamilyArtifact(
                ComponentFamily.ImageConfiguration,
                $"{entityLogicalName}|entity-image",
                displayName,
                entityImageSourcePath,
                EvidenceKind.Source,
                CreateProperties(
                    (ArtifactPropertyKeys.EntityLogicalName, entityLogicalName),
                    (ArtifactPropertyKeys.ImageConfigurationScope, "entity"),
                    (ArtifactPropertyKeys.PrimaryImageAttribute, primaryImageAttribute),
                    (ArtifactPropertyKeys.ImageAttributeLogicalName, primaryImageAttribute),
                    (ArtifactPropertyKeys.CanStoreFullImage, canStoreFullImage),
                    (ArtifactPropertyKeys.IsPrimaryImage, "true")));
        }

        foreach (var attribute in imageAttributes)
        {
            var attributeLogicalName = NormalizeLogicalName(Text(attribute.ElementLocal("LogicalName")) ?? Text(attribute.ElementLocal("Name")));
            if (string.IsNullOrWhiteSpace(attributeLogicalName))
            {
                continue;
            }

            attributeImageConfigurations.TryGetValue(attributeLogicalName, out var attributeImageConfiguration);
            var canStoreFullImage = NormalizeBoolean(
                Text(attributeImageConfiguration?.ElementLocal("canstorefullimage"))
                ?? Text(attribute.ElementLocal("CanStoreFullImage")));
            var isPrimaryImage = NormalizeBoolean(Text(attribute.ElementLocal("IsPrimaryImage")));
            if (string.Equals(isPrimaryImage, "false", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(primaryImageAttribute)
                && string.Equals(attributeLogicalName, primaryImageAttribute, StringComparison.OrdinalIgnoreCase))
            {
                isPrimaryImage = "true";
            }

            var attributeImageSourcePath = attributeImageConfiguration is null ? sourcePath : customizationsPath;

            yield return new FamilyArtifact(
                ComponentFamily.ImageConfiguration,
                $"{entityLogicalName}|{attributeLogicalName}|attribute-image",
                LocalizedDescription(attribute.ElementLocal("displaynames")) ?? attributeLogicalName,
                attributeImageSourcePath,
                EvidenceKind.Source,
                CreateProperties(
                    (ArtifactPropertyKeys.EntityLogicalName, entityLogicalName),
                    (ArtifactPropertyKeys.ImageConfigurationScope, "attribute"),
                    (ArtifactPropertyKeys.PrimaryImageAttribute, primaryImageAttribute),
                    (ArtifactPropertyKeys.ImageAttributeLogicalName, attributeLogicalName),
                    (ArtifactPropertyKeys.CanStoreFullImage, canStoreFullImage),
                    (ArtifactPropertyKeys.IsPrimaryImage, isPrimaryImage)));
        }
    }

    private static XElement? FindEntityImageConfiguration(XElement? customizationsRoot, string entityLogicalName) =>
        customizationsRoot?.ElementLocal("EntityImageConfigs")?
            .Elements()
            .FirstOrDefault(element =>
                element.Name.LocalName.Equals("EntityImageConfig", StringComparison.OrdinalIgnoreCase)
                && string.Equals(
                    NormalizeLogicalName(Text(element.ElementLocal("parententitylogicalname"))),
                    entityLogicalName,
                    StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyDictionary<string, XElement> FindAttributeImageConfigurations(XElement? customizationsRoot, string entityLogicalName) =>
        customizationsRoot?.ElementLocal("AttributeImageConfigs")?
            .Elements()
            .Where(element =>
                element.Name.LocalName.Equals("AttributeImageConfig", StringComparison.OrdinalIgnoreCase)
                && string.Equals(
                    NormalizeLogicalName(Text(element.ElementLocal("parententitylogicalname"))),
                    entityLogicalName,
                    StringComparison.OrdinalIgnoreCase))
            .Select(element => (LogicalName: NormalizeLogicalName(Text(element.ElementLocal("attributelogicalname"))), Element: element))
            .Where(entry => !string.IsNullOrWhiteSpace(entry.LogicalName))
            .ToDictionary(entry => entry.LogicalName!, entry => entry.Element, StringComparer.OrdinalIgnoreCase)
        ?? new Dictionary<string, XElement>(StringComparer.OrdinalIgnoreCase);

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
