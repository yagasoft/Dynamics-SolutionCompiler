using System.Globalization;
using System.Xml.Linq;
using DataverseSolutionCompiler.Domain.Model;

namespace DataverseSolutionCompiler.Readers.Xml;

internal sealed partial class XmlCanonicalSolutionParser
{
    private void ParsePluginRegistrationFamilies()
    {
        var customizationsPath = Path.Combine(_root, "Other", "Customizations.xml");
        if (!File.Exists(customizationsPath))
        {
            return;
        }

        var root = LoadRoot(customizationsPath);
        var pluginTypeNamesById = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var pluginStepLogicalNamesById = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var assemblyElement in root.ElementLocal("SolutionPluginAssemblies")
                     ?.Elements()
                     .Where(element => element.Name.LocalName.Equals("PluginAssembly", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(element => Text(element.ElementLocal("FileName")) ?? element.AttributeValue("FullName"), StringComparer.OrdinalIgnoreCase)
                     ?? Enumerable.Empty<XElement>())
        {
            var fullName = Text(assemblyElement.ElementLocal("FullName"))
                ?? assemblyElement.AttributeValue("FullName");
            if (string.IsNullOrWhiteSpace(fullName))
            {
                continue;
            }

            var normalizedFileName = NormalizeSourceRelativePath(Text(assemblyElement.ElementLocal("FileName")));
            var assemblySourcePath = string.IsNullOrWhiteSpace(normalizedFileName)
                ? null
                : Path.Combine(_root, normalizedFileName.Replace('/', Path.DirectorySeparatorChar));
            var displayName = fullName.Split(',', 2)[0].Trim();
            var summaryJson = SerializeJson(new
            {
                fullName,
                fileName = Path.GetFileName(normalizedFileName),
                isolationMode = Text(assemblyElement.ElementLocal("IsolationMode")),
                sourceType = Text(assemblyElement.ElementLocal("SourceType")),
                introducedVersion = Text(assemblyElement.ElementLocal("IntroducedVersion"))
            });

            AddArtifact(
                ComponentFamily.PluginAssembly,
                fullName,
                displayName,
                customizationsPath,
                CreateProperties(
                    (Key: ArtifactPropertyKeys.MetadataSourcePath, Value: RelativePath(customizationsPath)),
                    (Key: ArtifactPropertyKeys.AssetSourcePath, Value: normalizedFileName),
                    (Key: ArtifactPropertyKeys.AssemblyFullName, Value: fullName),
                    (Key: ArtifactPropertyKeys.AssemblyFileName, Value: Path.GetFileName(normalizedFileName)),
                    (Key: ArtifactPropertyKeys.IsolationMode, Value: Text(assemblyElement.ElementLocal("IsolationMode"))),
                    (Key: ArtifactPropertyKeys.SourceType, Value: Text(assemblyElement.ElementLocal("SourceType"))),
                    (Key: ArtifactPropertyKeys.IntroducedVersion, Value: Text(assemblyElement.ElementLocal("IntroducedVersion"))),
                    (Key: ArtifactPropertyKeys.ByteLength, Value: assemblySourcePath is not null && File.Exists(assemblySourcePath)
                        ? new FileInfo(assemblySourcePath).Length.ToString(CultureInfo.InvariantCulture)
                        : null),
                    (Key: ArtifactPropertyKeys.ContentHash, Value: assemblySourcePath is not null && File.Exists(assemblySourcePath)
                        ? ComputeFileHash(assemblySourcePath)
                        : null),
                    (Key: ArtifactPropertyKeys.SummaryJson, Value: summaryJson),
                    (Key: ArtifactPropertyKeys.ComparisonSignature, Value: ComputeSignature(summaryJson))));

            foreach (var pluginTypeElement in assemblyElement.ElementLocal("PluginTypes")
                         ?.Elements()
                         .Where(element => element.Name.LocalName.Equals("PluginType", StringComparison.OrdinalIgnoreCase))
                         .OrderBy(element => element.AttributeValue("Name"), StringComparer.OrdinalIgnoreCase)
                         ?? Enumerable.Empty<XElement>())
            {
                var typeName = Text(pluginTypeElement.ElementLocal("TypeName"))
                    ?? pluginTypeElement.AttributeValue("Name")
                    ?? Text(pluginTypeElement.ElementLocal("Name"));
                if (string.IsNullOrWhiteSpace(typeName))
                {
                    continue;
                }

                var pluginTypeId = NormalizeGuid(pluginTypeElement.AttributeValue("PluginTypeId"));
                if (!string.IsNullOrWhiteSpace(pluginTypeId))
                {
                    pluginTypeNamesById[pluginTypeId] = typeName;
                }

                var assemblyQualifiedName = Text(pluginTypeElement.ElementLocal("AssemblyQualifiedName"))
                    ?? pluginTypeElement.AttributeValue("AssemblyQualifiedName");
                var summaryJsonForType = SerializeJson(new
                {
                    typeName,
                    fullName,
                    assemblyQualifiedName
                });

                AddArtifact(
                    ComponentFamily.PluginType,
                    typeName,
                    typeName,
                    customizationsPath,
                    CreateProperties(
                        (Key: ArtifactPropertyKeys.MetadataSourcePath, Value: RelativePath(customizationsPath)),
                        (Key: ArtifactPropertyKeys.AssemblyFullName, Value: fullName),
                        (Key: ArtifactPropertyKeys.AssemblyQualifiedName, Value: assemblyQualifiedName),
                        (Key: ArtifactPropertyKeys.FriendlyName, Value: Text(pluginTypeElement.ElementLocal("FriendlyName"))),
                        (Key: ArtifactPropertyKeys.WorkflowActivityGroupName, Value: Text(pluginTypeElement.ElementLocal("WorkflowActivityGroupName"))),
                        (Key: ArtifactPropertyKeys.Description, Value: Text(pluginTypeElement.ElementLocal("Description"))),
                        (Key: ArtifactPropertyKeys.SummaryJson, Value: summaryJsonForType),
                        (Key: ArtifactPropertyKeys.ComparisonSignature, Value: ComputeSignature(summaryJsonForType))));
            }
        }

        foreach (var stepElement in root.ElementLocal("SdkMessageProcessingSteps")
                     ?.Elements()
                     .Where(element => element.Name.LocalName.Equals("SdkMessageProcessingStep", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(element => element.AttributeValue("Name") ?? Text(element.ElementLocal("Name")), StringComparer.OrdinalIgnoreCase)
                     ?? Enumerable.Empty<XElement>())
        {
            var stepName = Text(stepElement.ElementLocal("Name"))
                ?? stepElement.AttributeValue("Name");
            if (string.IsNullOrWhiteSpace(stepName))
            {
                continue;
            }

            var stepId = NormalizeGuid(stepElement.AttributeValue("SdkMessageProcessingStepId") ?? Text(stepElement.ElementLocal("SdkMessageProcessingStepId")));
            var stage = Text(stepElement.ElementLocal("Stage"));
            var mode = Text(stepElement.ElementLocal("Mode"));
            var rank = Text(stepElement.ElementLocal("Rank"));
            var supportedDeployment = Text(stepElement.ElementLocal("SupportedDeployment"));
            var messageName = Text(stepElement.ElementLocal("MessageName"));
            var primaryEntity = NormalizeLogicalName(Text(stepElement.ElementLocal("PrimaryEntity")));
            var handlerPluginTypeName = Text(stepElement.ElementLocal("EventHandlerPluginTypeName"));
            if (string.IsNullOrWhiteSpace(handlerPluginTypeName))
            {
                var pluginTypeId = NormalizeGuid(Text(stepElement.ElementLocal("EventHandlerPluginTypeId")));
                if (!string.IsNullOrWhiteSpace(pluginTypeId) && pluginTypeNamesById.TryGetValue(pluginTypeId, out var mappedTypeName))
                {
                    handlerPluginTypeName = mappedTypeName;
                }
            }

            var filteringAttributes = NormalizeAttributeList(Text(stepElement.ElementLocal("FilteringAttributes")));
            var logicalName = BuildPluginStepLogicalName(handlerPluginTypeName, messageName, primaryEntity, stage, mode, stepName);
            if (!string.IsNullOrWhiteSpace(stepId))
            {
                pluginStepLogicalNamesById[stepId] = logicalName;
            }

            var summaryJson = SerializeJson(new
            {
                stepName,
                stage,
                mode,
                rank,
                supportedDeployment,
                messageName,
                primaryEntity,
                handlerPluginTypeName,
                filteringAttributes
            });

            AddArtifact(
                ComponentFamily.PluginStep,
                logicalName,
                stepName,
                customizationsPath,
                CreateProperties(
                    (Key: ArtifactPropertyKeys.MetadataSourcePath, Value: RelativePath(customizationsPath)),
                    (Key: ArtifactPropertyKeys.Description, Value: Text(stepElement.ElementLocal("Description"))),
                    (Key: ArtifactPropertyKeys.Stage, Value: stage),
                    (Key: ArtifactPropertyKeys.Mode, Value: mode),
                    (Key: ArtifactPropertyKeys.Rank, Value: rank),
                    (Key: ArtifactPropertyKeys.SupportedDeployment, Value: supportedDeployment),
                    (Key: ArtifactPropertyKeys.MessageName, Value: messageName),
                    (Key: ArtifactPropertyKeys.PrimaryEntity, Value: primaryEntity),
                    (Key: ArtifactPropertyKeys.HandlerPluginTypeName, Value: handlerPluginTypeName),
                    (Key: ArtifactPropertyKeys.FilteringAttributes, Value: filteringAttributes),
                    (Key: ArtifactPropertyKeys.IntroducedVersion, Value: Text(stepElement.ElementLocal("IntroducedVersion"))),
                    (Key: ArtifactPropertyKeys.SummaryJson, Value: summaryJson),
                    (Key: ArtifactPropertyKeys.ComparisonSignature, Value: ComputeSignature(summaryJson))));
        }

        foreach (var imageElement in root.ElementLocal("SdkMessageProcessingStepImages")
                     ?.Elements()
                     .Where(element => element.Name.LocalName.Equals("SdkMessageProcessingStepImage", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(element => element.AttributeValue("Name") ?? Text(element.ElementLocal("Name")), StringComparer.OrdinalIgnoreCase)
                     ?? Enumerable.Empty<XElement>())
        {
            var imageName = Text(imageElement.ElementLocal("Name"))
                ?? imageElement.AttributeValue("Name");
            if (string.IsNullOrWhiteSpace(imageName))
            {
                continue;
            }

            var parentStepLogicalName = string.Empty;
            var parentStepId = NormalizeGuid(Text(imageElement.ElementLocal("SdkMessageProcessingStepId")));
            if (!string.IsNullOrWhiteSpace(parentStepId)
                && pluginStepLogicalNamesById.TryGetValue(parentStepId, out var mappedStepLogicalName))
            {
                parentStepLogicalName = mappedStepLogicalName;
            }

            var entityAlias = NormalizeLogicalName(Text(imageElement.ElementLocal("EntityAlias")));
            var imageType = Text(imageElement.ElementLocal("ImageType"));
            var selectedAttributes = NormalizeAttributeList(Text(imageElement.ElementLocal("Attributes")));
            var logicalName = BuildPluginStepImageLogicalName(parentStepLogicalName, imageName, entityAlias, imageType);
            var summaryJson = SerializeJson(new
            {
                imageName,
                parentStepLogicalName,
                entityAlias,
                imageType,
                messagePropertyName = Text(imageElement.ElementLocal("MessagePropertyName")),
                selectedAttributes
            });

            AddArtifact(
                ComponentFamily.PluginStepImage,
                logicalName,
                imageName,
                customizationsPath,
                CreateProperties(
                    (Key: ArtifactPropertyKeys.MetadataSourcePath, Value: RelativePath(customizationsPath)),
                    (Key: ArtifactPropertyKeys.Description, Value: Text(imageElement.ElementLocal("Description"))),
                    (Key: ArtifactPropertyKeys.ParentPluginStepLogicalName, Value: parentStepLogicalName),
                    (Key: ArtifactPropertyKeys.EntityAlias, Value: entityAlias),
                    (Key: ArtifactPropertyKeys.ImageType, Value: imageType),
                    (Key: ArtifactPropertyKeys.MessagePropertyName, Value: Text(imageElement.ElementLocal("MessagePropertyName"))),
                    (Key: ArtifactPropertyKeys.SelectedAttributes, Value: selectedAttributes),
                    (Key: ArtifactPropertyKeys.IntroducedVersion, Value: Text(imageElement.ElementLocal("IntroducedVersion"))),
                    (Key: ArtifactPropertyKeys.SummaryJson, Value: summaryJson),
                    (Key: ArtifactPropertyKeys.ComparisonSignature, Value: ComputeSignature(summaryJson))));
        }
    }

    private static string BuildPluginStepLogicalName(
        string? handlerPluginTypeName,
        string? messageName,
        string? primaryEntity,
        string? stage,
        string? mode,
        string? stepName) =>
        string.Join("|",
            new[]
            {
                handlerPluginTypeName?.Trim() ?? "handler",
                messageName?.Trim() ?? "message",
                primaryEntity ?? "*",
                stage?.Trim() ?? "stage",
                mode?.Trim() ?? "mode",
                stepName?.Trim() ?? "step"
            });

    private static string BuildPluginStepImageLogicalName(
        string? parentStepLogicalName,
        string? imageName,
        string? entityAlias,
        string? imageType) =>
        string.Join("|",
            new[]
            {
                parentStepLogicalName?.Trim() ?? "step",
                imageName?.Trim() ?? "image",
                entityAlias ?? "alias",
                imageType?.Trim() ?? "type"
            });

    private static string? NormalizeSourceRelativePath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim()
            .TrimStart('/', '\\')
            .Replace('\\', '/');
    }

    private static string? NormalizeAttributeList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var values = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeLogicalName)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return values.Length == 0 ? null : string.Join(",", values);
    }
}
