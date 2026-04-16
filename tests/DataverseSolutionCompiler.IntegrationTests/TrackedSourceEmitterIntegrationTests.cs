using FluentAssertions;
using DataverseSolutionCompiler.Compiler;
using DataverseSolutionCompiler.Domain.Compilation;
using DataverseSolutionCompiler.Emitters.TrackedSource;
using DataverseSolutionCompiler.Domain.Emission;
using DataverseSolutionCompiler.Domain.Model;
using DataverseSolutionCompiler.Domain.Read;
using DataverseSolutionCompiler.Readers.Xml;
using Xunit;

namespace DataverseSolutionCompiler.IntegrationTests;

public sealed class TrackedSourceEmitterIntegrationTests
{
    [Fact]
    public void Emitter_materializes_deterministic_tracked_source_files()
    {
        var model = ReadFixture("seed-advanced-ui");
        var emitter = new TrackedSourceEmitter();
        var outputRoot = Path.Combine(Path.GetTempPath(), $"dsc-tracked-source-{Guid.NewGuid():N}");

        try
        {
            var first = emitter.Emit(model, new EmitRequest(outputRoot, EmitLayout.TrackedSource));
            var firstSnapshot = SnapshotTrackedSource(outputRoot);
            var second = emitter.Emit(model, new EmitRequest(outputRoot, EmitLayout.TrackedSource));
            var secondSnapshot = SnapshotTrackedSource(outputRoot);

            first.Success.Should().BeTrue();
            second.Success.Should().BeTrue();
            first.Files.Should().Contain(file => file.RelativePath == "tracked-source/manifest.json");
            firstSnapshot.Keys.Should().BeEquivalentTo(secondSnapshot.Keys);
            foreach (var path in firstSnapshot.Keys)
            {
                secondSnapshot[path].Should().Equal(firstSnapshot[path]);
            }

            File.Exists(Path.Combine(outputRoot, "tracked-source", "solution", "manifest.json")).Should().BeTrue();
            File.Exists(Path.Combine(outputRoot, "tracked-source", "app-modules", "codex_metadata_advanced_ui_924e69cb.json")).Should().BeTrue();
            File.Exists(Path.Combine(outputRoot, "tracked-source", "site-maps", "codex_metadata_advanced_ui_924e69cb.json")).Should().BeTrue();
            File.Exists(Path.Combine(outputRoot, "tracked-source", "ribbons", "account.json")).Should().BeTrue();
            File.Exists(Path.Combine(outputRoot, "tracked-source", "web-resources", "cdxmeta-advancedui-landing-html.json")).Should().BeTrue();
            File.Exists(Path.Combine(outputRoot, "tracked-source", "web-resources", "cdxmeta-advancedui-landing-html.html")).Should().BeTrue();
            File.Exists(Path.Combine(outputRoot, "tracked-source", "source-backed", "Entities", "Account", "RibbonDiff.xml")).Should().BeTrue();
            File.ReadAllText(Path.Combine(outputRoot, "tracked-source", "app-modules", "codex_metadata_advanced_ui_924e69cb.json")).Should().Contain("\"roleIds\"");
            File.ReadAllText(Path.Combine(outputRoot, "tracked-source", "app-modules", "codex_metadata_advanced_ui_924e69cb.json")).Should().Contain("\"roleMapCount\": 2");

            var manifestJson = File.ReadAllText(Path.Combine(outputRoot, "tracked-source", "manifest.json"));
            manifestJson.Should().NotContain("C:\\Git\\Dataverse-Solution-KB");
            manifestJson.Should().Contain("tracked-source/app-modules/codex_metadata_advanced_ui_924e69cb.json");
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
    public void Emitter_copies_canvas_app_package_evidence_files()
    {
        var model = ReadFixture("seed-environment");
        var emitter = new TrackedSourceEmitter();
        var outputRoot = Path.Combine(Path.GetTempPath(), $"dsc-canvas-{Guid.NewGuid():N}");

        try
        {
            emitter.Emit(model, new EmitRequest(outputRoot, EmitLayout.TrackedSource));

            File.Exists(Path.Combine(outputRoot, "tracked-source", "canvas-apps", "cat_overview_3dbf5.json")).Should().BeTrue();
            File.Exists(Path.Combine(outputRoot, "tracked-source", "canvas-apps", "cat_overview_3dbf5_DocumentUri.msapp")).Should().BeTrue();
            File.Exists(Path.Combine(outputRoot, "tracked-source", "canvas-apps", "cat_overview_3dbf5_BackgroundImageUri")).Should().BeTrue();
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
    public void Emitter_materializes_alternate_key_tracked_source_files()
    {
        var model = ReadFixture("seed-alternate-key");
        var emitter = new TrackedSourceEmitter();
        var outputRoot = Path.Combine(Path.GetTempPath(), $"dsc-alternate-key-{Guid.NewGuid():N}");

        try
        {
            emitter.Emit(model, new EmitRequest(outputRoot, EmitLayout.TrackedSource));

            var keysPath = Path.Combine(outputRoot, "tracked-source", "entities", "cdxmeta_workitem", "keys.json");
            File.Exists(keysPath).Should().BeTrue();
            File.ReadAllText(keysPath).Should().Contain("cdxmeta_externalcode");
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
    public void Emitter_materializes_import_map_tracked_source_files()
    {
        var model = ReadFixture("seed-import-map");
        var emitter = new TrackedSourceEmitter();
        var outputRoot = Path.Combine(Path.GetTempPath(), $"dsc-import-map-{Guid.NewGuid():N}");

        try
        {
            emitter.Emit(model, new EmitRequest(outputRoot, EmitLayout.TrackedSource));

            File.Exists(Path.Combine(outputRoot, "tracked-source", "import-maps", "codex_contact_csv_map.json")).Should().BeTrue();
            File.Exists(Path.Combine(outputRoot, "tracked-source", "data-source-mappings", "codex_contact_csv_map-fullname-fullname-1.json")).Should().BeTrue();
            File.Exists(Path.Combine(outputRoot, "tracked-source", "data-source-mappings", "codex_contact_csv_map-emailaddress1-emailaddress1-2.json")).Should().BeTrue();
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
    public void Emitter_materializes_entity_analytics_tracked_source_files()
    {
        var model = ReadFixture("seed-entity-analytics");
        var emitter = new TrackedSourceEmitter();
        var outputRoot = Path.Combine(Path.GetTempPath(), $"dsc-entity-analytics-{Guid.NewGuid():N}");

        try
        {
            emitter.Emit(model, new EmitRequest(outputRoot, EmitLayout.TrackedSource));

            File.Exists(Path.Combine(outputRoot, "tracked-source", "entity-analytics-configurations", "contact.json")).Should().BeTrue();
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
    public void Emitter_materializes_reporting_legacy_tracked_source_files()
    {
        var model = ReadFixture("seed-reporting-legacy");
        var emitter = new TrackedSourceEmitter();
        var outputRoot = Path.Combine(Path.GetTempPath(), $"dsc-reporting-legacy-{Guid.NewGuid():N}");

        try
        {
            emitter.Emit(model, new EmitRequest(outputRoot, EmitLayout.TrackedSource));

            File.Exists(Path.Combine(outputRoot, "tracked-source", "reports", "cdxmeta_account_summary.json")).Should().BeTrue();
            File.Exists(Path.Combine(outputRoot, "tracked-source", "templates", "cdxmeta_welcome_email.json")).Should().BeTrue();
            File.Exists(Path.Combine(outputRoot, "tracked-source", "display-strings", "cdxmeta_reporting_labels.json")).Should().BeTrue();
            File.Exists(Path.Combine(outputRoot, "tracked-source", "attachments", "cdxmeta_report_payload.json")).Should().BeTrue();
            File.Exists(Path.Combine(outputRoot, "tracked-source", "legacy-assets", "cdxmeta_onboarding_wizard.json")).Should().BeTrue();
            File.Exists(Path.Combine(outputRoot, "tracked-source", "source-backed", "Reports", "cdxmeta_account_summary.rdl")).Should().BeTrue();
            File.Exists(Path.Combine(outputRoot, "tracked-source", "source-backed", "Attachments", "cdxmeta_report_payload.txt")).Should().BeTrue();
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
    public void Emitter_materializes_image_configuration_tracked_source_files()
    {
        var model = ReadFixture("seed-image-config");
        var emitter = new TrackedSourceEmitter();
        var outputRoot = Path.Combine(Path.GetTempPath(), $"dsc-image-config-{Guid.NewGuid():N}");

        try
        {
            emitter.Emit(model, new EmitRequest(outputRoot, EmitLayout.TrackedSource));

            var path = Path.Combine(outputRoot, "tracked-source", "entities", "cdxmeta_photoasset", "image-configurations.json");
            File.Exists(path).Should().BeTrue();
            File.ReadAllText(path).Should().Contain("cdxmeta_profileimage");
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
    public void Emitter_materializes_ai_family_tracked_source_files()
    {
        var model = ReadFixture("seed-ai-families");
        var emitter = new TrackedSourceEmitter();
        var outputRoot = Path.Combine(Path.GetTempPath(), $"dsc-ai-families-{Guid.NewGuid():N}");

        try
        {
            emitter.Emit(model, new EmitRequest(outputRoot, EmitLayout.TrackedSource));

            File.Exists(Path.Combine(outputRoot, "tracked-source", "ai-project-types", "document_automation.json")).Should().BeTrue();
            File.Exists(Path.Combine(outputRoot, "tracked-source", "ai-projects", "invoice_processing.json")).Should().BeTrue();
            File.Exists(Path.Combine(outputRoot, "tracked-source", "ai-configurations", "invoice_processing_training.json")).Should().BeTrue();
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
    public void Emitter_materializes_plugin_registration_tracked_source_files()
    {
        var model = ReadFixture("seed-plugin-registration");
        var emitter = new TrackedSourceEmitter();
        var outputRoot = Path.Combine(Path.GetTempPath(), $"dsc-plugin-registration-{Guid.NewGuid():N}");

        try
        {
            emitter.Emit(model, new EmitRequest(outputRoot, EmitLayout.TrackedSource));

            Directory.EnumerateFiles(Path.Combine(outputRoot, "tracked-source", "plugin-assemblies"), "*.json")
                .Should().ContainSingle(path => Path.GetFileName(path).StartsWith("Codex.Metadata.Plugins, Version=1.0.0.0", StringComparison.Ordinal));
            File.Exists(Path.Combine(outputRoot, "tracked-source", "plugin-types", "Codex.Metadata.Plugins.AccountUpdateTrace.json")).Should().BeTrue();
            File.Exists(Path.Combine(outputRoot, "tracked-source", "plugin-steps", "Codex.Metadata.Plugins.AccountUpdateTrace-Update-account-20-0-Account Update Trace Step.json")).Should().BeTrue();
            File.Exists(Path.Combine(outputRoot, "tracked-source", "plugin-step-images", "Codex.Metadata.Plugins.AccountUpdateTrace-Update-account-20-0-Account Update Trace Step-Account PreImage-preimage-0.json")).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(outputRoot))
            {
                Directory.Delete(outputRoot, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData("seed-code-plugin-classic", "Codex.Metadata.CodeFirst.Classic", "ClassicAssembly", "plugins/Codex.Metadata.CodeFirst.Classic/AccountCreateDescriptionPlugin.cs")]
    [InlineData("seed-code-plugin-package", "Codex.Metadata.CodeFirst.Package", "PluginPackage", "plugins/Codex.Metadata.CodeFirst.Package.Dependency/ProofMarkerProvider.cs")]
    [InlineData("seed-code-plugin-imperative", "Codex.Metadata.CodeFirst.Imperative", "ClassicAssembly", "plugins/Codex.Metadata.CodeFirst.Imperative/AccountCreateDescriptionPlugin.cs")]
    [InlineData("seed-code-plugin-helper", "Codex.Metadata.CodeFirst.Helper", "ClassicAssembly", "plugins/Codex.Metadata.CodeFirst.Helper/AccountDescriptionActivity.cs")]
    [InlineData("seed-code-plugin-imperative-service", "Codex.Metadata.CodeFirst.Imperative.Service", "ClassicAssembly", "plugins/Codex.Metadata.CodeFirst.Imperative.Service/AccountCreateDescriptionPlugin.cs")]
    public void Emitter_materializes_code_first_plugin_registration_tracked_source_files(
        string fixtureName,
        string projectName,
        string deploymentFlavor,
        string expectedAssetPath)
    {
        var model = ReadCompiledFixture(fixtureName);
        var emitter = new TrackedSourceEmitter();
        var outputRoot = Path.Combine(Path.GetTempPath(), $"dsc-code-first-tracked-{fixtureName}-{Guid.NewGuid():N}");

        try
        {
            emitter.Emit(model, new EmitRequest(outputRoot, EmitLayout.TrackedSource));

            Directory.EnumerateFiles(Path.Combine(outputRoot, "tracked-source", "plugin-assemblies"), "*.json")
                .Should().ContainSingle();
            if (string.Equals(fixtureName, "seed-code-plugin-helper", StringComparison.OrdinalIgnoreCase))
            {
                Directory.EnumerateFiles(Path.Combine(outputRoot, "tracked-source", "plugin-types"), "*.json")
                    .Should().HaveCount(2);
            }
            else
            {
                Directory.EnumerateFiles(Path.Combine(outputRoot, "tracked-source", "plugin-types"), "*.json")
                    .Should().ContainSingle();
            }
            Directory.EnumerateFiles(Path.Combine(outputRoot, "tracked-source", "plugin-steps"), "*.json")
                .Should().ContainSingle();
            Directory.EnumerateFiles(Path.Combine(outputRoot, "tracked-source", "plugin-step-images"), "*.json")
                .Should().ContainSingle();

            var pluginAssemblyJson = File.ReadAllText(Directory.EnumerateFiles(
                Path.Combine(outputRoot, "tracked-source", "plugin-assemblies"),
                "*.json").Single());
            pluginAssemblyJson.Should().Contain($"\"deploymentFlavor\": \"{deploymentFlavor}\"");
            pluginAssemblyJson.Should().Contain($"\"codeProjectPath\": \"plugins/{projectName}/{projectName}.csproj\"");

            File.Exists(Path.Combine(outputRoot, "tracked-source", "source-backed", "plugins", projectName, $"{projectName}.csproj")).Should().BeTrue();
            File.Exists(Path.Combine(outputRoot, "tracked-source", "source-backed", expectedAssetPath.Replace('/', Path.DirectorySeparatorChar))).Should().BeTrue();
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
    public void Emitter_materializes_helper_based_custom_workflow_activity_and_plugin_type_side_by_side()
    {
        var model = ReadCompiledFixture("seed-code-plugin-helper");
        var emitter = new TrackedSourceEmitter();
        var outputRoot = Path.Combine(Path.GetTempPath(), $"dsc-code-first-tracked-helper-{Guid.NewGuid():N}");

        try
        {
            emitter.Emit(model, new EmitRequest(outputRoot, EmitLayout.TrackedSource));

            var pluginTypeFiles = Directory.EnumerateFiles(Path.Combine(outputRoot, "tracked-source", "plugin-types"), "*.json").ToArray();
            pluginTypeFiles.Should().HaveCount(2);
            File.ReadAllText(pluginTypeFiles.Single(path => Path.GetFileName(path).Contains("AccountDescriptionActivity", StringComparison.OrdinalIgnoreCase)))
                .Should().Contain("\"pluginTypeKind\": \"customWorkflowActivity\"");
            File.ReadAllText(pluginTypeFiles.Single(path => Path.GetFileName(path).Contains("AccountCreateDescriptionPlugin", StringComparison.OrdinalIgnoreCase)))
                .Should().Contain("\"pluginTypeKind\": \"plugin\"");
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
    public void Emitter_materializes_custom_workflow_activity_tracked_source_files_without_step_artifacts()
    {
        var model = ReadCompiledFixture("seed-code-workflow-activity-classic");
        var emitter = new TrackedSourceEmitter();
        var outputRoot = Path.Combine(Path.GetTempPath(), $"dsc-code-first-tracked-workflow-{Guid.NewGuid():N}");

        try
        {
            emitter.Emit(model, new EmitRequest(outputRoot, EmitLayout.TrackedSource));

            Directory.EnumerateFiles(Path.Combine(outputRoot, "tracked-source", "plugin-assemblies"), "*.json")
                .Should().ContainSingle();
            Directory.EnumerateFiles(Path.Combine(outputRoot, "tracked-source", "plugin-types"), "*.json")
                .Should().ContainSingle();
            Directory.Exists(Path.Combine(outputRoot, "tracked-source", "plugin-steps")).Should().BeFalse();
            Directory.Exists(Path.Combine(outputRoot, "tracked-source", "plugin-step-images")).Should().BeFalse();

            var pluginTypeJson = File.ReadAllText(Directory.EnumerateFiles(
                Path.Combine(outputRoot, "tracked-source", "plugin-types"),
                "*.json").Single());
            pluginTypeJson.Should().Contain("\"pluginTypeKind\": \"customWorkflowActivity\"");
            pluginTypeJson.Should().Contain("\"workflowActivityGroupName\": \"Codex.Metadata.CodeFirst.WorkflowActivity.Classic (1.0.0.0)\"");
        }
        finally
        {
            if (Directory.Exists(outputRoot))
            {
                Directory.Delete(outputRoot, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData("seed-workflow-classic", "cdxmeta_accountstampworkflow")]
    [InlineData("seed-workflow-action", "cdxmeta_accountstampaction")]
    public void Emitter_materializes_workflow_tracked_source_files(
        string fixtureName,
        string expectedSlug)
    {
        var model = ReadFixture(fixtureName);
        var emitter = new TrackedSourceEmitter();
        var outputRoot = Path.Combine(Path.GetTempPath(), $"dsc-tracked-workflow-{fixtureName}-{Guid.NewGuid():N}");

        try
        {
            emitter.Emit(model, new EmitRequest(outputRoot, EmitLayout.TrackedSource));

            File.Exists(Path.Combine(outputRoot, "tracked-source", "workflows", $"{expectedSlug}.json")).Should().BeTrue();
            File.Exists(Path.Combine(outputRoot, "tracked-source", "source-backed", "Workflows", $"{expectedSlug}.json")).Should().BeTrue();
            Directory.EnumerateFiles(Path.Combine(outputRoot, "tracked-source", "source-backed", "Workflows"), "*.xaml", SearchOption.TopDirectoryOnly)
                .Should().ContainSingle();
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
    public void Emitter_materializes_service_endpoint_connector_tracked_source_files()
    {
        var model = ReadFixture("seed-service-endpoint-connector");
        var emitter = new TrackedSourceEmitter();
        var outputRoot = Path.Combine(Path.GetTempPath(), $"dsc-service-endpoint-connector-{Guid.NewGuid():N}");

        try
        {
            emitter.Emit(model, new EmitRequest(outputRoot, EmitLayout.TrackedSource));

            File.Exists(Path.Combine(outputRoot, "tracked-source", "service-endpoints", "codex_webhook_endpoint.json")).Should().BeTrue();
            File.Exists(Path.Combine(outputRoot, "tracked-source", "connectors", "shared-offerings-connector.json")).Should().BeTrue();
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
    public void Emitter_materializes_process_policy_tracked_source_files()
    {
        var model = ReadFixture("seed-process-policy");
        var emitter = new TrackedSourceEmitter();
        var outputRoot = Path.Combine(Path.GetTempPath(), $"dsc-process-policy-{Guid.NewGuid():N}");

        try
        {
            emitter.Emit(model, new EmitRequest(outputRoot, EmitLayout.TrackedSource));

            File.Exists(Path.Combine(outputRoot, "tracked-source", "duplicate-rules", "dre67df5ba444cf6a6b4092b00952064b3b91ddc3e81f6d3746c2169ae4ed2c367.json")).Should().BeTrue();
            File.Exists(Path.Combine(outputRoot, "tracked-source", "duplicate-rule-conditions", "dre67df5ba444cf6a6b4092b00952064b3b91ddc3e81f6d3746c2169ae4ed2c367-name-name-0.json")).Should().BeTrue();
            File.Exists(Path.Combine(outputRoot, "tracked-source", "routing-rules", "codex metadata routing rule.json")).Should().BeTrue();
            File.Exists(Path.Combine(outputRoot, "tracked-source", "routing-rule-items", "codex metadata routing rule-route all.json")).Should().BeTrue();
            File.Exists(Path.Combine(outputRoot, "tracked-source", "mobile-offline-profiles", "codex metadata mobile offline profile.json")).Should().BeTrue();
            File.Exists(Path.Combine(outputRoot, "tracked-source", "mobile-offline-profile-items", "codex metadata mobile offline profile-account.json")).Should().BeTrue();
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
    public void Emitter_materializes_security_tracked_source_files()
    {
        var model = ReadFixture("seed-process-security");
        var emitter = new TrackedSourceEmitter();
        var outputRoot = Path.Combine(Path.GetTempPath(), $"dsc-process-security-{Guid.NewGuid():N}");

        try
        {
            emitter.Emit(model, new EmitRequest(outputRoot, EmitLayout.TrackedSource));

            File.Exists(Path.Combine(outputRoot, "tracked-source", "roles", "codex metadata seed role.json")).Should().BeTrue();
            File.Exists(Path.Combine(outputRoot, "tracked-source", "role-privileges", "codex metadata seed role-prvReadPluginAssembly-Global.json")).Should().BeTrue();
            File.Exists(Path.Combine(outputRoot, "tracked-source", "field-security-profiles", "codex metadata seed field security.json")).Should().BeTrue();
            File.Exists(Path.Combine(outputRoot, "tracked-source", "field-permissions", "codex metadata seed field security-cdxmeta_workitem-cdxmeta_details.json")).Should().BeTrue();
            File.Exists(Path.Combine(outputRoot, "tracked-source", "connection-roles", "codex metadata seed connection role.json")).Should().BeTrue();
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
    public void Emitter_materializes_source_only_similarity_and_sla_tracked_source_files()
    {
        var emitter = new TrackedSourceEmitter();
        var similarityOutputRoot = Path.Combine(Path.GetTempPath(), $"dsc-source-only-similarity-{Guid.NewGuid():N}");
        var slaOutputRoot = Path.Combine(Path.GetTempPath(), $"dsc-source-only-sla-{Guid.NewGuid():N}");

        try
        {
            emitter.Emit(ReadFixture("source-only-similarity-rule"), new EmitRequest(similarityOutputRoot, EmitLayout.TrackedSource));
            emitter.Emit(ReadFixture("source-only-sla"), new EmitRequest(slaOutputRoot, EmitLayout.TrackedSource));

            File.Exists(Path.Combine(similarityOutputRoot, "tracked-source", "similarity-rules", "codex metadata account similarity rule.json")).Should().BeTrue();
            File.Exists(Path.Combine(slaOutputRoot, "tracked-source", "slas", "codex metadata account sla.json")).Should().BeTrue();
            File.Exists(Path.Combine(slaOutputRoot, "tracked-source", "sla-items", "codex metadata account sla-codex metadata account sla item-account.json")).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(similarityOutputRoot))
            {
                Directory.Delete(similarityOutputRoot, recursive: true);
            }

            if (Directory.Exists(slaOutputRoot))
            {
                Directory.Delete(slaOutputRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void Emitter_rejects_path_traversal_like_segments()
    {
        var emitter = new TrackedSourceEmitter();
        var outputRoot = Path.Combine(Path.GetTempPath(), $"dsc-unsafe-{Guid.NewGuid():N}");
        var model = new CanonicalSolution(
            new SolutionIdentity("sample", "Sample", "1.0.0", LayeringIntent.Hybrid),
            new PublisherDefinition("dsc", "dsc", "dsc", "Dataverse Solution Compiler"),
            [new FamilyArtifact(ComponentFamily.AppModule, "..\\escape")],
            [],
            [],
            []);

        try
        {
            var action = () => emitter.Emit(model, new EmitRequest(outputRoot, EmitLayout.TrackedSource));
            action.Should().Throw<InvalidOperationException>();
        }
        finally
        {
            if (Directory.Exists(outputRoot))
            {
                Directory.Delete(outputRoot, recursive: true);
            }
        }
    }

    private static CanonicalSolution ReadFixture(string fixtureName)
    {
        var reader = new XmlSolutionReader();
        return reader.Read(new ReadRequest(Path.Combine(
            "C:\\Git\\Dataverse-Solution-KB",
            "fixtures",
            "skill-corpus",
            "examples",
            fixtureName,
            "unpacked")));
    }

    private static CanonicalSolution ReadCompiledFixture(string fixtureName) =>
        new CompilerKernel().Compile(new CompilationRequest(Path.Combine(
            "C:\\Git\\Dataverse-Solution-KB",
            "fixtures",
            "skill-corpus",
            "examples",
            fixtureName),
            Array.Empty<string>())).Solution;

    private static IReadOnlyDictionary<string, byte[]> SnapshotTrackedSource(string outputRoot) =>
        Directory.EnumerateFiles(Path.Combine(outputRoot, "tracked-source"), "*", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToDictionary(
                path => Path.GetRelativePath(outputRoot, path).Replace('\\', '/'),
                File.ReadAllBytes,
                StringComparer.Ordinal);
}
