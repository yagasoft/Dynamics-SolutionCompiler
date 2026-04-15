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

    private static readonly string SeedAlternateKeyEntityPath = Path.Combine(
        "C:\\Git\\Dataverse-Solution-KB",
        "fixtures",
        "skill-corpus",
        "examples",
        "seed-alternate-key",
        "unpacked",
        "Entities",
        "cdxmeta_WorkItem",
        "Entity.xml");

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
    public void Compile_and_package_support_integer_columns_in_json_intent()
    {
        var intentPath = Path.Combine(Path.GetTempPath(), $"dsc-intent-integer-{Guid.NewGuid():N}.json");
        var outputRoot = Path.Combine(Path.GetTempPath(), $"dsc-intent-integer-package-{Guid.NewGuid():N}");

        try
        {
            File.WriteAllText(
                intentPath,
                """
                {
                  "specVersion": "1.0",
                  "solution": {
                    "uniqueName": "IntegerIntent",
                    "displayName": "Integer Intent",
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
                      "logicalName": "cdxmeta_counter",
                      "schemaName": "cdxmeta_Counter",
                      "displayName": "Counter",
                      "columns": [
                        {
                          "logicalName": "cdxmeta_sequence",
                          "schemaName": "cdxmeta_Sequence",
                          "displayName": "Sequence",
                          "type": "integer"
                        }
                      ],
                      "forms": [],
                      "views": []
                    }
                  ]
                }
                """);

            var compiled = new CompilerKernel().Compile(new CompilationRequest(intentPath, Array.Empty<string>()));
            compiled.Success.Should().BeTrue();
            compiled.Solution.Artifacts.Should().Contain(artifact =>
                artifact.Family == ComponentFamily.Column
                && artifact.LogicalName == "cdxmeta_counter|cdxmeta_sequence"
                && artifact.Properties![ArtifactPropertyKeys.AttributeType] == "integer");

            var emitted = new PackageEmitter().Emit(compiled.Solution, new EmitRequest(outputRoot, EmitLayout.PackageInputs));
            emitted.Success.Should().BeTrue();

            File.ReadAllText(Path.Combine(outputRoot, "package-inputs", "Entities", "cdxmeta_Counter", "Entity.xml"))
                .Should()
                .Contain("<Type>int</Type>")
                .And.Contain("<Name>cdxmeta_sequence</Name>");
        }
        finally
        {
            if (File.Exists(intentPath))
            {
                File.Delete(intentPath);
            }

            if (Directory.Exists(outputRoot))
            {
                Directory.Delete(outputRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void Compile_and_package_reference_global_option_sets_without_nested_attribute_optionset()
    {
        var intentPath = Path.Combine(Path.GetTempPath(), $"dsc-intent-global-optionset-{Guid.NewGuid():N}.json");
        var outputRoot = Path.Combine(Path.GetTempPath(), $"dsc-intent-global-optionset-package-{Guid.NewGuid():N}");

        try
        {
            File.WriteAllText(
                intentPath,
                """
                {
                  "specVersion": "1.0",
                  "solution": {
                    "uniqueName": "GlobalOptionIntent",
                    "displayName": "Global Option Intent",
                    "version": "1.0.0.0",
                    "layeringIntent": "UnmanagedDevelopment"
                  },
                  "publisher": {
                    "uniqueName": "CodexMetadata",
                    "prefix": "cdxmeta",
                    "displayName": "Codex Metadata"
                  },
                  "globalOptionSets": [
                    {
                      "logicalName": "cdxmeta_priorityband_test",
                      "displayName": "Priority Band",
                      "optionSetType": "picklist",
                      "options": [
                        { "value": "727270010", "label": "Low" },
                        { "value": "727270011", "label": "Medium" }
                      ]
                    }
                  ],
                  "tables": [
                    {
                      "logicalName": "cdxmeta_task",
                      "schemaName": "cdxmeta_Task",
                      "displayName": "Task",
                      "columns": [
                        {
                          "logicalName": "cdxmeta_priorityband",
                          "schemaName": "cdxmeta_PriorityBand",
                          "displayName": "Priority Band",
                          "type": "choice",
                          "globalOptionSet": "cdxmeta_priorityband_test"
                        }
                      ],
                      "forms": [],
                      "views": []
                    }
                  ]
                }
                """);

            var compiled = new CompilerKernel().Compile(new CompilationRequest(intentPath, Array.Empty<string>()));
            compiled.Success.Should().BeTrue();

            var emitted = new PackageEmitter().Emit(compiled.Solution, new EmitRequest(outputRoot, EmitLayout.PackageInputs));
            emitted.Success.Should().BeTrue();

            File.ReadAllText(Path.Combine(outputRoot, "package-inputs", "Entities", "cdxmeta_Task", "Entity.xml"))
                .Should()
                .Contain("<OptionSetName>cdxmeta_priorityband_test</OptionSetName>")
                .And.NotContain("<optionset Name=\"cdxmeta_priorityband_test\">");
        }
        finally
        {
            if (File.Exists(intentPath))
            {
                File.Delete(intentPath);
            }

            if (Directory.Exists(outputRoot))
            {
                Directory.Delete(outputRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void Compile_and_package_scope_local_choice_and_boolean_option_sets_to_the_entity()
    {
        var intentPath = Path.Combine(Path.GetTempPath(), $"dsc-intent-local-options-{Guid.NewGuid():N}.json");
        var outputRoot = Path.Combine(Path.GetTempPath(), $"dsc-intent-local-options-package-{Guid.NewGuid():N}");

        try
        {
            File.WriteAllText(
                intentPath,
                """
                {
                  "specVersion": "1.0",
                  "solution": {
                    "uniqueName": "LocalOptionIntent",
                    "displayName": "Local Option Intent",
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
                      "logicalName": "cdxmeta_task",
                      "schemaName": "cdxmeta_Task",
                      "displayName": "Task",
                      "columns": [
                        {
                          "logicalName": "cdxmeta_isblocked",
                          "schemaName": "cdxmeta_IsBlocked",
                          "displayName": "Blocked",
                          "type": "boolean",
                          "options": [
                            { "value": "1", "label": "Yes" },
                            { "value": "0", "label": "No" }
                          ]
                        },
                        {
                          "logicalName": "cdxmeta_stage",
                          "schemaName": "cdxmeta_Stage",
                          "displayName": "Stage",
                          "type": "choice",
                          "options": [
                            { "value": "727270000", "label": "Planned" },
                            { "value": "727270001", "label": "Active" }
                          ]
                        }
                      ],
                      "forms": [],
                      "views": []
                    }
                  ]
                }
                """);

            var compiled = new CompilerKernel().Compile(new CompilationRequest(intentPath, Array.Empty<string>()));
            compiled.Success.Should().BeTrue();

            var emitted = new PackageEmitter().Emit(compiled.Solution, new EmitRequest(outputRoot, EmitLayout.PackageInputs));
            emitted.Success.Should().BeTrue();

            var entityXml = File.ReadAllText(Path.Combine(outputRoot, "package-inputs", "Entities", "cdxmeta_Task", "Entity.xml"));
            entityXml.Should().Contain("<optionset Name=\"cdxmeta_task_cdxmeta_isblocked\">");
            entityXml.Should().Contain("<optionset Name=\"cdxmeta_task_cdxmeta_stage\">");
            entityXml.Should().NotContain("<optionset Name=\"cdxmeta_isblocked\">");
            entityXml.Should().NotContain("<optionset Name=\"cdxmeta_stage\">");
        }
        finally
        {
            if (File.Exists(intentPath))
            {
                File.Delete(intentPath);
            }

            if (Directory.Exists(outputRoot))
            {
                Directory.Delete(outputRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void Compile_and_package_emit_card_forms_with_dataverse_card_shell_sections()
    {
        var intentPath = Path.Combine(Path.GetTempPath(), $"dsc-intent-card-form-{Guid.NewGuid():N}.json");
        var outputRoot = Path.Combine(Path.GetTempPath(), $"dsc-intent-card-form-package-{Guid.NewGuid():N}");

        try
        {
            File.WriteAllText(
                intentPath,
                """
                {
                  "specVersion": "1.0",
                  "solution": {
                    "uniqueName": "CardIntent",
                    "displayName": "Card Intent",
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
                      "logicalName": "cdxmeta_task",
                      "schemaName": "cdxmeta_Task",
                      "displayName": "Task",
                      "columns": [
                        {
                          "logicalName": "cdxmeta_summary",
                          "schemaName": "cdxmeta_Summary",
                          "displayName": "Summary",
                          "type": "string"
                        }
                      ],
                      "forms": [
                        {
                          "id": "11111111-1111-1111-1111-111111111111",
                          "name": "Task Card",
                          "type": "card",
                          "tabs": [
                            {
                              "name": "card",
                              "label": "Card",
                              "sections": [
                                {
                                  "name": "details",
                                  "label": "Details",
                                  "fields": [ "cdxmeta_taskname", "cdxmeta_summary" ]
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
                """);

            var compiled = new CompilerKernel().Compile(new CompilationRequest(intentPath, Array.Empty<string>()));
            compiled.Success.Should().BeTrue();

            var emitted = new PackageEmitter().Emit(compiled.Solution, new EmitRequest(outputRoot, EmitLayout.PackageInputs));
            emitted.Success.Should().BeTrue();

            File.ReadAllText(Path.Combine(outputRoot, "package-inputs", "Entities", "cdxmeta_Task", "FormXml", "card", "{11111111-1111-1111-1111-111111111111}.xml"))
                .Should()
                .Contain("section name=\"ColorStrip\"")
                .And.Contain("section name=\"CardDetails\"")
                .And.Contain("section name=\"CardFooter\"")
                .And.NotContain("section name=\"details\"");
        }
        finally
        {
            if (File.Exists(intentPath))
            {
                File.Delete(intentPath);
            }

            if (Directory.Exists(outputRoot))
            {
                Directory.Delete(outputRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void Hybrid_source_backed_tables_do_not_emit_standalone_entity_key_root_components()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"dsc-source-backed-key-{Guid.NewGuid():N}");
        var intentPath = Path.Combine(tempRoot, "intent-spec.json");
        var sourceBackedEntityDirectory = Path.Combine(tempRoot, "source-backed", "Entities", "cdxmeta_WorkItem");
        var outputRoot = Path.Combine(tempRoot, "out");

        Directory.CreateDirectory(sourceBackedEntityDirectory);
        File.Copy(SeedAlternateKeyEntityPath, Path.Combine(sourceBackedEntityDirectory, "Entity.xml"), overwrite: true);

        try
        {
            File.WriteAllText(
                intentPath,
                """
                {
                  "specVersion": "1.0",
                  "solution": {
                    "uniqueName": "SourceBackedKeyIntent",
                    "displayName": "Source-Backed Key Intent",
                    "version": "1.0.0.0",
                    "layeringIntent": "UnmanagedDevelopment"
                  },
                  "publisher": {
                    "uniqueName": "CodexMetadata",
                    "prefix": "cdxmeta",
                    "displayName": "Codex Metadata"
                  },
                  "tables": [],
                  "sourceBackedArtifacts": [
                    {
                      "family": "Table",
                      "logicalName": "cdxmeta_workitem",
                      "displayName": "Work Item",
                      "metadataSourcePath": "source-backed/Entities/cdxmeta_WorkItem/Entity.xml",
                      "packageRelativePath": "Entities/cdxmeta_WorkItem/Entity.xml"
                    }
                  ]
                }
                """);

            var compiled = new CompilerKernel().Compile(new CompilationRequest(intentPath, Array.Empty<string>()));
            compiled.Success.Should().BeTrue();

            var emitted = new PackageEmitter().Emit(compiled.Solution, new EmitRequest(outputRoot, EmitLayout.PackageInputs));
            emitted.Success.Should().BeTrue();

            File.ReadAllText(Path.Combine(outputRoot, "package-inputs", "Other", "Solution.xml"))
                .Should()
                .Contain("type=\"1\"")
                .And.Contain("schemaName=\"cdxmeta_workitem\"")
                .And.NotContain("type=\"14\"")
                .And.NotContain("schemaName=\"cdxmeta_WorkItem_ExternalCode\"");
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void Hybrid_source_backed_tables_normalize_mismatched_entity_name_and_report_diagnostic()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"dsc-source-backed-entity-name-{Guid.NewGuid():N}");
        var intentPath = Path.Combine(tempRoot, "intent-spec.json");
        var sourceBackedEntityDirectory = Path.Combine(tempRoot, "source-backed", "Entities", "cdxmeta_WorkItem");
        var outputRoot = Path.Combine(tempRoot, "out");

        Directory.CreateDirectory(sourceBackedEntityDirectory);
        var malformedXml = File.ReadAllText(SeedAlternateKeyEntityPath)
            .Replace("entity Name=\"cdxmeta_WorkItem\"", "entity Name=\"cdxmeta_workitem\"", StringComparison.Ordinal);
        File.WriteAllText(Path.Combine(sourceBackedEntityDirectory, "Entity.xml"), malformedXml);

        try
        {
            File.WriteAllText(
                intentPath,
                """
                {
                  "specVersion": "1.0",
                  "solution": {
                    "uniqueName": "SourceBackedEntityNameIntent",
                    "displayName": "Source-Backed Entity Name Intent",
                    "version": "1.0.0.0",
                    "layeringIntent": "UnmanagedDevelopment"
                  },
                  "publisher": {
                    "uniqueName": "CodexMetadata",
                    "prefix": "cdxmeta",
                    "displayName": "Codex Metadata"
                  },
                  "tables": [],
                  "sourceBackedArtifacts": [
                    {
                      "family": "Table",
                      "logicalName": "cdxmeta_workitem",
                      "displayName": "Work Item",
                      "metadataSourcePath": "source-backed/Entities/cdxmeta_WorkItem/Entity.xml",
                      "packageRelativePath": "Entities/cdxmeta_WorkItem/Entity.xml"
                    }
                  ]
                }
                """);

            var compiled = new CompilerKernel().Compile(new CompilationRequest(intentPath, Array.Empty<string>()));
            compiled.Success.Should().BeTrue();

            var emitted = new PackageEmitter().Emit(compiled.Solution, new EmitRequest(outputRoot, EmitLayout.PackageInputs));
            emitted.Success.Should().BeTrue();
            emitted.Diagnostics.Should().Contain(diagnostic =>
                diagnostic.Code == "package-emitter-normalized-source-backed-table-entity-name"
                && diagnostic.Message.Contains("cdxmeta_workitem", StringComparison.Ordinal));

            File.ReadAllText(Path.Combine(outputRoot, "package-inputs", "Entities", "cdxmeta_WorkItem", "Entity.xml"))
                .Should()
                .Contain("entity Name=\"cdxmeta_WorkItem\"")
                .And.NotContain("entity Name=\"cdxmeta_workitem\"");
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
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
    [InlineData("seed-process-policy", "MobileOfflineProfile", "MobileOfflineProfiles/Codex Metadata Mobile Offline Profile.xml")]
    [InlineData("seed-process-security", "Role", "Roles/Codex Metadata Seed Role.xml")]
    [InlineData("seed-process-security", "ConnectionRole", "Other/ConnectionRoles.xml")]
    [InlineData("seed-reporting-legacy", "Report", "Reports/cdxmeta_account_summary.rdl.data.xml")]
    [InlineData("seed-plugin-registration", "PluginAssembly", "Other/Customizations.xml")]
    [InlineData("seed-service-endpoint-connector", "ServiceEndpoint", "ServiceEndpoints/codex_webhook_endpoint/ServiceEndpoint.xml")]
    [InlineData("seed-service-endpoint-connector", "Connector", "Connectors/shared-offerings-connector/Connector.xml")]
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
            var expectedPackagePath = Path.Combine(packageOutputRoot, "package-inputs", expectedPackageRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (IsApplyOnlyHybridFamily(expectedFamily))
            {
                if (string.Equals(expectedPackageRelativePath, "Other/Customizations.xml", StringComparison.OrdinalIgnoreCase))
                {
                    File.Exists(expectedPackagePath).Should().BeTrue();
                }
                else
                {
                    File.Exists(expectedPackagePath).Should().BeFalse();
                }

                File.ReadAllText(Path.Combine(packageOutputRoot, "package-inputs", "Other", "Solution.xml"))
                    .Should()
                    .NotContain(GetApplyOnlyRootComponentMarker(expectedFamily));
            }
            else
            {
                File.Exists(expectedPackagePath).Should().BeTrue();
                File.ReadAllText(Path.Combine(packageOutputRoot, "package-inputs", "manifest.json"))
                    .Should()
                    .Contain(expectedPackageRelativePath.Replace('\\', '/'));
            }
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
    public void Reverse_generation_from_seed_reporting_legacy_emits_source_backed_artifacts_and_rebuilds_package_layout()
    {
        var seedPath = Path.Combine(ExamplesRoot, "seed-reporting-legacy");
        var intentOutputRoot = Path.Combine(Path.GetTempPath(), $"dsc-seed-reporting-legacy-intent-{Guid.NewGuid():N}");
        var packageOutputRoot = Path.Combine(Path.GetTempPath(), $"dsc-seed-reporting-legacy-package-{Guid.NewGuid():N}");

        try
        {
            var compiled = new CompilerKernel().Compile(new CompilationRequest(seedPath, Array.Empty<string>()));
            compiled.Success.Should().BeTrue();

            var reverseEmit = new IntentSpecEmitter().Emit(compiled.Solution, new EmitRequest(intentOutputRoot, EmitLayout.IntentSpec));
            reverseEmit.Success.Should().BeTrue();

            var report = JsonNode.Parse(File.ReadAllText(Path.Combine(intentOutputRoot, "intent-spec", "reverse-generation-report.json")))!.AsObject();
            report["isPartial"]!.GetValue<bool>().Should().BeFalse();
            report["sourceBackedArtifactsIncluded"]!.ToJsonString().Should().Contain("\"family\":\"Report\"");
            report["sourceBackedArtifactsIncluded"]!.ToJsonString().Should().Contain("\"family\":\"Template\"");
            report["sourceBackedArtifactsIncluded"]!.ToJsonString().Should().Contain("\"family\":\"DisplayString\"");
            report["sourceBackedArtifactsIncluded"]!.ToJsonString().Should().Contain("\"family\":\"Attachment\"");
            report["sourceBackedArtifactsIncluded"]!.ToJsonString().Should().Contain("\"family\":\"LegacyAsset\"");

            var intent = JsonNode.Parse(File.ReadAllText(Path.Combine(intentOutputRoot, "intent-spec", "intent-spec.json")))!.AsObject();
            intent["sourceBackedArtifacts"]!.ToJsonString().Should().Contain("\"family\":\"Report\"");
            intent["sourceBackedArtifacts"]!.ToJsonString().Should().Contain("\"packageRelativePath\":\"Reports/cdxmeta_account_summary.rdl.data.xml\"");
            intent["sourceBackedArtifacts"]!.ToJsonString().Should().Contain("\"family\":\"Template\"");
            intent["sourceBackedArtifacts"]!.ToJsonString().Should().Contain("\"packageRelativePath\":\"Templates/cdxmeta_welcome_email.xml\"");
            intent["sourceBackedArtifacts"]!.ToJsonString().Should().Contain("\"family\":\"DisplayString\"");
            intent["sourceBackedArtifacts"]!.ToJsonString().Should().Contain("\"family\":\"Attachment\"");
            intent["sourceBackedArtifacts"]!.ToJsonString().Should().Contain("\"family\":\"LegacyAsset\"");

            var reversed = new CompilerKernel().Compile(new CompilationRequest(Path.Combine(intentOutputRoot, "intent-spec", "intent-spec.json"), Array.Empty<string>()));
            reversed.Success.Should().BeTrue();

            var packageEmit = new PackageEmitter().Emit(reversed.Solution, new EmitRequest(packageOutputRoot, EmitLayout.PackageInputs));
            packageEmit.Success.Should().BeTrue();
            File.Exists(Path.Combine(packageOutputRoot, "package-inputs", "Reports", "cdxmeta_account_summary.rdl.data.xml")).Should().BeTrue();
            File.Exists(Path.Combine(packageOutputRoot, "package-inputs", "Reports", "cdxmeta_account_summary.rdl")).Should().BeTrue();
            File.Exists(Path.Combine(packageOutputRoot, "package-inputs", "Templates", "cdxmeta_welcome_email.xml")).Should().BeTrue();
            File.Exists(Path.Combine(packageOutputRoot, "package-inputs", "DisplayStrings", "cdxmeta_reporting_labels.xml")).Should().BeTrue();
            File.Exists(Path.Combine(packageOutputRoot, "package-inputs", "Attachments", "cdxmeta_report_payload.txt.data.xml")).Should().BeTrue();
            File.Exists(Path.Combine(packageOutputRoot, "package-inputs", "Attachments", "cdxmeta_report_payload.txt")).Should().BeTrue();
            File.Exists(Path.Combine(packageOutputRoot, "package-inputs", "WebWizards", "cdxmeta_onboarding_wizard.xml")).Should().BeTrue();
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

    [Theory]
    [InlineData("seed-environment", "CodexMetadataSeedEnvironment.zip", "CanvasApp", "CanvasApps/cat_overview_3dbf5.meta.xml")]
    [InlineData("seed-process-policy", "CodexMetadataSeedProcessPolicy.zip", "DuplicateRule", "duplicaterules/dre67df5ba444cf6a6b4092b00952064b3b91ddc3e81f6d3746c2169ae4ed2c367/duplicaterule.xml")]
    [InlineData("seed-process-policy", "CodexMetadataSeedProcessPolicy.zip", "MobileOfflineProfile", "MobileOfflineProfiles/Codex Metadata Mobile Offline Profile.xml")]
    [InlineData("seed-process-security", "CodexMetadataSeedProcessSecurity.zip", "Role", "Roles/Codex Metadata Seed Role.xml")]
    [InlineData("seed-process-security", "CodexMetadataSeedProcessSecurity.zip", "ConnectionRole", "Other/ConnectionRoles.xml")]
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
            var expectedPackagePath = Path.Combine(packageOutputRoot, "package-inputs", expectedPackageRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (IsApplyOnlyHybridFamily(expectedFamily))
            {
                File.Exists(expectedPackagePath).Should().BeFalse();
                File.ReadAllText(Path.Combine(packageOutputRoot, "package-inputs", "Other", "Solution.xml"))
                    .Should()
                    .NotContain(GetApplyOnlyRootComponentMarker(expectedFamily));
            }
            else
            {
                File.Exists(expectedPackagePath).Should().BeTrue();
                File.ReadAllText(Path.Combine(packageOutputRoot, "package-inputs", "manifest.json"))
                    .Should()
                    .Contain(expectedPackageRelativePath.Replace('\\', '/'));
            }
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
    public void Sharded_plugin_step_reverse_generation_preserves_step_apply_metadata()
    {
        var sourceRoot = Path.Combine(Path.GetTempPath(), $"dsc-plugin-step-source-{Guid.NewGuid():N}");
        var intentOutputRoot = Path.Combine(Path.GetTempPath(), $"dsc-plugin-step-intent-{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(Path.Combine(sourceRoot, "Other"));
            Directory.CreateDirectory(Path.Combine(sourceRoot, "SdkMessageProcessingSteps"));

            File.Copy(
                Path.Combine(ExamplesRoot, "seed-plugin-registration", "unpacked", "Other", "Solution.xml"),
                Path.Combine(sourceRoot, "Other", "Solution.xml"),
                overwrite: true);

            File.WriteAllText(
                Path.Combine(sourceRoot, "Other", "Customizations.xml"),
                """
                <?xml version="1.0" encoding="utf-8"?>
                <ImportExportXml xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
                  <SolutionPluginAssemblies />
                  <SdkMessageProcessingSteps />
                </ImportExportXml>
                """);

            File.WriteAllText(
                Path.Combine(sourceRoot, "SdkMessageProcessingSteps", "{498a9146-c438-f111-88b3-0022489b9600}.xml"),
                """
                <?xml version="1.0" encoding="utf-8"?>
                <SdkMessageProcessingStep Name="Account Update Trace Step" SdkMessageProcessingStepId="{498a9146-c438-f111-88b3-0022489b9600}" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
                  <SdkMessageId>20bebb1b-ea3e-db11-86a7-000a3a5473e8</SdkMessageId>
                  <PluginTypeName>Codex.Metadata.Plugins.AccountUpdateTrace, Codex.Metadata.Plugins, Version=1.0.0.0, Culture=neutral, PublicKeyToken=9d006cbbfeff5098</PluginTypeName>
                  <PrimaryEntity>account</PrimaryEntity>
                  <Description>Neutral synchronous update step for account.</Description>
                  <FilteringAttributes>accountnumber,name</FilteringAttributes>
                  <Mode>0</Mode>
                  <Rank>1</Rank>
                  <Stage>20</Stage>
                  <SupportedDeployment>0</SupportedDeployment>
                  <IntroducedVersion>1.0</IntroducedVersion>
                </SdkMessageProcessingStep>
                """);

            var original = new CompilerKernel().Compile(new CompilationRequest(sourceRoot, Array.Empty<string>()));
            original.Success.Should().BeTrue();

            var reverseEmit = new IntentSpecEmitter().Emit(original.Solution, new EmitRequest(intentOutputRoot, EmitLayout.IntentSpec));
            reverseEmit.Success.Should().BeTrue();

            var intent = JsonNode.Parse(File.ReadAllText(Path.Combine(intentOutputRoot, "intent-spec", "intent-spec.json")))!.AsObject();
            var pluginStepArtifact = intent["sourceBackedArtifacts"]!.AsArray()
                .OfType<JsonObject>()
                .Single(artifact => string.Equals(artifact["family"]?.GetValue<string>(), ComponentFamily.PluginStep.ToString(), StringComparison.Ordinal));

            pluginStepArtifact["stableProperties"]!["handlerPluginTypeName"]!.GetValue<string>()
                .Should()
                .Be("Codex.Metadata.Plugins.AccountUpdateTrace");
            pluginStepArtifact["stableProperties"]!["sdkMessageId"]!.GetValue<string>()
                .Should()
                .Be("20bebb1b-ea3e-db11-86a7-000a3a5473e8");
        }
        finally
        {
            if (Directory.Exists(sourceRoot))
            {
                Directory.Delete(sourceRoot, recursive: true);
            }

            if (Directory.Exists(intentOutputRoot))
            {
                Directory.Delete(intentOutputRoot, recursive: true);
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
            report["sourceBackedArtifactsIncluded"]!.ToJsonString().Should().Contain("\"family\":\"Table\"");
            report["sourceBackedArtifactsIncluded"]!.ToJsonString().Should().Contain("\"artifact\":\"account\"");
            report["sourceBackedArtifactsIncluded"]!.ToJsonString().Should().NotContain("Visualization");
            report["unsupportedFamiliesOmitted"]!.ToJsonString().Should().NotContain("\"family\":\"Visualization\"");

            var intent = JsonNode.Parse(File.ReadAllText(Path.Combine(intentOutputRoot, "intent-spec", "intent-spec.json")))!.AsObject();
            var appModule = intent["appModules"]!.AsArray().Single()!.AsObject();
            appModule["appSettings"]!.AsArray().Count.Should().BeGreaterThan(0);
            appModule["siteMap"]!.ToJsonString().Should().Contain("\"webResource\":\"cdxmeta_/advancedui/landing.html\"");

            var accountTable = intent["tables"]!.AsArray().Single(node => string.Equals(node?["logicalName"]?.GetValue<string>(), "account", StringComparison.OrdinalIgnoreCase))!.AsObject();
            var visualizations = accountTable["visualizations"]!.AsArray();
            visualizations.Count.Should().Be(1);
            visualizations[0]!["id"]!.GetValue<string>().Should().Be("74a622c0-5193-de11-97d4-00155da3b01e");
            visualizations[0]!["chartTypes"]!.ToJsonString().Should().Contain("bar");
            intent["sourceBackedArtifacts"]!.ToJsonString().Should().Contain("\"family\":\"Table\"");
            intent["sourceBackedArtifacts"]!.ToJsonString().Should().Contain("\"packageRelativePath\":\"Entities/Account/Entity.xml\"");
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

    private static bool IsApplyOnlyHybridFamily(string family) =>
        string.Equals(family, ComponentFamily.EntityAnalyticsConfiguration.ToString(), StringComparison.Ordinal)
        || string.Equals(family, ComponentFamily.AiProjectType.ToString(), StringComparison.Ordinal)
        || string.Equals(family, ComponentFamily.AiProject.ToString(), StringComparison.Ordinal)
        || string.Equals(family, ComponentFamily.AiConfiguration.ToString(), StringComparison.Ordinal)
        || string.Equals(family, ComponentFamily.ServiceEndpoint.ToString(), StringComparison.Ordinal)
        || string.Equals(family, ComponentFamily.MobileOfflineProfile.ToString(), StringComparison.Ordinal)
        || string.Equals(family, ComponentFamily.ConnectionRole.ToString(), StringComparison.Ordinal)
        || string.Equals(family, ComponentFamily.Connector.ToString(), StringComparison.Ordinal)
        || string.Equals(family, ComponentFamily.PluginAssembly.ToString(), StringComparison.Ordinal)
        || string.Equals(family, ComponentFamily.PluginType.ToString(), StringComparison.Ordinal)
        || string.Equals(family, ComponentFamily.PluginStep.ToString(), StringComparison.Ordinal)
        || string.Equals(family, ComponentFamily.PluginStepImage.ToString(), StringComparison.Ordinal)
        ;

    private static string GetApplyOnlyRootComponentMarker(string family) =>
        family switch
        {
            nameof(ComponentFamily.EntityAnalyticsConfiguration) => "type=\"430\"",
            nameof(ComponentFamily.AiProjectType) => "type=\"400\"",
            nameof(ComponentFamily.AiProject) => "type=\"401\"",
            nameof(ComponentFamily.AiConfiguration) => "type=\"402\"",
            nameof(ComponentFamily.ServiceEndpoint) => "type=\"95\"",
            nameof(ComponentFamily.MobileOfflineProfile) => "type=\"161\"",
            nameof(ComponentFamily.ConnectionRole) => "type=\"63\"",
            nameof(ComponentFamily.Connector) => "type=\"371\"",
            nameof(ComponentFamily.PluginAssembly) => "type=\"91\"",
            nameof(ComponentFamily.PluginType) => "type=\"90\"",
            nameof(ComponentFamily.PluginStep) => "type=\"92\"",
            nameof(ComponentFamily.PluginStepImage) => "type=\"93\"",
            _ => throw new ArgumentOutOfRangeException(nameof(family), family, "Unknown apply-only hybrid family.")
        };
}
