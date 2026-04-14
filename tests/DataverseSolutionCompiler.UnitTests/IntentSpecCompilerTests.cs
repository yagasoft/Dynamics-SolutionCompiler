using FluentAssertions;
using System.Text.Json.Nodes;
using DataverseSolutionCompiler.Compiler;
using DataverseSolutionCompiler.Diff;
using DataverseSolutionCompiler.Domain.Compilation;
using DataverseSolutionCompiler.Domain.Diff;
using DataverseSolutionCompiler.Domain.Emission;
using DataverseSolutionCompiler.Domain.Live;
using DataverseSolutionCompiler.Domain.Model;
using DataverseSolutionCompiler.Domain.Planning;
using DataverseSolutionCompiler.Emitters.TrackedSource;
using DataverseSolutionCompiler.Emitters.Package;
using DataverseSolutionCompiler.Readers.Xml;
using Xunit;

namespace DataverseSolutionCompiler.UnitTests;

public sealed class IntentSpecCompilerTests
{
    private static readonly string IntentFixturePath = Path.Combine(
        "C:\\Git\\Dataverse-Solution-KB",
        "fixtures",
        "intent-specs",
        "seed-greenfield-v1.json");

    private static readonly string SeedCorePath = Path.Combine(
        "C:\\Git\\Dataverse-Solution-KB",
        "fixtures",
        "skill-corpus",
        "examples",
        "seed-core",
        "unpacked");

    [Fact]
    public void Compile_reads_json_intent_fixture_into_canonical_solution()
    {
        var result = new CompilerKernel().Compile(new CompilationRequest(IntentFixturePath, Array.Empty<string>()));

        result.Success.Should().BeTrue();
        result.Solution.Identity.UniqueName.Should().Be("CodexMetadataIntentV1");
        result.Diagnostics.Should().Contain(diagnostic => diagnostic.Code == "source-kind-detected" && diagnostic.Message.Contains("IntentSpecJson", StringComparison.Ordinal));
        result.Solution.Artifacts.Should().Contain(artifact => artifact.Family == ComponentFamily.Publisher && artifact.Evidence == EvidenceKind.Derived);
        result.Solution.Artifacts.Should().Contain(artifact => artifact.Family == ComponentFamily.Table && artifact.LogicalName == "cdxmeta_workitem");
        result.Solution.Artifacts.Should().Contain(artifact => artifact.Family == ComponentFamily.Relationship && artifact.LogicalName == "cdxmeta_category_workitem");
        result.Solution.Artifacts.Should().Contain(artifact => artifact.Family == ComponentFamily.OptionSet && artifact.LogicalName == "cdxmeta_priorityband");
        result.Solution.Artifacts.Should().Contain(artifact => artifact.Family == ComponentFamily.OptionSet && artifact.LogicalName == "cdxmeta_workitem|cdxmeta_stage");
        result.Solution.Artifacts.Should().Contain(artifact => artifact.Family == ComponentFamily.Key && artifact.LogicalName == "cdxmeta_workitem|cdxmeta_workitem_externalcode");
        result.Solution.Artifacts.Should().Contain(artifact =>
            artifact.Family == ComponentFamily.Table
            && artifact.LogicalName == "cdxmeta_workitem"
            && artifact.Properties![ArtifactPropertyKeys.IsCustomizable] == "true");
        result.Solution.Artifacts.Should().Contain(artifact => artifact.Family == ComponentFamily.Form && artifact.DisplayName == "Work Item Main");
        result.Solution.Artifacts.Should().Contain(artifact => artifact.Family == ComponentFamily.View && artifact.DisplayName == "Active Work Items");
        result.Solution.Artifacts.Should().Contain(artifact => artifact.Family == ComponentFamily.AppModule && artifact.LogicalName == "codex_metadata_intent_shell");
        result.Solution.Artifacts.Should().Contain(artifact => artifact.Family == ComponentFamily.SiteMap && artifact.LogicalName == "codex_metadata_intent_shell");
        result.Solution.Artifacts.Should().Contain(artifact => artifact.Family == ComponentFamily.EnvironmentVariableDefinition && artifact.LogicalName == "cdxmeta_AppShellMode");
        result.Solution.Artifacts.Should().Contain(artifact => artifact.Family == ComponentFamily.EnvironmentVariableValue && artifact.LogicalName == "cdxmeta_AppShellMode");
    }

