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
        if (!Directory.Exists(rootDirectory))
        {
            return;
        }

        foreach (var directory in Directory.GetDirectories(rootDirectory).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var metadataPath = Path.Combine(directory, "ServiceEndpoint.xml");
            if (!File.Exists(metadataPath))
            {
                continue;
            }

            var root = LoadRoot(metadataPath);
            var name = Text(root.ElementLocal("Name")) ?? Path.GetFileName(directory);
            var logicalName = NormalizeLogicalName(Text(root.ElementLocal("LogicalName")) ?? name);
            if (string.IsNullOrWhiteSpace(logicalName))
            {
                continue;
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

            var root = LoadRoot(metadataPath);
            var name = Text(root.ElementLocal("Name"));
            var connectorInternalId = NormalizeLogicalName(Text(root.ElementLocal("ConnectorInternalId")));
            var logicalName = NormalizeLogicalName(Text(root.ElementLocal("LogicalName")) ?? connectorInternalId ?? name ?? Path.GetFileName(directory));
            if (string.IsNullOrWhiteSpace(logicalName))
            {
                continue;
            }

            var displayName = Text(root.ElementLocal("DisplayName")) ?? name ?? logicalName;
            var description = Text(root.ElementLocal("Description"));
            var connectorType = Text(root.ElementLocal("ConnectorType"));
            var capabilitiesJson = NormalizeConnectorCapabilities(root.ElementLocal("Capabilities"));
            var introducedVersion = Text(root.ElementLocal("IntroducedVersion"));
            var isCustomizable = Text(root.ElementLocal("IsCustomizable")) is { } connectorIsCustomizable
                ? NormalizeBoolean(connectorIsCustomizable)
                : null;
            var summaryJson = SerializeJson(new
            {
                name,
                displayName,
                description,
                connectorInternalId,
                connectorType,
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
                    (ArtifactPropertyKeys.SummaryJson, summaryJson),
                    (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(summaryJson))));
        }
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
