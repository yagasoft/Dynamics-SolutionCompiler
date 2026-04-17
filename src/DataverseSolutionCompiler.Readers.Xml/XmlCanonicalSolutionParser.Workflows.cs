using System.Text.Json.Nodes;
using System.Xml.Linq;
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

        var parsedBaseNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var metadataPath in Directory.GetFiles(rootDirectory, "*.xaml.data.xml", SearchOption.TopDirectoryOnly)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            ParseWorkflowXmlMetadata(metadataPath);
            parsedBaseNames.Add(GetWorkflowMetadataBaseName(metadataPath));
        }

        foreach (var metadataPath in Directory.GetFiles(rootDirectory, "*.json", SearchOption.TopDirectoryOnly)
                     .Where(path => !IsWorkflowJsonSidecar(path))
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            if (parsedBaseNames.Contains(GetWorkflowMetadataBaseName(metadataPath)))
            {
                continue;
            }

            ParseWorkflowJsonMetadata(metadataPath);
        }
    }

    private void ParseWorkflowJsonMetadata(string metadataPath)
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
            return;
        }

        var workflowId = NormalizeGuid(ReadWorkflowString(workflow, "id", "workflowid"));
        var displayName = ReadWorkflowString(workflow, "name", "display_name") ?? GetWorkflowMetadataBaseName(metadataPath);
        var uniqueName = NormalizeLogicalName(ReadWorkflowString(workflow, "unique_name", "uniquename"));
        var logicalName = uniqueName
            ?? NormalizeLogicalName(displayName)
            ?? workflowId;
        if (string.IsNullOrWhiteSpace(logicalName))
        {
            return;
        }

        var actionMetadataJson = NormalizeActionMetadataJson(workflow)
            ?? ReadWorkflowSidecarJson(metadataPath, ".action-metadata.json");
        var workflowKind = NormalizeWorkflowKind(
            ReadWorkflowString(workflow, "category"),
            actionMetadataJson,
            ReadWorkflowString(workflow, "workflow_kind", "workflowkind", "kind"));
        var category = ReadWorkflowString(workflow, "category");
        var mode = ReadWorkflowString(workflow, "mode");
        var workflowScope = ReadWorkflowString(workflow, "scope");
        var onDemand = NormalizeBoolean(ReadWorkflowString(workflow, "on_demand", "ondemand"));
        var primaryEntity = NormalizeLogicalName(ReadWorkflowString(workflow, "primary_entity", "primaryentity", "primaryentitylogicalname"));
        var triggerMessageName = workflowKind == "businessProcessFlow"
            ? null
            : ReadWorkflowString(workflow, "trigger_message", "message_name", "action_message_name", "messagename");
        var clientData = NormalizeJson(ReadWorkflowString(workflow, "client_data", "clientdata", "client_data_json"))
            ?? ReadWorkflowSidecarJson(metadataPath, ".clientdata.json");
        var packageRelativePath = NormalizeWorkflowPackageRelativePath(
                ReadWorkflowString(workflow, "package_relative_path", "packageRelativePath"))
            ?? RelativePath(metadataPath);
        var xamlPackageRelativePath = NormalizeWorkflowPackageRelativePath(ReadWorkflowString(workflow, "xaml_file_name", "xaml_path"))
            ?? $"Workflows/{GetWorkflowMetadataBaseName(metadataPath)}.xaml";
        var xamlSourcePath = ResolveWorkflowAssetPath(xamlPackageRelativePath);
        var xamlHash = ResolveWorkflowXamlHash(workflow, xamlSourcePath);
        var clientDataHash = string.IsNullOrWhiteSpace(clientData) ? null : ComputeSignature(clientData);
        var assetSourceMapJson = BuildWorkflowAssetSourceMapJson(xamlPackageRelativePath);
        var businessProcessType = ReadWorkflowString(workflow, "business_process_type", "businessprocesstype");
        var processOrder = ReadWorkflowString(workflow, "process_order", "processorder");
        var processStagesJson = NormalizeWorkflowProcessStagesJson(workflow, workflowKind);

        AddWorkflowArtifact(
            metadataPath,
            logicalName!,
            displayName,
            workflowId,
            workflowKind,
            category,
            mode,
            workflowScope,
            onDemand,
            primaryEntity,
            triggerMessageName,
            xamlHash,
            clientDataHash,
            actionMetadataJson,
            businessProcessType,
            processOrder,
            processStagesJson,
            ReadWorkflowString(workflow, "description"),
            packageRelativePath,
            assetSourceMapJson);
    }

    private void ParseWorkflowXmlMetadata(string metadataPath)
    {
        XElement root;
        try
        {
            root = LoadRoot(metadataPath);
        }
        catch (Exception exception)
        {
            _diagnostics.Add(new CompilerDiagnostic(
                "xml-reader-workflow-xml-invalid",
                DiagnosticSeverity.Warning,
                $"Workflow source metadata '{RelativePath(metadataPath)}' could not be parsed as XML: {exception.Message}",
                metadataPath));
            return;
        }

        var workflow = root.Name.LocalName.Equals("Workflow", StringComparison.OrdinalIgnoreCase)
            ? root
            : root.Elements().FirstOrDefault(element => element.Name.LocalName.Equals("Workflow", StringComparison.OrdinalIgnoreCase));
        if (workflow is null)
        {
            _diagnostics.Add(new CompilerDiagnostic(
                "xml-reader-workflow-xml-missing-root",
                DiagnosticSeverity.Warning,
                $"Workflow source metadata '{RelativePath(metadataPath)}' did not contain a Workflow root element.",
                metadataPath));
            return;
        }

        var workflowId = NormalizeGuid(workflow.AttributeValue("WorkflowId") ?? Text(workflow.ElementLocal("WorkflowId")));
        var displayName = workflow.AttributeValue("Name")
            ?? LocalizedDescription(workflow.ElementLocal("LocalizedNames"))
            ?? GetWorkflowMetadataBaseName(metadataPath);
        var uniqueName = NormalizeLogicalName(Text(workflow.ElementLocal("UniqueName")));
        var logicalName = uniqueName
            ?? NormalizeLogicalName(displayName)
            ?? workflowId;
        if (string.IsNullOrWhiteSpace(logicalName))
        {
            return;
        }

        var actionMetadataJson = ReadWorkflowSidecarJson(metadataPath, ".action-metadata.json");
        var workflowKind = NormalizeWorkflowKind(
            Text(workflow.ElementLocal("Category")),
            actionMetadataJson,
            Text(workflow.ElementLocal("workflowkind")));
        var category = Text(workflow.ElementLocal("Category"));
        var mode = Text(workflow.ElementLocal("Mode"));
        var workflowScope = Text(workflow.ElementLocal("Scope"));
        var onDemand = NormalizeBoolean(Text(workflow.ElementLocal("OnDemand")));
        var primaryEntity = NormalizeLogicalName(Text(workflow.ElementLocal("PrimaryEntity")));
        var triggerMessageName = ResolveWorkflowTriggerMessage(workflow, workflowKind);
        var xamlPackageRelativePath = NormalizeWorkflowPackageRelativePath(Text(workflow.ElementLocal("XamlFileName")))
            ?? $"Workflows/{GetWorkflowMetadataBaseName(metadataPath)}.xaml";
        var xamlSourcePath = ResolveWorkflowAssetPath(xamlPackageRelativePath);
        var xamlHash = File.Exists(xamlSourcePath) ? ComputeFileHash(xamlSourcePath) : null;
        var clientDataJson = ReadWorkflowSidecarJson(metadataPath, ".clientdata.json");
        var clientDataHash = string.IsNullOrWhiteSpace(clientDataJson) ? null : ComputeSignature(clientDataJson);
        var businessProcessType = Text(workflow.ElementLocal("BusinessProcessType"));
        var processOrder = Text(workflow.ElementLocal("processorder"));
        var processStagesJson = workflowKind == "businessProcessFlow"
            ? BuildWorkflowProcessStagesJson(workflow.ElementLocal("labels"))
            : null;

        AddWorkflowArtifact(
            metadataPath,
            logicalName!,
            displayName,
            workflowId,
            workflowKind,
            category,
            mode,
            workflowScope,
            onDemand,
            primaryEntity,
            triggerMessageName,
            xamlHash,
            clientDataHash,
            actionMetadataJson,
            businessProcessType,
            processOrder,
            processStagesJson,
            workflow.AttributeValue("Description") ?? LocalizedDescription(workflow.ElementLocal("Descriptions")),
            RelativePath(metadataPath),
            BuildWorkflowAssetSourceMapJson(xamlPackageRelativePath));
    }

    private void AddWorkflowArtifact(
        string metadataPath,
        string logicalName,
        string? displayName,
        string workflowId,
        string workflowKind,
        string? category,
        string? mode,
        string? workflowScope,
        string onDemand,
        string? primaryEntity,
        string? triggerMessageName,
        string? xamlHash,
        string? clientDataHash,
        string? actionMetadataJson,
        string? businessProcessType,
        string? processOrder,
        string? processStagesJson,
        string? description,
        string packageRelativePath,
        string? assetSourceMapJson)
    {
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
            actionMetadata = actionMetadataJson is null ? null : JsonNode.Parse(actionMetadataJson),
            businessProcessType,
            processOrder,
            processStages = processStagesJson is null ? null : JsonNode.Parse(processStagesJson)
        });

        AddArtifact(
            ComponentFamily.Workflow,
            logicalName,
            displayName,
            metadataPath,
            CreateProperties(
                (ArtifactPropertyKeys.MetadataSourcePath, RelativePath(metadataPath)),
                (ArtifactPropertyKeys.PackageRelativePath, packageRelativePath),
                (ArtifactPropertyKeys.AssetSourceMapJson, assetSourceMapJson),
                (ArtifactPropertyKeys.Description, description),
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
                (ArtifactPropertyKeys.BusinessProcessType, businessProcessType),
                (ArtifactPropertyKeys.ProcessOrder, processOrder),
                (ArtifactPropertyKeys.ProcessStagesJson, processStagesJson),
                (ArtifactPropertyKeys.SummaryJson, summaryJson),
                (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(summaryJson))));
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

    private static string NormalizeWorkflowKind(string? category, string? actionMetadataJson, string? declaredKind)
    {
        if (!string.IsNullOrWhiteSpace(category) && category.Trim() == "4")
        {
            return "businessProcessFlow";
        }

        if (!string.IsNullOrWhiteSpace(declaredKind))
        {
            return declaredKind.Trim() switch
            {
                var value when value.Equals("businessProcessFlow", StringComparison.OrdinalIgnoreCase) => "businessProcessFlow",
                var value when value.Equals("bpf", StringComparison.OrdinalIgnoreCase) => "businessProcessFlow",
                var value when value.Equals("customAction", StringComparison.OrdinalIgnoreCase) => "customAction",
                var value when value.Equals("action", StringComparison.OrdinalIgnoreCase) => "customAction",
                _ => "workflow"
            };
        }

        if (!string.IsNullOrWhiteSpace(category) && category.Trim() == "3")
        {
            return "customAction";
        }

        return !string.IsNullOrWhiteSpace(actionMetadataJson)
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

    private static string? NormalizeWorkflowProcessStagesJson(JsonObject workflow, string workflowKind)
    {
        if (!string.Equals(workflowKind, "businessProcessFlow", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (workflow.TryGetPropertyValue("process_stages", out var processStages) && processStages is not null)
        {
            return NormalizeWorkflowProcessStagesJson(processStages.ToJsonString());
        }

        if (workflow.TryGetPropertyValue("processStages", out processStages) && processStages is not null)
        {
            return NormalizeWorkflowProcessStagesJson(processStages.ToJsonString());
        }

        return null;
    }

    private static string? NormalizeWorkflowProcessStagesJson(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            var node = JsonNode.Parse(raw);
            if (node is not JsonArray array)
            {
                return null;
            }

            var normalizedStages = array
                .Select(item => item as JsonObject)
                .Where(item => item is not null)
                .Select(item => new
                {
                    id = NormalizeGuid(item!["id"]?.GetValue<string>() ?? item["processstageid"]?.GetValue<string>()),
                    name = item!["name"]?.GetValue<string>() ?? item["stagename"]?.GetValue<string>()
                })
                .Where(stage => !string.IsNullOrWhiteSpace(stage.id) || !string.IsNullOrWhiteSpace(stage.name))
                .OrderBy(stage => stage.name ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(stage => stage.id ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return normalizedStages.Length == 0 ? null : SerializeJson(normalizedStages);
        }
        catch
        {
            return null;
        }
    }

    private static string? BuildWorkflowProcessStagesJson(XElement? labelsElement)
    {
        if (labelsElement is null)
        {
            return null;
        }

        var stages = labelsElement.Elements()
            .Where(element => element.Name.LocalName.Equals("steplabels", StringComparison.OrdinalIgnoreCase))
            .Select(element => new
            {
                id = NormalizeGuid(element.AttributeValue("id")),
                name = element.Elements()
                    .FirstOrDefault(label => label.Name.LocalName.Equals("label", StringComparison.OrdinalIgnoreCase))
                    ?.AttributeValue("description")
            })
            .Where(stage => !string.IsNullOrWhiteSpace(stage.id) || !string.IsNullOrWhiteSpace(stage.name))
            .OrderBy(stage => stage.name ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(stage => stage.id ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return stages.Length == 0 ? null : SerializeJson(stages);
    }

    private static string? ReadWorkflowSidecarJson(string metadataPath, string suffix)
    {
        var sidecarPath = Path.Combine(Path.GetDirectoryName(metadataPath)!, $"{GetWorkflowMetadataBaseName(metadataPath)}{suffix}");
        if (!File.Exists(sidecarPath))
        {
            return null;
        }

        return NormalizeJson(File.ReadAllText(sidecarPath));
    }

    private static string? ResolveWorkflowTriggerMessage(XElement workflow, string workflowKind)
    {
        if (string.Equals(workflowKind, "businessProcessFlow", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var explicitTrigger = Text(workflow.ElementLocal("TriggerMessageName"));
        if (!string.IsNullOrWhiteSpace(explicitTrigger))
        {
            return explicitTrigger;
        }

        if (string.Equals(workflowKind, "customAction", StringComparison.OrdinalIgnoreCase))
        {
            return Text(workflow.ElementLocal("UniqueName"));
        }

        if (NormalizeBoolean(Text(workflow.ElementLocal("TriggerOnCreate"))) == "true")
        {
            return "Create";
        }

        if (NormalizeBoolean(Text(workflow.ElementLocal("TriggerOnDelete"))) == "true")
        {
            return "Delete";
        }

        return Text(workflow.ElementLocal("ProcessTriggers")
            ?.Elements()
            .FirstOrDefault(element => element.Name.LocalName.Equals("ProcessTrigger", StringComparison.OrdinalIgnoreCase))
            ?.ElementLocal("event"));
    }

    private static bool IsWorkflowJsonSidecar(string path)
    {
        var fileName = Path.GetFileName(path);
        return fileName.EndsWith(".action-metadata.json", StringComparison.OrdinalIgnoreCase)
               || fileName.EndsWith(".clientdata.json", StringComparison.OrdinalIgnoreCase)
               || fileName.EndsWith(".process-stages.json", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetWorkflowMetadataBaseName(string path)
    {
        var fileName = Path.GetFileName(path);
        if (fileName.EndsWith(".xaml.data.xml", StringComparison.OrdinalIgnoreCase))
        {
            return fileName[..^".xaml.data.xml".Length];
        }

        if (fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            return fileName[..^".json".Length];
        }

        return Path.GetFileNameWithoutExtension(fileName);
    }
}
