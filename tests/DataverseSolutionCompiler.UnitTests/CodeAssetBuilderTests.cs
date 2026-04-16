using System.IO.Compression;
using DataverseSolutionCompiler.Apply;
using DataverseSolutionCompiler.Compiler;
using DataverseSolutionCompiler.Domain.Build;
using DataverseSolutionCompiler.Domain.Compilation;
using DataverseSolutionCompiler.Domain.Model;
using FluentAssertions;
using Xunit;

namespace DataverseSolutionCompiler.UnitTests;

public sealed class CodeAssetBuilderTests
{
    private static readonly string SeedCodePluginClassicPath = Path.Combine(
        "C:\\Git\\Dataverse-Solution-KB",
        "fixtures",
        "skill-corpus",
        "examples",
        "seed-code-plugin-classic");

    private static readonly string SeedCodePluginPackagePath = Path.Combine(
        "C:\\Git\\Dataverse-Solution-KB",
        "fixtures",
        "skill-corpus",
        "examples",
        "seed-code-plugin-package");
    private static readonly string SeedCodeWorkflowActivityClassicPath = Path.Combine(
        "C:\\Git\\Dataverse-Solution-KB",
        "fixtures",
        "skill-corpus",
        "examples",
        "seed-code-workflow-activity-classic");
    private static readonly string SeedCodePluginHelperPath = Path.Combine(
        "C:\\Git\\Dataverse-Solution-KB",
        "fixtures",
        "skill-corpus",
        "examples",
        "seed-code-plugin-helper");
    private static readonly string SeedCodeWorkflowActivityPackagePath = Path.Combine(
        "C:\\Git\\Dataverse-Solution-KB",
        "fixtures",
        "skill-corpus",
        "examples",
        "seed-code-workflow-activity-package");

