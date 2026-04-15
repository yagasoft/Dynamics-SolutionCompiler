using DataverseSolutionCompiler.Domain.Model;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace DataverseSolutionCompiler.Readers.Xml;

internal sealed partial class XmlCanonicalSolutionParser
{
    private void ParseServiceEndpointsAndConnectors()
    {
        ParseServiceEndpoints();
        ParseConnectors();
    }

    private void ParseServiceEndpoints()
    {
        var rootDirectory = Path.Combine(_root, "ServiceEndpoints");
        if (Directory.Exists(rootDirectory))
        {
            foreach (var directory in Directory.GetDirectories(rootDirectory).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                var metadataPath = Path.Combine(directory, "ServiceEndpoint.xml");
                if (!File.Exists(metadataPath))
                {
                    continue;
                }

                AddServiceEndpointArtifact(
                    LoadRoot(metadataPath),
                    metadataPath,
                    Path.GetFileName(directory));
            }
        }

        var aggregateMetadataPath = Path.Combine(_root, "PluginAssemblies", "ServiceEndpoints.xml");
        if (!File.Exists(aggregateMetadataPath))
        {
            return;
        }

        var aggregateRoot = LoadRoot(aggregateMetadataPath);
        foreach (var endpointElement in aggregateRoot.Elements()
                     .Where(element => element.Name.LocalName.Equals("ServiceEndpoint", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(element => element.AttributeValue("Name") ?? Text(element.ElementLocal("Name")), StringComparer.OrdinalIgnoreCase))
        {
            AddServiceEndpointArtifact(
                endpointElement,
                aggregateMetadataPath,
                endpointElement.AttributeValue("Name"));
        }
    }

    private void ParseConnectors()
    {
        var rootDirectory = Path.Combine(_root, "Connectors");
        if (!Directory.Exists(rootDirectory))
        {
            return;
        }

        foreach (var directory in Directory.GetDirectories(rootDirectory).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var metadataPath = Path.Combine(directory, "Connector.xml");
            if (!File.Exists(metadataPath))
            {
                continue;
            }

            AddConnectorArtifact(
                LoadRoot(metadataPath),
                metadataPath,
                Path.GetFileName(directory));
        }

        foreach (var metadataPath in Directory.GetFiles(rootDirectory, "*.xml", SearchOption.TopDirectoryOnly)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            AddConnectorArtifact(
                LoadRoot(metadataPath),
                metadataPath,
                Path.GetFileNameWithoutExtension(metadataPath));
        }
    }

    private void AddServiceEndpointArtifact(XElement root, string metadataPath, string? fallbackName)
    {
        var name = Text(root.ElementLocal("Name")) ?? root.AttributeValue("Name") ?? fallbackName;
        var logicalName = NormalizeLogicalName(Text(root.ElementLocal("LogicalName")) ?? name);
        if (string.IsNullOrWhiteSpace(logicalName))
        {
            return;
        }

        var contract = Text(root.ElementLocal("Contract"));
        var connectionMode = Text(root.ElementLocal("ConnectionMode"));
        var authType = Text(root.ElementLocal("AuthType"));
        var namespaceAddress = Text(root.ElementLocal("NamespaceAddress"));
        var endpointPath = Text(root.ElementLocal("Path")) ?? Text(root.ElementLocal("EndpointPath"));
        var url = Text(root.ElementLocal("Url"));
        var messageFormat = Text(root.ElementLocal("MessageFormat"));
        var messageCharset = Text(root.ElementLocal("MessageCharset"));
        var introducedVersion = Text(root.ElementLocal("IntroducedVersion"));
        var isCustomizable = Text(root.ElementLocal("IsCustomizable")) is { } serviceEndpointIsCustomizable
            ? NormalizeBoolean(serviceEndpointIsCustomizable)
            : null;
        var description = Text(root.ElementLocal("Description"));
        var summaryJson = SerializeJson(new
        {
            name,
            contract,
            connectionMode,
            authType,
            namespaceAddress,
            endpointPath,
            url,
            messageFormat,
            messageCharset,
            introducedVersion,
            isCustomizable
        });

        AddArtifact(
            ComponentFamily.ServiceEndpoint,
            logicalName!,
            name,
            metadataPath,
            CreateProperties(
                (ArtifactPropertyKeys.MetadataSourcePath, RelativePath(metadataPath)),
                (ArtifactPropertyKeys.Name, name),
                (ArtifactPropertyKeys.Description, description),
                (ArtifactPropertyKeys.Contract, contract),
                (ArtifactPropertyKeys.ConnectionMode, connectionMode),
                (ArtifactPropertyKeys.AuthType, authType),
                (ArtifactPropertyKeys.NamespaceAddress, namespaceAddress),
                (ArtifactPropertyKeys.EndpointPath, endpointPath),
                (ArtifactPropertyKeys.Url, url),
                (ArtifactPropertyKeys.MessageFormat, messageFormat),
                (ArtifactPropertyKeys.MessageCharset, messageCharset),
                (ArtifactPropertyKeys.IntroducedVersion, introducedVersion),
                (ArtifactPropertyKeys.IsCustomizable, isCustomizable),
                (ArtifactPropertyKeys.SummaryJson, summaryJson),
                (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(summaryJson))));
    }

    private void AddConnectorArtifact(XElement root, string metadataPath, string? fallbackName)
    {
        var name = ReadConnectorValue(root, "Name");
        var connectorInternalId = NormalizeLogicalName(ReadConnectorValue(root, "ConnectorInternalId"));
        var logicalName = NormalizeLogicalName(ReadConnectorValue(root, "LogicalName") ?? connectorInternalId ?? name ?? fallbackName);
        if (string.IsNullOrWhiteSpace(logicalName))
        {
            return;
        }

        var displayName = ReadConnectorValue(root, "DisplayName") ?? name ?? logicalName;
        var description = ReadConnectorValue(root, "Description");
        var connectorType = ReadConnectorValue(root, "ConnectorType");
        var capabilitiesJson = NormalizeConnectorCapabilities(root.ElementLocal("Capabilities") ?? root.ElementLocal("capabilities"));
        var introducedVersion = ReadConnectorValue(root, "IntroducedVersion");
        var isCustomizable = ReadConnectorValue(root, "IsCustomizable") is { } connectorIsCustomizable
            ? NormalizeBoolean(connectorIsCustomizable)
            : null;
        var backgroundColor = ReadConnectorValue(root, "IconBrandColor");
        var openApiDefinitionSourcePath = NormalizeConnectorAssetRelativePath(ReadConnectorValue(root, "OpenApiDefinition"));
        var connectionParametersSourcePath = NormalizeConnectorAssetRelativePath(ReadConnectorValue(root, "ConnectionParameters"));
        var policyTemplateInstancesSourcePath = NormalizeConnectorAssetRelativePath(ReadConnectorValue(root, "PolicyTemplateInstances"));
        var iconSourcePath = NormalizeConnectorAssetRelativePath(ReadConnectorValue(root, "Icon"));
        var scriptSourcePath = NormalizeConnectorAssetRelativePath(ReadConnectorValue(root, "Script"));
        var summaryJson = SerializeJson(new
        {
            name,
            displayName,
            description,
            connectorInternalId,
            connectorType,
            backgroundColor,
            capabilities = string.IsNullOrWhiteSpace(capabilitiesJson) ? null : JsonNode.Parse(capabilitiesJson),
            introducedVersion,
            isCustomizable
        });

        AddArtifact(
            ComponentFamily.Connector,
            logicalName!,
            displayName,
            metadataPath,
            CreateProperties(
                (ArtifactPropertyKeys.MetadataSourcePath, RelativePath(metadataPath)),
                (ArtifactPropertyKeys.Name, name),
                (ArtifactPropertyKeys.Description, description),
                (ArtifactPropertyKeys.ConnectorInternalId, connectorInternalId),
                (ArtifactPropertyKeys.ConnectorType, connectorType),
                (ArtifactPropertyKeys.CapabilitiesJson, capabilitiesJson),
                (ArtifactPropertyKeys.IntroducedVersion, introducedVersion),
                (ArtifactPropertyKeys.IsCustomizable, isCustomizable),
                (ArtifactPropertyKeys.BackgroundColor, backgroundColor),
                (ArtifactPropertyKeys.OpenApiDefinitionSourcePath, openApiDefinitionSourcePath),
                (ArtifactPropertyKeys.ConnectionParametersSourcePath, connectionParametersSourcePath),
                (ArtifactPropertyKeys.PolicyTemplateInstancesSourcePath, policyTemplateInstancesSourcePath),
                (ArtifactPropertyKeys.IconSourcePath, iconSourcePath),
                (ArtifactPropertyKeys.ScriptSourcePath, scriptSourcePath),
                (ArtifactPropertyKeys.SummaryJson, summaryJson),
                (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(summaryJson))));
    }

    private static string? ReadConnectorValue(XElement root, string localName) =>
        Text(root.ElementLocal(localName))
        ?? Text(root.ElementLocal(localName.ToLowerInvariant()));

    private static string? NormalizeConnectorAssetRelativePath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim().TrimStart('/', '\\').Replace('\\', '/');
        if (normalized.StartsWith("connector/", StringComparison.OrdinalIgnoreCase))
        {
            normalized = $"Connectors/{normalized["connector/".Length..]}";
        }
        else if (normalized.StartsWith("connectors/", StringComparison.OrdinalIgnoreCase))
        {
            normalized = $"Connectors/{normalized["connectors/".Length..]}";
        }
        else if (!normalized.Contains('/', StringComparison.Ordinal))
        {
            normalized = $"Connectors/{normalized}";
        }

        var fileName = Path.GetFileName(normalized);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        return $"Connectors/{fileName}";
    }

    private static string? NormalizeConnectorCapabilities(XElement? container)
    {
        if (container is null)
        {
            return null;
        }

        if (!container.HasElements)
        {
            var raw = Text(container);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            var normalizedJson = NormalizeJson(raw);
            if (!string.IsNullOrWhiteSpace(normalizedJson)
                && JsonNode.Parse(normalizedJson) is JsonArray parsedArray)
            {
                return SerializeJson(parsedArray
                    .Select(entry => entry?.ToString())
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => NormalizeLogicalName(value)!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                    .ToArray());
            }

            return SerializeJson(raw
                .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(NormalizeLogicalName)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Cast<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray());
        }

        return SerializeJson(container.Elements()
            .Select(Text)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => NormalizeLogicalName(value)!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray());
    }
}