    [Fact]
    public void Compile_reports_validation_errors_for_invalid_json_intent()
    {
        var invalidPath = Path.Combine(Path.GetTempPath(), $"dsc-intent-invalid-{Guid.NewGuid():N}.json");

        try
        {
            File.WriteAllText(
                invalidPath,
                """
                {
                  "specVersion": "1.0",
                  "solution": {
                    "uniqueName": "BrokenIntent",
                    "displayName": "Broken Intent",
                    "version": "1.0.0.0",
                    "layeringIntent": "UnmanagedDevelopment"
                  },
                  "publisher": {
                    "uniqueName": "BrokenPublisher",
                    "prefix": "brk",
                    "displayName": "Broken Publisher"
                  },
                  "tables": [
                    {
                      "logicalName": "brk_sample",
                      "schemaName": "brk_Sample",
                      "displayName": "Sample",
                      "columns": [],
                      "forms": [
                        {
                          "name": "Unsupported Quick Form",
                          "type": "quick",
                          "tabs": []
                        }
                      ]
                    }
                  ],
                  "unsupportedTopLevel": true
                }
                """);

            var result = new CompilerKernel().Compile(new CompilationRequest(invalidPath, Array.Empty<string>()));

            result.Success.Should().BeFalse();
            result.Diagnostics.Should().Contain(diagnostic => diagnostic.Code == "intent-spec-validation");
            result.Diagnostics.Should().Contain(diagnostic => diagnostic.Message.Contains("$.tables[0].forms[0].type", StringComparison.Ordinal));
            result.Diagnostics.Should().Contain(diagnostic => diagnostic.Message.Contains("unsupportedTopLevel", StringComparison.Ordinal));
        }
        finally
        {
            if (File.Exists(invalidPath))
            {
                File.Delete(invalidPath);
            }
        }
    }

    [Fact]
    public void Compile_reports_validation_errors_for_unknown_key_attribute()
    {
        var result = CompileInlineIntent(CreateMinimalIntentJson(
            """
            [
              {
                "logicalName": "cdxmeta_workitem_externalcode",
                "schemaName": "cdxmeta_WorkItem_ExternalCode",
                "keyAttributes": [ "cdxmeta_missing" ]
              }
            ]
            """));

        result.Success.Should().BeFalse();
        result.Diagnostics.Should().Contain(diagnostic =>
            diagnostic.Code == "intent-spec-validation"
            && diagnostic.Message.Contains("references unknown field 'cdxmeta_missing'", StringComparison.Ordinal));
    }

    [Fact]
    public void Compile_reports_validation_errors_for_duplicate_key_names()
    {
        var result = CompileInlineIntent(CreateMinimalIntentJson(
            """
            [
              {
                "logicalName": "cdxmeta_workitem_externalcode",
                "schemaName": "cdxmeta_WorkItem_ExternalCode",
                "keyAttributes": [ "cdxmeta_externalcode" ]
              },
              {
                "logicalName": "cdxmeta_workitem_externalcode",
                "schemaName": "cdxmeta_WorkItem_ExternalCode_Copy",
                "keyAttributes": [ "cdxmeta_externalcode" ]
              }
            ]
            """));

        result.Success.Should().BeFalse();
        result.Diagnostics.Should().Contain(diagnostic =>
            diagnostic.Code == "intent-spec-validation"
            && diagnostic.Message.Contains("Duplicate key logical name 'cdxmeta_workitem_externalcode'", StringComparison.Ordinal));
    }

    [Fact]
    public void Compile_reports_validation_errors_for_empty_key_attributes()
    {
        var result = CompileInlineIntent(CreateMinimalIntentJson(
            """
            [
              {
                "logicalName": "cdxmeta_workitem_externalcode",
                "schemaName": "cdxmeta_WorkItem_ExternalCode",
                "keyAttributes": []
              }
            ]
            """));

        result.Success.Should().BeFalse();
        result.Diagnostics.Should().Contain(diagnostic =>
            diagnostic.Code == "intent-spec-validation"
            && diagnostic.Message.Contains("Each key requires at least one keyAttributes entry", StringComparison.Ordinal));
    }

    [Fact]
    public void Compile_reports_validation_errors_for_duplicate_key_attributes()
    {
        var result = CompileInlineIntent(CreateMinimalIntentJson(
            """
            [
              {
                "logicalName": "cdxmeta_workitem_externalcode",
                "schemaName": "cdxmeta_WorkItem_ExternalCode",
                "keyAttributes": [ "cdxmeta_externalcode", "cdxmeta_externalcode" ]
              }
            ]
            """));

        result.Success.Should().BeFalse();
        result.Diagnostics.Should().Contain(diagnostic =>
            diagnostic.Code == "intent-spec-validation"
            && diagnostic.Message.Contains("Duplicate key attribute 'cdxmeta_externalcode'", StringComparison.Ordinal));
    }

