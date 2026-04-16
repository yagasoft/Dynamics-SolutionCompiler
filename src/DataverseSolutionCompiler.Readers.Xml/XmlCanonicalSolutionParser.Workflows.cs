using System.Text.Json.Nodes;
using DataverseSolutionCompiler.Domain.Diagnostics;
using DataverseSolutionCompiler.Domain.Model;

namespace DataverseSolutionCompiler.Readers.Xml;

internal sealed partial class XmlCanonicalSolutionParser
{
    private void ParseWorkflows()
    {
        var rootDirectory = Path.Combine(_root, "Workflows");
        if (!Directory.Exists(rootDirectory))
        {
            return;
        }

        foreach (var metadataPath in Directory.GetFiles(rootDirectory, "*.json", SearchOption.TopDirectoryOnly).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            JsonObject workflow;
            try
            {
                workflow = JsonNode.Parse(File.ReadAllText(metadataPath)) as JsonObject
                    ?? throw new InvalidOperationException("Workflow metadata must be a JSON object.");
            }
            catch (Exception exception)
            {
                _diagnostics.Add(new CompilerDiagnostic(
                    "xml-reader-workflow-json-invalid",
                    DiagnosticSeverity.Warning,
                    $"Workflow source metadata '{RelativePath(metadataPath)}' could not be parsed: {exception.Message}",
                    metadataPath));
                continue;
            }

            var workflowId = NormalizeGuid(ReadWorkflowString(workflow, "id", "workflowid"));
            var displayName = ReadWorkflowString(workflow, "name", "display_name") ?? Path.GetFileNameWithoutExtension(metadataPath);
            var uniqueName = NormalizeLogicalName(ReadWorkflowString(workflow, "unique_name", "uniquename"));
            var logicalName = uniqueName
                ?? NormalizeLogicalName(displayName)
                ?? workflowId;
            if (string.IsNullOrWhiteSpace(logicalName))
            {
                continue;
            }

            var workflowKind = NormalizeWorkflowKind(workflow);
            var category = ReadWorkflowString(workflow, "category");
            var mode = ReadWorkflowString(workflow, "mode");
            var workflowScope = ReadWorkflowString(workflow, "scope");
            var onDemand = NormalizeBoolean(ReadWorkflowString(workflow, "on_demand", "ondemand"));
            var primaryEntity = NormalizeLogicalName(ReadWorkflowString(workflow, "primary_entity", "primaryentity", "primaryentitylogicalname"));
            var triggerMessageName = ReadWorkflowString(workflow, "trigger_message", "message_name", "action_message_name");
            var clientData = NormalizeJson(ReadWorkflowString(workflow, "client_data", "clientdata", "client_data_json"));
            var actionMetadataJson = NormalizeActionMetadataJson(workflow);
            var xamlPackageRelativePath = NormalizeWorkflowPackageRelativePath(ReadWorkflowString(workflow, "xaml_file_name", "xaml_path"));
            var xamlSourcePath = ResolveWorkflowAssetPath(xamlPackageRelativePath);
            var xamlHash = ResolveWorkflowXamlHash(workflow, xamlSourcePath);
            var clientDataHash = string.IsNullOrWhiteSpace(clientData) ? null : ComputeSignature(clientData);
            var assetSourceMapJson = BuildWorkflowAssetSourceMapJson(xamlPackageRelativePath);

            if (string.IsNullOrWhiteSpace(xamlHash))
            {
                _diagnostics.Add(new CompilerDiagnostic(
                    "xml-reader-workflow-xaml-missing",
                    DiagnosticSeverity.Warning,
                    $"Workflow '{logicalName}' did not provide a resolvable XAML payload. The workflow lane preserves shell metadata but XAML fidelity is incomplete for this artifact.",
                    metadataPath));
            }

            var summaryJson = SerializeJson(new
            {
                logicalName,
                workflowId,
                workflowKind,
                category,
                mode,
                scope = workflowScope,
                onDemand,
                primaryEntity,
                triggerMessageName,
                xamlHash,
                clientDataHash,
                actionMetadata = actionMetadataJson is null ? null : JsonNode.Parse(actionMetadataJson)
            });

            AddArtifact(
                ComponentFamily.Workflow,
                logicalName!,
                displayName,
                metadataPath,
                CreateProperties(
                    (ArtifactPropertyKeys.MetadataSourcePath, RelativePath(metadataPath)),
                    (ArtifactPropertyKeys.PackageRelativePath, RelativePath(metadataPath)),
                    (ArtifactPropertyKeys.AssetSourceMapJson, assetSourceMapJson),
                    (ArtifactPropertyKeys.Description, ReadWorkflowString(workflow, "description")),
                    (ArtifactPropertyKeys.WorkflowId, workflowId),
                    (ArtifactPropertyKeys.WorkflowKind, workflowKind),
                    (ArtifactPropertyKeys.Category, category),
                    (ArtifactPropertyKeys.Mode, mode),
                    (ArtifactPropertyKeys.WorkflowScope, workflowScope),
                    (ArtifactPropertyKeys.OnDemand, onDemand),
                    (ArtifactPropertyKeys.PrimaryEntity, primaryEntity),
                    (ArtifactPropertyKeys.TriggerMessageName, triggerMessageName),
                    (ArtifactPropertyKeys.XamlHash, xamlHash),
                    (ArtifactPropertyKeys.ClientDataHash, clientDataHash),
                    (ArtifactPropertyKeys.WorkflowActionMetadataJson, actionMetadataJson),
                    (ArtifactPropertyKeys.SummaryJson, summaryJson),
                    (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(summaryJson))));
        }
    }

