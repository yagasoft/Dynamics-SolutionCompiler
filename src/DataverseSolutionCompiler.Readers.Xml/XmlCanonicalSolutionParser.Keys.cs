using System.Text.Json;
using System.Xml.Linq;
using DataverseSolutionCompiler.Domain.Model;

namespace DataverseSolutionCompiler.Readers.Xml;

internal sealed partial class XmlCanonicalSolutionParser
{
    private IEnumerable<FamilyArtifact> ParseEntityKeys(string entityLogicalName, string sourcePath, XElement entity)
    {
        var container = entity.ElementLocal("keys")
            ?? entity.ElementLocal("entitykeys")
            ?? entity.ElementLocal("EntityKeys")
            ?? entity.Parent?.ElementLocal("keys")
            ?? entity.Parent?.ElementLocal("entitykeys")
            ?? entity.Parent?.ElementLocal("EntityKeys");
        if (container is null)
        {
            yield break;
        }

        var keyElements = container.Elements()
            .Where(element =>
                element.Name.LocalName.Equals("key", StringComparison.OrdinalIgnoreCase)
                || element.Name.LocalName.Equals("entitykey", StringComparison.OrdinalIgnoreCase)
                || element.Name.LocalName.Equals("EntityKey", StringComparison.OrdinalIgnoreCase))
            .OrderBy(element => element.AttributeValue("Name") ?? Text(element.ElementLocal("LogicalName")) ?? Text(element.ElementLocal("SchemaName")), StringComparer.OrdinalIgnoreCase)
            .ToArray();
        foreach (var keyElement in keyElements)
        {
            var schemaName = Text(keyElement.ElementLocal("SchemaName"))
                ?? keyElement.AttributeValue("Name")
                ?? Text(keyElement.ElementLocal("Name"));
            var keyName = NormalizeLogicalName(Text(keyElement.ElementLocal("LogicalName")))
                ?? NormalizeLogicalName(schemaName);
            if (string.IsNullOrWhiteSpace(keyName))
            {
                continue;
            }

            var keyAttributes = ReadKeyAttributes(keyElement);
            if (keyAttributes.Count == 0)
            {
                continue;
            }

            var keyLogicalName = $"{entityLogicalName}|{keyName}";
            yield return new FamilyArtifact(
                ComponentFamily.Key,
                keyLogicalName,
                LocalizedDescription(keyElement.ElementLocal("displaynames"))
                ?? LocalizedDescription(keyElement.ElementLocal("DisplayNames"))
                ?? schemaName
                ?? keyName,
                sourcePath,
                EvidenceKind.Source,
                CreateProperties(
                    (ArtifactPropertyKeys.EntityLogicalName, entityLogicalName),
                    (ArtifactPropertyKeys.SchemaName, schemaName),
                    (ArtifactPropertyKeys.Description, LocalizedDescription(keyElement.ElementLocal("Descriptions"))),
                    (ArtifactPropertyKeys.KeyAttributesJson, SerializeJson(keyAttributes)),
                    (ArtifactPropertyKeys.IndexStatus, Text(keyElement.ElementLocal("EntityKeyIndexStatus")) ?? Text(keyElement.ElementLocal("IndexStatus")))));
        }
    }

    private static IReadOnlyList<string> ReadKeyAttributes(XElement keyElement)
    {
        var attributes = new List<string>();
        var keyAttributes = keyElement.ElementLocal("KeyAttributes")
            ?? keyElement.ElementLocal("keyattributes")
            ?? keyElement.ElementLocal("Attributes")
            ?? keyElement.ElementLocal("attributes");
        if (keyAttributes is not null)
        {
            foreach (var value in keyAttributes.Elements()
                         .Select(element => element.Name.LocalName.Equals("Attribute", StringComparison.OrdinalIgnoreCase)
                             || element.Name.LocalName.Equals("KeyAttribute", StringComparison.OrdinalIgnoreCase)
                             || element.Name.LocalName.Equals("string", StringComparison.OrdinalIgnoreCase)
                                 ? Text(element)
                                 : null)
                         .Where(value => !string.IsNullOrWhiteSpace(value)))
            {
                attributes.Add(NormalizeLogicalName(value)!);
            }
        }

        if (attributes.Count == 0)
        {
            var jsonAttributes = Text(keyElement.ElementLocal("KeyAttributesJson"));
            if (!string.IsNullOrWhiteSpace(jsonAttributes))
            {
                var parsed = JsonSerializer.Deserialize<List<string>>(jsonAttributes);
                if (parsed is not null)
                {
                    attributes.AddRange(parsed
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Select(value => NormalizeLogicalName(value)!));
                }
            }
        }

        return attributes
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
