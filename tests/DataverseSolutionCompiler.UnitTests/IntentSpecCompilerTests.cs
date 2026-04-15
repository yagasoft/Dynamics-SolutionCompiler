using FluentAssertions;
using System.Diagnostics;
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

    private static readonly string ExamplesRoot = Path.Combine(
        "C:\\Git\\Dataverse-Solution-KB",
        "fixtures",
        "skill-corpus",
        "examples");

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
                      "columns": [
                        {
                          "logicalName": "brk_lookupid",
                          "schemaName": "brk_LookupId",
                          "displayName": "Lookup",
                          "type": "lookup",
                          "targetTable": "brk_sample"
                        }
                      ],
                      "forms": [
                        {
                          "name": "Broken Quick Form",
                          "type": "quick",
                          "tabs": [
                            {
                              "name": "general",
                              "label": "General",
                              "sections": [
                                {
                                  "name": "main",
                                  "label": "Main",
                                  "controls": [
                                    {
                                      "kind": "unsupported-widget"
                                    }
                                  ]
                                }
                              ]
                            }
                          ]
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
    public void Compile_accepts_quick_card_and_control_rich_forms()
    {
        var intentJson =
            """
            {
              "specVersion": "1.0",
              "solution": {
                "uniqueName": "AdvancedFormIntent",
                "displayName": "Advanced Form Intent",
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
                  "logicalName": "cdxmeta_category",
                  "schemaName": "cdxmeta_Category",
                  "displayName": "Category",
                  "columns": [],
                  "forms": [
                    {
                      "name": "Category Quick",
                      "type": "quick",
                      "tabs": [
                        {
                          "name": "general",
                          "label": "General",
                          "sections": [
                            {
                              "name": "main",
                              "label": "Main",
                              "fields": [ "cdxmeta_categoryname" ]
                            }
                          ]
                        }
                      ]
                    }
                  ],
                  "views": []
                },
                {
                  "logicalName": "cdxmeta_workitem",
                  "schemaName": "cdxmeta_WorkItem",
                  "displayName": "Work Item",
                  "columns": [
                    {
                      "logicalName": "cdxmeta_categoryid",
                      "schemaName": "cdxmeta_CategoryId",
                      "displayName": "Category",
                      "type": "lookup",
                      "targetTable": "cdxmeta_category"
                    }
                  ],
                  "forms": [
                    {
                      "name": "Work Item Main",
                      "type": "main",
                      "tabs": [
                        {
                          "name": "summary",
                          "label": "Summary",
                          "sections": [
                            {
                              "name": "related",
                              "label": "Related",
                              "controls": [
                                {
                                  "kind": "field",
                                  "field": "cdxmeta_workitemname"
                                },
                                {
                                  "kind": "quickView",
                                  "field": "cdxmeta_categoryid",
                                  "quickFormEntity": "cdxmeta_category",
                                  "quickFormId": "5978624f-3b37-f111-88b3-0022489b9600",
                                  "controlMode": "Edit"
                                },
                                {
                                  "kind": "subgrid",
                                  "label": "Related Items",
                                  "relationshipName": "cdxmeta_workitem_children",
                                  "targetTable": "cdxmeta_category",
                                  "defaultViewId": "500a740d-e399-42c5-9f3a-0f9c203ef9cd",
                                  "enableViewPicker": true,
                                  "enableChartPicker": false,
                                  "recordsPerPage": 5
                                }
                              ]
                            }
                          ]
                        }
                      ],
                      "headerFields": [ "cdxmeta_categoryid" ]
                    },
                    {
                      "name": "Work Item Card",
                      "type": "card",
                      "tabs": [
                        {
                          "name": "card",
                          "label": "Card",
                          "sections": [
                            {
                              "name": "details",
                              "label": "Details",
                              "fields": [ "cdxmeta_workitemname", "cdxmeta_categoryid" ]
                            }
                          ]
                        }
                      ]
                    }
                  ],
                  "views": []
                }
              ]
            }
            """;

        var path = Path.Combine(Path.GetTempPath(), $"dsc-intent-advanced-forms-{Guid.NewGuid():N}.json");
        var outputRoot = Path.Combine(Path.GetTempPath(), $"dsc-intent-advanced-forms-out-{Guid.NewGuid():N}");

        try
        {
            File.WriteAllText(path, intentJson);
            var compiled = new CompilerKernel().Compile(new CompilationRequest(path, Array.Empty<string>()));
            compiled.Success.Should().BeTrue();
            compiled.Solution.Artifacts.Should().Contain(artifact => artifact.Family == ComponentFamily.Form && artifact.LogicalName.Contains("|quick|", StringComparison.Ordinal));
            compiled.Solution.Artifacts.Should().Contain(artifact => artifact.Family == ComponentFamily.Form && artifact.LogicalName.Contains("|card|", StringComparison.Ordinal));

            var emitted = new PackageEmitter().Emit(compiled.Solution, new EmitRequest(outputRoot, EmitLayout.PackageInputs));
            emitted.Success.Should().BeTrue();
            Directory.Exists(Path.Combine(outputRoot, "package-inputs", "Entities", "cdxmeta_Category", "FormXml", "quick")).Should().BeTrue();
            Directory.Exists(Path.Combine(outputRoot, "package-inputs", "Entities", "cdxmeta_WorkItem", "FormXml", "card")).Should().BeTrue();
            var mainFormFile = Directory.GetFiles(Path.Combine(outputRoot, "package-inputs", "Entities", "cdxmeta_WorkItem", "FormXml", "main"), "*.xml", SearchOption.TopDirectoryOnly).Single();
            File.ReadAllText(mainFormFile).Should().Contain("QuickForms");
            File.ReadAllText(mainFormFile).Should().Contain("RelationshipName");
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            if (Directory.Exists(outputRoot))
            {
                Directory.Delete(outputRoot, recursive: true);
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
            File.Exists(Path.Combine(outputRoot, "package-inputs", "Other", "Customizations.xml")).Should().BeTrue();
            File.Exists(Path.Combine(outputRoot, "package-inputs", "Entities", "cdxmeta_WorkItem", "Entity.xml")).Should().BeTrue();
            Directory.Exists(Path.Combine(outputRoot, "package-inputs", "Entities", "cdxmeta_WorkItem", "FormXml", "main")).Should().BeTrue();
            Directory.Exists(Path.Combine(outputRoot, "package-inputs", "Entities", "cdxmeta_WorkItem", "SavedQueries")).Should().BeTrue();
            File.Exists(Path.Combine(outputRoot, "package-inputs", "OptionSets", "cdxmeta_priorityband.xml")).Should().BeTrue();
            File.Exists(Path.Combine(outputRoot, "package-inputs", "Other", "Relationships", "cdxmeta_category.xml")).Should().BeTrue();
            File.Exists(Path.Combine(outputRoot, "package-inputs", "AppModules", "codex_metadata_intent_shell", "AppModule.xml")).Should().BeTrue();
            File.Exists(Path.Combine(outputRoot, "package-inputs", "AppModuleSiteMaps", "codex_metadata_intent_shell", "AppModuleSiteMap.xml")).Should().BeTrue();
            File.Exists(Path.Combine(outputRoot, "package-inputs", "environmentvariabledefinitions", "cdxmeta_AppShellMode", "environmentvariabledefinition.xml")).Should().BeTrue();
            File.ReadAllText(Path.Combine(outputRoot, "package-inputs", "Entities", "cdxmeta_WorkItem", "Entity.xml")).Should().Contain("<KeyAttribute>cdxmeta_externalcode</KeyAttribute>");
            File.ReadAllText(Path.Combine(outputRoot, "package-inputs", "Other", "Customizations.xml")).Should().Contain("<WebResources");
            File.ReadAllText(Path.Combine(outputRoot, "package-inputs", "Other", "Customizations.xml")).Should().Contain("<AppModuleSiteMaps");
            File.ReadAllText(Path.Combine(outputRoot, "package-inputs", "Other", "Customizations.xml")).Should().Contain("<AppModules");
            File.ReadAllText(Path.Combine(outputRoot, "package-inputs", "AppModules", "codex_metadata_intent_shell", "AppModule.xml")).Should().Contain("<WebResourceId>953b9fac-1e5e-e611-80d6-00155ded156f</WebResourceId>");
            File.ReadAllText(Path.Combine(outputRoot, "package-inputs", "AppModules", "codex_metadata_intent_shell", "AppModule.xml")).Should().Contain("<appsettings />");
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
            reverseDocument["appModules"]!.AsArray()[0]!["siteMap"]!["areas"]!.AsArray()[0]!["title"]!.GetValue<string>().Should().Be("Codex Metadata");
            reverseDocument["appModules"]!.AsArray()[0]!["siteMap"]!["areas"]!.AsArray()[0]!["groups"]!.AsArray()[0]!["title"]!.GetValue<string>().Should().Be("Work");
            reverseDocument["appModules"]!.AsArray()[0]!["siteMap"]!["areas"]!.AsArray()[0]!["groups"]!.AsArray()[0]!["subAreas"]!.AsArray()[0]!["title"]!.GetValue<string>().Should().Be("Work Items");
            reverseDocument["environmentVariables"]!.AsArray()[0]!["currentValue"]!.GetValue<string>().Should().Be("guided");
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

    [Theory]
    [InlineData("seed-process-policy", "DuplicateRule", "duplicaterules/dre67df5ba444cf6a6b4092b00952064b3b91ddc3e81f6d3746c2169ae4ed2c367/duplicaterule.xml")]
    [InlineData("seed-process-security", "Role", "Roles/Codex Metadata Seed Role.xml")]
    [InlineData("seed-plugin-registration", "PluginAssembly", "Other/Customizations.xml")]
    [InlineData("seed-service-endpoint-connector", "ServiceEndpoint", "ServiceEndpoints/codex_webhook_endpoint/ServiceEndpoint.xml")]
    [InlineData("seed-ai-families", "AiProjectType", "AIProjectTypes/document_automation/AIProjectType.xml")]
    [InlineData("seed-entity-analytics", "EntityAnalyticsConfiguration", "entityanalyticsconfigs/contact/entityanalyticsconfig.xml")]
    [InlineData("seed-environment", "CanvasApp", "CanvasApps/cat_overview_3dbf5.meta.xml")]
    public void Tracked_source_can_reverse_generate_source_backed_intent_and_rebuild_package_inputs(
        string seedName,
        string expectedFamily,
        string expectedPackageRelativePath)
    {
        var seedPath = Path.Combine(ExamplesRoot, seedName);
        var trackedSourceRoot = Path.Combine(Path.GetTempPath(), $"dsc-source-backed-tracked-{Guid.NewGuid():N}");
        var intentOutputRoot = Path.Combine(Path.GetTempPath(), $"dsc-source-backed-intent-{Guid.NewGuid():N}");
        var packageOutputRoot = Path.Combine(Path.GetTempPath(), $"dsc-source-backed-package-{Guid.NewGuid():N}");

        try
        {
            var original = new CompilerKernel().Compile(new CompilationRequest(seedPath, Array.Empty<string>()));
            original.Success.Should().BeTrue();

            new TrackedSourceEmitter().Emit(original.Solution, new EmitRequest(trackedSourceRoot, EmitLayout.TrackedSource)).Success.Should().BeTrue();

            var tracked = new CompilerKernel().Compile(new CompilationRequest(Path.Combine(trackedSourceRoot, "tracked-source"), Array.Empty<string>()));
            tracked.Success.Should().BeTrue();

            var reverseEmit = new IntentSpecEmitter().Emit(tracked.Solution, new EmitRequest(intentOutputRoot, EmitLayout.IntentSpec));
            reverseEmit.Success.Should().BeTrue();

            var reportPath = Path.Combine(intentOutputRoot, "intent-spec", "reverse-generation-report.json");
            File.Exists(reportPath).Should().BeTrue();
            var report = JsonNode.Parse(File.ReadAllText(reportPath))!.AsObject();
            report["isPartial"]!.GetValue<bool>().Should().BeFalse();
            report["sourceBackedArtifactsIncluded"]!.ToJsonString().Should().Contain(expectedFamily);

            var reversedIntentPath = Path.Combine(intentOutputRoot, "intent-spec", "intent-spec.json");
            var reversed = new CompilerKernel().Compile(new CompilationRequest(reversedIntentPath, Array.Empty<string>()));
            reversed.Success.Should().BeTrue();

            var packageEmit = new PackageEmitter().Emit(reversed.Solution, new EmitRequest(packageOutputRoot, EmitLayout.PackageInputs));
            packageEmit.Success.Should().BeTrue();

            File.Exists(Path.Combine(packageOutputRoot, "package-inputs", expectedPackageRelativePath.Replace('/', Path.DirectorySeparatorChar))).Should().BeTrue();
            File.ReadAllText(Path.Combine(packageOutputRoot, "package-inputs", "manifest.json"))
                .Should()
                .Contain(expectedPackageRelativePath.Replace('\\', '/'));
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

    [Theory]
    [InlineData("seed-environment", "CodexMetadataSeedEnvironment.zip", "CanvasApp", "CanvasApps/cat_overview_3dbf5.meta.xml")]
    [InlineData("seed-process-policy", "CodexMetadataSeedProcessPolicy.zip", "DuplicateRule", "duplicaterules/dre67df5ba444cf6a6b4092b00952064b3b91ddc3e81f6d3746c2169ae4ed2c367/duplicaterule.xml")]
    [InlineData("seed-process-security", "CodexMetadataSeedProcessSecurity.zip", "Role", "Roles/Codex Metadata Seed Role.xml")]
    public void Classic_export_zip_can_reverse_generate_source_backed_intent_and_rebuild_package_inputs(
        string seedName,
        string zipFileName,
        string expectedFamily,
        string expectedPackageRelativePath)
    {
        if (!IsPacAvailable())
        {
            return;
        }

        var seedZipPath = Path.Combine(ExamplesRoot, seedName, "export", zipFileName);
        var intentOutputRoot = Path.Combine(Path.GetTempPath(), $"dsc-source-backed-zip-intent-{Guid.NewGuid():N}");
        var packageOutputRoot = Path.Combine(Path.GetTempPath(), $"dsc-source-backed-zip-package-{Guid.NewGuid():N}");

        try
        {
            var original = new CompilerKernel().Compile(new CompilationRequest(seedZipPath, Array.Empty<string>()));
            original.Success.Should().BeTrue();

            var reverseEmit = new IntentSpecEmitter().Emit(original.Solution, new EmitRequest(intentOutputRoot, EmitLayout.IntentSpec));
            reverseEmit.Success.Should().BeTrue();

            var reportPath = Path.Combine(intentOutputRoot, "intent-spec", "reverse-generation-report.json");
            File.Exists(reportPath).Should().BeTrue();
            var report = JsonNode.Parse(File.ReadAllText(reportPath))!.AsObject();
            report["inputKind"]!.GetValue<string>().Should().Be("packed-zip");
            report["sourceBackedArtifactsIncluded"]!.ToJsonString().Should().Contain(expectedFamily);

            var reversedIntentPath = Path.Combine(intentOutputRoot, "intent-spec", "intent-spec.json");
            var reversed = new CompilerKernel().Compile(new CompilationRequest(reversedIntentPath, Array.Empty<string>()));
            reversed.Success.Should().BeTrue();

            var packageEmit = new PackageEmitter().Emit(reversed.Solution, new EmitRequest(packageOutputRoot, EmitLayout.PackageInputs));
            packageEmit.Success.Should().BeTrue();

            File.Exists(Path.Combine(packageOutputRoot, "package-inputs", expectedPackageRelativePath.Replace('/', Path.DirectorySeparatorChar))).Should().BeTrue();
            File.ReadAllText(Path.Combine(packageOutputRoot, "package-inputs", "manifest.json"))
                .Should()
                .Contain(expectedPackageRelativePath.Replace('\\', '/'));
        }
        finally
        {
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
    public void Reverse_generation_from_seed_advanced_ui_emits_structured_visualization_and_rebuilds_package_inputs()
    {
        var seedPath = Path.Combine(ExamplesRoot, "seed-advanced-ui");
        var intentOutputRoot = Path.Combine(Path.GetTempPath(), $"dsc-seed-chart-intent-{Guid.NewGuid():N}");
        var packageOutputRoot = Path.Combine(Path.GetTempPath(), $"dsc-seed-chart-package-{Guid.NewGuid():N}");

        try
        {
            var compiled = new CompilerKernel().Compile(new CompilationRequest(seedPath, Array.Empty<string>()));
            compiled.Success.Should().BeTrue();

            var reverseEmit = new IntentSpecEmitter().Emit(compiled.Solution, new EmitRequest(intentOutputRoot, EmitLayout.IntentSpec));
            reverseEmit.Success.Should().BeTrue();

            var report = JsonNode.Parse(File.ReadAllText(Path.Combine(intentOutputRoot, "intent-spec", "reverse-generation-report.json")))!.AsObject();
            report["sourceBackedArtifactsIncluded"]!.ToJsonString().Should().NotContain("Visualization");

            var intent = JsonNode.Parse(File.ReadAllText(Path.Combine(intentOutputRoot, "intent-spec", "intent-spec.json")))!.AsObject();
            var accountTable = intent["tables"]!.AsArray().Single(node => string.Equals(node?["logicalName"]?.GetValue<string>(), "account", StringComparison.OrdinalIgnoreCase))!.AsObject();
            var visualizations = accountTable["visualizations"]!.AsArray();
            visualizations.Count.Should().Be(1);
            visualizations[0]!["name"]!.GetValue<string>().Should().Be("Accounts by Industry");
            visualizations[0]!["chartTypes"]!.ToJsonString().Should().Contain("bar");
            visualizations[0]!["dataDescriptionXml"]!.GetValue<string>().Should().Contain("<datadescription");
            visualizations[0]!["presentationDescriptionXml"]!.GetValue<string>().Should().Contain("<presentationdescription");

            var reversed = new CompilerKernel().Compile(new CompilationRequest(Path.Combine(intentOutputRoot, "intent-spec", "intent-spec.json"), Array.Empty<string>()));
            reversed.Success.Should().BeTrue();

            var packageEmit = new PackageEmitter().Emit(reversed.Solution, new EmitRequest(packageOutputRoot, EmitLayout.PackageInputs));
            packageEmit.Success.Should().BeTrue();
            var visualizationPath = Path.Combine(packageOutputRoot, "package-inputs", "Entities", "Account", "Visualizations", "{74a622c0-5193-de11-97d4-00155da3b01e}.xml");
            File.Exists(visualizationPath).Should().BeTrue();
            File.ReadAllText(visualizationPath).Should().Contain("<savedqueryvisualizationid>{74a622c0-5193-de11-97d4-00155da3b01e}</savedqueryvisualizationid>");
        }
        finally
        {
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
    public void Reverse_generation_from_seed_advanced_ui_export_zip_keeps_structured_visualization_and_app_shell_details()
    {
        if (!IsPacAvailable())
        {
            return;
        }

        var seedPath = Path.Combine(ExamplesRoot, "seed-advanced-ui", "export", "CodexMetadataSeedAdvancedUI.zip");
        var intentOutputRoot = Path.Combine(Path.GetTempPath(), $"dsc-seed-advanced-ui-zip-intent-{Guid.NewGuid():N}");

        try
        {
            var compiled = new CompilerKernel().Compile(new CompilationRequest(seedPath, Array.Empty<string>()));
            compiled.Success.Should().BeTrue();

            var reverseEmit = new IntentSpecEmitter().Emit(compiled.Solution, new EmitRequest(intentOutputRoot, EmitLayout.IntentSpec));
            reverseEmit.Success.Should().BeTrue();

            var report = JsonNode.Parse(File.ReadAllText(Path.Combine(intentOutputRoot, "intent-spec", "reverse-generation-report.json")))!.AsObject();
            report["inputKind"]!.GetValue<string>().Should().Be("packed-zip");
            report["sourceBackedArtifactsIncluded"]!.ToJsonString().Should().Contain("WebResource");
            report["sourceBackedArtifactsIncluded"]!.ToJsonString().Should().NotContain("Visualization");

            var intent = JsonNode.Parse(File.ReadAllText(Path.Combine(intentOutputRoot, "intent-spec", "intent-spec.json")))!.AsObject();
            var appModule = intent["appModules"]!.AsArray().Single()!.AsObject();
            appModule["appSettings"]!.AsArray().Count.Should().BeGreaterThan(0);
            appModule["siteMap"]!.ToJsonString().Should().Contain("\"webResource\":\"cdxmeta_/advancedui/landing.html\"");

            var accountTable = intent["tables"]!.AsArray().Single(node => string.Equals(node?["logicalName"]?.GetValue<string>(), "account", StringComparison.OrdinalIgnoreCase))!.AsObject();
            var visualizations = accountTable["visualizations"]!.AsArray();
            visualizations.Count.Should().Be(1);
            visualizations[0]!["id"]!.GetValue<string>().Should().Be("74a622c0-5193-de11-97d4-00155da3b01e");
            visualizations[0]!["chartTypes"]!.ToJsonString().Should().Contain("bar");
        }
        finally
        {
            if (Directory.Exists(intentOutputRoot))
            {
                Directory.Delete(intentOutputRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void Reverse_generation_from_seed_forms_emits_structured_quick_card_and_control_rich_forms()
    {
        var seedPath = Path.Combine(ExamplesRoot, "seed-forms");
        var intentOutputRoot = Path.Combine(Path.GetTempPath(), $"dsc-seed-forms-intent-{Guid.NewGuid():N}");
        var packageOutputRoot = Path.Combine(Path.GetTempPath(), $"dsc-seed-forms-package-{Guid.NewGuid():N}");

        try
        {
            var compiled = new CompilerKernel().Compile(new CompilationRequest(seedPath, Array.Empty<string>()));
            compiled.Success.Should().BeTrue();

            var reverseEmit = new IntentSpecEmitter().Emit(compiled.Solution, new EmitRequest(intentOutputRoot, EmitLayout.IntentSpec));
            reverseEmit.Success.Should().BeTrue();

            var intent = JsonNode.Parse(File.ReadAllText(Path.Combine(intentOutputRoot, "intent-spec", "intent-spec.json")))!.AsObject();
            var workItemTable = intent["tables"]!.AsArray().Single(node => string.Equals(node?["logicalName"]?.GetValue<string>(), "cdxmeta_workitem", StringComparison.OrdinalIgnoreCase))!.AsObject();
            var forms = workItemTable["forms"]!.AsArray();
            forms.Any(node => string.Equals(node?["type"]?.GetValue<string>(), "main", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();
            forms.Any(node => string.Equals(node?["type"]?.GetValue<string>(), "quick", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();
            forms.Any(node => string.Equals(node?["type"]?.GetValue<string>(), "card", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();
            forms.ToJsonString().Should().Contain("\"kind\":\"quickView\"");
            forms.ToJsonString().Should().Contain("\"kind\":\"subgrid\"");

            var reversed = new CompilerKernel().Compile(new CompilationRequest(Path.Combine(intentOutputRoot, "intent-spec", "intent-spec.json"), Array.Empty<string>()));
            reversed.Success.Should().BeTrue();

            var packageEmit = new PackageEmitter().Emit(reversed.Solution, new EmitRequest(packageOutputRoot, EmitLayout.PackageInputs));
            packageEmit.Success.Should().BeTrue();
            Directory.Exists(Path.Combine(packageOutputRoot, "package-inputs", "Entities", "cdxmeta_WorkItem", "FormXml", "main")).Should().BeTrue();
            Directory.Exists(Path.Combine(packageOutputRoot, "package-inputs", "Entities", "cdxmeta_WorkItem", "FormXml", "quick")).Should().BeTrue();
            Directory.Exists(Path.Combine(packageOutputRoot, "package-inputs", "Entities", "cdxmeta_WorkItem", "FormXml", "card")).Should().BeTrue();
        }
        finally
        {
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
    public void Reverse_generation_from_seed_image_config_keeps_structured_image_authoring_surface()
    {
        var seedPath = Path.Combine(ExamplesRoot, "seed-image-config");
        var intentOutputRoot = Path.Combine(Path.GetTempPath(), $"dsc-seed-image-intent-{Guid.NewGuid():N}");

        try
        {
            var compiled = new CompilerKernel().Compile(new CompilationRequest(seedPath, Array.Empty<string>()));
            compiled.Success.Should().BeTrue();

            var reverseEmit = new IntentSpecEmitter().Emit(compiled.Solution, new EmitRequest(intentOutputRoot, EmitLayout.IntentSpec));
            reverseEmit.Success.Should().BeTrue();

            var intent = JsonNode.Parse(File.ReadAllText(Path.Combine(intentOutputRoot, "intent-spec", "intent-spec.json")))!.AsObject();
            var tableNode = intent["tables"]!.AsArray().Single();
            tableNode.Should().NotBeNull();
            var table = tableNode!.AsObject();
            var primaryImageAttribute = table["primaryImageAttribute"]?.GetValue<string>();
            primaryImageAttribute.Should().NotBeNullOrWhiteSpace();
            table["isCustomizable"]!.GetValue<bool>().Should().BeTrue();
            table["columns"]!.AsArray().Any(node => string.Equals(node?["type"]?.GetValue<string>(), "image", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();
            table["columns"]!.ToJsonString().Should().Contain("\"canStoreFullImage\":true");
        }
        finally
        {
            if (Directory.Exists(intentOutputRoot))
            {
                Directory.Delete(intentOutputRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void Reverse_generation_from_app_shell_seeds_preserves_structured_site_maps_and_source_backed_web_resources()
    {
        var seedPath = Path.Combine(ExamplesRoot, "seed-advanced-ui");
        var intentOutputRoot = Path.Combine(Path.GetTempPath(), $"dsc-seed-appshell-intent-{Guid.NewGuid():N}");
        var packageOutputRoot = Path.Combine(Path.GetTempPath(), $"dsc-seed-appshell-package-{Guid.NewGuid():N}");

        try
        {
            var compiled = new CompilerKernel().Compile(new CompilationRequest(seedPath, Array.Empty<string>()));
            compiled.Success.Should().BeTrue();

            var reverseEmit = new IntentSpecEmitter().Emit(compiled.Solution, new EmitRequest(intentOutputRoot, EmitLayout.IntentSpec));
            reverseEmit.Success.Should().BeTrue();

            var intent = JsonNode.Parse(File.ReadAllText(Path.Combine(intentOutputRoot, "intent-spec", "intent-spec.json")))!.AsObject();
            intent["appModules"]!.AsArray()[0]!["appSettings"]!.AsArray().Count.Should().BeGreaterThan(0);
            intent["appModules"]!.ToJsonString().Should().Contain("\"webResource\":\"cdxmeta_/advancedui/landing.html\"");
            intent["sourceBackedArtifacts"]!.ToJsonString().Should().Contain("\"family\":\"WebResource\"");

            var reversed = new CompilerKernel().Compile(new CompilationRequest(Path.Combine(intentOutputRoot, "intent-spec", "intent-spec.json"), Array.Empty<string>()));
            reversed.Success.Should().BeTrue();

            var packageEmit = new PackageEmitter().Emit(reversed.Solution, new EmitRequest(packageOutputRoot, EmitLayout.PackageInputs));
            packageEmit.Success.Should().BeTrue();
            File.Exists(Path.Combine(packageOutputRoot, "package-inputs", "AppModuleSiteMaps", "codex_metadata_advanced_ui_924e69cb", "AppModuleSiteMap.xml")).Should().BeTrue();
            Directory.GetFiles(Path.Combine(packageOutputRoot, "package-inputs", "WebResources"), "landing.html", SearchOption.AllDirectories).Should().NotBeEmpty();
        }
        finally
        {
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
    public void Reverse_generated_seed_advanced_ui_intent_packs_with_real_pac_when_available()
    {
        if (!IsPacAvailable())
        {
            return;
        }

        var seedPath = Path.Combine(ExamplesRoot, "seed-advanced-ui");
        var intentOutputRoot = Path.Combine(Path.GetTempPath(), $"dsc-seed-advanced-ui-pack-intent-{Guid.NewGuid():N}");
        var packageOutputRoot = Path.Combine(Path.GetTempPath(), $"dsc-seed-advanced-ui-pack-package-{Guid.NewGuid():N}");

        try
        {
            var compiled = new CompilerKernel().Compile(new CompilationRequest(seedPath, Array.Empty<string>()));
            compiled.Success.Should().BeTrue();

            var reverseEmit = new IntentSpecEmitter().Emit(compiled.Solution, new EmitRequest(intentOutputRoot, EmitLayout.IntentSpec));
            reverseEmit.Success.Should().BeTrue();

            var reversed = new CompilerKernel().Compile(new CompilationRequest(Path.Combine(intentOutputRoot, "intent-spec", "intent-spec.json"), Array.Empty<string>()));
            reversed.Success.Should().BeTrue();

            var packageEmit = new PackageEmitter().Emit(reversed.Solution, new EmitRequest(packageOutputRoot, EmitLayout.PackageInputs));
            packageEmit.Success.Should().BeTrue();

            var result = new DataverseSolutionCompiler.Packaging.Pac.PacCliExecutor().Pack(new DataverseSolutionCompiler.Domain.Packaging.PackageRequest(
                Path.Combine(packageOutputRoot, "package-inputs"),
                packageOutputRoot,
                DataverseSolutionCompiler.Domain.Packaging.PackageFlavor.Unmanaged));

            result.Success.Should().BeTrue();
            result.PackagePath.Should().NotBeNullOrWhiteSpace();
            File.Exists(result.PackagePath!).Should().BeTrue();
        }
        finally
        {
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

    private static bool IsPacAvailable()
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "pac",
                    ArgumentList = { "help" },
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }
}
