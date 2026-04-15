using System.Text;
using FluentAssertions;
using DataverseSolutionCompiler.Compiler;
using DataverseSolutionCompiler.Emitters.Package;
using DataverseSolutionCompiler.Emitters.TrackedSource;
using DataverseSolutionCompiler.Domain.Emission;
using DataverseSolutionCompiler.Domain.Model;
using DataverseSolutionCompiler.Domain.Read;
using DataverseSolutionCompiler.Readers.Xml;
using Xunit;

namespace DataverseSolutionCompiler.UnitTests;

public sealed class PackageEmitterTests
{
    [Fact]
    public void Emit_materializes_a_deterministic_package_input_tree()
    {
        var model = ReadFixture("seed-advanced-ui");
        var emitter = new PackageEmitter();
        var outputRoot = Path.Combine(Path.GetTempPath(), $"dsc-package-{Guid.NewGuid():N}");

        try
        {
            var first = emitter.Emit(model, new EmitRequest(outputRoot, EmitLayout.PackageInputs));
            var firstSnapshot = SnapshotPackageInputs(outputRoot);
            var second = emitter.Emit(model, new EmitRequest(outputRoot, EmitLayout.PackageInputs));
            var secondSnapshot = SnapshotPackageInputs(outputRoot);

            first.Success.Should().BeTrue();
            second.Success.Should().BeTrue();
            first.Files.Should().Contain(file => file.RelativePath == "package-inputs/manifest.json");
            File.Exists(Path.Combine(outputRoot, "package-inputs", "Other", "Solution.xml")).Should().BeTrue();
            File.Exists(Path.Combine(outputRoot, "package-inputs", "AppModules", "codex_metadata_advanced_ui_924e69cb", "AppModule.xml")).Should().BeTrue();
            File.Exists(Path.Combine(outputRoot, "package-inputs", "WebResources", "cdxmeta_", "advancedui", "landing.html")).Should().BeTrue();
            File.Exists(Path.Combine(outputRoot, "package-inputs", "settings", "deployment-settings.json")).Should().BeFalse();

            firstSnapshot.Keys.Should().BeEquivalentTo(secondSnapshot.Keys);
            foreach (var path in firstSnapshot.Keys)
            {
                secondSnapshot[path].Should().Equal(firstSnapshot[path]);
            }

            var manifestJson = File.ReadAllText(Path.Combine(outputRoot, "package-inputs", "manifest.json"));
            manifestJson.Should().Contain("package-inputs/Other/Solution.xml");
            manifestJson.Should().NotContain("C:\\Git\\Dataverse-Solution-KB");
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
    public void Emit_writes_deployment_settings_when_environment_bindings_are_present()
    {
        var sourceRoot = Path.Combine(Path.GetTempPath(), $"dsc-package-source-{Guid.NewGuid():N}");
        var outputRoot = Path.Combine(Path.GetTempPath(), $"dsc-package-output-{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(Path.Combine(sourceRoot, "Other"));
            File.WriteAllText(
                Path.Combine(sourceRoot, "Other", "Solution.xml"),
                """
                <ImportExportXml>
                  <SolutionManifest>
                    <UniqueName>sample</UniqueName>
                  </SolutionManifest>
                </ImportExportXml>
                """,
                new UTF8Encoding(false));

            var model = new CanonicalSolution(
                new SolutionIdentity("sample", "Sample", "1.0.0.0", LayeringIntent.UnmanagedDevelopment),
                new PublisherDefinition("dsc", "dsc", "dsc", "Dataverse Solution Compiler"),
                [
                    new FamilyArtifact(
                        ComponentFamily.SolutionShell,
                        "sample",
                        "Sample",
                        Path.Combine(sourceRoot, "Other", "Solution.xml"))
                ],
                [],
                [
                    new EnvironmentBinding("cdxmeta_Mode", "EnvironmentVariable", true, "live"),
                    new EnvironmentBinding("shared_connection", "ConnectionReference", true, "shared-connection-id")
                ],
                []);

            var emitted = new PackageEmitter().Emit(model, new EmitRequest(outputRoot, EmitLayout.PackageInputs));

            emitted.Success.Should().BeTrue();
            emitted.Files.Should().Contain(file => file.Role == EmittedArtifactRole.DeploymentSetting);
            var settingsPath = Path.Combine(outputRoot, "package-inputs", "settings", "deployment-settings.json");
            File.Exists(settingsPath).Should().BeTrue();
            File.ReadAllText(settingsPath).Should().Contain("cdxmeta_Mode");
            File.ReadAllText(settingsPath).Should().Contain("shared_connection");
        }
        finally
        {
            if (Directory.Exists(sourceRoot))
            {
                Directory.Delete(sourceRoot, recursive: true);
            }

            if (Directory.Exists(outputRoot))
            {
                Directory.Delete(outputRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void Emit_rejects_solution_shell_paths_that_escape_the_expected_root_shape()
    {
        var outputRoot = Path.Combine(Path.GetTempPath(), $"dsc-package-invalid-{Guid.NewGuid():N}");
        var model = new CanonicalSolution(
            new SolutionIdentity("sample", "Sample", "1.0.0.0", LayeringIntent.UnmanagedDevelopment),
            new PublisherDefinition("dsc", "dsc", "dsc", "Dataverse Solution Compiler"),
            [
                new FamilyArtifact(
                    ComponentFamily.SolutionShell,
                    "sample",
                    "Sample",
                    "C:\\unsafe\\escape\\Solution.xml")
            ],
            [],
            [],
            []);

        try
        {
            var emitted = new PackageEmitter().Emit(model, new EmitRequest(outputRoot, EmitLayout.PackageInputs));

            emitted.Success.Should().BeFalse();
            emitted.Diagnostics.Should().Contain(diagnostic => diagnostic.Code == "package-emitter-source-root-unresolved");
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
    public void Emit_preserves_source_backed_alternate_key_layout()
    {
        var model = ReadFixture("seed-alternate-key");
        var emitter = new PackageEmitter();
        var outputRoot = Path.Combine(Path.GetTempPath(), $"dsc-package-alternate-key-{Guid.NewGuid():N}");

        try
        {
            var first = emitter.Emit(model, new EmitRequest(outputRoot, EmitLayout.PackageInputs));
            var firstSnapshot = SnapshotPackageInputs(outputRoot);
            var second = emitter.Emit(model, new EmitRequest(outputRoot, EmitLayout.PackageInputs));
            var secondSnapshot = SnapshotPackageInputs(outputRoot);

            first.Success.Should().BeTrue();
            second.Success.Should().BeTrue();
            var entityPath = Path.Combine(outputRoot, "package-inputs", "Entities", "cdxmeta_WorkItem", "Entity.xml");
            File.Exists(entityPath).Should().BeTrue();
            File.ReadAllText(entityPath).Should().Contain("<keys>");
            File.ReadAllText(entityPath).Should().Contain("<KeyAttribute>cdxmeta_externalcode</KeyAttribute>");

            firstSnapshot.Keys.Should().BeEquivalentTo(secondSnapshot.Keys);
            foreach (var path in firstSnapshot.Keys)
            {
                secondSnapshot[path].Should().Equal(firstSnapshot[path]);
            }
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
    public void Emit_preserves_source_backed_import_map_layout()
    {
        var model = ReadFixture("seed-import-map");
        var emitter = new PackageEmitter();
        var outputRoot = Path.Combine(Path.GetTempPath(), $"dsc-package-import-map-{Guid.NewGuid():N}");

        try
        {
            var first = emitter.Emit(model, new EmitRequest(outputRoot, EmitLayout.PackageInputs));
            var firstSnapshot = SnapshotPackageInputs(outputRoot);
            var second = emitter.Emit(model, new EmitRequest(outputRoot, EmitLayout.PackageInputs));
            var secondSnapshot = SnapshotPackageInputs(outputRoot);

            first.Success.Should().BeTrue();
            second.Success.Should().BeTrue();
            File.Exists(Path.Combine(outputRoot, "package-inputs", "ImportMaps", "codex_contact_csv_map", "ImportMap.xml")).Should().BeTrue();
            File.ReadAllText(Path.Combine(outputRoot, "package-inputs", "manifest.json")).Should().Contain("package-inputs/ImportMaps/codex_contact_csv_map/ImportMap.xml");

            firstSnapshot.Keys.Should().BeEquivalentTo(secondSnapshot.Keys);
            foreach (var path in firstSnapshot.Keys)
            {
                secondSnapshot[path].Should().Equal(firstSnapshot[path]);
            }
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
    public void Emit_preserves_source_backed_entity_analytics_layout()
    {
        var model = ReadFixture("seed-entity-analytics");
        var emitter = new PackageEmitter();
        var outputRoot = Path.Combine(Path.GetTempPath(), $"dsc-package-analytics-{Guid.NewGuid():N}");

        try
        {
            var first = emitter.Emit(model, new EmitRequest(outputRoot, EmitLayout.PackageInputs));
            var firstSnapshot = SnapshotPackageInputs(outputRoot);
            var second = emitter.Emit(model, new EmitRequest(outputRoot, EmitLayout.PackageInputs));
            var secondSnapshot = SnapshotPackageInputs(outputRoot);

            first.Success.Should().BeTrue();
            second.Success.Should().BeTrue();
            File.Exists(Path.Combine(outputRoot, "package-inputs", "entityanalyticsconfigs", "contact", "entityanalyticsconfig.xml")).Should().BeTrue();
            File.ReadAllText(Path.Combine(outputRoot, "package-inputs", "manifest.json")).Should().Contain("package-inputs/entityanalyticsconfigs/contact/entityanalyticsconfig.xml");

            firstSnapshot.Keys.Should().BeEquivalentTo(secondSnapshot.Keys);
            foreach (var path in firstSnapshot.Keys)
            {
                secondSnapshot[path].Should().Equal(firstSnapshot[path]);
            }
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
    public void Emit_preserves_source_backed_canvas_app_layout()
    {
        var model = ReadFixture("seed-environment");
        var emitter = new PackageEmitter();
        var outputRoot = Path.Combine(Path.GetTempPath(), $"dsc-package-environment-{Guid.NewGuid():N}");

        try
        {
            var first = emitter.Emit(model, new EmitRequest(outputRoot, EmitLayout.PackageInputs));
            var firstSnapshot = SnapshotPackageInputs(outputRoot);
            var second = emitter.Emit(model, new EmitRequest(outputRoot, EmitLayout.PackageInputs));
            var secondSnapshot = SnapshotPackageInputs(outputRoot);

            first.Success.Should().BeTrue();
            second.Success.Should().BeTrue();
            File.Exists(Path.Combine(outputRoot, "package-inputs", "CanvasApps", "cat_overview_3dbf5.meta.xml")).Should().BeTrue();
            File.ReadAllText(Path.Combine(outputRoot, "package-inputs", "Other", "Customizations.xml")).Should().Contain("<CanvasApps");

            firstSnapshot.Keys.Should().BeEquivalentTo(secondSnapshot.Keys);
            foreach (var path in firstSnapshot.Keys)
            {
                secondSnapshot[path].Should().Equal(firstSnapshot[path]);
            }
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
    public void Emit_preserves_source_backed_reporting_legacy_layout()
    {
        var model = ReadFixture("seed-reporting-legacy");
        var emitter = new PackageEmitter();
        var outputRoot = Path.Combine(Path.GetTempPath(), $"dsc-package-reporting-legacy-{Guid.NewGuid():N}");

        try
        {
            var first = emitter.Emit(model, new EmitRequest(outputRoot, EmitLayout.PackageInputs));
            var firstSnapshot = SnapshotPackageInputs(outputRoot);
            var second = emitter.Emit(model, new EmitRequest(outputRoot, EmitLayout.PackageInputs));
            var secondSnapshot = SnapshotPackageInputs(outputRoot);

            first.Success.Should().BeTrue();
            second.Success.Should().BeTrue();
            File.Exists(Path.Combine(outputRoot, "package-inputs", "Reports", "cdxmeta_account_summary.rdl.data.xml")).Should().BeTrue();
            File.Exists(Path.Combine(outputRoot, "package-inputs", "Reports", "cdxmeta_account_summary.rdl")).Should().BeTrue();
            File.Exists(Path.Combine(outputRoot, "package-inputs", "Templates", "cdxmeta_welcome_email.xml")).Should().BeTrue();
            File.Exists(Path.Combine(outputRoot, "package-inputs", "DisplayStrings", "cdxmeta_reporting_labels.xml")).Should().BeTrue();
            File.Exists(Path.Combine(outputRoot, "package-inputs", "Attachments", "cdxmeta_report_payload.txt.data.xml")).Should().BeTrue();
            File.Exists(Path.Combine(outputRoot, "package-inputs", "Attachments", "cdxmeta_report_payload.txt")).Should().BeTrue();
            File.Exists(Path.Combine(outputRoot, "package-inputs", "WebWizards", "cdxmeta_onboarding_wizard.xml")).Should().BeTrue();
            File.ReadAllText(Path.Combine(outputRoot, "package-inputs", "manifest.json")).Should().Contain("package-inputs/Reports/cdxmeta_account_summary.rdl.data.xml");
            File.ReadAllText(Path.Combine(outputRoot, "package-inputs", "manifest.json")).Should().Contain("package-inputs/Attachments/cdxmeta_report_payload.txt");

            firstSnapshot.Keys.Should().BeEquivalentTo(secondSnapshot.Keys);
            foreach (var path in firstSnapshot.Keys)
            {
                secondSnapshot[path].Should().Equal(firstSnapshot[path]);
            }
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
    public void Emit_sanitizes_hybrid_entity_analytics_package_for_live_apply()
    {
        var sourceModel = ReadFixture("seed-entity-analytics");
        var reverseOutputRoot = Path.Combine(Path.GetTempPath(), $"dsc-package-analytics-intent-{Guid.NewGuid():N}");
        var packageOutputRoot = Path.Combine(Path.GetTempPath(), $"dsc-package-analytics-hybrid-{Guid.NewGuid():N}");

        try
        {
            var reverseEmit = new IntentSpecEmitter().Emit(sourceModel, new EmitRequest(reverseOutputRoot, EmitLayout.IntentSpec));
            reverseEmit.Success.Should().BeTrue();

            var reverseIntentPath = Path.Combine(reverseOutputRoot, "intent-spec", "intent-spec.json");
            var hybridModel = new CompilerKernel().Compile(new DataverseSolutionCompiler.Domain.Compilation.CompilationRequest(reverseIntentPath, Array.Empty<string>())).Solution;

            var emitted = new PackageEmitter().Emit(hybridModel, new EmitRequest(packageOutputRoot, EmitLayout.PackageInputs));

            emitted.Success.Should().BeTrue();
            Directory.Exists(Path.Combine(packageOutputRoot, "package-inputs", "entityanalyticsconfigs")).Should().BeFalse();
            var solutionXml = File.ReadAllText(Path.Combine(packageOutputRoot, "package-inputs", "Other", "Solution.xml"));
            solutionXml.Should().NotContain("type=\"430\"");
            solutionXml.Should().NotContain("schemaName=\"contact\"");
        }
        finally
        {
            if (Directory.Exists(reverseOutputRoot))
            {
                Directory.Delete(reverseOutputRoot, recursive: true);
            }

            if (Directory.Exists(packageOutputRoot))
            {
                Directory.Delete(packageOutputRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void Emit_sanitizes_hybrid_ai_package_for_live_apply()
    {
        var sourceModel = ReadFixture("seed-ai-families");
        var reverseOutputRoot = Path.Combine(Path.GetTempPath(), $"dsc-package-ai-intent-{Guid.NewGuid():N}");
        var packageOutputRoot = Path.Combine(Path.GetTempPath(), $"dsc-package-ai-hybrid-{Guid.NewGuid():N}");

        try
        {
            var reverseEmit = new IntentSpecEmitter().Emit(sourceModel, new EmitRequest(reverseOutputRoot, EmitLayout.IntentSpec));
            reverseEmit.Success.Should().BeTrue();

            var reverseIntentPath = Path.Combine(reverseOutputRoot, "intent-spec", "intent-spec.json");
            var hybridModel = new CompilerKernel().Compile(new DataverseSolutionCompiler.Domain.Compilation.CompilationRequest(reverseIntentPath, Array.Empty<string>())).Solution;

            var emitted = new PackageEmitter().Emit(hybridModel, new EmitRequest(packageOutputRoot, EmitLayout.PackageInputs));

            emitted.Success.Should().BeTrue();
            Directory.Exists(Path.Combine(packageOutputRoot, "package-inputs", "AIProjectTypes")).Should().BeFalse();
            Directory.Exists(Path.Combine(packageOutputRoot, "package-inputs", "AIProjects")).Should().BeFalse();
            Directory.Exists(Path.Combine(packageOutputRoot, "package-inputs", "AIConfigurations")).Should().BeFalse();
            var solutionXml = File.ReadAllText(Path.Combine(packageOutputRoot, "package-inputs", "Other", "Solution.xml"));
            solutionXml.Should().NotContain("type=\"400\"");
            solutionXml.Should().NotContain("type=\"401\"");
            solutionXml.Should().NotContain("type=\"402\"");
        }
        finally
        {
            if (Directory.Exists(reverseOutputRoot))
            {
                Directory.Delete(reverseOutputRoot, recursive: true);
            }

            if (Directory.Exists(packageOutputRoot))
            {
                Directory.Delete(packageOutputRoot, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData("seed-environment", "<CanvasApps")]
    [InlineData("seed-service-endpoint-connector", "")]
    [InlineData("seed-process-policy", "<RoutingRules")]
    [InlineData("seed-reporting-legacy", "<Reports;<DisplayStrings;<Attachments;<WebWizards")]
    public void Emit_hybrid_source_backed_packages_augment_customizations_shells(string fixtureName, string expectedShellFragments)
    {
        var sourceModel = ReadFixture(fixtureName);
        var reverseOutputRoot = Path.Combine(Path.GetTempPath(), $"dsc-package-hybrid-intent-{fixtureName}-{Guid.NewGuid():N}");
        var packageOutputRoot = Path.Combine(Path.GetTempPath(), $"dsc-package-hybrid-package-{fixtureName}-{Guid.NewGuid():N}");

        try
        {
            var reverseEmit = new IntentSpecEmitter().Emit(sourceModel, new EmitRequest(reverseOutputRoot, EmitLayout.IntentSpec));
            reverseEmit.Success.Should().BeTrue();

            var reverseIntentPath = Path.Combine(reverseOutputRoot, "intent-spec", "intent-spec.json");
            var hybridModel = new CompilerKernel().Compile(new DataverseSolutionCompiler.Domain.Compilation.CompilationRequest(reverseIntentPath, Array.Empty<string>())).Solution;

            var emitted = new PackageEmitter().Emit(hybridModel, new EmitRequest(packageOutputRoot, EmitLayout.PackageInputs));

            emitted.Success.Should().BeTrue();
            var customizationsXml = File.ReadAllText(Path.Combine(packageOutputRoot, "package-inputs", "Other", "Customizations.xml"));
            foreach (var expectedShellFragment in expectedShellFragments.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                customizationsXml.Should().Contain(expectedShellFragment);
            }

            if (string.Equals(fixtureName, "seed-service-endpoint-connector", StringComparison.Ordinal))
            {
                var solutionXml = File.ReadAllText(Path.Combine(packageOutputRoot, "package-inputs", "Other", "Solution.xml"));
                solutionXml.Should().NotContain("type=\"95\"");
                solutionXml.Should().NotContain("type=\"371\"");
                customizationsXml.Should().NotContain("<ServiceEndpoints");
                customizationsXml.Should().NotContain("<Connectors");
            }
        }
        finally
        {
            if (Directory.Exists(reverseOutputRoot))
            {
                Directory.Delete(reverseOutputRoot, recursive: true);
            }

            if (Directory.Exists(packageOutputRoot))
            {
                Directory.Delete(packageOutputRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void Emit_preserves_source_backed_image_configuration_layout()
    {
        var model = ReadFixture("seed-image-config");
        var emitter = new PackageEmitter();
        var outputRoot = Path.Combine(Path.GetTempPath(), $"dsc-package-image-config-{Guid.NewGuid():N}");

        try
        {
            var first = emitter.Emit(model, new EmitRequest(outputRoot, EmitLayout.PackageInputs));
            var firstSnapshot = SnapshotPackageInputs(outputRoot);
            var second = emitter.Emit(model, new EmitRequest(outputRoot, EmitLayout.PackageInputs));
            var secondSnapshot = SnapshotPackageInputs(outputRoot);

            first.Success.Should().BeTrue();
            second.Success.Should().BeTrue();
            var entityPath = Path.Combine(outputRoot, "package-inputs", "Entities", "cdxmeta_PhotoAsset", "Entity.xml");
            File.Exists(entityPath).Should().BeTrue();
            var entityXml = File.ReadAllText(entityPath);
            entityXml.Should().Contain("<PrimaryImageAttribute>cdxmeta_profileimage</PrimaryImageAttribute>");
            entityXml.Should().Contain("<CanStoreFullImage>1</CanStoreFullImage>");
            entityXml.Should().Contain("<attribute PhysicalName=\"cdxmeta_ProfileImage\">");
            entityXml.Should().Contain("<Name>cdxmeta_ProfileImage</Name>");

            firstSnapshot.Keys.Should().BeEquivalentTo(secondSnapshot.Keys);
            foreach (var path in firstSnapshot.Keys)
            {
                secondSnapshot[path].Should().Equal(firstSnapshot[path]);
            }
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
    public void Emit_preserves_source_backed_ai_family_layout()
    {
        var model = ReadFixture("seed-ai-families");
        var emitter = new PackageEmitter();
        var outputRoot = Path.Combine(Path.GetTempPath(), $"dsc-package-ai-{Guid.NewGuid():N}");

        try
        {
            var first = emitter.Emit(model, new EmitRequest(outputRoot, EmitLayout.PackageInputs));
            var firstSnapshot = SnapshotPackageInputs(outputRoot);
            var second = emitter.Emit(model, new EmitRequest(outputRoot, EmitLayout.PackageInputs));
            var secondSnapshot = SnapshotPackageInputs(outputRoot);

            first.Success.Should().BeTrue();
            second.Success.Should().BeTrue();
            File.Exists(Path.Combine(outputRoot, "package-inputs", "AIProjectTypes", "document_automation", "AIProjectType.xml")).Should().BeTrue();
            File.Exists(Path.Combine(outputRoot, "package-inputs", "AIProjects", "invoice_processing", "AIProject.xml")).Should().BeTrue();
            File.Exists(Path.Combine(outputRoot, "package-inputs", "AIConfigurations", "invoice_processing_training", "AIConfiguration.xml")).Should().BeTrue();
            File.ReadAllText(Path.Combine(outputRoot, "package-inputs", "manifest.json")).Should().Contain("package-inputs/AIConfigurations/invoice_processing_training/AIConfiguration.xml");

            firstSnapshot.Keys.Should().BeEquivalentTo(secondSnapshot.Keys);
            foreach (var path in firstSnapshot.Keys)
            {
                secondSnapshot[path].Should().Equal(firstSnapshot[path]);
            }
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
    public void Emit_preserves_source_backed_plugin_registration_layout()
    {
        var model = ReadFixture("seed-plugin-registration");
        var emitter = new PackageEmitter();
        var outputRoot = Path.Combine(Path.GetTempPath(), $"dsc-package-plugin-registration-{Guid.NewGuid():N}");

        try
        {
            var first = emitter.Emit(model, new EmitRequest(outputRoot, EmitLayout.PackageInputs));
            var firstSnapshot = SnapshotPackageInputs(outputRoot);
            var second = emitter.Emit(model, new EmitRequest(outputRoot, EmitLayout.PackageInputs));
            var secondSnapshot = SnapshotPackageInputs(outputRoot);

            first.Success.Should().BeTrue();
            second.Success.Should().BeTrue();
            File.Exists(Path.Combine(outputRoot, "package-inputs", "Other", "Customizations.xml")).Should().BeTrue();
            File.Exists(Path.Combine(outputRoot, "package-inputs", "PluginAssemblies", "CodexMetadataPlugins-2F08B2D4-7F38-4B6F-84C8-5AB6FA4B6D10", "Codex.Metadata.Plugins.dll")).Should().BeTrue();
            File.ReadAllText(Path.Combine(outputRoot, "package-inputs", "manifest.json")).Should().Contain("package-inputs/PluginAssemblies/CodexMetadataPlugins-2F08B2D4-7F38-4B6F-84C8-5AB6FA4B6D10/Codex.Metadata.Plugins.dll");

            firstSnapshot.Keys.Should().BeEquivalentTo(secondSnapshot.Keys);
            foreach (var path in firstSnapshot.Keys)
            {
                secondSnapshot[path].Should().Equal(firstSnapshot[path]);
            }
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
    public void Emit_preserves_source_backed_service_endpoint_connector_layout()
    {
        var model = ReadFixture("seed-service-endpoint-connector");
        var emitter = new PackageEmitter();
        var outputRoot = Path.Combine(Path.GetTempPath(), $"dsc-package-service-endpoint-connector-{Guid.NewGuid():N}");

        try
        {
            var first = emitter.Emit(model, new EmitRequest(outputRoot, EmitLayout.PackageInputs));
            var firstSnapshot = SnapshotPackageInputs(outputRoot);
            var second = emitter.Emit(model, new EmitRequest(outputRoot, EmitLayout.PackageInputs));
            var secondSnapshot = SnapshotPackageInputs(outputRoot);

            first.Success.Should().BeTrue();
            second.Success.Should().BeTrue();
            File.Exists(Path.Combine(outputRoot, "package-inputs", "ServiceEndpoints", "codex_webhook_endpoint", "ServiceEndpoint.xml")).Should().BeTrue();
            File.Exists(Path.Combine(outputRoot, "package-inputs", "Connectors", "shared-offerings-connector", "Connector.xml")).Should().BeTrue();
            File.ReadAllText(Path.Combine(outputRoot, "package-inputs", "manifest.json")).Should().Contain("package-inputs/ServiceEndpoints/codex_webhook_endpoint/ServiceEndpoint.xml");
            File.ReadAllText(Path.Combine(outputRoot, "package-inputs", "manifest.json")).Should().Contain("package-inputs/Connectors/shared-offerings-connector/Connector.xml");
            File.ReadAllText(Path.Combine(outputRoot, "package-inputs", "Other", "Customizations.xml")).Should().Contain("<ServiceEndpoints");
            File.ReadAllText(Path.Combine(outputRoot, "package-inputs", "Other", "Customizations.xml")).Should().Contain("<Connectors");

            firstSnapshot.Keys.Should().BeEquivalentTo(secondSnapshot.Keys);
            foreach (var path in firstSnapshot.Keys)
            {
                secondSnapshot[path].Should().Equal(firstSnapshot[path]);
            }
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
    public void Emit_preserves_source_backed_process_policy_layout()
    {
        var model = ReadFixture("seed-process-policy");
        var emitter = new PackageEmitter();
        var outputRoot = Path.Combine(Path.GetTempPath(), $"dsc-package-process-policy-{Guid.NewGuid():N}");

        try
        {
            var first = emitter.Emit(model, new EmitRequest(outputRoot, EmitLayout.PackageInputs));
            var firstSnapshot = SnapshotPackageInputs(outputRoot);
            var second = emitter.Emit(model, new EmitRequest(outputRoot, EmitLayout.PackageInputs));
            var secondSnapshot = SnapshotPackageInputs(outputRoot);

            first.Success.Should().BeTrue();
            second.Success.Should().BeTrue();
            File.Exists(Path.Combine(outputRoot, "package-inputs", "duplicaterules", "dre67df5ba444cf6a6b4092b00952064b3b91ddc3e81f6d3746c2169ae4ed2c367", "duplicaterule.xml")).Should().BeTrue();
            File.Exists(Path.Combine(outputRoot, "package-inputs", "RoutingRules", "Codex Metadata Routing Rule.meta.xml")).Should().BeTrue();
            File.Exists(Path.Combine(outputRoot, "package-inputs", "MobileOfflineProfiles", "Codex Metadata Mobile Offline Profile.xml")).Should().BeTrue();
            File.ReadAllText(Path.Combine(outputRoot, "package-inputs", "Other", "Customizations.xml")).Should().Contain("<RoutingRules");
            File.ReadAllText(Path.Combine(outputRoot, "package-inputs", "Other", "Customizations.xml")).Should().Contain("<MobileOfflineProfiles");

            firstSnapshot.Keys.Should().BeEquivalentTo(secondSnapshot.Keys);
            foreach (var path in firstSnapshot.Keys)
            {
                secondSnapshot[path].Should().Equal(firstSnapshot[path]);
            }
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
    public void Emit_preserves_source_backed_security_layout()
    {
        var model = ReadFixture("seed-process-security");
        var emitter = new PackageEmitter();
        var outputRoot = Path.Combine(Path.GetTempPath(), $"dsc-package-process-security-{Guid.NewGuid():N}");

        try
        {
            var first = emitter.Emit(model, new EmitRequest(outputRoot, EmitLayout.PackageInputs));
            var firstSnapshot = SnapshotPackageInputs(outputRoot);
            var second = emitter.Emit(model, new EmitRequest(outputRoot, EmitLayout.PackageInputs));
            var secondSnapshot = SnapshotPackageInputs(outputRoot);

            first.Success.Should().BeTrue();
            second.Success.Should().BeTrue();
            File.Exists(Path.Combine(outputRoot, "package-inputs", "Roles", "Codex Metadata Seed Role.xml")).Should().BeTrue();
            File.Exists(Path.Combine(outputRoot, "package-inputs", "Other", "FieldSecurityProfiles.xml")).Should().BeTrue();
            File.Exists(Path.Combine(outputRoot, "package-inputs", "Other", "ConnectionRoles.xml")).Should().BeTrue();

            firstSnapshot.Keys.Should().BeEquivalentTo(secondSnapshot.Keys);
            foreach (var path in firstSnapshot.Keys)
            {
                secondSnapshot[path].Should().Equal(firstSnapshot[path]);
            }
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
    public void Emit_preserves_source_only_similarity_and_sla_layouts()
    {
        var similarityOutputRoot = Path.Combine(Path.GetTempPath(), $"dsc-package-similarity-{Guid.NewGuid():N}");
        var slaOutputRoot = Path.Combine(Path.GetTempPath(), $"dsc-package-sla-{Guid.NewGuid():N}");
        var emitter = new PackageEmitter();

        try
        {
            var similarity = emitter.Emit(ReadFixture("source-only-similarity-rule"), new EmitRequest(similarityOutputRoot, EmitLayout.PackageInputs));
            var sla = emitter.Emit(ReadFixture("source-only-sla"), new EmitRequest(slaOutputRoot, EmitLayout.PackageInputs));

            similarity.Success.Should().BeTrue();
            sla.Success.Should().BeTrue();
            File.Exists(Path.Combine(similarityOutputRoot, "package-inputs", "Other", "Customizations.xml")).Should().BeTrue();
            File.Exists(Path.Combine(slaOutputRoot, "package-inputs", "Other", "Customizations.xml")).Should().BeTrue();
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

    private static IReadOnlyDictionary<string, byte[]> SnapshotPackageInputs(string outputRoot) =>
        Directory.EnumerateFiles(Path.Combine(outputRoot, "package-inputs"), "*", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToDictionary(
                path => Path.GetRelativePath(outputRoot, path).Replace('\\', '/'),
                File.ReadAllBytes,
                StringComparer.Ordinal);
}
