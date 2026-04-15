using System.Text;
using System.Text.Json;
using DataverseSolutionCompiler.Domain.Abstractions;
using DataverseSolutionCompiler.Domain.Diagnostics;
using DataverseSolutionCompiler.Domain.Emission;
using DataverseSolutionCompiler.Domain.Model;

namespace DataverseSolutionCompiler.Emitters.Package;

public sealed partial class PackageEmitter : ISolutionEmitter
{
    private static readonly string[] SupportedRootDirectories =
    [
        "AIConfigurations",
        "AIProjects",
        "AIProjectTypes",
        "AppModules",
        "AppModuleSiteMaps",
        "CanvasApps",
        "Connectors",
        "duplicaterules",
        "Entities",
        "ImportMaps",
        "MobileOfflineProfiles",
        "entityanalyticsconfigs",
        "environmentvariabledefinitions",
        "OptionSets",
        "Other",
        "PluginAssemblies",
        "Roles",
        "RoutingRules",
        "ServiceEndpoints",
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
            var structuredArtifacts = model.Artifacts.Where(artifact => !IsSourceBackedIntentArtifact(artifact)).ToArray();
            var structuredModel = new CanonicalSolution(
                model.Identity,
                model.Publisher,
                structuredArtifacts,
                model.Dependencies,
                model.EnvironmentBindings,
                model.Diagnostics);

            WriteDerivedPackageInputTree(structuredModel, packageRoot, emittedFiles, diagnostics);
            WriteHybridSourceBackedFiles(packageRoot, sourceBackedArtifacts, emittedFiles);

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

    private static void WriteHybridSourceBackedFiles(string packageRoot, IEnumerable<FamilyArtifact> artifacts, List<EmittedArtifact> emittedFiles)
    {
        var copiedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var artifact in artifacts.OrderBy(artifact => artifact.Family).ThenBy(artifact => artifact.LogicalName, StringComparer.OrdinalIgnoreCase))
        {
            var metadataTarget = GetProperty(artifact, ArtifactPropertyKeys.PackageRelativePath);
            if (!string.IsNullOrWhiteSpace(metadataTarget) && File.Exists(artifact.SourcePath) && copiedPaths.Add(metadataTarget))
            {
                CopyFileToPackage(packageRoot, artifact.SourcePath, metadataTarget, emittedFiles, artifact.LogicalName);
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

    private static void CopyFileToPackage(string packageRoot, string sourcePath, string packageRelativePath, List<EmittedArtifact> emittedFiles, string logicalName)
    {
        var destinationPath = GetContainedPath(packageRoot, packageRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

        if (ShouldNormalizeAsText(sourcePath))
        {
            var normalizedText = File.ReadAllText(sourcePath)
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace("\r", "\n", StringComparison.Ordinal);
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

    private static IReadOnlyList<SourceBackedAssetMapEntry> ReadSourceBackedAssetMap(FamilyArtifact artifact)
    {
        var json = GetProperty(artifact, ArtifactPropertyKeys.AssetSourceMapJson);
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<SourceBackedAssetMapEntry>>(json, JsonOptions) ?? [];
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
