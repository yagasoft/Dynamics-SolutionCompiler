using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using DataverseSolutionCompiler.Domain.Abstractions;
using DataverseSolutionCompiler.Domain.Diagnostics;
using DataverseSolutionCompiler.Domain.Emission;
using DataverseSolutionCompiler.Domain.Model;

namespace DataverseSolutionCompiler.Emitters.Package;

public sealed partial class PackageEmitter : ISolutionEmitter
{
    private static readonly IReadOnlySet<ComponentFamily> HybridApplyOnlyFamilies = new HashSet<ComponentFamily>
    {
        ComponentFamily.EntityAnalyticsConfiguration,
        ComponentFamily.AiProjectType,
        ComponentFamily.AiProject,
        ComponentFamily.AiConfiguration,
        ComponentFamily.PluginAssembly,
        ComponentFamily.PluginType,
        ComponentFamily.PluginStep,
        ComponentFamily.PluginStepImage,
        ComponentFamily.ServiceEndpoint,
        ComponentFamily.MobileOfflineProfile,
        ComponentFamily.MobileOfflineProfileItem,
        ComponentFamily.ConnectionRole,
        ComponentFamily.Connector
    };

    private static readonly string[] SupportedRootDirectories =
    [
        "AIConfigurations",
        "AIProjects",
        "AIProjectTypes",
        "AppModules",
        "AppModuleSiteMaps",
        "Attachments",
        "CanvasApps",
        "Connectors",
        "DisplayStrings",
        "duplicaterules",
        "Entities",
        "EntityMaps",
        "ImportMaps",
        "MobileOfflineProfiles",
        "entityanalyticsconfigs",
        "environmentvariabledefinitions",
        "OptionSets",
        "Other",
        "PluginAssemblies",
        "Reports",
        "Roles",
        "RoutingRules",
        "ServiceEndpoints",
        "Templates",
        "Workflows",
        "WebWizard",
        "WebWizards",
        "WebResources"
    ];

    private static readonly HashSet<string> TextFileExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".config",
        ".css",
        ".htm",
        ".html",
        ".js",
        ".json",
        ".resx",
        ".svg",
        ".txt",
        ".xml",
        ".yml",
        ".yaml"
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private static readonly UTF8Encoding Utf8NoBom = new(false);

    public EmittedArtifacts Emit(CanonicalSolution model, EmitRequest request)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(request);

        var diagnostics = new List<CompilerDiagnostic>();
        var packageRoot = GetContainedPath(request.OutputRoot, "package-inputs");
        if (Directory.Exists(packageRoot))
        {
            Directory.Delete(packageRoot, recursive: true);
        }

        Directory.CreateDirectory(packageRoot);

        var emittedFiles = new List<EmittedArtifact>();
        var sourceBackedArtifacts = model.Artifacts.Where(IsSourceBackedIntentArtifact).ToArray();
        if (sourceBackedArtifacts.Length > 0)
        {
            var applyOnlyArtifacts = sourceBackedArtifacts.Where(IsHybridApplyOnlyArtifact).ToArray();
            var packageableSourceBackedArtifacts = sourceBackedArtifacts.Where(artifact => !IsHybridApplyOnlyArtifact(artifact)).ToArray();
            var structuredArtifacts = model.Artifacts.Where(artifact => !IsSourceBackedIntentArtifact(artifact)).ToArray();
            var structuredModel = new CanonicalSolution(
                model.Identity,
                model.Publisher,
                structuredArtifacts,
                model.Dependencies,
                model.EnvironmentBindings,
                model.Diagnostics);

            WriteDerivedPackageInputTree(structuredModel, packageRoot, emittedFiles, diagnostics);
            WriteHybridSourceBackedFiles(packageRoot, packageableSourceBackedArtifacts, emittedFiles, diagnostics);
            AugmentSolutionManifestForHybridSourceBackedArtifacts(packageRoot, packageableSourceBackedArtifacts, diagnostics);
            AugmentCustomizationsManifestForHybridSourceBackedArtifacts(packageRoot, packageableSourceBackedArtifacts, diagnostics);
            if (applyOnlyArtifacts.Length > 0)
            {
                SanitizeSolutionManifestForHybridApplyOnlyArtifacts(packageRoot, applyOnlyArtifacts, diagnostics);
                diagnostics.Add(new CompilerDiagnostic(
                    "package-emitter-hybrid-apply-only",
                    DiagnosticSeverity.Info,
                    $"Hybrid rebuild staged {applyOnlyArtifacts.Length} apply-only source-backed artifact(s) outside the package payload. These families will be finalized by live apply after solution import: {string.Join(", ", applyOnlyArtifacts.Select(artifact => artifact.Family.ToString()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value, StringComparer.OrdinalIgnoreCase))}.",
                    packageRoot));
                if (applyOnlyArtifacts.Any(artifact =>
                        artifact.Family == ComponentFamily.PluginAssembly
                        && string.Equals(GetProperty(artifact, ArtifactPropertyKeys.DeploymentFlavor), "PluginPackage", StringComparison.OrdinalIgnoreCase)))
                {
                    diagnostics.Add(new CompilerDiagnostic(
                        "package-emitter-plugin-package-live-boundary",
                        DiagnosticSeverity.Info,
                        "Code-first plug-in packages remain a live finalize-apply boundary. The compiler preserves their registration evidence, but the NuGet payload itself is not rebuilt into the solution ZIP package inputs.",
                        packageRoot));
                }
            }

            var copiedDirectories = emittedFiles
                .Select(file => file.RelativePath["package-inputs/".Length..].Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            WritePackageManifest(
                model,
                packageRoot,
                emittedFiles,
                diagnostics,
                copiedDirectories,
                unsupportedDirectories: [],
                sourceLayout: "intent-spec-hybrid",
                deploymentSettingsWritten: false);

            diagnostics.Add(new CompilerDiagnostic(
                "package-emitter-materialized",
                DiagnosticSeverity.Info,
                "Package emitter synthesized the structured subset and overlaid staged source-backed artifacts for hybrid rebuild intent.",
                packageRoot));

            return new EmittedArtifacts(
                true,
                request.OutputRoot,
                emittedFiles.OrderBy(file => file.RelativePath, StringComparer.Ordinal).ToArray(),
                diagnostics);
        }

        if (model.Artifacts.Any(artifact => artifact.Evidence == EvidenceKind.Derived)
            && model.Artifacts.All(artifact => artifact.Evidence != EvidenceKind.Source))
        {
            WriteDerivedPackageInputTree(model, packageRoot, emittedFiles, diagnostics);
            WritePackageManifest(
                model,
                packageRoot,
                emittedFiles,
                diagnostics,
                copiedDirectories: ["AppModuleSiteMaps", "AppModules", "Entities", "environmentvariabledefinitions", "OptionSets", "Other"],
                unsupportedDirectories: [],
                sourceLayout: "intent-spec-derived",
                deploymentSettingsWritten: false);

            diagnostics.Add(new CompilerDiagnostic(
                "package-emitter-materialized",
                DiagnosticSeverity.Info,
                "Package emitter synthesized a deterministic unpacked solution tree from derived compiler intent for the supported greenfield families.",
                packageRoot));

            return new EmittedArtifacts(
                true,
                request.OutputRoot,
                emittedFiles.OrderBy(file => file.RelativePath, StringComparer.Ordinal).ToArray(),
                diagnostics);
        }

        string sourceRoot;
        try
        {
            sourceRoot = ResolveSourceRoot(model);
        }
        catch (InvalidOperationException exception)
        {
            return new EmittedArtifacts(
                false,
                request.OutputRoot,
                [],
                [
                    new CompilerDiagnostic(
                        "package-emitter-source-root-unresolved",
                        DiagnosticSeverity.Error,
                        exception.Message,
                        request.OutputRoot)
                ]);
        }

        foreach (var directoryName in SupportedRootDirectories.OrderBy(value => value, StringComparer.Ordinal))
        {
            var sourceDirectory = Path.Combine(sourceRoot, directoryName);
            if (!Directory.Exists(sourceDirectory))
            {
                continue;
            }

            CopyDirectory(sourceRoot, sourceDirectory, packageRoot, emittedFiles);
        }

        var unsupportedDirectories = Directory.Exists(sourceRoot)
            ? Directory.GetDirectories(sourceRoot)
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!)
                .Where(name => !SupportedRootDirectories.Contains(name, StringComparer.OrdinalIgnoreCase))
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : Array.Empty<string>();
        if (unsupportedDirectories.Length > 0)
        {
            diagnostics.Add(new CompilerDiagnostic(
                "package-emitter-unsupported-directories",
                DiagnosticSeverity.Warning,
                $"Package-input emission currently copies the proven release-path directories only. Source root also contains: {string.Join(", ", unsupportedDirectories)}.",
                sourceRoot));
        }

