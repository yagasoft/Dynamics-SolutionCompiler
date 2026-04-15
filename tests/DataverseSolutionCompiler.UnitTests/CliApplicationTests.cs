using FluentAssertions;
using System.Diagnostics;
using DataverseSolutionCompiler.Cli;
using DataverseSolutionCompiler.Compiler;
using DataverseSolutionCompiler.Domain.Abstractions;
using DataverseSolutionCompiler.Domain.Apply;
using DataverseSolutionCompiler.Domain.Compilation;
using DataverseSolutionCompiler.Domain.Diagnostics;
using DataverseSolutionCompiler.Domain.Diff;
using DataverseSolutionCompiler.Domain.Emission;
using DataverseSolutionCompiler.Domain.Explanations;
using DataverseSolutionCompiler.Domain.Live;
using DataverseSolutionCompiler.Domain.Model;
using DataverseSolutionCompiler.Domain.Packaging;
using DataverseSolutionCompiler.Domain.Planning;
using DataverseSolutionCompiler.Emitters.TrackedSource;
using Xunit;

namespace DataverseSolutionCompiler.UnitTests;

public sealed class CliApplicationTests
{
    private static readonly string SeedCorePath = Path.Combine(
        "C:\\Git\\Dataverse-Solution-KB",
        "fixtures",
        "skill-corpus",
        "examples",
        "seed-core",
        "unpacked");

    private static readonly string IntentSpecPath = Path.Combine(
        "C:\\Git\\Dataverse-Solution-KB",
        "fixtures",
        "intent-specs",
        "seed-greenfield-v1.json");

    private static readonly string SeedImportMapPath = Path.Combine(
        "C:\\Git\\Dataverse-Solution-KB",
        "fixtures",
        "skill-corpus",
        "examples",
        "seed-import-map",
        "unpacked");

    private static readonly string SeedCoreExportZipPath = Path.Combine(
        "C:\\Git\\Dataverse-Solution-KB",
        "fixtures",
        "skill-corpus",
        "examples",
        "seed-core",
        "export",
        "CodexMetadataSeedCore.zip");

    private static readonly string SeedAlternateKeyPath = Path.Combine(
        "C:\\Git\\Dataverse-Solution-KB",
        "fixtures",
        "skill-corpus",
        "examples",
        "seed-alternate-key",
        "unpacked");

    private static readonly string SeedEntityAnalyticsPath = Path.Combine(
        "C:\\Git\\Dataverse-Solution-KB",
        "fixtures",
        "skill-corpus",
        "examples",
        "seed-entity-analytics",
        "unpacked");

    private static readonly string SeedImageConfigPath = Path.Combine(
        "C:\\Git\\Dataverse-Solution-KB",
        "fixtures",
        "skill-corpus",
        "examples",
        "seed-image-config",
        "unpacked");

    private static readonly string SeedAiFamiliesPath = Path.Combine(
        "C:\\Git\\Dataverse-Solution-KB",
        "fixtures",
        "skill-corpus",
        "examples",
        "seed-ai-families",
        "unpacked");

    private static readonly string SeedPluginRegistrationPath = Path.Combine(
        "C:\\Git\\Dataverse-Solution-KB",
        "fixtures",
        "skill-corpus",
        "examples",
        "seed-plugin-registration",
        "unpacked");

    private static readonly string SeedServiceEndpointConnectorPath = Path.Combine(
        "C:\\Git\\Dataverse-Solution-KB",
        "fixtures",
        "skill-corpus",
        "examples",
        "seed-service-endpoint-connector",
        "unpacked");

    private static readonly string SeedProcessPolicyPath = Path.Combine(
        "C:\\Git\\Dataverse-Solution-KB",
        "fixtures",
        "skill-corpus",
        "examples",
        "seed-process-policy",
        "unpacked");

    private static readonly string SeedProcessSecurityPath = Path.Combine(
        "C:\\Git\\Dataverse-Solution-KB",
        "fixtures",
        "skill-corpus",
        "examples",
        "seed-process-security",
        "unpacked");

    private static readonly string SourceOnlySimilarityRulePath = Path.Combine(
        "C:\\Git\\Dataverse-Solution-KB",
        "fixtures",
        "skill-corpus",
        "examples",
        "source-only-similarity-rule",
        "unpacked");

    private static readonly string SourceOnlySlaPath = Path.Combine(
        "C:\\Git\\Dataverse-Solution-KB",
        "fixtures",
        "skill-corpus",
        "examples",
        "source-only-sla",
        "unpacked");

    [Fact]
    public void Help_prints_registered_commands_and_common_options()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = CliApplication.Run(["--help"], output, error);

