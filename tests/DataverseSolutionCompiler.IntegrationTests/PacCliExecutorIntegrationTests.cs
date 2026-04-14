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