    [Fact]
    public void Compile_reports_validation_errors_when_key_references_primary_name_column()
    {
        var result = CompileInlineIntent(CreateMinimalIntentJson(
            """
            [
              {
                "logicalName": "cdxmeta_workitem_namekey",
                "schemaName": "cdxmeta_WorkItem_NameKey",
                "keyAttributes": [ "cdxmeta_workitemname" ]
              }
            ]
            """));

        result.Success.Should().BeFalse();
        result.Diagnostics.Should().Contain(diagnostic =>
            diagnostic.Code == "intent-spec-validation"
            && diagnostic.Message.Contains("cannot reference autogenerated primary id or primary name columns", StringComparison.Ordinal));
    }

    [Fact]
    public void PackageEmitter_synthesizes_package_inputs_from_json_intent()
    {
        var model = new CompilerKernel().Compile(new CompilationRequest(IntentFixturePath, Array.Empty<string>())).Solution;
        var outputRoot = Path.Combine(Path.GetTempPath(), $"dsc-intent-package-{Guid.NewGuid():N}");

        try
        {
            var emitted = new PackageEmitter().Emit(model, new EmitRequest(outputRoot, EmitLayout.PackageInputs));

            emitted.Success.Should().BeTrue();
            File.Exists(Path.Combine(outputRoot, "package-inputs", "Other", "Solution.xml")).Should().BeTrue();
            File.Exists(Path.Combine(outputRoot, "package-inputs", "Entities", "cdxmeta_WorkItem", "Entity.xml")).Should().BeTrue();
            Directory.Exists(Path.Combine(outputRoot, "package-inputs", "Entities", "cdxmeta_WorkItem", "FormXml", "main")).Should().BeTrue();
            Directory.Exists(Path.Combine(outputRoot, "package-inputs", "Entities", "cdxmeta_WorkItem", "SavedQueries")).Should().BeTrue();
            File.Exists(Path.Combine(outputRoot, "package-inputs", "OptionSets", "cdxmeta_priorityband.xml")).Should().BeTrue();
            File.Exists(Path.Combine(outputRoot, "package-inputs", "Other", "Relationships", "cdxmeta_category.xml")).Should().BeTrue();
            File.Exists(Path.Combine(outputRoot, "package-inputs", "AppModules", "codex_metadata_intent_shell", "AppModule.xml")).Should().BeTrue();
            File.Exists(Path.Combine(outputRoot, "package-inputs", "AppModuleSiteMaps", "codex_metadata_intent_shell", "AppModuleSiteMap.xml")).Should().BeTrue();
            File.Exists(Path.Combine(outputRoot, "package-inputs", "environmentvariabledefinitions", "cdxmeta_AppShellMode", "environmentvariabledefinition.xml")).Should().BeTrue();
            File.ReadAllText(Path.Combine(outputRoot, "package-inputs", "Entities", "cdxmeta_WorkItem", "Entity.xml")).Should().Contain("<KeyAttribute>cdxmeta_externalcode</KeyAttribute>");
            File.ReadAllText(Path.Combine(outputRoot, "package-inputs", "manifest.json")).Should().Contain("\"sourceLayout\": \"intent-spec-derived\"");
        }
        finally
        {
            if (Directory.Exists(outputRoot))
            {
                Directory.Delete(outputRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void Json_intent_round_trips_through_generated_package_inputs_without_blocking_drift()
    {
        var compiled = new CompilerKernel().Compile(new CompilationRequest(IntentFixturePath, Array.Empty<string>()));
        var outputRoot = Path.Combine(Path.GetTempPath(), $"dsc-intent-roundtrip-{Guid.NewGuid():N}");

        try
        {
            var emitted = new PackageEmitter().Emit(compiled.Solution, new EmitRequest(outputRoot, EmitLayout.PackageInputs));
            emitted.Success.Should().BeTrue();

            var reread = new XmlSolutionReader().Read(new Domain.Read.ReadRequest(Path.Combine(outputRoot, "package-inputs")));
            reread.Artifacts.Should().Contain(artifact => artifact.Family == ComponentFamily.Key && artifact.LogicalName == "cdxmeta_workitem|cdxmeta_workitem_externalcode");
            reread.Artifacts.Should().Contain(artifact =>
                artifact.Family == ComponentFamily.Table
                && artifact.LogicalName == "cdxmeta_workitem"
                && artifact.Properties![ArtifactPropertyKeys.IsCustomizable] == "true");
            var snapshot = new LiveSnapshot(
                new EnvironmentProfile("roundtrip"),
                compiled.Solution.Identity.UniqueName,
                reread.Artifacts,
                reread.Diagnostics);
            var report = new StableOverlapDriftComparer().Compare(compiled.Solution, snapshot, new CompareRequest());

            report.HasBlockingDrift.Should().BeFalse();
            report.Findings.Should().BeEmpty();
        }
        finally
        {
            if (Directory.Exists(outputRoot))
            {
                Directory.Delete(outputRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void Compile_reads_tracked_source_subset_into_supported_canonical_solution()
    {
        var trackedSourceRoot = Path.Combine(Path.GetTempPath(), $"dsc-tracked-source-subset-{Guid.NewGuid():N}");

        try
        {
            var compiled = new CompilerKernel().Compile(new CompilationRequest(IntentFixturePath, Array.Empty<string>()));
            new TrackedSourceEmitter().Emit(compiled.Solution, new EmitRequest(trackedSourceRoot, EmitLayout.TrackedSource)).Success.Should().BeTrue();

            var reread = new CompilerKernel().Compile(new CompilationRequest(Path.Combine(trackedSourceRoot, "tracked-source"), Array.Empty<string>()));

            reread.Success.Should().BeTrue();
            reread.Diagnostics.Should().Contain(diagnostic => diagnostic.Code == "source-kind-detected" && diagnostic.Message.Contains("TrackedSource", StringComparison.Ordinal));
            reread.Solution.Artifacts.Should().Contain(artifact => artifact.Family == ComponentFamily.Table && artifact.LogicalName == "cdxmeta_workitem");
            reread.Solution.Artifacts.Should().Contain(artifact => artifact.Family == ComponentFamily.Column && artifact.LogicalName == "cdxmeta_workitem|cdxmeta_externalcode");
            reread.Solution.Artifacts.Should().Contain(artifact => artifact.Family == ComponentFamily.Key && artifact.LogicalName == "cdxmeta_workitem|cdxmeta_workitem_externalcode");
            reread.Solution.Artifacts.Should().Contain(artifact => artifact.Family == ComponentFamily.Form && artifact.DisplayName == "Work Item Main");
            reread.Solution.Artifacts.Should().Contain(artifact => artifact.Family == ComponentFamily.View && artifact.DisplayName == "Active Work Items");
            reread.Solution.Artifacts.Should().Contain(artifact => artifact.Family == ComponentFamily.AppModule && artifact.LogicalName == "codex_metadata_intent_shell");
            reread.Solution.Artifacts.Should().Contain(artifact => artifact.Family == ComponentFamily.SiteMap && artifact.LogicalName == "codex_metadata_intent_shell");
            reread.Solution.Artifacts.Should().Contain(artifact => artifact.Family == ComponentFamily.EnvironmentVariableDefinition && artifact.LogicalName == "cdxmeta_AppShellMode");
        }
        finally
        {
            if (Directory.Exists(trackedSourceRoot))
            {
                Directory.Delete(trackedSourceRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void Tracked_source_can_reverse_generate_intent_and_round_trip_without_blocking_drift()
    {
        var trackedSourceRoot = Path.Combine(Path.GetTempPath(), $"dsc-tracked-source-reverse-{Guid.NewGuid():N}");
        var intentOutputRoot = Path.Combine(Path.GetTempPath(), $"dsc-reverse-intent-out-{Guid.NewGuid():N}");
        var packageOutputRoot = Path.Combine(Path.GetTempPath(), $"dsc-reverse-package-out-{Guid.NewGuid():N}");

        try
        {
            var original = new CompilerKernel().Compile(new CompilationRequest(IntentFixturePath, Array.Empty<string>()));
            new TrackedSourceEmitter().Emit(original.Solution, new EmitRequest(trackedSourceRoot, EmitLayout.TrackedSource)).Success.Should().BeTrue();

            var tracked = new CompilerKernel().Compile(new CompilationRequest(Path.Combine(trackedSourceRoot, "tracked-source"), Array.Empty<string>()));
            tracked.Success.Should().BeTrue();

            var reverseEmit = new IntentSpecEmitter().Emit(tracked.Solution, new EmitRequest(intentOutputRoot, EmitLayout.IntentSpec));
            reverseEmit.Success.Should().BeTrue();

            var reversedIntentPath = Path.Combine(intentOutputRoot, "intent-spec", "intent-spec.json");
            File.Exists(reversedIntentPath).Should().BeTrue();

            var reversed = new CompilerKernel().Compile(new CompilationRequest(reversedIntentPath, Array.Empty<string>()));
            reversed.Success.Should().BeTrue();

            var packageEmit = new PackageEmitter().Emit(reversed.Solution, new EmitRequest(packageOutputRoot, EmitLayout.PackageInputs));
            packageEmit.Success.Should().BeTrue();

            var reread = new XmlSolutionReader().Read(new Domain.Read.ReadRequest(Path.Combine(packageOutputRoot, "package-inputs")));
            var snapshot = new LiveSnapshot(
                new EnvironmentProfile("reverse-roundtrip"),
                reversed.Solution.Identity.UniqueName,
                reread.Artifacts,
                reread.Diagnostics);
            var report = new StableOverlapDriftComparer().Compare(reversed.Solution, snapshot, new CompareRequest());

            report.HasBlockingDrift.Should().BeFalse();
            report.Findings.Should().BeEmpty();

            var reverseDocument = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(reversedIntentPath))!.AsObject();
            reverseDocument["tables"]!.AsArray()[0]!["forms"]!.AsArray()[0]!["id"]!.GetValue<string>().Should().NotBeNullOrWhiteSpace();
            reverseDocument["tables"]!.AsArray()[0]!["views"]!.AsArray()[0]!["id"]!.GetValue<string>().Should().NotBeNullOrWhiteSpace();
        }
        finally
        {
            if (Directory.Exists(trackedSourceRoot))
            {
                Directory.Delete(trackedSourceRoot, recursive: true);
            }

            if (Directory.Exists(intentOutputRoot))
            {
                Directory.Delete(intentOutputRoot, recursive: true);
            }

            if (Directory.Exists(packageOutputRoot))
            {
                Directory.Delete(packageOutputRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void Reverse_generation_report_classifies_platform_generated_views_explicitly()
    {
        var outputRoot = Path.Combine(Path.GetTempPath(), $"dsc-intent-report-{Guid.NewGuid():N}");

        try
        {
            var compiled = new CompilerKernel().Compile(new CompilationRequest(SeedCorePath, Array.Empty<string>()));

            var emitted = new IntentSpecEmitter().Emit(compiled.Solution, new EmitRequest(outputRoot, EmitLayout.IntentSpec));

            emitted.Success.Should().BeTrue();
            var reportPath = Path.Combine(outputRoot, "intent-spec", "reverse-generation-report.json");
            var report = JsonNode.Parse(File.ReadAllText(reportPath))!.AsObject();

            report["isPartial"]!.GetValue<bool>().Should().BeTrue();
            report["unsupportedFamiliesOmitted"]!.AsArray()
                .Any(entry => string.Equals(entry?["category"]?.GetValue<string>(), "platformGeneratedArtifact", StringComparison.Ordinal))
                .Should()
                .BeTrue();
        }
        finally
        {
            if (Directory.Exists(outputRoot))
            {
                Directory.Delete(outputRoot, recursive: true);
            }
        }
    }

    private static CompilationResult CompileInlineIntent(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), $"dsc-intent-inline-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, json);
            return new CompilerKernel().Compile(new CompilationRequest(path, Array.Empty<string>()));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static string CreateMinimalIntentJson(string keysJson) =>
        $$"""
        {
          "specVersion": "1.0",
          "solution": {
            "uniqueName": "KeyValidationIntent",
            "displayName": "Key Validation Intent",
            "version": "1.0.0.0",
            "layeringIntent": "UnmanagedDevelopment"
          },
          "publisher": {
            "uniqueName": "CodexMetadata",
            "prefix": "cdxmeta",
            "displayName": "Codex Metadata"
          },
          "tables": [
            {
              "logicalName": "cdxmeta_workitem",
              "schemaName": "cdxmeta_WorkItem",
              "displayName": "Work Item",
              "columns": [
                {
                  "logicalName": "cdxmeta_externalcode",
                  "schemaName": "cdxmeta_ExternalCode",
                  "displayName": "External Code",
                  "type": "string"
                }
              ],
              "keys": {{keysJson}},
              "forms": [],
              "views": []
            }
          ]
        }
        """;
}