        exitCode.Should().Be(0);
        output.ToString().Should().Contain("Registered commands");
        output.ToString().Should().Contain("read <path> [capabilities...] [options]");
        output.ToString().Should().Contain("--layout tracked-source|intent-spec|package-inputs");
        output.ToString().Should().Contain("--environment <absolute Dataverse URL>");
    }

    [Fact]
    public void Unknown_command_returns_non_zero_exit_code()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = CliApplication.Run(["unknown"], output, error);

        exitCode.Should().Be(1);
        error.ToString().Should().Contain("Unknown command");
    }

    [Fact]
    public void Emit_command_writes_real_tracked_source_files()
    {
        var outputRoot = Path.Combine(Path.GetTempPath(), $"dsc-cli-tracked-{Guid.NewGuid():N}");
        var output = new StringWriter();
        var error = new StringWriter();

        try
        {
            var exitCode = CliApplication.Run(["emit", SeedCorePath, "--output", outputRoot], output, error);

            exitCode.Should().Be(0);
            File.Exists(Path.Combine(outputRoot, "tracked-source", "manifest.json")).Should().BeTrue();
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
    public void Emit_command_can_write_real_package_inputs()
    {
        var outputRoot = Path.Combine(Path.GetTempPath(), $"dsc-cli-package-{Guid.NewGuid():N}");
        var output = new StringWriter();
        var error = new StringWriter();

        try
        {
            var exitCode = CliApplication.Run(["emit", SeedCorePath, "--layout", "package-inputs", "--output", outputRoot], output, error);

            exitCode.Should().Be(0);
            File.Exists(Path.Combine(outputRoot, "package-inputs", "manifest.json")).Should().BeTrue();
            File.Exists(Path.Combine(outputRoot, "package-inputs", "Other", "Solution.xml")).Should().BeTrue();
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
    public void Emit_command_can_write_intent_spec_from_unpacked_xml_input()
    {
        var outputRoot = Path.Combine(Path.GetTempPath(), $"dsc-cli-xml-intent-{Guid.NewGuid():N}");

        try
        {
            var exitCode = CliApplication.Run(["emit", SeedCorePath, "--layout", "intent-spec", "--output", outputRoot], new StringWriter(), new StringWriter());

            exitCode.Should().Be(0);
            File.Exists(Path.Combine(outputRoot, "intent-spec", "intent-spec.json")).Should().BeTrue();
            File.Exists(Path.Combine(outputRoot, "intent-spec", "reverse-generation-report.json")).Should().BeTrue();
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
    public void Emit_command_can_write_intent_spec_directly_from_classic_export_zip_input()
    {
        if (!IsPacAvailable())
        {
            return;
        }

        var outputRoot = Path.Combine(Path.GetTempPath(), $"dsc-cli-zip-intent-{Guid.NewGuid():N}");

        try
        {
            var exitCode = CliApplication.Run(["emit", SeedCoreExportZipPath, "--layout", "intent-spec", "--output", outputRoot], new StringWriter(), new StringWriter());

            exitCode.Should().Be(0);

            var intentSpecPath = Path.Combine(outputRoot, "intent-spec", "intent-spec.json");
            var reportPath = Path.Combine(outputRoot, "intent-spec", "reverse-generation-report.json");
            File.Exists(intentSpecPath).Should().BeTrue();
            File.Exists(reportPath).Should().BeTrue();

            var report = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(reportPath))!.AsObject();
            report["inputKind"]!.GetValue<string>().Should().Be("packed-zip");
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

    [Fact]
    public void Read_and_plan_commands_accept_json_intent_input()
    {
        var readOutput = new StringWriter();
        var readError = new StringWriter();
        var readExitCode = CliApplication.Run(["read", IntentSpecPath], readOutput, readError);

        var planOutput = new StringWriter();
        var planError = new StringWriter();
        var planExitCode = CliApplication.Run(["plan", IntentSpecPath], planOutput, planError);

        readExitCode.Should().Be(0);
        planExitCode.Should().Be(0);
        readOutput.ToString().Should().Contain("CodexMetadataIntentV1");
        planOutput.ToString().Should().Contain("Read");
    }

    [Fact]
    public void Emit_command_writes_real_outputs_for_json_intent_input()
    {
        var trackedOutputRoot = Path.Combine(Path.GetTempPath(), $"dsc-cli-intent-tracked-{Guid.NewGuid():N}");
        var packageOutputRoot = Path.Combine(Path.GetTempPath(), $"dsc-cli-intent-package-{Guid.NewGuid():N}");

        try
        {
            var trackedExitCode = CliApplication.Run(["emit", IntentSpecPath, "--output", trackedOutputRoot], new StringWriter(), new StringWriter());
            var packageExitCode = CliApplication.Run(["emit", IntentSpecPath, "--layout", "package-inputs", "--output", packageOutputRoot], new StringWriter(), new StringWriter());

            trackedExitCode.Should().Be(0);
            packageExitCode.Should().Be(0);
            File.Exists(Path.Combine(trackedOutputRoot, "tracked-source", "manifest.json")).Should().BeTrue();
            File.Exists(Path.Combine(packageOutputRoot, "package-inputs", "Other", "Solution.xml")).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(trackedOutputRoot))
            {
                Directory.Delete(trackedOutputRoot, recursive: true);
            }

            if (Directory.Exists(packageOutputRoot))
            {
                Directory.Delete(packageOutputRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void Emit_command_can_write_intent_spec_from_json_intent_input()
    {
        var outputRoot = Path.Combine(Path.GetTempPath(), $"dsc-cli-intent-spec-{Guid.NewGuid():N}");

        try
        {
            var exitCode = CliApplication.Run(["emit", IntentSpecPath, "--layout", "intent-spec", "--output", outputRoot], new StringWriter(), new StringWriter());

            exitCode.Should().Be(0);
            var intentSpecPath = Path.Combine(outputRoot, "intent-spec", "intent-spec.json");
            var reportPath = Path.Combine(outputRoot, "intent-spec", "reverse-generation-report.json");
            File.Exists(intentSpecPath).Should().BeTrue();
            File.Exists(reportPath).Should().BeTrue();

            var intentDocument = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(intentSpecPath))!.AsObject();
            intentDocument["specVersion"]!.GetValue<string>().Should().Be("1.0");
            intentDocument["tables"]!.AsArray()[0]!["forms"]!.AsArray()[0]!["id"]!.GetValue<string>().Should().NotBeNullOrWhiteSpace();
            intentDocument["tables"]!.AsArray()[0]!["views"]!.AsArray()[0]!["id"]!.GetValue<string>().Should().NotBeNullOrWhiteSpace();

            var report = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(reportPath))!.AsObject();
            report["isPartial"]!.GetValue<bool>().Should().BeFalse();
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
    public void Emit_command_can_reverse_generate_partial_intent_from_tracked_source_input()
    {
        var trackedOutputRoot = Path.Combine(Path.GetTempPath(), $"dsc-cli-process-policy-tracked-{Guid.NewGuid():N}");
        var intentOutputRoot = Path.Combine(Path.GetTempPath(), $"dsc-cli-process-policy-intent-{Guid.NewGuid():N}");

        try
        {
            var compiled = new CompilerKernel().Compile(new CompilationRequest(SeedProcessPolicyPath, Array.Empty<string>()));
            compiled.Success.Should().BeTrue();
            new TrackedSourceEmitter().Emit(compiled.Solution, new EmitRequest(trackedOutputRoot, EmitLayout.TrackedSource)).Success.Should().BeTrue();

            var exitCode = CliApplication.Run(
                ["emit", Path.Combine(trackedOutputRoot, "tracked-source"), "--layout", "intent-spec", "--output", intentOutputRoot],
                new StringWriter(),
                new StringWriter());

            exitCode.Should().Be(0);
            var reportPath = Path.Combine(intentOutputRoot, "intent-spec", "reverse-generation-report.json");
            File.Exists(reportPath).Should().BeTrue();
            var report = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(reportPath))!.AsObject();
            report["inputKind"]!.GetValue<string>().Should().Be("tracked-source");
            report["isPartial"]!.GetValue<bool>().Should().BeFalse();
            report["supportedFamiliesEmitted"]!.ToJsonString().Should().Contain("DuplicateRule");
            report["sourceBackedArtifactsIncluded"]!.ToJsonString().Should().Contain("DuplicateRule");
            report["sourceBackedArtifactsIncluded"]!.AsArray()
                .Any(entry => string.Equals(entry?["family"]?.GetValue<string>(), "DuplicateRule", StringComparison.Ordinal))
                .Should()
                .BeTrue();
        }
        finally
        {
            if (Directory.Exists(trackedOutputRoot))
            {
                Directory.Delete(trackedOutputRoot, recursive: true);
            }

            if (Directory.Exists(intentOutputRoot))
            {
                Directory.Delete(intentOutputRoot, recursive: true);
            }
        }
    }

    private static bool IsPacAvailable()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "pac",
                ArgumentList = { "--version" },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return false;
            }

            process.WaitForExit(5000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    [Fact]
    public void Read_and_plan_commands_accept_import_map_seed_input()
    {
        var readOutput = new StringWriter();
        var readError = new StringWriter();
        var readExitCode = CliApplication.Run(["read", SeedImportMapPath], readOutput, readError);

        var planOutput = new StringWriter();
        var planError = new StringWriter();
        var planExitCode = CliApplication.Run(["plan", SeedImportMapPath], planOutput, planError);

        readExitCode.Should().Be(0);
        planExitCode.Should().Be(0);
        readOutput.ToString().Should().Contain("CodexMetadataSeedImportMap");
        planOutput.ToString().Should().Contain("Read");
    }

    [Fact]
    public void Read_and_plan_commands_accept_alternate_key_seed_input()
    {
        var readOutput = new StringWriter();
        var readError = new StringWriter();
        var readExitCode = CliApplication.Run(["read", SeedAlternateKeyPath], readOutput, readError);

        var planOutput = new StringWriter();
        var planError = new StringWriter();
        var planExitCode = CliApplication.Run(["plan", SeedAlternateKeyPath], planOutput, planError);

        readExitCode.Should().Be(0);
        planExitCode.Should().Be(0);
        readOutput.ToString().Should().Contain("CodexMetadataSeedAlternateKey");
        planOutput.ToString().Should().Contain("Read");
    }

    [Fact]
    public void Emit_command_writes_real_outputs_for_alternate_key_seed_input()
    {
        var trackedOutputRoot = Path.Combine(Path.GetTempPath(), $"dsc-cli-alternate-key-tracked-{Guid.NewGuid():N}");
        var packageOutputRoot = Path.Combine(Path.GetTempPath(), $"dsc-cli-alternate-key-package-{Guid.NewGuid():N}");

        try
        {
            var trackedExitCode = CliApplication.Run(["emit", SeedAlternateKeyPath, "--output", trackedOutputRoot], new StringWriter(), new StringWriter());
            var packageExitCode = CliApplication.Run(["emit", SeedAlternateKeyPath, "--layout", "package-inputs", "--output", packageOutputRoot], new StringWriter(), new StringWriter());

            trackedExitCode.Should().Be(0);
            packageExitCode.Should().Be(0);
            File.Exists(Path.Combine(trackedOutputRoot, "tracked-source", "entities", "cdxmeta_workitem", "keys.json")).Should().BeTrue();
            File.Exists(Path.Combine(packageOutputRoot, "package-inputs", "Entities", "cdxmeta_WorkItem", "Entity.xml")).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(trackedOutputRoot))
            {
                Directory.Delete(trackedOutputRoot, recursive: true);
            }

            if (Directory.Exists(packageOutputRoot))
            {
                Directory.Delete(packageOutputRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void Readback_and_diff_commands_work_against_alternate_key_seed()
    {
        var runtime = new CompilerCliRuntime(
            new CompilerKernel(),
            new DataverseSolutionCompiler.Emitters.TrackedSource.TrackedSourceEmitter(),
            new DataverseSolutionCompiler.Emitters.Package.PackageEmitter(),
            new HarnessBackedLiveSnapshotProvider("seed-alternate-key"),
            new DataverseSolutionCompiler.Diff.StableOverlapDriftComparer(),
            new RecordingPackageExecutor(new PackageResult(true, Path.Combine(Path.GetTempPath(), "unused.zip"), [])),
            new RecordingImportExecutor(new ImportResult(true, Path.Combine(Path.GetTempPath(), "unused.zip"), true, [])),
            new StubApplyExecutor(),
            new StubExplanationService());

        var readbackOutput = new StringWriter();
        var readbackError = new StringWriter();
        var readbackExitCode = CliApplication.Run(
            ["readback", SeedAlternateKeyPath, "--environment", "https://example.crm.dynamics.com", "--solution", "CodexMetadataSeedAlternateKey"],
            readbackOutput,
            readbackError,
            runtime);

        var diffOutput = new StringWriter();
        var diffError = new StringWriter();
        var diffExitCode = CliApplication.Run(
            ["diff", SeedAlternateKeyPath, "--environment", "https://example.crm.dynamics.com", "--solution", "CodexMetadataSeedAlternateKey"],
            diffOutput,
            diffError,
            runtime);

        readbackExitCode.Should().Be(0);
        diffExitCode.Should().Be(0);
        readbackOutput.ToString().Should().Contain("Live artifacts:");
        diffOutput.ToString().Should().Contain("Findings:");
    }

    [Fact]
    public void Pack_command_accepts_alternate_key_seed_input_with_real_kernel_and_emitter()
    {
        var outputRoot = Path.Combine(Path.GetTempPath(), $"dsc-cli-alternate-key-pack-{Guid.NewGuid():N}");
        var runtime = new CompilerCliRuntime(
            new CompilerKernel(),
            new DataverseSolutionCompiler.Emitters.TrackedSource.TrackedSourceEmitter(),
            new DataverseSolutionCompiler.Emitters.Package.PackageEmitter(),
            new RecordingLiveSnapshotProvider(new LiveSnapshot(new EnvironmentProfile("dev"), "sample", [], [])),
            new RecordingDriftComparer(new DriftReport(false, [], [])),
            new RecordingPackageExecutor(new PackageResult(true, Path.Combine(outputRoot, "alternate-key-unmanaged.zip"), [])),
            new RecordingImportExecutor(new ImportResult(true, Path.Combine(outputRoot, "alternate-key-unmanaged.zip"), true, [])),
            new StubApplyExecutor(),
            new StubExplanationService());

        try
        {
            var output = new StringWriter();
            var error = new StringWriter();
            var exitCode = CliApplication.Run(["pack", SeedAlternateKeyPath, "--output", outputRoot], output, error, runtime);

            exitCode.Should().Be(0);
            File.ReadAllText(Path.Combine(outputRoot, "package-inputs", "Entities", "cdxmeta_WorkItem", "Entity.xml"))
                .Should().Contain("<KeyAttribute>cdxmeta_externalcode</KeyAttribute>");
            output.ToString().Should().Contain("alternate-key-unmanaged.zip");
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
    public void Emit_command_writes_real_outputs_for_import_map_seed_input()
    {
        var trackedOutputRoot = Path.Combine(Path.GetTempPath(), $"dsc-cli-import-tracked-{Guid.NewGuid():N}");
        var packageOutputRoot = Path.Combine(Path.GetTempPath(), $"dsc-cli-import-package-{Guid.NewGuid():N}");

        try
        {
            var trackedExitCode = CliApplication.Run(["emit", SeedImportMapPath, "--output", trackedOutputRoot], new StringWriter(), new StringWriter());
            var packageExitCode = CliApplication.Run(["emit", SeedImportMapPath, "--layout", "package-inputs", "--output", packageOutputRoot], new StringWriter(), new StringWriter());

            trackedExitCode.Should().Be(0);
            packageExitCode.Should().Be(0);
            File.Exists(Path.Combine(trackedOutputRoot, "tracked-source", "import-maps", "codex_contact_csv_map.json")).Should().BeTrue();
            File.Exists(Path.Combine(packageOutputRoot, "package-inputs", "ImportMaps", "codex_contact_csv_map", "ImportMap.xml")).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(trackedOutputRoot))
            {
                Directory.Delete(trackedOutputRoot, recursive: true);
            }

            if (Directory.Exists(packageOutputRoot))
            {
                Directory.Delete(packageOutputRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void Read_and_plan_commands_accept_entity_analytics_seed_input()
    {
        var readOutput = new StringWriter();
        var readError = new StringWriter();
        var readExitCode = CliApplication.Run(["read", SeedEntityAnalyticsPath], readOutput, readError);

        var planOutput = new StringWriter();
        var planError = new StringWriter();
        var planExitCode = CliApplication.Run(["plan", SeedEntityAnalyticsPath], planOutput, planError);

        readExitCode.Should().Be(0);
        planExitCode.Should().Be(0);
        readOutput.ToString().Should().Contain("CodexMetadataSeedEntityAnalytics");
        planOutput.ToString().Should().Contain("Read");
    }

    [Fact]
    public void Read_and_plan_commands_accept_image_config_seed_input()
    {
        var readOutput = new StringWriter();
        var readError = new StringWriter();
        var readExitCode = CliApplication.Run(["read", SeedImageConfigPath], readOutput, readError);

        var planOutput = new StringWriter();
        var planError = new StringWriter();
        var planExitCode = CliApplication.Run(["plan", SeedImageConfigPath], planOutput, planError);

        readExitCode.Should().Be(0);
        planExitCode.Should().Be(0);
        readOutput.ToString().Should().Contain("CodexMetadataSeedImageConfig");
        planOutput.ToString().Should().Contain("Read");
    }

    [Fact]
    public void Emit_command_writes_real_outputs_for_entity_analytics_seed_input()
    {
        var trackedOutputRoot = Path.Combine(Path.GetTempPath(), $"dsc-cli-analytics-tracked-{Guid.NewGuid():N}");
        var packageOutputRoot = Path.Combine(Path.GetTempPath(), $"dsc-cli-analytics-package-{Guid.NewGuid():N}");

        try
        {
            var trackedExitCode = CliApplication.Run(["emit", SeedEntityAnalyticsPath, "--output", trackedOutputRoot], new StringWriter(), new StringWriter());
            var packageExitCode = CliApplication.Run(["emit", SeedEntityAnalyticsPath, "--layout", "package-inputs", "--output", packageOutputRoot], new StringWriter(), new StringWriter());

            trackedExitCode.Should().Be(0);
            packageExitCode.Should().Be(0);
            File.Exists(Path.Combine(trackedOutputRoot, "tracked-source", "entity-analytics-configurations", "contact.json")).Should().BeTrue();
            File.Exists(Path.Combine(packageOutputRoot, "package-inputs", "entityanalyticsconfigs", "contact", "entityanalyticsconfig.xml")).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(trackedOutputRoot))
            {
                Directory.Delete(trackedOutputRoot, recursive: true);
            }

            if (Directory.Exists(packageOutputRoot))
            {
                Directory.Delete(packageOutputRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void Readback_and_diff_commands_work_against_entity_analytics_seed()
    {
        var runtime = new CompilerCliRuntime(
            new CompilerKernel(),
            new DataverseSolutionCompiler.Emitters.TrackedSource.TrackedSourceEmitter(),
            new DataverseSolutionCompiler.Emitters.Package.PackageEmitter(),
            new HarnessBackedLiveSnapshotProvider("seed-entity-analytics"),
            new DataverseSolutionCompiler.Diff.StableOverlapDriftComparer(),
            new RecordingPackageExecutor(new PackageResult(true, Path.Combine(Path.GetTempPath(), "unused.zip"), [])),
            new RecordingImportExecutor(new ImportResult(true, Path.Combine(Path.GetTempPath(), "unused.zip"), true, [])),
            new StubApplyExecutor(),
            new StubExplanationService());

        var readbackOutput = new StringWriter();
        var readbackError = new StringWriter();
        var readbackExitCode = CliApplication.Run(
            ["readback", SeedEntityAnalyticsPath, "--environment", "https://example.crm.dynamics.com", "--solution", "CodexMetadataSeedEntityAnalytics"],
            readbackOutput,
            readbackError,
            runtime);

        var diffOutput = new StringWriter();
        var diffError = new StringWriter();
        var diffExitCode = CliApplication.Run(
            ["diff", SeedEntityAnalyticsPath, "--environment", "https://example.crm.dynamics.com", "--solution", "CodexMetadataSeedEntityAnalytics"],
            diffOutput,
            diffError,
            runtime);

        readbackExitCode.Should().Be(0);
        diffExitCode.Should().Be(0);
        readbackOutput.ToString().Should().Contain("Live artifacts: 2");
        diffOutput.ToString().Should().Contain("Findings:");
    }

    [Fact]
    public void Emit_command_writes_real_outputs_for_image_config_seed_input()
    {
        var trackedOutputRoot = Path.Combine(Path.GetTempPath(), $"dsc-cli-image-config-tracked-{Guid.NewGuid():N}");
        var packageOutputRoot = Path.Combine(Path.GetTempPath(), $"dsc-cli-image-config-package-{Guid.NewGuid():N}");

        try
        {
            var trackedExitCode = CliApplication.Run(["emit", SeedImageConfigPath, "--output", trackedOutputRoot], new StringWriter(), new StringWriter());
            var packageExitCode = CliApplication.Run(["emit", SeedImageConfigPath, "--layout", "package-inputs", "--output", packageOutputRoot], new StringWriter(), new StringWriter());

            trackedExitCode.Should().Be(0);
            packageExitCode.Should().Be(0);
            File.Exists(Path.Combine(trackedOutputRoot, "tracked-source", "entities", "cdxmeta_photoasset", "image-configurations.json")).Should().BeTrue();
            File.Exists(Path.Combine(packageOutputRoot, "package-inputs", "Entities", "cdxmeta_PhotoAsset", "Entity.xml")).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(trackedOutputRoot))
            {
                Directory.Delete(trackedOutputRoot, recursive: true);
            }

            if (Directory.Exists(packageOutputRoot))
            {
                Directory.Delete(packageOutputRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void Readback_and_diff_commands_work_against_image_config_seed()
    {
        var runtime = new CompilerCliRuntime(
            new CompilerKernel(),
            new DataverseSolutionCompiler.Emitters.TrackedSource.TrackedSourceEmitter(),
            new DataverseSolutionCompiler.Emitters.Package.PackageEmitter(),
            new HarnessBackedLiveSnapshotProvider("seed-image-config"),
            new DataverseSolutionCompiler.Diff.StableOverlapDriftComparer(),
            new RecordingPackageExecutor(new PackageResult(true, Path.Combine(Path.GetTempPath(), "unused.zip"), [])),
            new RecordingImportExecutor(new ImportResult(true, Path.Combine(Path.GetTempPath(), "unused.zip"), true, [])),
            new StubApplyExecutor(),
            new StubExplanationService());

        var readbackOutput = new StringWriter();
        var readbackError = new StringWriter();
        var readbackExitCode = CliApplication.Run(
            ["readback", SeedImageConfigPath, "--environment", "https://example.crm.dynamics.com", "--solution", "CodexMetadataSeedImageConfig"],
            readbackOutput,
            readbackError,
            runtime);

        var diffOutput = new StringWriter();
        var diffError = new StringWriter();
        var diffExitCode = CliApplication.Run(
            ["diff", SeedImageConfigPath, "--environment", "https://example.crm.dynamics.com", "--solution", "CodexMetadataSeedImageConfig"],
            diffOutput,
            diffError,
            runtime);

        readbackExitCode.Should().Be(0);
        diffExitCode.Should().Be(0);
        readbackOutput.ToString().Should().Contain("Live artifacts:");
        diffOutput.ToString().Should().Contain("Findings:");
    }

    [Fact]
    public void Read_and_plan_commands_accept_ai_family_seed_input()
    {
        var readOutput = new StringWriter();
        var readError = new StringWriter();
        var readExitCode = CliApplication.Run(["read", SeedAiFamiliesPath], readOutput, readError);

        var planOutput = new StringWriter();
        var planError = new StringWriter();
        var planExitCode = CliApplication.Run(["plan", SeedAiFamiliesPath], planOutput, planError);

        readExitCode.Should().Be(0);
        planExitCode.Should().Be(0);
        readOutput.ToString().Should().Contain("CodexMetadataSeedAiFamilies");
        planOutput.ToString().Should().Contain("Read");
    }

    [Fact]
    public void Emit_command_writes_real_outputs_for_ai_family_seed_input()
    {
        var trackedOutputRoot = Path.Combine(Path.GetTempPath(), $"dsc-cli-ai-tracked-{Guid.NewGuid():N}");
        var packageOutputRoot = Path.Combine(Path.GetTempPath(), $"dsc-cli-ai-package-{Guid.NewGuid():N}");

        try
        {
            var trackedExitCode = CliApplication.Run(["emit", SeedAiFamiliesPath, "--output", trackedOutputRoot], new StringWriter(), new StringWriter());
            var packageExitCode = CliApplication.Run(["emit", SeedAiFamiliesPath, "--layout", "package-inputs", "--output", packageOutputRoot], new StringWriter(), new StringWriter());

            trackedExitCode.Should().Be(0);
            packageExitCode.Should().Be(0);
            File.Exists(Path.Combine(trackedOutputRoot, "tracked-source", "ai-project-types", "document_automation.json")).Should().BeTrue();
            File.Exists(Path.Combine(packageOutputRoot, "package-inputs", "AIConfigurations", "invoice_processing_training", "AIConfiguration.xml")).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(trackedOutputRoot))
            {
                Directory.Delete(trackedOutputRoot, recursive: true);
            }

            if (Directory.Exists(packageOutputRoot))
            {
                Directory.Delete(packageOutputRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void Readback_and_diff_commands_work_against_ai_family_seed()
    {
        var runtime = new CompilerCliRuntime(
            new CompilerKernel(),
            new DataverseSolutionCompiler.Emitters.TrackedSource.TrackedSourceEmitter(),
            new DataverseSolutionCompiler.Emitters.Package.PackageEmitter(),
            new HarnessBackedLiveSnapshotProvider("seed-ai-families"),
            new DataverseSolutionCompiler.Diff.StableOverlapDriftComparer(),
            new RecordingPackageExecutor(new PackageResult(true, Path.Combine(Path.GetTempPath(), "unused.zip"), [])),
            new RecordingImportExecutor(new ImportResult(true, Path.Combine(Path.GetTempPath(), "unused.zip"), true, [])),
            new StubApplyExecutor(),
            new StubExplanationService());

        var readbackOutput = new StringWriter();
        var readbackError = new StringWriter();
        var readbackExitCode = CliApplication.Run(
            ["readback", SeedAiFamiliesPath, "--environment", "https://example.crm.dynamics.com", "--solution", "CodexMetadataSeedAiFamilies"],
            readbackOutput,
            readbackError,
            runtime);

        var diffOutput = new StringWriter();
        var diffError = new StringWriter();
        var diffExitCode = CliApplication.Run(
            ["diff", SeedAiFamiliesPath, "--environment", "https://example.crm.dynamics.com", "--solution", "CodexMetadataSeedAiFamilies"],
            diffOutput,
            diffError,
            runtime);

        readbackExitCode.Should().Be(0);
        diffExitCode.Should().Be(0);
        readbackOutput.ToString().Should().Contain("Live artifacts: 4");
        diffOutput.ToString().Should().Contain("Findings:");
    }

    [Fact]
    public void Read_and_plan_commands_accept_plugin_registration_seed_input()
    {
        var readOutput = new StringWriter();
        var readError = new StringWriter();
        var readExitCode = CliApplication.Run(["read", SeedPluginRegistrationPath], readOutput, readError);

        var planOutput = new StringWriter();
        var planError = new StringWriter();
        var planExitCode = CliApplication.Run(["plan", SeedPluginRegistrationPath], planOutput, planError);

        readExitCode.Should().Be(0);
        planExitCode.Should().Be(0);
        readOutput.ToString().Should().Contain("CodexMetadataSeedPluginRegistration");
        planOutput.ToString().Should().Contain("Read");
    }

    [Fact]
    public void Emit_command_writes_real_outputs_for_plugin_registration_seed_input()
    {
        var trackedOutputRoot = Path.Combine(Path.GetTempPath(), $"dsc-cli-plugin-tracked-{Guid.NewGuid():N}");
        var packageOutputRoot = Path.Combine(Path.GetTempPath(), $"dsc-cli-plugin-package-{Guid.NewGuid():N}");

        try
        {
            var trackedExitCode = CliApplication.Run(["emit", SeedPluginRegistrationPath, "--output", trackedOutputRoot], new StringWriter(), new StringWriter());
            var packageExitCode = CliApplication.Run(["emit", SeedPluginRegistrationPath, "--layout", "package-inputs", "--output", packageOutputRoot], new StringWriter(), new StringWriter());

            trackedExitCode.Should().Be(0);
            packageExitCode.Should().Be(0);
            Directory.EnumerateFiles(Path.Combine(trackedOutputRoot, "tracked-source", "plugin-assemblies"), "*.json")
                .Should().ContainSingle(path => Path.GetFileName(path).StartsWith("Codex.Metadata.Plugins, Version=1.0.0.0", StringComparison.Ordinal));
            File.Exists(Path.Combine(packageOutputRoot, "package-inputs", "PluginAssemblies", "CodexMetadataPlugins-2F08B2D4-7F38-4B6F-84C8-5AB6FA4B6D10", "Codex.Metadata.Plugins.dll")).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(trackedOutputRoot))
            {
                Directory.Delete(trackedOutputRoot, recursive: true);
            }

            if (Directory.Exists(packageOutputRoot))
            {
                Directory.Delete(packageOutputRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void Readback_and_diff_commands_work_against_plugin_registration_seed()
    {
        var runtime = new CompilerCliRuntime(
            new CompilerKernel(),
            new DataverseSolutionCompiler.Emitters.TrackedSource.TrackedSourceEmitter(),
            new DataverseSolutionCompiler.Emitters.Package.PackageEmitter(),
            new HarnessBackedLiveSnapshotProvider("seed-plugin-registration"),
            new DataverseSolutionCompiler.Diff.StableOverlapDriftComparer(),
            new RecordingPackageExecutor(new PackageResult(true, Path.Combine(Path.GetTempPath(), "unused.zip"), [])),
            new RecordingImportExecutor(new ImportResult(true, Path.Combine(Path.GetTempPath(), "unused.zip"), true, [])),
            new StubApplyExecutor(),
            new StubExplanationService());

        var readbackOutput = new StringWriter();
        var readbackError = new StringWriter();
        var readbackExitCode = CliApplication.Run(
            ["readback", SeedPluginRegistrationPath, "--environment", "https://example.crm.dynamics.com", "--solution", "CodexMetadataSeedPluginRegistration"],
            readbackOutput,
            readbackError,
            runtime);

        var diffOutput = new StringWriter();
        var diffError = new StringWriter();
        var diffExitCode = CliApplication.Run(
            ["diff", SeedPluginRegistrationPath, "--environment", "https://example.crm.dynamics.com", "--solution", "CodexMetadataSeedPluginRegistration"],
            diffOutput,
            diffError,
            runtime);

        readbackExitCode.Should().Be(0);
        diffExitCode.Should().Be(0);
        readbackOutput.ToString().Should().Contain("Live artifacts: 5");
        diffOutput.ToString().Should().Contain("Findings: 0");
    }

    [Fact]
    public void Read_and_plan_commands_accept_service_endpoint_connector_seed_input()
    {
        var readOutput = new StringWriter();
        var readError = new StringWriter();
        var readExitCode = CliApplication.Run(["read", SeedServiceEndpointConnectorPath], readOutput, readError);

        var planOutput = new StringWriter();
        var planError = new StringWriter();
        var planExitCode = CliApplication.Run(["plan", SeedServiceEndpointConnectorPath], planOutput, planError);

        readExitCode.Should().Be(0);
        planExitCode.Should().Be(0);
        readOutput.ToString().Should().Contain("CodexMetadataSeedServiceEndpointConnector");
        planOutput.ToString().Should().Contain("Read");
    }

    [Fact]
    public void Emit_command_writes_real_outputs_for_service_endpoint_connector_seed_input()
    {
        var trackedOutputRoot = Path.Combine(Path.GetTempPath(), $"dsc-cli-service-endpoint-connector-tracked-{Guid.NewGuid():N}");
        var packageOutputRoot = Path.Combine(Path.GetTempPath(), $"dsc-cli-service-endpoint-connector-package-{Guid.NewGuid():N}");

        try
        {
            var trackedExitCode = CliApplication.Run(["emit", SeedServiceEndpointConnectorPath, "--output", trackedOutputRoot], new StringWriter(), new StringWriter());
            var packageExitCode = CliApplication.Run(["emit", SeedServiceEndpointConnectorPath, "--layout", "package-inputs", "--output", packageOutputRoot], new StringWriter(), new StringWriter());

            trackedExitCode.Should().Be(0);
            packageExitCode.Should().Be(0);
            File.Exists(Path.Combine(trackedOutputRoot, "tracked-source", "service-endpoints", "codex_webhook_endpoint.json")).Should().BeTrue();
            File.Exists(Path.Combine(trackedOutputRoot, "tracked-source", "connectors", "shared-offerings-connector.json")).Should().BeTrue();
            File.Exists(Path.Combine(packageOutputRoot, "package-inputs", "ServiceEndpoints", "codex_webhook_endpoint", "ServiceEndpoint.xml")).Should().BeTrue();
            File.Exists(Path.Combine(packageOutputRoot, "package-inputs", "Connectors", "shared-offerings-connector", "Connector.xml")).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(trackedOutputRoot))
            {
                Directory.Delete(trackedOutputRoot, recursive: true);
            }

            if (Directory.Exists(packageOutputRoot))
            {
                Directory.Delete(packageOutputRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void Readback_and_diff_commands_work_against_service_endpoint_connector_seed()
    {
        var runtime = new CompilerCliRuntime(
            new CompilerKernel(),
            new DataverseSolutionCompiler.Emitters.TrackedSource.TrackedSourceEmitter(),
            new DataverseSolutionCompiler.Emitters.Package.PackageEmitter(),
            new HarnessBackedLiveSnapshotProvider("seed-service-endpoint-connector"),
            new DataverseSolutionCompiler.Diff.StableOverlapDriftComparer(),
            new RecordingPackageExecutor(new PackageResult(true, Path.Combine(Path.GetTempPath(), "unused.zip"), [])),
            new RecordingImportExecutor(new ImportResult(true, Path.Combine(Path.GetTempPath(), "unused.zip"), true, [])),
            new StubApplyExecutor(),
            new StubExplanationService());

        var readbackOutput = new StringWriter();
        var readbackError = new StringWriter();
        var readbackExitCode = CliApplication.Run(
            ["readback", SeedServiceEndpointConnectorPath, "--environment", "https://example.crm.dynamics.com", "--solution", "CodexMetadataSeedServiceEndpointConnector"],
            readbackOutput,
            readbackError,
            runtime);

        var diffOutput = new StringWriter();
        var diffError = new StringWriter();
        var diffExitCode = CliApplication.Run(
            ["diff", SeedServiceEndpointConnectorPath, "--environment", "https://example.crm.dynamics.com", "--solution", "CodexMetadataSeedServiceEndpointConnector"],
            diffOutput,
            diffError,
            runtime);

        readbackExitCode.Should().Be(0);
        diffExitCode.Should().Be(0);
        readbackOutput.ToString().Should().Contain("Live artifacts: 3");
        diffOutput.ToString().Should().Contain("Findings: 0");
    }

    [Fact]
    public void Read_and_plan_commands_accept_process_policy_seed_input()
    {
        var readOutput = new StringWriter();
        var readError = new StringWriter();
        var readExitCode = CliApplication.Run(["read", SeedProcessPolicyPath], readOutput, readError);

        var planOutput = new StringWriter();
        var planError = new StringWriter();
        var planExitCode = CliApplication.Run(["plan", SeedProcessPolicyPath], planOutput, planError);

        readExitCode.Should().Be(0);
        planExitCode.Should().Be(0);
        readOutput.ToString().Should().Contain("CodexMetadataSeedProcessPolicy");
        planOutput.ToString().Should().Contain("Read");
    }

    [Fact]
    public void Emit_command_writes_real_outputs_for_process_policy_seed_input()
    {
        var trackedOutputRoot = Path.Combine(Path.GetTempPath(), $"dsc-cli-process-policy-tracked-{Guid.NewGuid():N}");
        var packageOutputRoot = Path.Combine(Path.GetTempPath(), $"dsc-cli-process-policy-package-{Guid.NewGuid():N}");

        try
        {
            var trackedExitCode = CliApplication.Run(["emit", SeedProcessPolicyPath, "--output", trackedOutputRoot], new StringWriter(), new StringWriter());
            var packageExitCode = CliApplication.Run(["emit", SeedProcessPolicyPath, "--layout", "package-inputs", "--output", packageOutputRoot], new StringWriter(), new StringWriter());

            trackedExitCode.Should().Be(0);
            packageExitCode.Should().Be(0);
            File.Exists(Path.Combine(trackedOutputRoot, "tracked-source", "duplicate-rules", "dre67df5ba444cf6a6b4092b00952064b3b91ddc3e81f6d3746c2169ae4ed2c367.json")).Should().BeTrue();
            File.Exists(Path.Combine(packageOutputRoot, "package-inputs", "RoutingRules", "Codex Metadata Routing Rule.meta.xml")).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(trackedOutputRoot))
            {
                Directory.Delete(trackedOutputRoot, recursive: true);
            }

            if (Directory.Exists(packageOutputRoot))
            {
                Directory.Delete(packageOutputRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void Readback_and_diff_commands_work_against_process_policy_seed()
    {
        var runtime = new CompilerCliRuntime(
            new CompilerKernel(),
            new DataverseSolutionCompiler.Emitters.TrackedSource.TrackedSourceEmitter(),
            new DataverseSolutionCompiler.Emitters.Package.PackageEmitter(),
            new RecordingLiveSnapshotProvider(new LiveSnapshot(
                new EnvironmentProfile("dev"),
                "CodexMetadataSourceOnlySla",
                [new FamilyArtifact(ComponentFamily.SolutionShell, "CodexMetadataSourceOnlySla")],
                [])),
            new DataverseSolutionCompiler.Diff.StableOverlapDriftComparer(),
            new RecordingPackageExecutor(new PackageResult(true, Path.Combine(Path.GetTempPath(), "unused.zip"), [])),
            new RecordingImportExecutor(new ImportResult(true, Path.Combine(Path.GetTempPath(), "unused.zip"), true, [])),
            new StubApplyExecutor(),
            new StubExplanationService());

        var readbackOutput = new StringWriter();
        var readbackError = new StringWriter();
        var readbackExitCode = CliApplication.Run(
            ["readback", SeedProcessPolicyPath, "--environment", "https://example.crm.dynamics.com", "--solution", "CodexMetadataSeedProcessPolicy"],
            readbackOutput,
            readbackError,
            runtime);

        var diffOutput = new StringWriter();
        var diffError = new StringWriter();
        var diffExitCode = CliApplication.Run(
            ["diff", SeedProcessPolicyPath, "--environment", "https://example.crm.dynamics.com", "--solution", "CodexMetadataSeedProcessPolicy"],
            diffOutput,
            diffError,
            runtime);

        readbackExitCode.Should().Be(0);
        diffExitCode.Should().Be(0);
        readbackOutput.ToString().Should().Contain("Live artifacts:");
        diffOutput.ToString().Should().Contain("Findings:");
    }

    [Fact]
    public void Read_and_plan_commands_accept_process_security_seed_input()
    {
        var readOutput = new StringWriter();
        var readError = new StringWriter();
        var readExitCode = CliApplication.Run(["read", SeedProcessSecurityPath], readOutput, readError);

        var planOutput = new StringWriter();
        var planError = new StringWriter();
        var planExitCode = CliApplication.Run(["plan", SeedProcessSecurityPath], planOutput, planError);

        readExitCode.Should().Be(0);
        planExitCode.Should().Be(0);
        readOutput.ToString().Should().Contain("CodexMetadataSeedProcessSecurity");
        planOutput.ToString().Should().Contain("Read");
    }

    [Fact]
    public void Emit_command_writes_real_outputs_for_process_security_seed_input()
    {
        var trackedOutputRoot = Path.Combine(Path.GetTempPath(), $"dsc-cli-process-security-tracked-{Guid.NewGuid():N}");
        var packageOutputRoot = Path.Combine(Path.GetTempPath(), $"dsc-cli-process-security-package-{Guid.NewGuid():N}");

        try
        {
            var trackedExitCode = CliApplication.Run(["emit", SeedProcessSecurityPath, "--output", trackedOutputRoot], new StringWriter(), new StringWriter());
            var packageExitCode = CliApplication.Run(["emit", SeedProcessSecurityPath, "--layout", "package-inputs", "--output", packageOutputRoot], new StringWriter(), new StringWriter());

            trackedExitCode.Should().Be(0);
            packageExitCode.Should().Be(0);
            File.Exists(Path.Combine(trackedOutputRoot, "tracked-source", "roles", "codex metadata seed role.json")).Should().BeTrue();
            File.Exists(Path.Combine(packageOutputRoot, "package-inputs", "Roles", "Codex Metadata Seed Role.xml")).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(trackedOutputRoot))
            {
                Directory.Delete(trackedOutputRoot, recursive: true);
            }

            if (Directory.Exists(packageOutputRoot))
            {
                Directory.Delete(packageOutputRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void Readback_and_diff_commands_work_against_process_security_seed()
    {
        var runtime = new CompilerCliRuntime(
            new CompilerKernel(),
            new DataverseSolutionCompiler.Emitters.TrackedSource.TrackedSourceEmitter(),
            new DataverseSolutionCompiler.Emitters.Package.PackageEmitter(),
            new HarnessBackedLiveSnapshotProvider("seed-process-security"),
            new DataverseSolutionCompiler.Diff.StableOverlapDriftComparer(),
            new RecordingPackageExecutor(new PackageResult(true, Path.Combine(Path.GetTempPath(), "unused.zip"), [])),
            new RecordingImportExecutor(new ImportResult(true, Path.Combine(Path.GetTempPath(), "unused.zip"), true, [])),
            new StubApplyExecutor(),
            new StubExplanationService());

        var readbackOutput = new StringWriter();
        var readbackError = new StringWriter();
        var readbackExitCode = CliApplication.Run(
            ["readback", SeedProcessSecurityPath, "--environment", "https://example.crm.dynamics.com", "--solution", "CodexMetadataSeedProcessSecurity"],
            readbackOutput,
            readbackError,
            runtime);

        var diffOutput = new StringWriter();
        var diffError = new StringWriter();
        var diffExitCode = CliApplication.Run(
            ["diff", SeedProcessSecurityPath, "--environment", "https://example.crm.dynamics.com", "--solution", "CodexMetadataSeedProcessSecurity"],
            diffOutput,
            diffError,
            runtime);

        readbackExitCode.Should().Be(0);
        diffExitCode.Should().Be(0);
        readbackOutput.ToString().Should().Contain("Live artifacts:");
        diffOutput.ToString().Should().Contain("Findings:");
    }

    [Fact]
    public void Read_and_plan_commands_accept_source_only_policy_seed_inputs()
    {
        var similarityRead = CliApplication.Run(["read", SourceOnlySimilarityRulePath], new StringWriter(), new StringWriter());
        var slaRead = CliApplication.Run(["read", SourceOnlySlaPath], new StringWriter(), new StringWriter());
        var similarityPlan = CliApplication.Run(["plan", SourceOnlySimilarityRulePath], new StringWriter(), new StringWriter());
        var slaPlan = CliApplication.Run(["plan", SourceOnlySlaPath], new StringWriter(), new StringWriter());

        similarityRead.Should().Be(0);
        slaRead.Should().Be(0);
        similarityPlan.Should().Be(0);
        slaPlan.Should().Be(0);
    }

    [Fact]
    public void Emit_command_writes_real_outputs_for_source_only_policy_seed_inputs()
    {
        var similarityTrackedOutputRoot = Path.Combine(Path.GetTempPath(), $"dsc-cli-source-only-similarity-tracked-{Guid.NewGuid():N}");
        var slaTrackedOutputRoot = Path.Combine(Path.GetTempPath(), $"dsc-cli-source-only-sla-tracked-{Guid.NewGuid():N}");

        try
        {
            var similarityExitCode = CliApplication.Run(["emit", SourceOnlySimilarityRulePath, "--output", similarityTrackedOutputRoot], new StringWriter(), new StringWriter());
            var slaExitCode = CliApplication.Run(["emit", SourceOnlySlaPath, "--output", slaTrackedOutputRoot], new StringWriter(), new StringWriter());

            similarityExitCode.Should().Be(0);
            slaExitCode.Should().Be(0);
            File.Exists(Path.Combine(similarityTrackedOutputRoot, "tracked-source", "similarity-rules", "codex metadata account similarity rule.json")).Should().BeTrue();
            File.Exists(Path.Combine(slaTrackedOutputRoot, "tracked-source", "slas", "codex metadata account sla.json")).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(similarityTrackedOutputRoot))
            {
                Directory.Delete(similarityTrackedOutputRoot, recursive: true);
            }

            if (Directory.Exists(slaTrackedOutputRoot))
            {
                Directory.Delete(slaTrackedOutputRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void Readback_and_diff_commands_keep_source_only_policy_families_non_blocking()
    {
        var runtime = new CompilerCliRuntime(
            new CompilerKernel(),
            new DataverseSolutionCompiler.Emitters.TrackedSource.TrackedSourceEmitter(),
            new DataverseSolutionCompiler.Emitters.Package.PackageEmitter(),
            new RecordingLiveSnapshotProvider(new LiveSnapshot(
                new EnvironmentProfile("dev"),
                "CodexMetadataSourceOnlySla",
                [new FamilyArtifact(ComponentFamily.SolutionShell, "CodexMetadataSourceOnlySla")],
                [])),
            new DataverseSolutionCompiler.Diff.StableOverlapDriftComparer(),
            new RecordingPackageExecutor(new PackageResult(true, Path.Combine(Path.GetTempPath(), "unused.zip"), [])),
            new RecordingImportExecutor(new ImportResult(true, Path.Combine(Path.GetTempPath(), "unused.zip"), true, [])),
            new StubApplyExecutor(),
            new StubExplanationService());

        var readbackOutput = new StringWriter();
        var readbackError = new StringWriter();
        var readbackExitCode = CliApplication.Run(
            ["readback", SourceOnlySlaPath, "--environment", "https://example.crm.dynamics.com", "--solution", "CodexMetadataSourceOnlySla"],
            readbackOutput,
            readbackError,
            runtime);

        var diffOutput = new StringWriter();
        var diffError = new StringWriter();
        var diffExitCode = CliApplication.Run(
            ["diff", SourceOnlySlaPath, "--environment", "https://example.crm.dynamics.com", "--solution", "CodexMetadataSourceOnlySla"],
            diffOutput,
            diffError,
            runtime);

        readbackExitCode.Should().Be(0);
        diffExitCode.Should().Be(0);
        readbackOutput.ToString().Should().Contain("Live artifacts:");
        diffOutput.ToString().Should().Contain("Findings: 0");
    }

    [Fact]
    public void Readback_and_diff_commands_use_the_runtime_services()
    {
        var kernel = new StubKernel(CreateCompilationResult());
        var liveProvider = new RecordingLiveSnapshotProvider(new LiveSnapshot(
            new EnvironmentProfile("dev", new Uri("https://example.crm.dynamics.com")),
            "sample",
            [new FamilyArtifact(ComponentFamily.Table, "account")],
            []));
        var driftComparer = new RecordingDriftComparer(new DriftReport(false, [], []));
        var runtime = CreateRuntime(kernel, liveProvider: liveProvider, driftComparer: driftComparer);

        var readbackOutput = new StringWriter();
        var readbackError = new StringWriter();
        var readbackExitCode = CliApplication.Run(
            ["readback", "C:\\source", "--environment", "https://example.crm.dynamics.com", "--solution", "sample"],
            readbackOutput,
            readbackError,
            runtime);

        var diffOutput = new StringWriter();
        var diffError = new StringWriter();
        var diffExitCode = CliApplication.Run(
            ["diff", "C:\\source", "--environment", "https://example.crm.dynamics.com", "--solution", "sample"],
            diffOutput,
            diffError,
            runtime);

        readbackExitCode.Should().Be(0);
        diffExitCode.Should().Be(0);
        liveProvider.Requests.Should().HaveCount(2);
        driftComparer.Requests.Should().ContainSingle();
        liveProvider.Requests[0].SolutionUniqueName.Should().Be("sample");
        liveProvider.Requests[0].Families.Should().Contain(ComponentFamily.Table);
    }

    [Fact]
    public void Pack_and_check_commands_orchestrate_package_emission_and_packaging()
    {
        var outputRoot = Path.Combine(Path.GetTempPath(), $"dsc-cli-check-{Guid.NewGuid():N}");
        var kernel = new StubKernel(CreateCompilationResult());
        var packageEmitter = new RecordingEmitter((model, request) =>
        {
            Directory.CreateDirectory(Path.Combine(request.OutputRoot, "package-inputs"));
            return new EmittedArtifacts(true, request.OutputRoot, [new EmittedArtifact("package-inputs/manifest.json", EmittedArtifactRole.PackageInput, "fixture")], []);
        });
        var packageExecutor = new RecordingPackageExecutor(new PackageResult(true, Path.Combine(outputRoot, "sample-managed.zip"), []));
        var runtime = CreateRuntime(kernel, packageEmitter: packageEmitter, packageExecutor: packageExecutor);

        try
        {
            var output = new StringWriter();
            var error = new StringWriter();
            var exitCode = CliApplication.Run(
                ["check", "C:\\source", "--output", outputRoot, "--managed"],
                output,
                error,
                runtime);

            exitCode.Should().Be(0);
            packageEmitter.Requests.Should().ContainSingle(request => request.Layout == EmitLayout.PackageInputs);
            packageExecutor.Requests.Should().ContainSingle();
            packageExecutor.Requests[0].Flavor.Should().Be(PackageFlavor.Managed);
            packageExecutor.Requests[0].RunSolutionCheck.Should().BeTrue();
            packageExecutor.Requests[0].InputRoot.Should().Be(Path.Combine(outputRoot, "package-inputs"));
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
    public void Pack_command_accepts_json_intent_input_with_real_kernel_and_emitter()
    {
        var outputRoot = Path.Combine(Path.GetTempPath(), $"dsc-cli-intent-pack-{Guid.NewGuid():N}");
        var runtime = new CompilerCliRuntime(
            new CompilerKernel(),
            new DataverseSolutionCompiler.Emitters.TrackedSource.TrackedSourceEmitter(),
            new DataverseSolutionCompiler.Emitters.Package.PackageEmitter(),
            new RecordingLiveSnapshotProvider(new LiveSnapshot(new EnvironmentProfile("dev"), "sample", [], [])),
            new RecordingDriftComparer(new DriftReport(false, [], [])),
            new RecordingPackageExecutor(new PackageResult(true, Path.Combine(outputRoot, "intent-unmanaged.zip"), [])),
            new RecordingImportExecutor(new ImportResult(true, Path.Combine(outputRoot, "intent-unmanaged.zip"), true, [])),
            new StubApplyExecutor(),
            new StubExplanationService());

        try
        {
            var output = new StringWriter();
            var error = new StringWriter();
            var exitCode = CliApplication.Run(["pack", IntentSpecPath, "--output", outputRoot], output, error, runtime);

            exitCode.Should().Be(0);
            File.Exists(Path.Combine(outputRoot, "package-inputs", "Other", "Solution.xml")).Should().BeTrue();
            output.ToString().Should().Contain("intent-unmanaged.zip");
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
    public void Publish_command_packs_and_imports_with_publish_enabled()
    {
        var outputRoot = Path.Combine(Path.GetTempPath(), $"dsc-cli-publish-{Guid.NewGuid():N}");
        var kernel = new StubKernel(CreateCompilationResult());
        var packageEmitter = new RecordingEmitter((model, request) =>
        {
            Directory.CreateDirectory(Path.Combine(request.OutputRoot, "package-inputs"));
            return new EmittedArtifacts(true, request.OutputRoot, [new EmittedArtifact("package-inputs/manifest.json", EmittedArtifactRole.PackageInput, "fixture")], []);
        });
        var packageExecutor = new RecordingPackageExecutor(new PackageResult(true, Path.Combine(outputRoot, "sample-unmanaged.zip"), []));
        var importExecutor = new RecordingImportExecutor(new ImportResult(true, Path.Combine(outputRoot, "sample-unmanaged.zip"), true, []));
        var runtime = CreateRuntime(kernel, packageEmitter: packageEmitter, packageExecutor: packageExecutor, importExecutor: importExecutor);

        try
        {
            var output = new StringWriter();
            var error = new StringWriter();
            var exitCode = CliApplication.Run(
                ["publish", "C:\\source", "--output", outputRoot, "--environment", "https://example.crm.dynamics.com"],
                output,
                error,
                runtime);

            exitCode.Should().Be(0);
            packageExecutor.Requests.Should().ContainSingle();
            importExecutor.Requests.Should().ContainSingle();
            importExecutor.Requests[0].PublishAfterImport.Should().BeTrue();
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
    public void Diff_returns_non_zero_when_blocking_drift_exists()
    {
        var kernel = new StubKernel(CreateCompilationResult());
        var liveProvider = new RecordingLiveSnapshotProvider(new LiveSnapshot(
            new EnvironmentProfile("dev", new Uri("https://example.crm.dynamics.com")),
            "sample",
            [new FamilyArtifact(ComponentFamily.Table, "account")],
            []));
        var driftComparer = new RecordingDriftComparer(new DriftReport(
            true,
            [new DriftFinding("Mismatch", DriftSeverity.Error, DriftCategory.Mismatch, ComponentFamily.Table, "table mismatch")],
            []));
        var runtime = CreateRuntime(kernel, liveProvider: liveProvider, driftComparer: driftComparer);

        var output = new StringWriter();
        var error = new StringWriter();
        var exitCode = CliApplication.Run(
            ["diff", "C:\\source", "--environment", "https://example.crm.dynamics.com", "--solution", "sample"],
            output,
            error,
            runtime);

        exitCode.Should().Be(1);
    }

    private static CompilationResult CreateCompilationResult() =>
        new(
            true,
            "compiled",
            new CanonicalSolution(
                new SolutionIdentity("sample", "Sample", "1.0.0.0", LayeringIntent.UnmanagedDevelopment),
                new PublisherDefinition("dsc", "dsc", "dsc", "Dataverse Solution Compiler"),
                [new FamilyArtifact(ComponentFamily.Table, "account", "Account", "C:\\source\\Entities\\Account\\Entity.xml")],
                [],
                [],
                []),
            new CompilationPlan("plan", [new PlanStep("emit-tracked-source", PlanStepKind.Emit, "emit", null, true)], []),
            [],
            []);

    private static CompilerCliRuntime CreateRuntime(
        ICompilerKernel kernel,
        ILiveSnapshotProvider? liveProvider = null,
        IDriftComparer? driftComparer = null,
        ISolutionEmitter? packageEmitter = null,
        IPackageExecutor? packageExecutor = null,
        IImportExecutor? importExecutor = null) =>
        new(
            kernel,
            new RecordingEmitter((model, request) => new EmittedArtifacts(true, request.OutputRoot, [], [])),
            packageEmitter ?? new RecordingEmitter((model, request) => new EmittedArtifacts(true, request.OutputRoot, [], [])),
            liveProvider ?? new RecordingLiveSnapshotProvider(new LiveSnapshot(new EnvironmentProfile("dev"), "sample", [], [])),
            driftComparer ?? new RecordingDriftComparer(new DriftReport(false, [], [])),
            packageExecutor ?? new RecordingPackageExecutor(new PackageResult(true, Path.Combine(Path.GetTempPath(), "sample.zip"), [])),
            importExecutor ?? new RecordingImportExecutor(new ImportResult(true, Path.Combine(Path.GetTempPath(), "sample.zip"), true, [])),
            new StubApplyExecutor(),
            new StubExplanationService());
}

internal sealed class StubKernel(CompilationResult result) : ICompilerKernel
{
    public CompilationRequest? LastRequest { get; private set; }

    public CompilationResult Compile(CompilationRequest request)
    {
        LastRequest = request;
        return result;
    }
}

internal sealed class RecordingEmitter(Func<CanonicalSolution, EmitRequest, EmittedArtifacts> handler) : ISolutionEmitter
{
    public List<EmitRequest> Requests { get; } = [];

    public EmittedArtifacts Emit(CanonicalSolution model, EmitRequest request)
    {
        Requests.Add(request);
        return handler(model, request);
    }
}

internal sealed class RecordingLiveSnapshotProvider(LiveSnapshot snapshot) : ILiveSnapshotProvider
{
    public List<ReadbackRequest> Requests { get; } = [];

    public LiveSnapshot Readback(ReadbackRequest request)
    {
        Requests.Add(request);
        return snapshot;
    }
}

internal sealed class HarnessBackedLiveSnapshotProvider(string fixtureName) : ILiveSnapshotProvider
{
    public LiveSnapshot Readback(ReadbackRequest request)
    {
        var harness = LiveFixtureHarness.Create(fixtureName);
        return harness.ReadAsync(request.Families?.ToArray() ?? []).GetAwaiter().GetResult();
    }
}

internal sealed class RecordingDriftComparer(DriftReport report) : IDriftComparer
{
    public List<(CanonicalSolution Source, LiveSnapshot Snapshot, CompareRequest Request)> Requests { get; } = [];

    public DriftReport Compare(CanonicalSolution source, LiveSnapshot snapshot, CompareRequest request)
    {
        Requests.Add((source, snapshot, request));
        return report;
    }
}

internal sealed class RecordingPackageExecutor(PackageResult result) : IPackageExecutor
{
    public List<PackageRequest> Requests { get; } = [];

    public PackageResult Pack(PackageRequest request)
    {
        Requests.Add(request);
        return result;
    }
}

internal sealed class RecordingImportExecutor(ImportResult result) : IImportExecutor
{
    public List<ImportRequest> Requests { get; } = [];

    public ImportResult Import(ImportRequest request)
    {
        Requests.Add(request);
        return result;
    }
}

internal sealed class StubApplyExecutor : IApplyExecutor
{
    public ApplyResult Apply(CanonicalSolution model, ApplyRequest request) =>
        new(true, request.Mode, [], []);
}

internal sealed class StubExplanationService : IExplanationService
{
    public HumanReport Explain(object compilerResult) =>
        new("explain", ["section"], []);
}