    private static string? ReadWorkflowString(JsonObject workflow, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (workflow.TryGetPropertyValue(propertyName, out var value) && value is not null)
            {
                return value switch
                {
                    JsonValue jsonValue => jsonValue.ToString().Trim(),
                    JsonObject or JsonArray => value.ToJsonString(),
                    _ => value.ToString()
                };
            }
        }

        return null;
    }

    private static string NormalizeWorkflowKind(JsonObject workflow)
    {
        var declaredKind = ReadWorkflowString(workflow, "workflow_kind", "workflowkind", "kind");
        if (!string.IsNullOrWhiteSpace(declaredKind))
        {
            return declaredKind.Trim() switch
            {
                var value when value.Equals("customAction", StringComparison.OrdinalIgnoreCase) => "customAction",
                var value when value.Equals("action", StringComparison.OrdinalIgnoreCase) => "customAction",
                _ => "workflow"
            };
        }

        return workflow.TryGetPropertyValue("action_metadata", out var actionMetadata) && actionMetadata is not null
            ? "customAction"
            : "workflow";
    }

    private static string? NormalizeActionMetadataJson(JsonObject workflow)
    {
        if (!workflow.TryGetPropertyValue("action_metadata", out var actionMetadata) || actionMetadata is null)
        {
            return null;
        }

        return actionMetadata switch
        {
            JsonObject or JsonArray => NormalizeJson(actionMetadata.ToJsonString()),
            JsonValue jsonValue => NormalizeJson(jsonValue.ToString()),
            _ => null
        };
    }

    private static string? NormalizeWorkflowPackageRelativePath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Replace('\\', '/').Trim();
        normalized = normalized.TrimStart('/');
        return normalized.StartsWith("Workflows/", StringComparison.OrdinalIgnoreCase)
            ? normalized
            : $"Workflows/{normalized}";
    }

    private string ResolveWorkflowAssetPath(string? packageRelativePath)
    {
        if (string.IsNullOrWhiteSpace(packageRelativePath))
        {
            return string.Empty;
        }

        return Path.Combine(_root, packageRelativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static string? ResolveWorkflowXamlHash(JsonObject workflow, string xamlSourcePath)
    {
        if (!string.IsNullOrWhiteSpace(xamlSourcePath) && File.Exists(xamlSourcePath))
        {
            return ComputeFileHash(xamlSourcePath);
        }

        var xamlText = ReadWorkflowString(workflow, "xaml_text", "xaml");
        return string.IsNullOrWhiteSpace(xamlText) ? null : ComputeSignature(xamlText.Replace("\r\n", "\n", StringComparison.Ordinal));
    }

    private static string? BuildWorkflowAssetSourceMapJson(string? xamlPackageRelativePath)
    {
        if (string.IsNullOrWhiteSpace(xamlPackageRelativePath))
        {
            return null;
        }

        return SerializeJson(new[]
        {
            new
            {
                sourcePath = xamlPackageRelativePath,
                packageRelativePath = xamlPackageRelativePath
            }
        });
    }
}
