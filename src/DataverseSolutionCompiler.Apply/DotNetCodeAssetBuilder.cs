using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DataverseSolutionCompiler.Domain.Abstractions;
using DataverseSolutionCompiler.Domain.Build;
using DataverseSolutionCompiler.Domain.Diagnostics;
using DataverseSolutionCompiler.Domain.Model;

namespace DataverseSolutionCompiler.Apply;

public sealed class DotNetCodeAssetBuilder : ICodeAssetBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    public CodeAssetBuildResult Build(CodeAssetBuildRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var diagnostics = new List<CompilerDiagnostic>();
        var updatedArtifacts = new List<FamilyArtifact>(request.Solution.Artifacts.Count);
        var builtAssemblyArtifacts = new Dictionary<string, FamilyArtifact>(StringComparer.OrdinalIgnoreCase);

        foreach (var assemblyArtifact in request.Solution.Artifacts
                     .Where(artifact => artifact.Family == ComponentFamily.PluginAssembly)
                     .OrderBy(artifact => artifact.LogicalName, StringComparer.OrdinalIgnoreCase))
        {
            if (!IsCodeFirstAssembly(assemblyArtifact))
            {
                builtAssemblyArtifacts[assemblyArtifact.LogicalName] = assemblyArtifact;
                continue;
            }

            var builtArtifact = BuildAssemblyArtifact(request.Solution, assemblyArtifact, request.StagingRoot, request.Configuration, diagnostics);
            builtAssemblyArtifacts[assemblyArtifact.LogicalName] = builtArtifact;
        }

        foreach (var artifact in request.Solution.Artifacts)
        {
            if (artifact.Family == ComponentFamily.PluginAssembly
                && builtAssemblyArtifacts.TryGetValue(artifact.LogicalName, out var builtAssembly))
            {
                updatedArtifacts.Add(builtAssembly);
            }
            else
            {
                updatedArtifacts.Add(artifact);
            }
        }

        if (!builtAssemblyArtifacts.Values.Any(artifact => artifact.Properties?.ContainsKey(ArtifactPropertyKeys.StagedBuildOutputPath) == true))
        {
            diagnostics.Add(new CompilerDiagnostic(
                "code-asset-build-noop",
                DiagnosticSeverity.Info,
                "No code-first plug-in assemblies required staged build outputs.",
                request.StagingRoot));
        }

        return new CodeAssetBuildResult(
            request.Solution with
            {
                Artifacts = updatedArtifacts
            },
            diagnostics);
    }

    private static FamilyArtifact BuildAssemblyArtifact(
        CanonicalSolution solution,
        FamilyArtifact artifact,
        string stagingRoot,
        string configuration,
        ICollection<CompilerDiagnostic> diagnostics)
    {
        var projectPath = GetProperty(artifact, ArtifactPropertyKeys.CodeProjectPath);
        var deploymentFlavorText = GetProperty(artifact, ArtifactPropertyKeys.DeploymentFlavor);
        if (string.IsNullOrWhiteSpace(projectPath)
            || !Enum.TryParse<CodeAssetDeploymentFlavor>(deploymentFlavorText, ignoreCase: true, out var deploymentFlavor))
        {
            diagnostics.Add(new CompilerDiagnostic(
                "code-asset-build-metadata-missing",
                DiagnosticSeverity.Error,
                $"Code-first plug-in assembly '{artifact.LogicalName}' is missing code build metadata.",
                artifact.SourcePath));
            return artifact;
        }

        if (deploymentFlavor == CodeAssetDeploymentFlavor.PluginPackage
            && ContainsCustomWorkflowActivity(solution, artifact))
        {
            diagnostics.Add(new CompilerDiagnostic(
                "code-asset-build-workflow-activity-package-unsupported",
                DiagnosticSeverity.Error,
                $"Code-first plug-in assembly '{artifact.LogicalName}' includes custom workflow activity types. Dataverse workflow extensions are supported only through the classic assembly lane; plug-in package deployment remains an explicit boundary.",
                artifact.SourcePath));
            return artifact;
        }

        var assetMap = ReadAssetMap(artifact);
        if (assetMap.Count == 0)
        {
            diagnostics.Add(new CompilerDiagnostic(
                "code-asset-build-asset-map-missing",
                DiagnosticSeverity.Error,
                $"Code-first plug-in assembly '{artifact.LogicalName}' is missing staged source asset metadata.",
                artifact.SourcePath));
            return artifact;
        }

        var assemblyRoot = Path.Combine(stagingRoot, "code-assets", SafeSegment(artifact.LogicalName));
        var sourceRoot = Path.Combine(assemblyRoot, "source");
        var outputRoot = Path.Combine(assemblyRoot, "output");
        if (Directory.Exists(assemblyRoot))
        {
            Directory.Delete(assemblyRoot, recursive: true);
        }

        Directory.CreateDirectory(sourceRoot);
        Directory.CreateDirectory(outputRoot);

        foreach (var entry in assetMap)
        {
            if (!File.Exists(entry.SourcePath))
            {
                diagnostics.Add(new CompilerDiagnostic(
                    "code-asset-build-source-missing",
                    DiagnosticSeverity.Error,
                    $"Code-first staged source asset '{entry.SourcePath}' could not be found.",
                    entry.SourcePath));
                return artifact;
            }

            var destinationPath = Path.Combine(sourceRoot, entry.PackageRelativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.Copy(entry.SourcePath, destinationPath, overwrite: true);
        }

        var stagedProjectPath = Path.Combine(sourceRoot, projectPath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(stagedProjectPath))
        {
            diagnostics.Add(new CompilerDiagnostic(
                "code-asset-build-project-missing",
                DiagnosticSeverity.Error,
                $"Code-first staged project '{projectPath}' could not be materialized for '{artifact.LogicalName}'.",
                stagedProjectPath));
            return artifact;
        }

        var builtOutputPath = deploymentFlavor switch
        {
            CodeAssetDeploymentFlavor.PluginPackage => BuildPluginPackage(stagedProjectPath, outputRoot, configuration, diagnostics),
            _ => BuildClassicAssembly(stagedProjectPath, artifact, outputRoot, configuration, diagnostics)
        };

        if (string.IsNullOrWhiteSpace(builtOutputPath) || !File.Exists(builtOutputPath))
        {
            return artifact;
        }

        var updatedProperties = artifact.Properties is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(artifact.Properties, StringComparer.Ordinal);
        updatedProperties[ArtifactPropertyKeys.StagedBuildOutputPath] = builtOutputPath;
        updatedProperties[ArtifactPropertyKeys.ByteLength] = new FileInfo(builtOutputPath).Length.ToString(System.Globalization.CultureInfo.InvariantCulture);
        updatedProperties[ArtifactPropertyKeys.ContentHash] = ComputeFileHash(builtOutputPath);

        diagnostics.Add(new CompilerDiagnostic(
            "code-asset-build-complete",
            DiagnosticSeverity.Info,
            $"Built staged {deploymentFlavor} output for plug-in assembly '{artifact.LogicalName}'.",
            builtOutputPath));

        return artifact with
        {
            Properties = updatedProperties
        };
    }

    private static string? BuildClassicAssembly(
        string stagedProjectPath,
        FamilyArtifact artifact,
        string outputRoot,
        string configuration,
        ICollection<CompilerDiagnostic> diagnostics)
    {
        if (!RunDotNet(
                "build",
                stagedProjectPath,
                outputRoot,
                configuration,
                diagnostics,
                additionalArguments: ["-o", outputRoot]))
        {
            return null;
        }

        var assemblyFileName = GetProperty(artifact, ArtifactPropertyKeys.AssemblyFileName)
            ?? $"{artifact.DisplayName ?? artifact.LogicalName}.dll";
        var candidate = Path.Combine(outputRoot, assemblyFileName);
        if (File.Exists(candidate))
        {
            return candidate;
        }

        return Directory.EnumerateFiles(outputRoot, "*.dll", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static string? BuildPluginPackage(
        string stagedProjectPath,
        string outputRoot,
        string configuration,
        ICollection<CompilerDiagnostic> diagnostics)
    {
        if (!RunDotNet(
                "pack",
                stagedProjectPath,
                outputRoot,
                configuration,
                diagnostics,
                additionalArguments: ["-o", outputRoot]))
        {
            return null;
        }

        return Directory.EnumerateFiles(outputRoot, "*.nupkg", SearchOption.AllDirectories)
            .Where(path => !path.EndsWith(".snupkg", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static bool RunDotNet(
        string verb,
        string stagedProjectPath,
        string outputRoot,
        string configuration,
        ICollection<CompilerDiagnostic> diagnostics,
        IReadOnlyList<string>? additionalArguments = null)
    {
        var request = new ProcessStartInfo
        {
            FileName = "dotnet",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(stagedProjectPath) ?? Environment.CurrentDirectory
        };
        request.ArgumentList.Add(verb);
        request.ArgumentList.Add(stagedProjectPath);
        request.ArgumentList.Add("-c");
        request.ArgumentList.Add(configuration);
        request.ArgumentList.Add("-nologo");
        if (additionalArguments is not null)
        {
            foreach (var argument in additionalArguments)
            {
                request.ArgumentList.Add(argument);
            }
        }

        using var process = new Process { StartInfo = request };
        process.Start();
        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (!string.IsNullOrWhiteSpace(standardOutput))
        {
            diagnostics.Add(new CompilerDiagnostic(
                "code-asset-build-stdout",
                DiagnosticSeverity.Info,
                standardOutput.Trim(),
                stagedProjectPath));
        }

        if (!string.IsNullOrWhiteSpace(standardError))
        {
            diagnostics.Add(new CompilerDiagnostic(
                "code-asset-build-stderr",
                process.ExitCode == 0 ? DiagnosticSeverity.Warning : DiagnosticSeverity.Error,
                standardError.Trim(),
                stagedProjectPath));
        }

        if (process.ExitCode == 0)
        {
            return true;
        }

        diagnostics.Add(new CompilerDiagnostic(
            "code-asset-build-failed",
            DiagnosticSeverity.Error,
            $"dotnet {verb} exited with code {process.ExitCode} for '{stagedProjectPath}'.",
            stagedProjectPath));
        return false;
    }

    private static bool IsCodeFirstAssembly(FamilyArtifact artifact) =>
        artifact.Family == ComponentFamily.PluginAssembly
        && !string.IsNullOrWhiteSpace(GetProperty(artifact, ArtifactPropertyKeys.CodeProjectPath));

    private static bool ContainsCustomWorkflowActivity(CanonicalSolution solution, FamilyArtifact assemblyArtifact)
    {
        var assemblyFullName = GetProperty(assemblyArtifact, ArtifactPropertyKeys.AssemblyFullName) ?? assemblyArtifact.LogicalName;
        if (string.IsNullOrWhiteSpace(assemblyFullName))
        {
            return false;
        }

        return solution.Artifacts.Any(artifact =>
            artifact.Family == ComponentFamily.PluginType
            && string.Equals(GetProperty(artifact, ArtifactPropertyKeys.AssemblyFullName), assemblyFullName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(GetProperty(artifact, ArtifactPropertyKeys.PluginTypeKind), "customWorkflowActivity", StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<AssetMapEntry> ReadAssetMap(FamilyArtifact artifact)
    {
        var json = GetProperty(artifact, ArtifactPropertyKeys.AssetSourceMapJson);
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            if (JsonSerializer.Deserialize<List<AssetMapEntry>>(json, JsonOptions) is { Count: > 0 } mappedEntries)
            {
                return mappedEntries
                    .Where(entry => !string.IsNullOrWhiteSpace(entry.SourcePath) && !string.IsNullOrWhiteSpace(entry.PackageRelativePath))
                    .Select(entry => entry with
                    {
                        SourcePath = Path.GetFullPath(entry.SourcePath),
                        PackageRelativePath = NormalizeRelativePath(entry.PackageRelativePath)
                    })
                    .ToArray();
            }

            if (JsonSerializer.Deserialize<List<string>>(json, JsonOptions) is { Count: > 0 } stringEntries)
            {
                return stringEntries
                    .Where(entry => !string.IsNullOrWhiteSpace(entry))
                    .Select(entry =>
                    {
                        var sourcePath = ResolveArtifactMaterializedPath(artifact, entry!);
                        return new AssetMapEntry(sourcePath, NormalizeRelativePath(entry!));
                    })
                    .ToArray();
            }
        }
        catch (JsonException)
        {
            return [];
        }

        return [];
    }

    private static string ResolveArtifactMaterializedPath(FamilyArtifact artifact, string relativeOrAbsolutePath)
    {
        if (Path.IsPathRooted(relativeOrAbsolutePath))
        {
            return Path.GetFullPath(relativeOrAbsolutePath);
        }

        var metadataRelativePath = GetProperty(artifact, ArtifactPropertyKeys.MetadataSourcePath);
        if (!string.IsNullOrWhiteSpace(metadataRelativePath)
            && !string.IsNullOrWhiteSpace(artifact.SourcePath)
            && File.Exists(artifact.SourcePath))
        {
            var sourceRoot = artifact.SourcePath;
            foreach (var _ in metadataRelativePath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                sourceRoot = Path.GetDirectoryName(sourceRoot) ?? string.Empty;
            }

            return Path.GetFullPath(Path.Combine(sourceRoot, relativeOrAbsolutePath.Replace('/', Path.DirectorySeparatorChar)));
        }

        return Path.GetFullPath(relativeOrAbsolutePath);
    }

    private static string? GetProperty(FamilyArtifact artifact, string key) =>
        artifact.Properties is not null && artifact.Properties.TryGetValue(key, out var value)
            ? value
            : null;

    private static string NormalizeRelativePath(string path) =>
        path.Replace('\\', '/').TrimStart('/');

    private static string ComputeFileHash(string path)
    {
        using var stream = File.OpenRead(path);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string SafeSegment(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(char.IsLetterOrDigit(character) ? character : '-');
        }

        var normalized = builder.ToString().Trim('-');
        var prefix = normalized.Length > 48 ? normalized[..48] : normalized;
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant()[..12];
        return $"{prefix}-{hash}";
    }

    private sealed record AssetMapEntry(string SourcePath, string PackageRelativePath);
}