    [Fact]
    public void Builder_stages_the_classic_code_first_seed_outside_the_source_tree()
    {
        var stagingRoot = Path.Combine(Path.GetTempPath(), $"dsc-code-assets-classic-{Guid.NewGuid():N}");
        DeleteGeneratedSourceOutputs(
            Path.Combine(SeedCodePluginClassicPath, "plugins", "Codex.Metadata.CodeFirst.Classic"));

        try
        {
            var compiled = new CompilerKernel().Compile(new CompilationRequest(SeedCodePluginClassicPath, Array.Empty<string>()));
            compiled.Success.Should().BeTrue();

            var result = new DotNetCodeAssetBuilder().Build(new CodeAssetBuildRequest(compiled.Solution, stagingRoot, "Debug"));

            result.Success.Should().BeTrue();
            var assembly = result.Solution.Artifacts.Single(artifact => artifact.Family == ComponentFamily.PluginAssembly);
            var stagedPath = GetProperty(assembly, ArtifactPropertyKeys.StagedBuildOutputPath);
            stagedPath.Should().NotBeNullOrWhiteSpace();
            stagedPath.Should().EndWith(".dll");
            Path.GetFullPath(stagedPath!).StartsWith(Path.GetFullPath(stagingRoot), StringComparison.OrdinalIgnoreCase).Should().BeTrue();
            File.Exists(stagedPath!).Should().BeTrue();
            GetProperty(assembly, ArtifactPropertyKeys.ByteLength).Should().NotBeNullOrWhiteSpace();
            GetProperty(assembly, ArtifactPropertyKeys.ContentHash).Should().NotBeNullOrWhiteSpace();

            Directory.Exists(Path.Combine(SeedCodePluginClassicPath, "plugins", "Codex.Metadata.CodeFirst.Classic", "bin")).Should().BeFalse();
            Directory.Exists(Path.Combine(SeedCodePluginClassicPath, "plugins", "Codex.Metadata.CodeFirst.Classic", "obj")).Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(stagingRoot))
            {
                Directory.Delete(stagingRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void Builder_stages_the_package_code_first_seed_as_a_nuget_with_its_dependency()
    {
        var stagingRoot = Path.Combine(Path.GetTempPath(), $"dsc-code-assets-package-{Guid.NewGuid():N}");
        DeleteGeneratedSourceOutputs(
            Path.Combine(SeedCodePluginPackagePath, "plugins", "Codex.Metadata.CodeFirst.Package"),
            Path.Combine(SeedCodePluginPackagePath, "plugins", "Codex.Metadata.CodeFirst.Package.Dependency"));

        try
        {
            var compiled = new CompilerKernel().Compile(new CompilationRequest(SeedCodePluginPackagePath, Array.Empty<string>()));
            compiled.Success.Should().BeTrue();

            var result = new DotNetCodeAssetBuilder().Build(new CodeAssetBuildRequest(compiled.Solution, stagingRoot, "Debug"));

            result.Success.Should().BeTrue();
            var assembly = result.Solution.Artifacts.Single(artifact => artifact.Family == ComponentFamily.PluginAssembly);
            var stagedPath = GetProperty(assembly, ArtifactPropertyKeys.StagedBuildOutputPath);
            stagedPath.Should().NotBeNullOrWhiteSpace();
            stagedPath.Should().EndWith(".nupkg");
            File.Exists(stagedPath!).Should().BeTrue();

            using var package = ZipFile.OpenRead(stagedPath!);
            package.Entries.Select(entry => entry.FullName).Should().Contain(entry => entry.EndsWith("Codex.Metadata.CodeFirst.Package.dll", StringComparison.Ordinal));
            package.Entries.Select(entry => entry.FullName).Should().Contain(entry => entry.EndsWith("Codex.Metadata.CodeFirst.Package.Dependency.dll", StringComparison.Ordinal));

            Directory.Exists(Path.Combine(SeedCodePluginPackagePath, "plugins", "Codex.Metadata.CodeFirst.Package", "bin")).Should().BeFalse();
            Directory.Exists(Path.Combine(SeedCodePluginPackagePath, "plugins", "Codex.Metadata.CodeFirst.Package", "obj")).Should().BeFalse();
            Directory.Exists(Path.Combine(SeedCodePluginPackagePath, "plugins", "Codex.Metadata.CodeFirst.Package.Dependency", "bin")).Should().BeFalse();
            Directory.Exists(Path.Combine(SeedCodePluginPackagePath, "plugins", "Codex.Metadata.CodeFirst.Package.Dependency", "obj")).Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(stagingRoot))
            {
                Directory.Delete(stagingRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void Builder_stages_the_classic_custom_workflow_activity_seed_as_a_dll()
    {
        var stagingRoot = Path.Combine(Path.GetTempPath(), $"dsc-code-assets-workflow-classic-{Guid.NewGuid():N}");
        DeleteGeneratedSourceOutputs(
            Path.Combine(SeedCodeWorkflowActivityClassicPath, "plugins", "Codex.Metadata.CodeFirst.WorkflowActivity.Classic"));

        try
        {
            var compiled = new CompilerKernel().Compile(new CompilationRequest(SeedCodeWorkflowActivityClassicPath, Array.Empty<string>()));
            compiled.Success.Should().BeTrue();

            var result = new DotNetCodeAssetBuilder().Build(new CodeAssetBuildRequest(compiled.Solution, stagingRoot, "Debug"));

            result.Success.Should().BeTrue();
            var assembly = result.Solution.Artifacts.Single(artifact => artifact.Family == ComponentFamily.PluginAssembly);
            var stagedPath = GetProperty(assembly, ArtifactPropertyKeys.StagedBuildOutputPath);
            stagedPath.Should().NotBeNullOrWhiteSpace();
            stagedPath.Should().EndWith(".dll");
            File.Exists(stagedPath!).Should().BeTrue();

            var pluginType = result.Solution.Artifacts.Single(artifact => artifact.Family == ComponentFamily.PluginType);
            GetProperty(pluginType, ArtifactPropertyKeys.PluginTypeKind).Should().Be("customWorkflowActivity");
        }
        finally
        {
            if (Directory.Exists(stagingRoot))
            {
                Directory.Delete(stagingRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void Builder_stages_helper_based_mixed_plugin_and_workflow_activity_seed_as_a_dll()
    {
        var stagingRoot = Path.Combine(Path.GetTempPath(), $"dsc-code-assets-helper-{Guid.NewGuid():N}");
        DeleteGeneratedSourceOutputs(
            Path.Combine(SeedCodePluginHelperPath, "plugins", "Codex.Metadata.CodeFirst.Helper"));

        try
        {
            var compiled = new CompilerKernel().Compile(new CompilationRequest(SeedCodePluginHelperPath, Array.Empty<string>()));
            compiled.Success.Should().BeTrue();

            var result = new DotNetCodeAssetBuilder().Build(new CodeAssetBuildRequest(compiled.Solution, stagingRoot, "Debug"));

            result.Success.Should().BeTrue();
            var assembly = result.Solution.Artifacts.Single(artifact => artifact.Family == ComponentFamily.PluginAssembly);
            var stagedPath = GetProperty(assembly, ArtifactPropertyKeys.StagedBuildOutputPath);
            stagedPath.Should().NotBeNullOrWhiteSpace();
            stagedPath.Should().EndWith(".dll");
            File.Exists(stagedPath!).Should().BeTrue();

            result.Solution.Artifacts.Count(artifact => artifact.Family == ComponentFamily.PluginType).Should().Be(2);
            result.Solution.Artifacts.Should().Contain(artifact =>
                artifact.Family == ComponentFamily.PluginType
                && GetProperty(artifact, ArtifactPropertyKeys.PluginTypeKind) == "customWorkflowActivity");
            result.Solution.Artifacts.Should().Contain(artifact =>
                artifact.Family == ComponentFamily.PluginType
                && GetProperty(artifact, ArtifactPropertyKeys.PluginTypeKind) == "plugin");
        }
        finally
        {
            if (Directory.Exists(stagingRoot))
            {
                Directory.Delete(stagingRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void Builder_rejects_plugin_package_deployment_for_custom_workflow_activity_inputs()
    {
        var stagingRoot = Path.Combine(Path.GetTempPath(), $"dsc-code-assets-workflow-package-{Guid.NewGuid():N}");
        DeleteGeneratedSourceOutputs(
            Path.Combine(SeedCodeWorkflowActivityPackagePath, "plugins", "Codex.Metadata.CodeFirst.WorkflowActivity.Package"));

        try
        {
            var compiled = new CompilerKernel().Compile(new CompilationRequest(SeedCodeWorkflowActivityPackagePath, Array.Empty<string>()));
            compiled.Success.Should().BeTrue();

            var result = new DotNetCodeAssetBuilder().Build(new CodeAssetBuildRequest(compiled.Solution, stagingRoot, "Debug"));

            result.Success.Should().BeFalse();
            result.Diagnostics.Should().Contain(diagnostic =>
                diagnostic.Code == "code-asset-build-workflow-activity-package-unsupported"
                && diagnostic.Severity == Domain.Diagnostics.DiagnosticSeverity.Error);

            var assembly = result.Solution.Artifacts.Single(artifact => artifact.Family == ComponentFamily.PluginAssembly);
            GetProperty(assembly, ArtifactPropertyKeys.StagedBuildOutputPath).Should().BeNull();
        }
        finally
        {
            if (Directory.Exists(stagingRoot))
            {
                Directory.Delete(stagingRoot, recursive: true);
            }
        }
    }

    private static string? GetProperty(FamilyArtifact artifact, string key) =>
        artifact.Properties is not null && artifact.Properties.TryGetValue(key, out var value) ? value : null;

    private static void DeleteGeneratedSourceOutputs(params string[] projectDirectories)
    {
        foreach (var projectDirectory in projectDirectories)
        {
            foreach (var child in new[] { "bin", "obj" })
            {
                var path = Path.Combine(projectDirectory, child);
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }
            }
        }
    }
}
