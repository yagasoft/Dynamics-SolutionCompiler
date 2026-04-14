using FluentAssertions;
using DataverseSolutionCompiler.Domain.Packaging;
using DataverseSolutionCompiler.Domain.Planning;
using DataverseSolutionCompiler.Packaging.Pac;
using Xunit;

namespace DataverseSolutionCompiler.UnitTests;

public sealed class PacCliExecutorTests
{
    [Fact]
    public void Pack_builds_expected_pack_and_check_commands()
    {
        var root = Path.Combine(Path.GetTempPath(), $"dsc-pac-pack-{Guid.NewGuid():N}");
        var inputRoot = Path.Combine(root, "package-inputs");
        var outputRoot = Path.Combine(root, "out");

        Directory.CreateDirectory(inputRoot);
        File.WriteAllText(
            Path.Combine(inputRoot, "manifest.json"),
            """
            {
              "solution": {
                "uniqueName": "codex_metadata_release"
              }
            }
            """);

        var runner = new RecordingProcessRunner(request =>
        {
            if (request.Arguments.SequenceEqual(["help"]))
            {
                return new ProcessRunResult(request, 0, "2.6.3", string.Empty);
            }

            if (request.Arguments.Count >= 2
                && request.Arguments[0] == "solution"
                && request.Arguments[1] == "pack")
            {
                File.WriteAllText(GetArgumentValue(request.Arguments, "--zipfile"), "zip");
                return new ProcessRunResult(request, 0, "pack ok", string.Empty);
            }

            if (request.Arguments.Count >= 2
                && request.Arguments[0] == "solution"
                && request.Arguments[1] == "check")
            {
                Directory.CreateDirectory(GetArgumentValue(request.Arguments, "--outputDirectory"));
                return new ProcessRunResult(request, 0, "check ok", string.Empty);
            }

            return new ProcessRunResult(request, 0, string.Empty, string.Empty);
        });

        try
        {
            var executor = new PacCliExecutor(runner);

            var result = executor.Pack(new PackageRequest(inputRoot, outputRoot, PackageFlavor.Managed, RunSolutionCheck: true));

            result.Success.Should().BeTrue();
            result.PackagePath.Should().Be(Path.Combine(outputRoot, "codex_metadata_release-managed.zip"));
            runner.Requests.Should().HaveCount(3);
            runner.Requests[1].Arguments.Should().ContainInOrder(
                "solution",
                "pack",
                "--zipfile",
                Path.Combine(outputRoot, "codex_metadata_release-managed.zip"),
                "--folder",
                Path.GetFullPath(inputRoot),
                "--packagetype",
                "Managed");
            runner.Requests[2].Arguments.Should().ContainInOrder(
                "solution",
                "check",
                "--path",
                Path.Combine(outputRoot, "codex_metadata_release-managed.zip"),
                "--outputDirectory",
                Path.Combine(outputRoot, "solution-check"));
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
    public void Import_includes_environment_publish_and_settings_file_when_present()
    {
        var root = Path.Combine(Path.GetTempPath(), $"dsc-pac-import-{Guid.NewGuid():N}");
        var packagePath = Path.Combine(root, "codex_metadata_release-unmanaged.zip");
        var settingsPath = Path.Combine(root, "package-inputs", "settings", "deployment-settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
        File.WriteAllText(packagePath, "zip");
        File.WriteAllText(settingsPath, "{}");

        var runner = new RecordingProcessRunner(request =>
            request.Arguments.SequenceEqual(["help"])
                ? new ProcessRunResult(request, 0, "2.6.3", string.Empty)
                : new ProcessRunResult(request, 0, "import ok", string.Empty));

        try
        {
            var executor = new PacCliExecutor(runner);

            var result = executor.Import(new ImportRequest(
                new EnvironmentProfile("dev", new Uri("https://example.crm.dynamics.com")),
                packagePath,
                PublishAfterImport: true));

            result.Success.Should().BeTrue();
            runner.Requests.Should().HaveCount(2);
            runner.Requests[1].Arguments.Should().ContainInOrder(
                "solution",
                "import",
                "--path",
                Path.GetFullPath(packagePath),
                "--environment",
                "https://example.crm.dynamics.com/");
            runner.Requests[1].Arguments.Should().Contain("--publish-changes");
            runner.Requests[1].Arguments.Should().Contain("--settings-file");
            runner.Requests[1].Arguments.Should().Contain(settingsPath);
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
    public void Pack_returns_failure_when_pac_is_unavailable()
    {
        var root = Path.Combine(Path.GetTempPath(), $"dsc-pac-missing-{Guid.NewGuid():N}");
        var inputRoot = Path.Combine(root, "package-inputs");
        Directory.CreateDirectory(inputRoot);
        var runner = new RecordingProcessRunner(request =>
            new ProcessRunResult(request, -1, string.Empty, string.Empty, new InvalidOperationException("pac missing")));
        var executor = new PacCliExecutor(runner);

        try
        {
            var result = executor.Pack(new PackageRequest(inputRoot, root, PackageFlavor.Unmanaged));

            result.Success.Should().BeFalse();
            result.Diagnostics.Should().Contain(diagnostic => diagnostic.Code == "pac-cli-unavailable");
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
    public void Import_returns_failure_on_non_zero_exit_and_captures_streams()
    {
        var root = Path.Combine(Path.GetTempPath(), $"dsc-pac-failure-{Guid.NewGuid():N}");
        var packagePath = Path.Combine(root, "codex_metadata_release-unmanaged.zip");
        Directory.CreateDirectory(root);
        File.WriteAllText(packagePath, "zip");

        var runner = new RecordingProcessRunner(request =>
            request.Arguments.SequenceEqual(["help"])
                ? new ProcessRunResult(request, 0, "2.6.3", string.Empty)
                : new ProcessRunResult(request, 9, "stdout text", "stderr text"));

        try
        {
            var executor = new PacCliExecutor(runner);

            var result = executor.Import(new ImportRequest(
                new EnvironmentProfile("dev", new Uri("https://example.crm.dynamics.com")),
                packagePath,
                PublishAfterImport: false));

            result.Success.Should().BeFalse();
            result.Diagnostics.Should().Contain(diagnostic => diagnostic.Code == "pac-import-failed");
            result.Diagnostics.Should().Contain(diagnostic => diagnostic.Code == "pac-import-failed-stdout" && diagnostic.Message.Contains("stdout text", StringComparison.Ordinal));
            result.Diagnostics.Should().Contain(diagnostic => diagnostic.Code == "pac-import-failed-stderr" && diagnostic.Message.Contains("stderr text", StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static string GetArgumentValue(IReadOnlyList<string> arguments, string flag)
    {
        var index = -1;
        for (var i = 0; i < arguments.Count; i++)
        {
            if (string.Equals(arguments[i], flag, StringComparison.Ordinal))
            {
                index = i;
                break;
            }
        }

        return index >= 0 && index + 1 < arguments.Count
            ? arguments[index + 1]
            : throw new InvalidOperationException($"Flag {flag} was not present.");
    }
}

internal sealed class RecordingProcessRunner(Func<ProcessRunRequest, ProcessRunResult> handler) : IProcessRunner
{
    public List<ProcessRunRequest> Requests { get; } = [];

    public ProcessRunResult Run(ProcessRunRequest request)
    {
        Requests.Add(request);
        return handler(request);
    }
}