        var sourceApplyOnlyArtifacts = model.Artifacts.Where(IsHybridApplyOnlyArtifact).ToArray();
        if (sourceApplyOnlyArtifacts.Length > 0)
        {
            SanitizeSolutionManifestForHybridApplyOnlyArtifacts(packageRoot, sourceApplyOnlyArtifacts, diagnostics);
        }

        var deploymentSettingsWritten = TryWriteDeploymentSettings(model, packageRoot, emittedFiles, diagnostics);
        WritePackageManifest(
            model,
            packageRoot,
            emittedFiles,
            diagnostics,
            SupportedRootDirectories.Where(name => Directory.Exists(Path.Combine(sourceRoot, name))).OrderBy(name => name, StringComparer.Ordinal).ToArray(),
            unsupportedDirectories,
            "unpacked-xml",
            deploymentSettingsWritten);

        diagnostics.Add(new CompilerDiagnostic(
            "package-emitter-materialized",
            DiagnosticSeverity.Info,
            "Package emitter wrote a deterministic source-first package-input tree for the proven release-path directories.",
            packageRoot));

        return new EmittedArtifacts(
            true,
            request.OutputRoot,
            emittedFiles.OrderBy(file => file.RelativePath, StringComparer.Ordinal).ToArray(),
            diagnostics);
    }

    private static void WritePackageManifest(
        CanonicalSolution model,
        string packageRoot,
        List<EmittedArtifact> emittedFiles,
        List<CompilerDiagnostic> diagnostics,
        IReadOnlyCollection<string> copiedDirectories,
        IReadOnlyCollection<string> unsupportedDirectories,
        string sourceLayout,
        bool deploymentSettingsWritten)
    {
        var inventory = emittedFiles.Select(file => file.RelativePath)
            .Append("package-inputs/manifest.json")
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        WriteJson(
            packageRoot,
            "manifest.json",
            new
            {
                solution = new
                {
                    model.Identity.UniqueName,
                    model.Identity.DisplayName,
                    model.Identity.Version,
                    layeringIntent = model.Identity.LayeringIntent.ToString()
                },
                publisher = new
                {
                    model.Publisher.UniqueName,
                    model.Publisher.Prefix,
                    model.Publisher.CustomizationPrefix,
                    model.Publisher.DisplayName
                },
                sourceLayout,
                copiedDirectories = copiedDirectories.OrderBy(name => name, StringComparer.Ordinal).ToArray(),
                unsupportedDirectories = unsupportedDirectories.OrderBy(name => name, StringComparer.Ordinal).ToArray(),
                deploymentSettingsIncluded = deploymentSettingsWritten,
                files = inventory
            },
            emittedFiles,
            "Root manifest for deterministic PAC package-input emission.",
            EmittedArtifactRole.PackageInput);
    }

    private static bool TryWriteDeploymentSettings(
        CanonicalSolution model,
        string packageRoot,
        List<EmittedArtifact> emittedFiles,
        List<CompilerDiagnostic> diagnostics)
    {
        var bindings = model.EnvironmentBindings
            .Where(binding => binding.IsEnvironmentLocal && !string.IsNullOrWhiteSpace(binding.DefaultValue))
            .OrderBy(binding => binding.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (bindings.Length == 0)
        {
            diagnostics.Add(new CompilerDiagnostic(
                "package-emitter-deployment-settings-omitted",
                DiagnosticSeverity.Info,
                "Deployment settings were omitted because the canonical model does not yet carry environment-local binding values with durable evidence."));
            return false;
        }

        var connectionReferences = bindings
            .Where(binding => binding.BindingType.Contains("connection", StringComparison.OrdinalIgnoreCase))
            .Select(binding => new
            {
                LogicalName = binding.Name,
                ConnectionId = binding.DefaultValue
            })
            .ToArray();
        var environmentVariables = bindings
            .Where(binding => !binding.BindingType.Contains("connection", StringComparison.OrdinalIgnoreCase))
            .Select(binding => new
            {
                SchemaName = binding.Name,
                Value = binding.DefaultValue
            })
            .ToArray();

        if (connectionReferences.Length == 0 && environmentVariables.Length == 0)
        {
            diagnostics.Add(new CompilerDiagnostic(
                "package-emitter-deployment-settings-omitted",
                DiagnosticSeverity.Info,
                "Deployment settings were omitted because no supported environment binding families were present."));
            return false;
        }

        WriteJson(
            packageRoot,
            "settings/deployment-settings.json",
            new
            {
                ConnectionReferences = connectionReferences,
                EnvironmentVariables = environmentVariables
            },
            emittedFiles,
            "Deployment settings synthesized from canonical environment binding evidence.",
            EmittedArtifactRole.DeploymentSetting);

        return true;
    }

    private static void CopyDirectory(string sourceRoot, string sourceDirectory, string packageRoot, List<EmittedArtifact> emittedFiles)
    {
        foreach (var sourceFile in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories)
                     .OrderBy(path => Path.GetRelativePath(sourceRoot, path), StringComparer.Ordinal))
        {
            var relativePath = GetContainedRelativePath(sourceRoot, sourceFile);
            var destinationPath = GetContainedPath(packageRoot, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

            if (ShouldNormalizeAsText(sourceFile))
            {
                var normalizedText = File.ReadAllText(sourceFile)
                    .Replace("\r\n", "\n", StringComparison.Ordinal)
                    .Replace("\r", "\n", StringComparison.Ordinal);
                File.WriteAllText(destinationPath, normalizedText, Utf8NoBom);
            }
            else
            {
                File.Copy(sourceFile, destinationPath, overwrite: true);
            }

            emittedFiles.Add(new EmittedArtifact(
                $"package-inputs/{relativePath}",
                EmittedArtifactRole.PackageInput,
                $"Package input copied from source evidence: {relativePath}."));
        }
    }

    private static bool ShouldNormalizeAsText(string sourceFile)
    {
        if (sourceFile.EndsWith(".data.xml", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return TextFileExtensions.Contains(Path.GetExtension(sourceFile));
    }

    private static bool IsSourceBackedIntentArtifact(FamilyArtifact artifact) =>
        artifact.Properties is not null
        && artifact.Properties.ContainsKey(ArtifactPropertyKeys.PackageRelativePath)
        && !string.IsNullOrWhiteSpace(artifact.SourcePath);

    private static bool IsHybridApplyOnlyArtifact(FamilyArtifact artifact) =>
        HybridApplyOnlyFamilies.Contains(artifact.Family);

    private static void WriteHybridSourceBackedFiles(
        string packageRoot,
        IEnumerable<FamilyArtifact> artifacts,
        List<EmittedArtifact> emittedFiles,
        ICollection<CompilerDiagnostic> diagnostics)
    {
        var copiedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var artifact in artifacts.OrderBy(artifact => artifact.Family).ThenBy(artifact => artifact.LogicalName, StringComparer.OrdinalIgnoreCase))
        {
            var metadataTarget = GetProperty(artifact, ArtifactPropertyKeys.PackageRelativePath);
            if (!string.IsNullOrWhiteSpace(metadataTarget) && File.Exists(artifact.SourcePath) && copiedPaths.Add(metadataTarget))
            {
                CopySourceBackedMetadataToPackage(packageRoot, artifact, metadataTarget, emittedFiles, diagnostics);
            }

            foreach (var asset in ReadSourceBackedAssetMap(artifact))
            {
                if (File.Exists(asset.SourcePath) && copiedPaths.Add(asset.PackageRelativePath))
                {
                    CopyFileToPackage(packageRoot, asset.SourcePath, asset.PackageRelativePath, emittedFiles, artifact.LogicalName);
                }
            }
        }
    }

    private static void CopySourceBackedMetadataToPackage(
        string packageRoot,
        FamilyArtifact artifact,
        string packageRelativePath,
        List<EmittedArtifact> emittedFiles,
        ICollection<CompilerDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(artifact.SourcePath))
        {
            return;
        }

        var sourcePath = artifact.SourcePath;
        var destinationPath = GetContainedPath(packageRoot, packageRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

        if (ShouldNormalizeAsText(sourcePath))
        {
            var normalizedText = ReadNormalizedText(sourcePath);
            var normalizedWorkflowXml = TryNormalizeSourceBackedWorkflowMetadata(artifact, packageRelativePath, normalizedText, diagnostics);
            var normalizedEntityXml = TryNormalizeSourceBackedTableEntityXml(artifact, packageRelativePath, normalizedText, diagnostics);
            File.WriteAllText(destinationPath, normalizedWorkflowXml ?? normalizedEntityXml ?? normalizedText, Utf8NoBom);
        }
        else
        {
            File.Copy(sourcePath, destinationPath, overwrite: true);
        }

        emittedFiles.Add(new EmittedArtifact(
            $"package-inputs/{packageRelativePath.Replace('\\', '/')}",
            EmittedArtifactRole.PackageInput,
            $"Package input copied from staged source-backed artifact evidence for {artifact.LogicalName}."));
    }

    private static string? TryNormalizeSourceBackedWorkflowMetadata(
        FamilyArtifact artifact,
        string packageRelativePath,
        string normalizedText,
        ICollection<CompilerDiagnostic> diagnostics)
    {
        if (artifact.Family != ComponentFamily.Workflow
            || !packageRelativePath.Replace('\\', '/').EndsWith(".xaml.data.xml", StringComparison.OrdinalIgnoreCase)
            || !artifact.SourcePath!.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var xml = BuildWorkflowMetadataXml(artifact);
        diagnostics.Add(new CompilerDiagnostic(
            "package-emitter-normalized-source-backed-workflow-metadata",
            DiagnosticSeverity.Info,
            $"Normalized staged source-backed workflow metadata for '{artifact.LogicalName}' into export-backed XAML data XML layout.",
            artifact.SourcePath!));
        return xml;
    }

    private static void CopyFileToPackage(string packageRoot, string sourcePath, string packageRelativePath, List<EmittedArtifact> emittedFiles, string logicalName)
    {
        var destinationPath = GetContainedPath(packageRoot, packageRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

        if (ShouldNormalizeAsText(sourcePath))
        {
            var normalizedText = ReadNormalizedText(sourcePath);
            File.WriteAllText(destinationPath, normalizedText, Utf8NoBom);
        }
        else
        {
            File.Copy(sourcePath, destinationPath, overwrite: true);
        }

        emittedFiles.Add(new EmittedArtifact(
            $"package-inputs/{packageRelativePath.Replace('\\', '/')}",
            EmittedArtifactRole.PackageInput,
            $"Package input copied from staged source-backed artifact evidence for {logicalName}."));
    }

    private static string ReadNormalizedText(string sourcePath) =>
        File.ReadAllText(sourcePath)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal);

    private static string BuildWorkflowMetadataXml(FamilyArtifact artifact)
    {
        var workflowId = NormalizeGuid(GetProperty(artifact, ArtifactPropertyKeys.WorkflowId)) ?? Guid.Empty.ToString("D");
        var workflowKind = GetProperty(artifact, ArtifactPropertyKeys.WorkflowKind);
        var displayName = artifact.DisplayName ?? artifact.LogicalName;
        var description = GetProperty(artifact, ArtifactPropertyKeys.Description);
        var uniqueName = artifact.LogicalName;
        var xamlFileName = ReadSourceBackedAssetMap(artifact)
            .Select(asset => asset.PackageRelativePath)
            .FirstOrDefault(path => path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase));
        xamlFileName = string.IsNullOrWhiteSpace(xamlFileName)
            ? $"{artifact.LogicalName}.xaml"
            : Path.GetFileName(xamlFileName.Replace('/', Path.DirectorySeparatorChar));
        var category = GetProperty(artifact, ArtifactPropertyKeys.Category)
            ?? workflowKind switch
            {
                "customAction" => "3",
                "businessProcessFlow" => "4",
                _ => "0"
            };

        var workflow = new XElement("Workflow",
            new XAttribute("Name", displayName),
            new XAttribute("WorkflowId", FormatRootComponentId(workflowId).Trim('{', '}')));

        if (!string.IsNullOrWhiteSpace(description))
        {
            workflow.SetAttributeValue("Description", description);
        }

        workflow.Add(new XElement("XamlFileName", xamlFileName));
        workflow.Add(new XElement("Category", category));

        AddWorkflowElementIfPresent(workflow, "Mode", GetProperty(artifact, ArtifactPropertyKeys.Mode));
        AddWorkflowElementIfPresent(workflow, "Scope", GetProperty(artifact, ArtifactPropertyKeys.WorkflowScope));
        AddWorkflowElementIfPresent(workflow, "OnDemand", NormalizeWorkflowBoolean01(GetProperty(artifact, ArtifactPropertyKeys.OnDemand)));
        AddWorkflowElementIfPresent(workflow, "UniqueName", uniqueName);
        AddWorkflowElementIfPresent(workflow, "BusinessProcessType", GetProperty(artifact, ArtifactPropertyKeys.BusinessProcessType));
        AddWorkflowElementIfPresent(workflow, "processorder", GetProperty(artifact, ArtifactPropertyKeys.ProcessOrder));
        AddWorkflowElementIfPresent(workflow, "PrimaryEntity", GetProperty(artifact, ArtifactPropertyKeys.PrimaryEntity));
        AddWorkflowElementIfPresent(workflow, "IntroducedVersion", GetProperty(artifact, ArtifactPropertyKeys.IntroducedVersion));

        var localizedNames = BuildWorkflowLocalizedNames(displayName);
        if (localizedNames is not null)
        {
            workflow.Add(localizedNames);
        }

        var descriptions = BuildWorkflowDescriptions(description);
        if (descriptions is not null)
        {
            workflow.Add(descriptions);
        }

        var processTriggers = BuildWorkflowProcessTriggers(artifact);
        if (processTriggers is not null)
        {
            workflow.Add(processTriggers);
        }

        var labels = BuildWorkflowLabels(GetProperty(artifact, ArtifactPropertyKeys.ProcessStagesJson));
        if (labels is not null)
        {
            workflow.Add(labels);
        }

        return workflow.ToString(SaveOptions.DisableFormatting).Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private static void AddWorkflowElementIfPresent(XElement workflow, string elementName, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            workflow.Add(new XElement(elementName, value));
        }
    }

    private static string? NormalizeWorkflowBoolean01(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
                ? "1"
                : string.Equals(value, "false", StringComparison.OrdinalIgnoreCase)
                    ? "0"
                    : value;

    private static XElement? BuildWorkflowLocalizedNames(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return null;
        }

        return new XElement("LocalizedNames",
            new XElement("LocalizedName",
                new XAttribute("description", displayName),
                new XAttribute("languagecode", "1033")));
    }

    private static XElement? BuildWorkflowDescriptions(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        return new XElement("Descriptions",
            new XElement("Description",
                new XAttribute("description", description),
                new XAttribute("languagecode", "1033")));
    }

    private static XElement? BuildWorkflowProcessTriggers(FamilyArtifact artifact)
    {
        var workflowKind = GetProperty(artifact, ArtifactPropertyKeys.WorkflowKind);
        if (string.Equals(workflowKind, "customAction", StringComparison.OrdinalIgnoreCase)
            || string.Equals(workflowKind, "businessProcessFlow", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var triggerMessageName = GetProperty(artifact, ArtifactPropertyKeys.TriggerMessageName);
        var primaryEntity = GetProperty(artifact, ArtifactPropertyKeys.PrimaryEntity);
        if (string.IsNullOrWhiteSpace(triggerMessageName) || string.IsNullOrWhiteSpace(primaryEntity))
        {
            return null;
        }

        return new XElement("ProcessTriggers",
            new XElement("ProcessTrigger",
                new XElement("event", triggerMessageName),
                new XElement("primaryentitytypecode", primaryEntity)));
    }

    private static XElement? BuildWorkflowLabels(string? processStagesJson)
    {
        if (string.IsNullOrWhiteSpace(processStagesJson))
        {
            return null;
        }

        try
        {
            if (JsonNode.Parse(processStagesJson) is not JsonArray stages)
            {
                return null;
            }

            var steplabels = stages
                .Select(stage => stage as JsonObject)
                .Where(stage => stage is not null)
                .Select(stage => new
                {
                    Id = NormalizeGuid(stage!["id"]?.GetValue<string>()),
                    Name = stage!["name"]?.GetValue<string>()
                })
                .Where(stage => !string.IsNullOrWhiteSpace(stage.Id) && !string.IsNullOrWhiteSpace(stage.Name))
                .Select(stage => new XElement("steplabels",
                    new XAttribute("id", stage.Id!),
                    new XElement("label",
                        new XAttribute("languagecode", "1033"),
                        new XAttribute("description", stage.Name!))))
                .ToArray();

            return steplabels.Length == 0 ? null : new XElement("labels", steplabels);
        }
        catch
        {
            return null;
        }
    }

    private static string? TryNormalizeSourceBackedTableEntityXml(
        FamilyArtifact artifact,
        string packageRelativePath,
        string normalizedText,
        ICollection<CompilerDiagnostic> diagnostics)
    {
        if (artifact.Family != ComponentFamily.Table
            || !packageRelativePath.Replace('\\', '/').EndsWith("/Entity.xml", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            var document = XDocument.Parse(normalizedText, LoadOptions.PreserveWhitespace);
            var outerName = document.Root?
                .Elements()
                .FirstOrDefault(element => element.Name.LocalName.Equals("Name", StringComparison.OrdinalIgnoreCase))?
                .Value?
                .Trim();
            var entityElement = document.Root?
                .Elements()
                .FirstOrDefault(element => element.Name.LocalName.Equals("EntityInfo", StringComparison.OrdinalIgnoreCase))?
                .Elements()
                .FirstOrDefault(element => element.Name.LocalName.Equals("entity", StringComparison.OrdinalIgnoreCase));
            var innerName = entityElement?.Attribute("Name")?.Value?.Trim();
            if (string.IsNullOrWhiteSpace(outerName)
                || entityElement is null
                || string.IsNullOrWhiteSpace(innerName)
                || string.Equals(outerName, innerName, StringComparison.Ordinal))
            {
                return null;
            }

            entityElement.SetAttributeValue("Name", outerName);
            diagnostics.Add(new CompilerDiagnostic(
                "package-emitter-normalized-source-backed-table-entity-name",
                DiagnosticSeverity.Warning,
                $"Normalized staged source-backed table Entity.xml for '{artifact.LogicalName}' so inner EntityInfo/entity Name matched outer Name '{outerName}'.",
                artifact.SourcePath!));

            var normalizedXml = document.Declaration is null
                ? document.ToString(SaveOptions.DisableFormatting)
                : $"{document.Declaration}{Environment.NewLine}{document.Root}";
            return normalizedXml.Replace("\r\n", "\n", StringComparison.Ordinal);
        }
        catch
        {
            return null;
        }
    }

    private static IReadOnlyList<SourceBackedAssetMapEntry> ReadSourceBackedAssetMap(FamilyArtifact artifact)
    {
        var json = GetProperty(artifact, ArtifactPropertyKeys.AssetSourceMapJson);
        if (!string.IsNullOrWhiteSpace(json))
        {
            var structuredEntries = JsonSerializer.Deserialize<List<SourceBackedAssetMapEntry>>(json, JsonOptions);
            if (structuredEntries is { Count: > 0 })
            {
                return structuredEntries
                    .Where(entry =>
                        !string.IsNullOrWhiteSpace(entry.SourcePath)
                        && !string.IsNullOrWhiteSpace(entry.PackageRelativePath))
                    .Select(entry => new SourceBackedAssetMapEntry(
                        ResolveAssetSourcePath(artifact, entry.SourcePath),
                        DeriveSourceBackedAssetPackageRelativePath(entry.PackageRelativePath)))
                    .Distinct()
                    .ToArray();
            }

            var relativePaths = JsonSerializer.Deserialize<List<string>>(json, JsonOptions);
            if (relativePaths is { Count: > 0 })
            {
                return relativePaths
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .Select(path => new SourceBackedAssetMapEntry(
                        ResolveAssetSourcePath(artifact, path!),
                        DeriveSourceBackedAssetPackageRelativePath(path!)))
                    .Distinct()
                    .ToArray();
            }
        }

        if (artifact.Properties is null)
        {
            return [];
        }

        return artifact.Properties
            .Where(pair =>
                pair.Key.EndsWith("SourcePath", StringComparison.Ordinal)
                && !string.Equals(pair.Key, ArtifactPropertyKeys.MetadataSourcePath, StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(pair.Value))
            .Select(pair => new SourceBackedAssetMapEntry(
                ResolveAssetSourcePath(artifact, pair.Value),
                DeriveSourceBackedAssetPackageRelativePath(pair.Value)))
            .Distinct()
            .ToArray();
    }

    private static string ResolveAssetSourcePath(FamilyArtifact artifact, string path)
    {
        if (Path.IsPathRooted(path))
        {
            return Path.GetFullPath(path);
        }

        if (!string.IsNullOrWhiteSpace(artifact.SourcePath)
            && artifact.SourcePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            var trackedSourceRoot = ResolveTrackedSourceRoot(artifact.SourcePath);
            var trackedCandidate = Path.Combine(trackedSourceRoot, "source-backed", path.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(trackedCandidate))
            {
                return trackedCandidate;
            }
        }

        return !string.IsNullOrWhiteSpace(artifact.SourcePath) && File.Exists(artifact.SourcePath)
            ? Path.GetFullPath(Path.Combine(Path.GetDirectoryName(artifact.SourcePath) ?? string.Empty, "..", path.Replace('/', Path.DirectorySeparatorChar)))
            : path;
    }

    private static string ResolveTrackedSourceRoot(string summaryPath)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(summaryPath)) ?? string.Empty;
        while (!string.IsNullOrWhiteSpace(directory))
        {
            if (File.Exists(Path.Combine(directory, "manifest.json")) && File.Exists(Path.Combine(directory, "solution", "manifest.json")))
            {
                return directory;
            }

            directory = Path.GetDirectoryName(directory) ?? string.Empty;
        }

        return Path.GetDirectoryName(Path.GetFullPath(summaryPath)) ?? string.Empty;
    }

    private static string DeriveSourceBackedAssetPackageRelativePath(string assetSourcePath)
    {
        var normalized = assetSourcePath.Replace('\\', '/').TrimStart('/');
        const string prefix = "source-backed/";
        return normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? normalized[prefix.Length..]
            : normalized;
    }

    private static void SanitizeSolutionManifestForHybridApplyOnlyArtifacts(
        string packageRoot,
        IEnumerable<FamilyArtifact> applyOnlyArtifacts,
        ICollection<CompilerDiagnostic> diagnostics)
    {
        var solutionPath = Path.Combine(packageRoot, "Other", "Solution.xml");
        if (!File.Exists(solutionPath))
        {
            return;
        }

        var rootComponentTypes = applyOnlyArtifacts
            .SelectMany(BuildApplyOnlyRootComponentTypes)
            .ToHashSet();
        var rootComponentKeys = applyOnlyArtifacts
            .SelectMany(BuildApplyOnlyRootComponentKeys)
            .ToHashSet();
        if (rootComponentTypes.Count == 0 && rootComponentKeys.Count == 0)
        {
            return;
        }

        var document = System.Xml.Linq.XDocument.Load(solutionPath, System.Xml.Linq.LoadOptions.PreserveWhitespace);
        var manifest = document.Root?.Elements().FirstOrDefault(element => element.Name.LocalName.Equals("SolutionManifest", StringComparison.OrdinalIgnoreCase));
        var rootComponents = manifest?.Elements().FirstOrDefault(element => element.Name.LocalName.Equals("RootComponents", StringComparison.OrdinalIgnoreCase));
        if (rootComponents is null)
        {
            return;
        }

        var removedCount = 0;
        foreach (var rootComponent in rootComponents.Elements().Where(element => element.Name.LocalName.Equals("RootComponent", StringComparison.OrdinalIgnoreCase)).ToArray())
        {
            var componentTypeText = rootComponent.Attribute("type")?.Value;
            var schemaName = rootComponent.Attribute("schemaName")?.Value?.Trim();
            if (int.TryParse(componentTypeText, out var componentType)
                && (rootComponentTypes.Contains(componentType)
                    || (!string.IsNullOrWhiteSpace(schemaName) && rootComponentKeys.Contains((componentType, schemaName)))))
            {
                rootComponent.Remove();
                removedCount++;
            }
        }

        if (removedCount == 0)
        {
            return;
        }

        var missingDependencies = manifest?.Elements().FirstOrDefault(element => element.Name.LocalName.Equals("MissingDependencies", StringComparison.OrdinalIgnoreCase));
        if (missingDependencies is not null)
        {
            foreach (var dependency in missingDependencies.Elements().Where(element => element.Name.LocalName.Equals("MissingDependency", StringComparison.OrdinalIgnoreCase)).ToArray())
            {
                var required = dependency.Elements().FirstOrDefault(element => element.Name.LocalName.Equals("Required", StringComparison.OrdinalIgnoreCase));
                var dependent = dependency.Elements().FirstOrDefault(element => element.Name.LocalName.Equals("Dependent", StringComparison.OrdinalIgnoreCase));
                if (MatchesApplyOnlyRootComponent(required, rootComponentTypes, rootComponentKeys)
                    || MatchesApplyOnlyRootComponent(dependent, rootComponentTypes, rootComponentKeys))
                {
                    dependency.Remove();
                }
            }
        }

        var normalizedXml = document.Declaration is null
            ? document.ToString(System.Xml.Linq.SaveOptions.DisableFormatting)
            : $"{document.Declaration}{Environment.NewLine}{document.Root}";
        File.WriteAllText(solutionPath, normalizedXml.Replace("\r\n", "\n", StringComparison.Ordinal), Utf8NoBom);

        diagnostics.Add(new CompilerDiagnostic(
            "package-emitter-sanitized-solution-manifest",
            DiagnosticSeverity.Info,
            $"Removed {removedCount} apply-only hybrid root component(s) from Solution.xml before packaging so they can be recreated and attached through live apply after import.",
            solutionPath));
    }

    private static void AugmentSolutionManifestForHybridSourceBackedArtifacts(
        string packageRoot,
        IEnumerable<FamilyArtifact> artifacts,
        ICollection<CompilerDiagnostic> diagnostics)
    {
        var sourceBackedArtifacts = artifacts
            .OrderBy(artifact => artifact.Family)
            .ThenBy(artifact => artifact.LogicalName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (sourceBackedArtifacts.Length == 0)
        {
            return;
        }

        var solutionPath = Path.Combine(packageRoot, "Other", "Solution.xml");
        if (!File.Exists(solutionPath))
        {
            return;
        }

        var document = XDocument.Load(solutionPath, LoadOptions.PreserveWhitespace);
        var manifest = document.Root?.Elements().FirstOrDefault(element => element.Name.LocalName.Equals("SolutionManifest", StringComparison.OrdinalIgnoreCase));
        if (manifest is null)
        {
            return;
        }

        var rootComponents = manifest.Elements().FirstOrDefault(element => element.Name.LocalName.Equals("RootComponents", StringComparison.OrdinalIgnoreCase));
        if (rootComponents is null)
        {
            rootComponents = new XElement("RootComponents");
            manifest.Add(rootComponents);
        }

        var addedCount = 0;
        var updatedCount = 0;
        foreach (var artifact in sourceBackedArtifacts)
        {
            foreach (var descriptor in BuildPackageableHybridRootComponents(packageRoot, artifact))
            {
                var existing = FindMatchingRootComponent(rootComponents, descriptor);
                if (existing is null)
                {
                    rootComponents.Add(BuildRootComponentElement(descriptor));
                    addedCount++;
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(descriptor.Behavior)
                    && !string.Equals(existing.Attribute("behavior")?.Value, descriptor.Behavior, StringComparison.Ordinal))
                {
                    existing.SetAttributeValue("behavior", descriptor.Behavior);
                    updatedCount++;
                }

                if (!string.IsNullOrWhiteSpace(descriptor.SchemaName)
                    && !string.Equals(existing.Attribute("schemaName")?.Value, descriptor.SchemaName, StringComparison.OrdinalIgnoreCase))
                {
                    existing.SetAttributeValue("schemaName", descriptor.SchemaName);
                    updatedCount++;
                }

                if (!string.IsNullOrWhiteSpace(descriptor.Id)
                    && !string.Equals(existing.Attribute("id")?.Value, descriptor.Id, StringComparison.OrdinalIgnoreCase))
                {
                    existing.SetAttributeValue("id", FormatRootComponentId(descriptor.Id));
                    updatedCount++;
                }
            }
        }

        if (addedCount == 0 && updatedCount == 0)
        {
            return;
        }

        var normalizedXml = document.Declaration is null
            ? document.ToString(SaveOptions.DisableFormatting)
            : $"{document.Declaration}{Environment.NewLine}{document.Root}";
        File.WriteAllText(solutionPath, normalizedXml.Replace("\r\n", "\n", StringComparison.Ordinal), Utf8NoBom);

        diagnostics.Add(new CompilerDiagnostic(
            "package-emitter-augmented-solution-manifest",
            DiagnosticSeverity.Info,
            $"Augmented Solution.xml with {addedCount} packageable hybrid root component(s) and updated {updatedCount} existing root component(s) from staged source-backed artifacts.",
            solutionPath));
    }

    private static void AugmentCustomizationsManifestForHybridSourceBackedArtifacts(
        string packageRoot,
        IEnumerable<FamilyArtifact> artifacts,
        ICollection<CompilerDiagnostic> diagnostics)
    {
        var requiredShellNames = artifacts
            .SelectMany(GetHybridCustomizationsShellNames)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (requiredShellNames.Length == 0)
        {
            return;
        }

        var customizationsPath = Path.Combine(packageRoot, "Other", "Customizations.xml");
        if (!File.Exists(customizationsPath))
        {
            return;
        }

        var document = XDocument.Load(customizationsPath, LoadOptions.PreserveWhitespace);
        var root = document.Root;
        if (root is null)
        {
            return;
        }

        var addedCount = 0;
        foreach (var shellName in requiredShellNames)
        {
            if (root.Elements().Any(element => element.Name.LocalName.Equals(shellName, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            InsertCustomizationsShellElement(root, shellName);
            addedCount++;
        }

        if (addedCount == 0)
        {
            return;
        }

        var normalizedXml = document.Declaration is null
            ? document.ToString(SaveOptions.DisableFormatting)
            : $"{document.Declaration}{Environment.NewLine}{document.Root}";
        File.WriteAllText(customizationsPath, normalizedXml.Replace("\r\n", "\n", StringComparison.Ordinal), Utf8NoBom);

        diagnostics.Add(new CompilerDiagnostic(
            "package-emitter-augmented-customizations-manifest",
            DiagnosticSeverity.Info,
            $"Augmented Customizations.xml with {addedCount} childless component shell(s) required by packageable hybrid source-backed artifacts: {string.Join(", ", requiredShellNames.OrderBy(value => value, StringComparer.OrdinalIgnoreCase))}.",
            customizationsPath));
    }

    private static IEnumerable<string> GetHybridCustomizationsShellNames(FamilyArtifact artifact)
    {
        switch (artifact.Family)
        {
            case ComponentFamily.CanvasApp:
                yield return "CanvasApps";
                yield break;
            case ComponentFamily.ServiceEndpoint:
                yield return "ServiceEndpoints";
                yield break;
            case ComponentFamily.Connector:
                yield return "Connectors";
                yield break;
            case ComponentFamily.RoutingRule:
            case ComponentFamily.RoutingRuleItem:
                yield return "RoutingRules";
                yield break;
            case ComponentFamily.MobileOfflineProfile:
            case ComponentFamily.MobileOfflineProfileItem:
                yield return "MobileOfflineProfiles";
                yield break;
            case ComponentFamily.Workflow:
                yield return "Workflows";
                yield break;
            case ComponentFamily.Report:
                yield return "Reports";
                yield break;
            case ComponentFamily.Template:
                yield return "Templates";
                yield break;
            case ComponentFamily.DisplayString:
                yield return "DisplayStrings";
                yield break;
            case ComponentFamily.Attachment:
                yield return "Attachments";
                yield break;
            case ComponentFamily.LegacyAsset:
            {
                var packageRelativePath = GetProperty(artifact, ArtifactPropertyKeys.PackageRelativePath) ?? string.Empty;
                if (packageRelativePath.StartsWith("WebWizard/", StringComparison.OrdinalIgnoreCase)
                    || packageRelativePath.StartsWith("WebWizards/", StringComparison.OrdinalIgnoreCase))
                {
                    yield return "WebWizards";
                }

                yield break;
            }
            default:
                yield break;
        }
    }

    private static void InsertCustomizationsShellElement(XElement root, string shellName)
    {
        var preferredOrder = new[]
        {
            "Entities",
            "Roles",
            "Workflows",
            "FieldSecurityProfiles",
            "Templates",
            "Reports",
            "DisplayStrings",
            "Attachments",
            "EntityMaps",
            "EntityRelationships",
            "OrganizationSettings",
            "optionsets",
            "CanvasApps",
            "ServiceEndpoints",
            "Connectors",
            "RoutingRules",
            "MobileOfflineProfiles",
            "WebWizards",
            "SavedQueryVisualizations",
            "WebResources",
            "CustomControls",
            "AppModuleSiteMaps",
            "AppModules",
            "EntityDataProviders",
            "Languages"
        };

        var targetIndex = Array.FindIndex(preferredOrder, value => value.Equals(shellName, StringComparison.OrdinalIgnoreCase));
        if (targetIndex < 0)
        {
            root.Add(new XElement(shellName));
            return;
        }

        var anchor = root.Elements()
            .FirstOrDefault(element =>
            {
                var elementIndex = Array.FindIndex(preferredOrder, value => value.Equals(element.Name.LocalName, StringComparison.OrdinalIgnoreCase));
                return elementIndex > targetIndex;
            });

        if (anchor is null)
        {
            root.Add(new XElement(shellName));
        }
        else
        {
            anchor.AddBeforeSelf(new XElement(shellName));
        }
    }

    private static IEnumerable<RootComponentDescriptor> BuildPackageableHybridRootComponents(string packageRoot, FamilyArtifact artifact)
    {
        switch (artifact.Family)
        {
            case ComponentFamily.Table:
            {
                var behavior = DetermineHybridSourceBackedTableBehavior(artifact);
                if (!string.IsNullOrWhiteSpace(artifact.LogicalName))
                {
                    yield return new RootComponentDescriptor(1, artifact.LogicalName, null, behavior);
                }

                yield break;
            }
            case ComponentFamily.WebResource:
                if (!string.IsNullOrWhiteSpace(artifact.LogicalName))
                {
                    yield return new RootComponentDescriptor(61, artifact.LogicalName, null, "0");
                }

                yield break;
            case ComponentFamily.CanvasApp:
                if (!string.IsNullOrWhiteSpace(artifact.LogicalName))
                {
                    yield return new RootComponentDescriptor(300, artifact.LogicalName, null, "0");
                }

                yield break;
            case ComponentFamily.AppModule:
                if (!string.IsNullOrWhiteSpace(artifact.LogicalName))
                {
                    yield return new RootComponentDescriptor(80, artifact.LogicalName, null, "0");
                }

                yield break;
            case ComponentFamily.SiteMap:
                if (!string.IsNullOrWhiteSpace(artifact.LogicalName))
                {
                    yield return new RootComponentDescriptor(62, artifact.LogicalName, null, "0");
                }

                yield break;
            case ComponentFamily.Workflow:
            {
                var workflowId = NormalizeGuid(GetProperty(artifact, ArtifactPropertyKeys.WorkflowId));
                if (!string.IsNullOrWhiteSpace(workflowId))
                {
                    yield return new RootComponentDescriptor(29, null, workflowId, "0");
                }

                yield break;
            }
            case ComponentFamily.Role:
            {
                var roleId = TryReadRoleId(packageRoot, artifact);
                if (!string.IsNullOrWhiteSpace(roleId))
                {
                    yield return new RootComponentDescriptor(20, null, roleId, "0");
                }

                yield break;
            }
            case ComponentFamily.ConnectionRole:
            {
                var connectionRoleId = TryReadConnectionRoleId(packageRoot, artifact);
                if (!string.IsNullOrWhiteSpace(connectionRoleId))
                {
                    yield return new RootComponentDescriptor(63, null, connectionRoleId, "0");
                }

                yield break;
            }
            case ComponentFamily.FieldSecurityProfile:
            {
                var fieldSecurityProfileId = TryReadFieldSecurityProfileId(packageRoot, artifact);
                if (!string.IsNullOrWhiteSpace(fieldSecurityProfileId))
                {
                    yield return new RootComponentDescriptor(70, null, fieldSecurityProfileId, "0");
                }

                yield break;
            }
            case ComponentFamily.RoutingRule:
            {
                var routingRuleId = TryReadRoutingRuleId(packageRoot, artifact);
                if (!string.IsNullOrWhiteSpace(routingRuleId))
                {
                    yield return new RootComponentDescriptor(150, null, routingRuleId, "0");
                }

                yield break;
            }
            case ComponentFamily.MobileOfflineProfile:
            {
                var mobileOfflineProfileId = TryReadMobileOfflineProfileId(packageRoot, artifact);
                if (!string.IsNullOrWhiteSpace(mobileOfflineProfileId))
                {
                    yield return new RootComponentDescriptor(161, null, mobileOfflineProfileId, "0");
                }

                yield break;
            }
            case ComponentFamily.ServiceEndpoint:
            {
                var serviceEndpointSchemaName = GetProperty(artifact, ArtifactPropertyKeys.Name)?.Trim()
                    ?? artifact.LogicalName?.Trim()
                    ?? artifact.DisplayName?.Trim();
                if (!string.IsNullOrWhiteSpace(serviceEndpointSchemaName))
                {
                    yield return new RootComponentDescriptor(95, serviceEndpointSchemaName, null, "0");
                }

                yield break;
            }
            case ComponentFamily.Connector:
            {
                var connectorSchemaName = GetProperty(artifact, ArtifactPropertyKeys.Name)?.Trim()
                    ?? artifact.LogicalName?.Trim()
                    ?? artifact.DisplayName?.Trim();
                if (!string.IsNullOrWhiteSpace(connectorSchemaName))
                {
                    yield return new RootComponentDescriptor(371, connectorSchemaName, null, "0");
                }

                yield break;
            }
            default:
                yield break;
        }
    }

    private static XElement? FindMatchingRootComponent(XElement rootComponents, RootComponentDescriptor descriptor)
    {
        foreach (var rootComponent in rootComponents.Elements().Where(element => element.Name.LocalName.Equals("RootComponent", StringComparison.OrdinalIgnoreCase)))
        {
            if (!int.TryParse(rootComponent.Attribute("type")?.Value, out var componentType)
                || componentType != descriptor.ComponentType)
            {
                continue;
            }

            var schemaName = rootComponent.Attribute("schemaName")?.Value?.Trim();
            var id = NormalizeGuid(rootComponent.Attribute("id")?.Value);
            if (!string.IsNullOrWhiteSpace(descriptor.Id)
                && !string.IsNullOrWhiteSpace(id)
                && string.Equals(id, descriptor.Id, StringComparison.OrdinalIgnoreCase))
            {
                return rootComponent;
            }

            if (!string.IsNullOrWhiteSpace(descriptor.SchemaName)
                && !string.IsNullOrWhiteSpace(schemaName)
                && string.Equals(schemaName, descriptor.SchemaName, StringComparison.OrdinalIgnoreCase))
            {
                return rootComponent;
            }
        }

        return null;
    }

    private static XElement BuildRootComponentElement(RootComponentDescriptor descriptor)
    {
        var element = new XElement("RootComponent",
            new XAttribute("type", descriptor.ComponentType.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new XAttribute("behavior", descriptor.Behavior));

        if (!string.IsNullOrWhiteSpace(descriptor.SchemaName))
        {
            element.SetAttributeValue("schemaName", descriptor.SchemaName);
        }

        if (!string.IsNullOrWhiteSpace(descriptor.Id))
        {
            element.SetAttributeValue("id", FormatRootComponentId(descriptor.Id));
        }

        return element;
    }

    private static string FormatRootComponentId(string id)
    {
        var normalized = NormalizeGuid(id) ?? id.Trim();
        return normalized.StartsWith("{", StringComparison.Ordinal) ? normalized : $"{{{normalized}}}";
    }

    private static string DetermineHybridSourceBackedTableBehavior(FamilyArtifact artifact)
    {
        var shellOnly = GetProperty(artifact, ArtifactPropertyKeys.ShellOnly);
        if (string.Equals(shellOnly, "true", StringComparison.OrdinalIgnoreCase))
        {
            return "1";
        }

        var isCustomizable = GetProperty(artifact, ArtifactPropertyKeys.IsCustomizable);
        if (string.Equals(isCustomizable, "false", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(artifact.LogicalName)
            && !artifact.LogicalName.Contains('_', StringComparison.Ordinal))
        {
            return "2";
        }

        return "0";
    }

    private static string? TryReadRoleId(string packageRoot, FamilyArtifact artifact) =>
        NormalizeGuid(ReadXmlAttribute(packageRoot, artifact, "Role", "id"));

    private static string? TryReadConnectionRoleId(string packageRoot, FamilyArtifact artifact) =>
        NormalizeGuid(ReadXmlElementValue(packageRoot, artifact, "connectionroleid"));

    private static string? TryReadFieldSecurityProfileId(string packageRoot, FamilyArtifact artifact) =>
        NormalizeGuid(ReadXmlAttribute(packageRoot, artifact, "FieldSecurityProfile", "fieldsecurityprofileid"));

    private static string? TryReadRoutingRuleId(string packageRoot, FamilyArtifact artifact) =>
        NormalizeGuid(ReadXmlAttribute(packageRoot, artifact, "RoutingRule", "RoutingRuleId")
            ?? ReadXmlElementValue(packageRoot, artifact, "RoutingRuleId"));

    private static string? TryReadMobileOfflineProfileId(string packageRoot, FamilyArtifact artifact) =>
        NormalizeGuid(ReadXmlAttribute(packageRoot, artifact, "MobileOfflineProfile", "MobileOfflineProfileId")
            ?? ReadXmlElementValue(packageRoot, artifact, "MobileOfflineProfileId"));

    private static string? TryReadServiceEndpointId(string packageRoot, FamilyArtifact artifact) =>
        NormalizeGuid(ReadXmlAttribute(packageRoot, artifact, "ServiceEndpoint", "ServiceEndpointId"));

    private static string? TryReadConnectorId(string packageRoot, FamilyArtifact artifact) =>
        NormalizeGuid(ReadXmlElementValue(packageRoot, artifact, "connectorid"));

    private static string? ReadXmlAttribute(string packageRoot, FamilyArtifact artifact, string elementName, string attributeName)
    {
        var root = TryReadPackageArtifactRoot(packageRoot, artifact);
        return root?
            .DescendantsAndSelf()
            .FirstOrDefault(element => element.Name.LocalName.Equals(elementName, StringComparison.OrdinalIgnoreCase))
            ?.Attribute(attributeName)
            ?.Value;
    }

    private static string? ReadXmlElementValue(string packageRoot, FamilyArtifact artifact, string elementName)
    {
        var root = TryReadPackageArtifactRoot(packageRoot, artifact);
        return root?
            .DescendantsAndSelf()
            .FirstOrDefault(element => element.Name.LocalName.Equals(elementName, StringComparison.OrdinalIgnoreCase))
            ?.Value
            ?.Trim();
    }

    private static XElement? TryReadPackageArtifactRoot(string packageRoot, FamilyArtifact artifact)
    {
        var packageRelativePath = GetProperty(artifact, ArtifactPropertyKeys.PackageRelativePath);
        if (string.IsNullOrWhiteSpace(packageRelativePath))
        {
            return null;
        }

        var candidatePath = GetContainedPath(packageRoot, packageRelativePath);
        if (!File.Exists(candidatePath))
        {
            return null;
        }

        try
        {
            return XDocument.Load(candidatePath, LoadOptions.PreserveWhitespace).Root;
        }
        catch
        {
            return null;
        }
    }

    private sealed record RootComponentDescriptor(int ComponentType, string? SchemaName, string? Id, string Behavior);

    private static IEnumerable<int> BuildApplyOnlyRootComponentTypes(FamilyArtifact artifact)
    {
        switch (artifact.Family)
        {
            case ComponentFamily.EntityAnalyticsConfiguration:
                yield return 430;
                yield break;
            case ComponentFamily.AiProjectType:
                yield return 400;
                yield break;
            case ComponentFamily.AiProject:
                yield return 401;
                yield break;
            case ComponentFamily.AiConfiguration:
                yield return 402;
                yield break;
            case ComponentFamily.PluginAssembly:
                yield return 91;
                yield break;
            case ComponentFamily.PluginType:
                yield return 90;
                yield break;
            case ComponentFamily.PluginStep:
                yield return 92;
                yield break;
            case ComponentFamily.PluginStepImage:
                yield return 93;
                yield break;
            case ComponentFamily.ServiceEndpoint:
                yield return 95;
                yield break;
            case ComponentFamily.MobileOfflineProfile:
                yield return 161;
                yield break;
            case ComponentFamily.ConnectionRole:
                yield return 63;
                yield break;
            case ComponentFamily.Connector:
                yield return 371;
                yield return 372;
                yield break;
            default:
                yield break;
        }
    }

    private static IEnumerable<(int ComponentType, string SchemaName)> BuildApplyOnlyRootComponentKeys(FamilyArtifact artifact)
    {
        switch (artifact.Family)
        {
            case ComponentFamily.EntityAnalyticsConfiguration when !string.IsNullOrWhiteSpace(artifact.LogicalName):
                yield return (430, artifact.LogicalName);
                yield break;
            case ComponentFamily.AiProjectType when !string.IsNullOrWhiteSpace(artifact.LogicalName):
                yield return (400, artifact.LogicalName);
                yield break;
            case ComponentFamily.AiProject when !string.IsNullOrWhiteSpace(artifact.LogicalName):
                yield return (401, artifact.LogicalName);
                yield break;
            case ComponentFamily.AiConfiguration when !string.IsNullOrWhiteSpace(artifact.LogicalName):
                yield return (402, artifact.LogicalName);
                yield break;
            case ComponentFamily.PluginAssembly when !string.IsNullOrWhiteSpace(artifact.DisplayName):
                yield return (91, artifact.DisplayName);
                yield break;
            case ComponentFamily.PluginType when !string.IsNullOrWhiteSpace(artifact.LogicalName):
                yield return (90, artifact.LogicalName);
                yield break;
            case ComponentFamily.PluginStep when !string.IsNullOrWhiteSpace(artifact.DisplayName):
                yield return (92, artifact.DisplayName);
                yield break;
            case ComponentFamily.PluginStepImage when !string.IsNullOrWhiteSpace(artifact.DisplayName):
                yield return (93, artifact.DisplayName);
                yield break;
            case ComponentFamily.ServiceEndpoint:
            {
                var schemaName = GetProperty(artifact, ArtifactPropertyKeys.Name) ?? artifact.DisplayName;
                if (!string.IsNullOrWhiteSpace(schemaName))
                {
                    yield return (95, schemaName);
                }

                yield break;
            }
            case ComponentFamily.Connector:
            {
                var preferredSchemaName = GetProperty(artifact, ArtifactPropertyKeys.Name) ?? artifact.LogicalName ?? artifact.DisplayName;
                if (!string.IsNullOrWhiteSpace(preferredSchemaName))
                {
                    yield return (371, preferredSchemaName);
                }

                if (!string.IsNullOrWhiteSpace(artifact.LogicalName)
                    && !string.Equals(artifact.LogicalName, preferredSchemaName, StringComparison.OrdinalIgnoreCase))
                {
                    yield return (371, artifact.LogicalName);
                }

                yield break;
            }
            default:
                yield break;
        }
    }

    private static bool MatchesApplyOnlyRootComponent(
        System.Xml.Linq.XElement? element,
        IReadOnlySet<int> rootComponentTypes,
        IReadOnlySet<(int ComponentType, string SchemaName)> rootComponentKeys)
    {
        if (element is null)
        {
            return false;
        }

        var componentTypeText = element.Attribute("type")?.Value;
        var schemaName = element.Attribute("schemaName")?.Value?.Trim();
        return int.TryParse(componentTypeText, out var componentType)
               && (rootComponentTypes.Contains(componentType)
                   || (!string.IsNullOrWhiteSpace(schemaName) && rootComponentKeys.Contains((componentType, schemaName))));
    }

    private static string ResolveSourceRoot(CanonicalSolution model)
    {
        var solutionShellPath = model.Artifacts
            .Where(artifact => artifact.Family == ComponentFamily.SolutionShell && artifact.Evidence == EvidenceKind.Source)
            .Select(artifact => artifact.SourcePath)
            .FirstOrDefault(path => !string.IsNullOrWhiteSpace(path));
        if (!string.IsNullOrWhiteSpace(solutionShellPath))
        {
            var fullPath = Path.GetFullPath(solutionShellPath!);
            var otherDirectory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(otherDirectory)
                && string.Equals(Path.GetFileName(otherDirectory), "Other", StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetFullPath(Path.Combine(otherDirectory, ".."));
            }
        }

        foreach (var artifact in model.Artifacts.Where(artifact => artifact.Evidence == EvidenceKind.Source && artifact.SourcePath is not null))
        {
            var metadataRelativePath = GetProperty(artifact, ArtifactPropertyKeys.MetadataSourcePath);
            if (string.IsNullOrWhiteSpace(metadataRelativePath))
            {
                continue;
            }

            var candidate = Path.GetFullPath(artifact.SourcePath!);
            foreach (var _ in metadataRelativePath.Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                candidate = Path.GetDirectoryName(candidate) ?? string.Empty;
            }

            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException(
            "Package-input emission requires source-backed artifacts rooted in an unpacked Dataverse solution tree. No stable source root could be inferred from the canonical model.");
    }

    private static string? GetProperty(FamilyArtifact artifact, string key) =>
        artifact.Properties is not null && artifact.Properties.TryGetValue(key, out var value) ? value : null;

    private static void WriteJson(
        string packageRoot,
        string relativePath,
        object document,
        List<EmittedArtifact> emittedFiles,
        string description,
        EmittedArtifactRole role)
    {
        var fullPath = GetContainedPath(packageRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var json = JsonSerializer.Serialize(document, JsonOptions).Replace("\r\n", "\n", StringComparison.Ordinal);
        File.WriteAllText(fullPath, json + "\n", Utf8NoBom);
        emittedFiles.Add(new EmittedArtifact($"package-inputs/{relativePath.Replace('\\', '/')}", role, description));
    }

    private static string GetContainedRelativePath(string root, string path)
    {
        var relativePath = Path.GetRelativePath(Path.GetFullPath(root), Path.GetFullPath(path)).Replace('\\', '/');
        if (relativePath.StartsWith("../", StringComparison.Ordinal) || relativePath.Equals("..", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Refusing to copy a source file outside the package-input root: {path}");
        }

        return relativePath;
    }

    private static string GetContainedPath(string root, string relativePath)
    {
        var rootFullPath = Path.GetFullPath(root);
        var candidatePath = Path.GetFullPath(Path.Combine(rootFullPath, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var prefix = rootFullPath.EndsWith(Path.DirectorySeparatorChar) ? rootFullPath : rootFullPath + Path.DirectorySeparatorChar;
        if (!candidatePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && !candidatePath.Equals(rootFullPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Refusing to write outside the package-input root: {relativePath}");
        }

        return candidatePath;
    }

    private sealed record SourceBackedAssetMapEntry(string SourcePath, string PackageRelativePath);
}
