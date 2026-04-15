using System.Diagnostics;
using FluentAssertions;
using DataverseSolutionCompiler.Compiler;
using DataverseSolutionCompiler.Emitters.Package;
using DataverseSolutionCompiler.Domain.Emission;
using DataverseSolutionCompiler.Domain.Packaging;
using DataverseSolutionCompiler.Domain.Compilation;
using DataverseSolutionCompiler.Domain.Read;
using DataverseSolutionCompiler.Packaging.Pac;
using DataverseSolutionCompiler.Readers.Xml;
using Xunit;

namespace DataverseSolutionCompiler.IntegrationTests;

public sealed class PacCliExecutorIntegrationTests
{
    [Fact]
    public void Pack_invokes_real_pac_when_available()
    {
        if (!IsPacAvailable())
        {
            return;
        }

        var reader = new XmlSolutionReader();
        var model = reader.Read(new ReadRequest(Path.Combine(
            "C:\\Git\\Dataverse-Solution-KB",
            "fixtures",
            "skill-corpus",
            "examples",
            "seed-core",
            "unpacked")));
        var outputRoot = Path.Combine(Path.GetTempPath(), $"dsc-pac-integration-{Guid.NewGuid():N}");

        try
        {
            var emitted = new PackageEmitter().Emit(model, new EmitRequest(outputRoot, EmitLayout.PackageInputs));
            emitted.Success.Should().BeTrue();

            var result = new PacCliExecutor().Pack(new PackageRequest(
                Path.Combine(outputRoot, "package-inputs"),
                outputRoot,
                PackageFlavor.Unmanaged));

            result.Success.Should().BeTrue();
            result.PackagePath.Should().NotBeNullOrWhiteSpace();
            File.Exists(result.PackagePath!).Should().BeTrue();
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
    public void Pack_invokes_real_pac_for_generated_package_inputs_when_available()
    {
        if (!IsPacAvailable())
        {
            return;
        }

        var model = new CompilerKernel().Compile(new CompilationRequest(Path.Combine(
            "C:\\Git\\Dataverse-Solution-KB",
            "fixtures",
            "intent-specs",
            "seed-greenfield-v1.json"), Array.Empty<string>())).Solution;
        var outputRoot = Path.Combine(Path.GetTempPath(), $"dsc-pac-intent-{Guid.NewGuid():N}");

        try
        {
            var emitted = new PackageEmitter().Emit(model, new EmitRequest(outputRoot, EmitLayout.PackageInputs));
            emitted.Success.Should().BeTrue();
            File.ReadAllText(Path.Combine(outputRoot, "package-inputs", "Entities", "cdxmeta_WorkItem", "Entity.xml"))
                .Should().Contain("<KeyAttribute>cdxmeta_externalcode</KeyAttribute>");

            var result = new PacCliExecutor().Pack(new PackageRequest(
                Path.Combine(outputRoot, "package-inputs"),
                outputRoot,
                PackageFlavor.Unmanaged));

            result.Success.Should().BeTrue();
            result.PackagePath.Should().NotBeNullOrWhiteSpace();
            File.Exists(result.PackagePath!).Should().BeTrue();
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
    public void Pack_invokes_real_pac_for_ai_family_package_inputs_when_available()
    {
        if (!IsPacAvailable())
        {
            return;
        }

        var reader = new XmlSolutionReader();
        var model = reader.Read(new ReadRequest(Path.Combine(
            "C:\\Git\\Dataverse-Solution-KB",
            "fixtures",
            "skill-corpus",
            "examples",
            "seed-ai-families",
            "unpacked")));
        var outputRoot = Path.Combine(Path.GetTempPath(), $"dsc-pac-ai-{Guid.NewGuid():N}");

        try
        {
            var emitted = new PackageEmitter().Emit(model, new EmitRequest(outputRoot, EmitLayout.PackageInputs));
            emitted.Success.Should().BeTrue();

            var result = new PacCliExecutor().Pack(new PackageRequest(
                Path.Combine(outputRoot, "package-inputs"),
                outputRoot,
                PackageFlavor.Unmanaged));

            result.Success.Should().BeTrue();
            result.PackagePath.Should().NotBeNullOrWhiteSpace();
            File.Exists(result.PackagePath!).Should().BeTrue();
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
    public void Pack_invokes_real_pac_for_image_configuration_package_inputs_when_available()
    {
        if (!IsPacAvailable())
        {
            return;
        }

        var reader = new XmlSolutionReader();
        var model = reader.Read(new ReadRequest(Path.Combine(
            "C:\\Git\\Dataverse-Solution-KB",
            "fixtures",
            "skill-corpus",
            "examples",
            "seed-image-config",
            "unpacked")));
        var outputRoot = Path.Combine(Path.GetTempPath(), $"dsc-pac-image-config-{Guid.NewGuid():N}");

        try
        {
            var emitted = new PackageEmitter().Emit(model, new EmitRequest(outputRoot, EmitLayout.PackageInputs));
            emitted.Success.Should().BeTrue();

            var result = new PacCliExecutor().Pack(new PackageRequest(
                Path.Combine(outputRoot, "package-inputs"),
                outputRoot,
                PackageFlavor.Unmanaged));

            result.Success.Should().BeTrue();
            result.PackagePath.Should().NotBeNullOrWhiteSpace();
            File.Exists(result.PackagePath!).Should().BeTrue();
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
    public void Pack_invokes_real_pac_for_plugin_registration_package_inputs_when_available()
    {
        if (!IsPacAvailable())
        {
            return;
        }

        var reader = new XmlSolutionReader();
        var model = reader.Read(new ReadRequest(Path.Combine(
            "C:\\Git\\Dataverse-Solution-KB",
            "fixtures",
            "skill-corpus",
            "examples",
            "seed-plugin-registration",
            "unpacked")));
        var outputRoot = Path.Combine(Path.GetTempPath(), $"dsc-pac-plugin-registration-{Guid.NewGuid():N}");

        try
        {
            var emitted = new PackageEmitter().Emit(model, new EmitRequest(outputRoot, EmitLayout.PackageInputs));
            emitted.Success.Should().BeTrue();

            var result = new PacCliExecutor().Pack(new PackageRequest(
                Path.Combine(outputRoot, "package-inputs"),
                outputRoot,
                PackageFlavor.Unmanaged));

            result.Success.Should().BeTrue();
            result.PackagePath.Should().NotBeNullOrWhiteSpace();
            File.Exists(result.PackagePath!).Should().BeTrue();
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
    public void Pack_invokes_real_pac_for_service_endpoint_connector_package_inputs_when_available()
    {
        if (!IsPacAvailable())
        {
            return;
        }

        var reader = new XmlSolutionReader();
        var model = reader.Read(new ReadRequest(Path.Combine(
            "C:\\Git\\Dataverse-Solution-KB",
            "fixtures",
            "skill-corpus",
            "examples",
            "seed-service-endpoint-connector",
            "unpacked")));
        var outputRoot = Path.Combine(Path.GetTempPath(), $"dsc-pac-service-endpoint-connector-{Guid.NewGuid():N}");

        try
        {
            var emitted = new PackageEmitter().Emit(model, new EmitRequest(outputRoot, EmitLayout.PackageInputs));
            emitted.Success.Should().BeTrue();

            var result = new PacCliExecutor().Pack(new PackageRequest(
                Path.Combine(outputRoot, "package-inputs"),
                outputRoot,
                PackageFlavor.Unmanaged));

            result.Success.Should().BeTrue();
            result.PackagePath.Should().NotBeNullOrWhiteSpace();
            File.Exists(result.PackagePath!).Should().BeTrue();
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
    public void Pack_reports_explicit_root_component_boundary_for_reverse_generated_reporting_legacy_package_inputs_when_available()
    {
        if (!IsPacAvailable())
        {
            return;
        }

        var seedPath = Path.Combine(
            "C:\\Git\\Dataverse-Solution-KB",
            "fixtures",
            "skill-corpus",
            "examples",
            "seed-reporting-legacy",
            "unpacked");
        var intentOutputRoot = Path.Combine(Path.GetTempPath(), $"dsc-pac-reporting-legacy-intent-{Guid.NewGuid():N}");
        var packageOutputRoot = Path.Combine(Path.GetTempPath(), $"dsc-pac-reporting-legacy-package-{Guid.NewGuid():N}");

        try
        {
            var model = new CompilerKernel().Compile(new CompilationRequest(seedPath, Array.Empty<string>())).Solution;
            var reverseEmit = new DataverseSolutionCompiler.Emitters.TrackedSource.IntentSpecEmitter().Emit(model, new EmitRequest(intentOutputRoot, EmitLayout.IntentSpec));
            reverseEmit.Success.Should().BeTrue();

            var reversed = new CompilerKernel().Compile(new CompilationRequest(
                Path.Combine(intentOutputRoot, "intent-spec", "intent-spec.json"),
                Array.Empty<string>())).Solution;

            var emitted = new PackageEmitter().Emit(reversed, new EmitRequest(packageOutputRoot, EmitLayout.PackageInputs));
            emitted.Success.Should().BeTrue();

            var result = new PacCliExecutor().Pack(new PackageRequest(
                Path.Combine(packageOutputRoot, "package-inputs"),
                packageOutputRoot,
                PackageFlavor.Unmanaged));

            result.Success.Should().BeFalse();
            result.Diagnostics.Should().Contain(diagnostic =>
                diagnostic.Code == "pac-pack-failed-stdout"
                && diagnostic.Message.Contains("RootComponent validation failed", StringComparison.Ordinal));
            result.Diagnostics.Should().Contain(diagnostic =>
                diagnostic.Code == "pac-pack-failed-stdout"
                && diagnostic.Message.Contains("Following root components are not defined in customizations", StringComparison.Ordinal));
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
    [InlineData("seed-environment", "CodexMetadataSeedEnvironment.zip")]
    [InlineData("seed-process-policy", "CodexMetadataSeedProcessPolicy.zip")]
    [InlineData("seed-process-security", "CodexMetadataSeedProcessSecurity.zip")]
    public void Pack_invokes_real_pac_for_reverse_generated_intent_from_classic_export_zip_when_available(string seedName, string zipFileName)
    {
        if (!IsPacAvailable())
        {
            return;
        }

        var seedZipPath = Path.Combine(
            "C:\\Git\\Dataverse-Solution-KB",
            "fixtures",
            "skill-corpus",
            "examples",
            seedName,
            "export",
            zipFileName);
        var intentOutputRoot = Path.Combine(Path.GetTempPath(), $"dsc-pac-zip-intent-{Guid.NewGuid():N}");
        var packageOutputRoot = Path.Combine(Path.GetTempPath(), $"dsc-pac-zip-package-{Guid.NewGuid():N}");

        try
        {
            var model = new CompilerKernel().Compile(new CompilationRequest(seedZipPath, Array.Empty<string>())).Solution;
            var reverseEmit = new DataverseSolutionCompiler.Emitters.TrackedSource.IntentSpecEmitter().Emit(model, new EmitRequest(intentOutputRoot, EmitLayout.IntentSpec));
            reverseEmit.Success.Should().BeTrue();

            var reversed = new CompilerKernel().Compile(new CompilationRequest(
                Path.Combine(intentOutputRoot, "intent-spec", "intent-spec.json"),
                Array.Empty<string>())).Solution;

            var emitted = new PackageEmitter().Emit(reversed, new EmitRequest(packageOutputRoot, EmitLayout.PackageInputs));
            emitted.Success.Should().BeTrue();

            var result = new PacCliExecutor().Pack(new PackageRequest(
                Path.Combine(packageOutputRoot, "package-inputs"),
                packageOutputRoot,
                PackageFlavor.Unmanaged));

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
