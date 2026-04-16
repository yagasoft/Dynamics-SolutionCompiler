using DataverseSolutionCompiler.Domain.Build;
using DataverseSolutionCompiler.Domain.Model;
using DataverseSolutionCompiler.Domain.Read;
using DataverseSolutionCompiler.Readers.Code;
using FluentAssertions;
using Xunit;

namespace DataverseSolutionCompiler.UnitTests;

public sealed class CodeFirstSdkRegistrationReaderTests
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
    private static readonly string SeedCodePluginImperativePath = Path.Combine(
        "C:\\Git\\Dataverse-Solution-KB",
        "fixtures",
        "skill-corpus",
        "examples",
        "seed-code-plugin-imperative");
    private static readonly string SeedCodePluginHelperPath = Path.Combine(
        "C:\\Git\\Dataverse-Solution-KB",
        "fixtures",
        "skill-corpus",
        "examples",
        "seed-code-plugin-helper");
    private static readonly string SeedCodePluginImperativeServicePath = Path.Combine(
        "C:\\Git\\Dataverse-Solution-KB",
        "fixtures",
        "skill-corpus",
        "examples",
        "seed-code-plugin-imperative-service");
    private static readonly string SeedCodeWorkflowActivityClassicPath = Path.Combine(
        "C:\\Git\\Dataverse-Solution-KB",
        "fixtures",
        "skill-corpus",
        "examples",
        "seed-code-workflow-activity-classic");

    [Fact]
    public void Reader_projects_the_classic_code_first_seed_into_canonical_plugin_artifacts()
    {
        var reader = new CodeFirstSdkRegistrationReader();

        var solution = reader.Read(new ReadRequest(SeedCodePluginClassicPath, ReadSourceKind.CodeFirstSdkRegistration));

        solution.Diagnostics.Should().NotContain(diagnostic => diagnostic.Severity == Domain.Diagnostics.DiagnosticSeverity.Error);
        solution.Artifacts.Should().ContainSingle(artifact => artifact.Family == ComponentFamily.PluginAssembly);
        solution.Artifacts.Should().ContainSingle(artifact => artifact.Family == ComponentFamily.PluginType);
        solution.Artifacts.Should().ContainSingle(artifact => artifact.Family == ComponentFamily.PluginStep);
        solution.Artifacts.Should().ContainSingle(artifact => artifact.Family == ComponentFamily.PluginStepImage);

        var assembly = solution.Artifacts.Single(artifact => artifact.Family == ComponentFamily.PluginAssembly);
        assembly.LogicalName.Should().Be("Codex.Metadata.CodeFirst.Classic, Version=1.0.0.0, Culture=neutral, PublicKeyToken=9d006cbbfeff5098");
        GetProperty(assembly, ArtifactPropertyKeys.DeploymentFlavor).Should().Be(nameof(CodeAssetDeploymentFlavor.ClassicAssembly));
        GetProperty(assembly, ArtifactPropertyKeys.CodeProjectPath).Should().Be("plugins/Codex.Metadata.CodeFirst.Classic/Codex.Metadata.CodeFirst.Classic.csproj");
        GetProperty(assembly, ArtifactPropertyKeys.PackageId).Should().Be("Codex.Metadata.CodeFirst.Classic");
        GetProperty(assembly, ArtifactPropertyKeys.PackageVersion).Should().Be("1.0.0.0");
        GetProperty(assembly, ArtifactPropertyKeys.AssetSourceMapJson).Should().Contain("PluginRegistration.cs");
        GetProperty(assembly, ArtifactPropertyKeys.AssetSourceMapJson).Should().Contain("Codex.Metadata.CodeFirst.Classic.csproj");

        var pluginType = solution.Artifacts.Single(artifact => artifact.Family == ComponentFamily.PluginType);
        GetProperty(pluginType, ArtifactPropertyKeys.PluginTypeKind).Should().Be("plugin");
    }

    [Fact]
    public void Reader_projects_the_package_code_first_seed_with_package_metadata_and_dependency_assets()
    {
        var reader = new CodeFirstSdkRegistrationReader();

        var solution = reader.Read(new ReadRequest(SeedCodePluginPackagePath, ReadSourceKind.CodeFirstSdkRegistration));

        solution.Diagnostics.Should().NotContain(diagnostic => diagnostic.Severity == Domain.Diagnostics.DiagnosticSeverity.Error);
        var assembly = solution.Artifacts.Single(artifact => artifact.Family == ComponentFamily.PluginAssembly);
        GetProperty(assembly, ArtifactPropertyKeys.DeploymentFlavor).Should().Be(nameof(CodeAssetDeploymentFlavor.PluginPackage));
        GetProperty(assembly, ArtifactPropertyKeys.PackageId).Should().Be("Codex.Metadata.CodeFirst.Package");
        GetProperty(assembly, ArtifactPropertyKeys.PackageUniqueName).Should().Be("codex_codefirst_package");
        GetProperty(assembly, ArtifactPropertyKeys.AssetSourceMapJson).Should().Contain("Codex.Metadata.CodeFirst.Package.Dependency.csproj");
        GetProperty(assembly, ArtifactPropertyKeys.AssetSourceMapJson).Should().Contain("ProofMarkerProvider.cs");
    }

    [Fact]
    public void Reader_projects_the_imperative_code_first_seed_into_canonical_plugin_artifacts()
    {
        var reader = new CodeFirstSdkRegistrationReader();

        var solution = reader.Read(new ReadRequest(SeedCodePluginImperativePath, ReadSourceKind.CodeFirstSdkRegistration));

        solution.Diagnostics.Should().NotContain(diagnostic => diagnostic.Severity == Domain.Diagnostics.DiagnosticSeverity.Error);
        solution.Artifacts.Should().ContainSingle(artifact => artifact.Family == ComponentFamily.PluginAssembly);
        solution.Artifacts.Should().ContainSingle(artifact => artifact.Family == ComponentFamily.PluginType);
        solution.Artifacts.Should().ContainSingle(artifact => artifact.Family == ComponentFamily.PluginStep);
        solution.Artifacts.Should().ContainSingle(artifact => artifact.Family == ComponentFamily.PluginStepImage);

        var pluginType = solution.Artifacts.Single(artifact => artifact.Family == ComponentFamily.PluginType);
        GetProperty(pluginType, ArtifactPropertyKeys.PluginTypeKind).Should().Be("plugin");

        var step = solution.Artifacts.Single(artifact => artifact.Family == ComponentFamily.PluginStep);
        step.LogicalName.Should().Be("Codex.Metadata.CodeFirst.Imperative.AccountCreateDescriptionPlugin|Create|account|20|0|Imperative Account Create Description Stamp");

        var image = solution.Artifacts.Single(artifact => artifact.Family == ComponentFamily.PluginStepImage);
        image.LogicalName.Should().Be("Codex.Metadata.CodeFirst.Imperative.AccountCreateDescriptionPlugin|Create|account|20|0|Imperative Account Create Description Stamp|Imperative Account Step Image|postimage|1");
        GetProperty(image, ArtifactPropertyKeys.MessagePropertyName).Should().Be("Id");
    }

    [Fact]
    public void Reader_projects_helper_based_registration_collections_and_marks_mixed_workflow_activity_types()
    {
        var reader = new CodeFirstSdkRegistrationReader();

        var solution = reader.Read(new ReadRequest(SeedCodePluginHelperPath, ReadSourceKind.CodeFirstSdkRegistration));

        solution.Diagnostics.Should().NotContain(diagnostic => diagnostic.Severity == Domain.Diagnostics.DiagnosticSeverity.Error);
        solution.Artifacts.Should().ContainSingle(artifact => artifact.Family == ComponentFamily.PluginAssembly);
        solution.Artifacts.Count(artifact => artifact.Family == ComponentFamily.PluginType).Should().Be(2);
        solution.Artifacts.Should().ContainSingle(artifact => artifact.Family == ComponentFamily.PluginStep);
        solution.Artifacts.Should().ContainSingle(artifact => artifact.Family == ComponentFamily.PluginStepImage);

        solution.Artifacts.Should().Contain(artifact =>
            artifact.Family == ComponentFamily.PluginType
            && artifact.LogicalName == "Codex.Metadata.CodeFirst.Helper.AccountCreateDescriptionPlugin"
            && GetProperty(artifact, ArtifactPropertyKeys.PluginTypeKind) == "plugin");
        solution.Artifacts.Should().Contain(artifact =>
            artifact.Family == ComponentFamily.PluginType
            && artifact.LogicalName == "Codex.Metadata.CodeFirst.Helper.AccountDescriptionActivity"
            && GetProperty(artifact, ArtifactPropertyKeys.PluginTypeKind) == "customWorkflowActivity");

        var step = solution.Artifacts.Single(artifact => artifact.Family == ComponentFamily.PluginStep);
        step.LogicalName.Should().Be("Codex.Metadata.CodeFirst.Helper.AccountCreateDescriptionPlugin|Create|account|20|0|Account Create Description Helper Stamp");

        var image = solution.Artifacts.Single(artifact => artifact.Family == ComponentFamily.PluginStepImage);
        image.LogicalName.Should().Be("Codex.Metadata.CodeFirst.Helper.AccountCreateDescriptionPlugin|Create|account|20|0|Account Create Description Helper Stamp|Account Helper Post Image|postimage|1");
    }

    [Fact]
    public void Reader_projects_service_aware_imperative_get_message_shape_into_canonical_plugin_artifacts()
    {
        var reader = new CodeFirstSdkRegistrationReader();

        var solution = reader.Read(new ReadRequest(SeedCodePluginImperativeServicePath, ReadSourceKind.CodeFirstSdkRegistration));

        solution.Diagnostics.Should().NotContain(diagnostic => diagnostic.Severity == Domain.Diagnostics.DiagnosticSeverity.Error);
        solution.Artifacts.Should().ContainSingle(artifact => artifact.Family == ComponentFamily.PluginAssembly);
        solution.Artifacts.Should().ContainSingle(artifact => artifact.Family == ComponentFamily.PluginType);
        solution.Artifacts.Should().ContainSingle(artifact => artifact.Family == ComponentFamily.PluginStep);
        solution.Artifacts.Should().ContainSingle(artifact => artifact.Family == ComponentFamily.PluginStepImage);

        var step = solution.Artifacts.Single(artifact => artifact.Family == ComponentFamily.PluginStep);
        step.LogicalName.Should().Be("Codex.Metadata.CodeFirst.Imperative.Service.AccountCreateDescriptionPlugin|Create|account|20|0|Imperative Service Account Create Description Stamp");

        var image = solution.Artifacts.Single(artifact => artifact.Family == ComponentFamily.PluginStepImage);
        image.LogicalName.Should().Be("Codex.Metadata.CodeFirst.Imperative.Service.AccountCreateDescriptionPlugin|Create|account|20|0|Imperative Service Account Create Description Stamp|Imperative Service Account Step Image|postimage|1");
        GetProperty(image, ArtifactPropertyKeys.MessagePropertyName).Should().Be("Id");
    }

    [Fact]
    public void Reader_marks_code_activity_types_as_custom_workflow_activity_plugin_types()
    {
        var reader = new CodeFirstSdkRegistrationReader();

        var solution = reader.Read(new ReadRequest(SeedCodeWorkflowActivityClassicPath, ReadSourceKind.CodeFirstSdkRegistration));

        solution.Diagnostics.Should().NotContain(diagnostic => diagnostic.Severity == Domain.Diagnostics.DiagnosticSeverity.Error);
        solution.Artifacts.Should().ContainSingle(artifact => artifact.Family == ComponentFamily.PluginAssembly);
        solution.Artifacts.Should().ContainSingle(artifact => artifact.Family == ComponentFamily.PluginType);
        solution.Artifacts.Should().NotContain(artifact => artifact.Family == ComponentFamily.PluginStep);

        var pluginType = solution.Artifacts.Single(artifact => artifact.Family == ComponentFamily.PluginType);
        GetProperty(pluginType, ArtifactPropertyKeys.PluginTypeKind).Should().Be("customWorkflowActivity");
        GetProperty(pluginType, ArtifactPropertyKeys.WorkflowActivityGroupName).Should().Be("Codex.Metadata.CodeFirst.WorkflowActivity.Classic (1.0.0.0)");
    }

    [Fact]
    public void Reader_supports_bounded_common_code_first_idioms_without_falling_back_to_unsupported_shape()
    {
        var root = Path.Combine(Path.GetTempPath(), $"dsc-code-first-bounded-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var projectPath = Path.Combine(root, "BoundedIdioms.csproj");
            File.WriteAllText(projectPath,
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net462</TargetFramework>
                  </PropertyGroup>
                </Project>
                """);

            var registrationPath = Path.Combine(root, "BoundedIdiomsRegistration.cs");
            File.WriteAllText(registrationPath,
                """
                using System.Collections.Generic;
                using Microsoft.Xrm.Sdk;

                public enum StageValue
                {
                    PreOperation = 20
                }

                public static class RegistrationMarkers
                {
                    public const string StepEntityLogicalName = "sdkmessageprocessingstep";
                    public const string StepImageEntityLogicalName = "sdkmessageprocessingstepimage";
                }

                public static class BoundValues
                {
                    public const string EntityName = "account";
                    public static readonly string AssemblyName = "Bounded.Idioms, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";
                    public static readonly string HandlerName = "Bounded.Idioms.AccountPlugin";
                }

                public static class BoundedIdiomsRegistration
                {
                    public static readonly DbmPluginAssemblyRegistration Assembly = CreateAssembly();

                    private static DbmPluginAssemblyRegistration CreateAssembly() => new DbmPluginAssemblyRegistration
                    {
                        AssemblyFullName = BoundValues.AssemblyName,
                        Types = BuildTypes(),
                        Steps = BuildSteps()
                    };

                    private static DbmPluginTypeRegistration[] BuildTypes() =>
                    [
                        new DbmPluginTypeRegistration
                        {
                            LogicalName = BoundValues.HandlerName,
                            FriendlyName = $"{nameof(BoundedIdiomsRegistration)} Handler",
                            Description = BoundValues.EntityName switch
                            {
                                "account" => "Bounded idiom description",
                                _ => "Other"
                            },
                            AssemblyQualifiedName = $"{BoundValues.HandlerName}, Bounded.Idioms, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null"
                        }
                    ];

                    private static IEnumerable<DbmPluginStepRegistration> BuildSteps()
                    {
                        yield return CreateStep(BoundValues.EntityName);
                    }

                    private static DbmPluginStepRegistration CreateStep(string primaryEntity) => new DbmPluginStepRegistration
                    {
                        Name = $"{nameof(BoundedIdiomsRegistration)} {primaryEntity switch { "account" => "Create", _ => "Other" }} Step",
                        HandlerPluginTypeName = BoundValues.HandlerName,
                        MessageName = "Create" switch
                        {
                            "Create" => "Create",
                            _ => "Update"
                        },
                        PrimaryEntity = primaryEntity,
                        Stage = new OptionSetValue(true
                            ? StageValue.PreOperation switch
                            {
                                StageValue.PreOperation => 20,
                                _ => 40
                            }
                            : 40),
                        Mode = new OptionSetValue(0),
                        Rank = 1,
                        SupportedDeployment = 0,
                        FilteringAttributes = "name",
                        Images = BuildImages()
                    };

                    private static IEnumerable<DbmPluginStepImageRegistration> BuildImages()
                    {
                        yield return new DbmPluginStepImageRegistration
                        {
                            Name = $"{nameof(BoundedIdiomsRegistration)} Image",
                            EntityAlias = "postimage",
                            ImageType = new OptionSetValue(1),
                            MessagePropertyName = "Target",
                            SelectedAttributes = $"name,{"description"}"
                        };
                    }
                }

                public sealed class DbmPluginAssemblyRegistration
                {
                    public string AssemblyFullName { get; set; }
                    public DbmPluginTypeRegistration[] Types { get; set; }
                    public IEnumerable<DbmPluginStepRegistration> Steps { get; set; }
                }

                public sealed class DbmPluginTypeRegistration
                {
                    public string LogicalName { get; set; }
                    public string FriendlyName { get; set; }
                    public string Description { get; set; }
                    public string AssemblyQualifiedName { get; set; }
                }

                public sealed class DbmPluginStepRegistration
                {
                    public string Name { get; set; }
                    public string HandlerPluginTypeName { get; set; }
                    public string MessageName { get; set; }
                    public string PrimaryEntity { get; set; }
                    public OptionSetValue Stage { get; set; }
                    public OptionSetValue Mode { get; set; }
                    public int Rank { get; set; }
                    public int SupportedDeployment { get; set; }
                    public string FilteringAttributes { get; set; }
                    public IEnumerable<DbmPluginStepImageRegistration> Images { get; set; }
                }

                public sealed class DbmPluginStepImageRegistration
                {
                    public string Name { get; set; }
                    public string EntityAlias { get; set; }
                    public OptionSetValue ImageType { get; set; }
                    public string MessagePropertyName { get; set; }
                    public string SelectedAttributes { get; set; }
                }
                """);

            var reader = new CodeFirstSdkRegistrationReader();
            var solution = reader.Read(new ReadRequest(root, ReadSourceKind.CodeFirstSdkRegistration));

            solution.Diagnostics.Should().NotContain(diagnostic => diagnostic.Severity == Domain.Diagnostics.DiagnosticSeverity.Error);
            solution.Diagnostics.Should().NotContain(diagnostic => diagnostic.Code == "code-first-registration-unsupported-string-expression");
            solution.Diagnostics.Should().NotContain(diagnostic => diagnostic.Code == "code-first-registration-unsupported-int-expression");
            solution.Artifacts.Should().ContainSingle(artifact => artifact.Family == ComponentFamily.PluginAssembly);
            solution.Artifacts.Should().ContainSingle(artifact => artifact.Family == ComponentFamily.PluginType);
            solution.Artifacts.Should().ContainSingle(artifact => artifact.Family == ComponentFamily.PluginStep);
            solution.Artifacts.Should().ContainSingle(artifact => artifact.Family == ComponentFamily.PluginStepImage);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void Reader_reports_unsupported_dynamic_string_values_with_file_line_provenance()
    {
        var root = Path.Combine(Path.GetTempPath(), $"dsc-code-first-reader-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var projectPath = Path.Combine(root, "UnsupportedRegistration.csproj");
            File.WriteAllText(projectPath,
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net462</TargetFramework>
                  </PropertyGroup>
                </Project>
                """);

            var registrationPath = Path.Combine(root, "UnsupportedRegistration.cs");
            File.WriteAllText(registrationPath,
                """
                using Microsoft.Xrm.Sdk;

                public static class UnsupportedRegistration
                {
                    private const string StepEntityLogicalName = "sdkmessageprocessingstep";
                    private const string StepImageEntityLogicalName = "sdkmessageprocessingstepimage";

                    public static readonly DbmPluginAssemblyRegistration Assembly = new DbmPluginAssemblyRegistration
                    {
                        AssemblyFullName = "Unsupported.Plugin, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
                        Types =
                        [
                            new DbmPluginTypeRegistration
                            {
                                LogicalName = "Unsupported.Plugin.Handler",
                                Description = string.Join("-", "not", "supported")
                            }
                        ],
                        Steps = []
                    };
                }

                public sealed class DbmPluginAssemblyRegistration
                {
                    public string AssemblyFullName { get; set; }
                    public DbmPluginTypeRegistration[] Types { get; set; }
                    public DbmPluginStepRegistration[] Steps { get; set; }
                }

                public sealed class DbmPluginTypeRegistration
                {
                    public string LogicalName { get; set; }
                    public string Description { get; set; }
                }

                public sealed class DbmPluginStepRegistration { }
                """);

            var reader = new CodeFirstSdkRegistrationReader();
            var solution = reader.Read(new ReadRequest(root, ReadSourceKind.CodeFirstSdkRegistration));

            solution.Diagnostics.Should().Contain(diagnostic =>
                diagnostic.Code == "code-first-registration-unsupported-string-expression"
                && !string.IsNullOrWhiteSpace(diagnostic.Location)
                && diagnostic.Location.Contains("UnsupportedRegistration.cs:", StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static string? GetProperty(FamilyArtifact artifact, string key) =>
        artifact.Properties is not null && artifact.Properties.TryGetValue(key, out var value) ? value : null;
}
