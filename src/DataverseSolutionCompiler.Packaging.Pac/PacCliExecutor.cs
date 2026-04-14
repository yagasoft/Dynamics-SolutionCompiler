using System.Diagnostics;
using System.Text.Json;
using DataverseSolutionCompiler.Domain.Abstractions;
using DataverseSolutionCompiler.Domain.Diagnostics;
using DataverseSolutionCompiler.Domain.Packaging;

namespace DataverseSolutionCompiler.Packaging.Pac;

public sealed class PacCliExecutor : IPackageExecutor, IImportExecutor
{
    private readonly IProcessRunner _processRunner;

    public PacCliExecutor()
        : this(new DefaultProcessRunner())
    {
    }

    internal PacCliExecutor(IProcessRunner processRunner)
    {
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
    }

    public PackageResult Pack(PackageRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var diagnostics = new List<CompilerDiagnostic>();
        if (!Directory.Exists(request.InputRoot))
        {
            diagnostics.Add(new CompilerDiagnostic(
                "pac-pack-input-missing",
                DiagnosticSeverity.Error,
                $"PAC packaging input folder was not found: {request.InputRoot}",
                request.InputRoot));
            return new PackageResult(false, null, diagnostics);
        }

        if (!EnsurePacAvailable(diagnostics))
        {
            return new PackageResult(false, null, diagnostics);
        }

        Directory.CreateDirectory(request.OutputRoot);

        var packageName = $"{ResolvePackageBaseName(request.InputRoot)}-{request.Flavor.ToString().ToLowerInvariant()}.zip";
        var packagePath = Path.Combine(Path.GetFullPath(request.OutputRoot), packageName);
        var packArguments = new[]
        {
            "solution",
            "pack",
            "--zipfile",
            packagePath,
            "--folder",
            Path.GetFullPath(request.InputRoot),
            "--packagetype",
            request.Flavor == PackageFlavor.Managed ? "Managed" : "Unmanaged",
            "--clobber"
        };

        var packResult = RunPacCommand(
            diagnostics,
            "pac-pack-failed",
            packArguments,
            request.OutputRoot,
            $"Packed solution input from {request.InputRoot}.");
        if (!packResult.Success)
        {
            return new PackageResult(false, packagePath, diagnostics);
        }

        if (request.RunSolutionCheck)
        {
            var checkOutputDirectory = Path.Combine(Path.GetFullPath(request.OutputRoot), "solution-check");
            Directory.CreateDirectory(checkOutputDirectory);
            var checkResult = RunPacCommand(
                diagnostics,
                "pac-check-failed",
                [
                    "solution",
                    "check",
                    "--path",
                    packagePath,
                    "--outputDirectory",
                    checkOutputDirectory
                ],
                request.OutputRoot,
                $"Ran solution check for {packagePath}.");
            if (!checkResult.Success)
            {
                return new PackageResult(false, packagePath, diagnostics);
            }
        }

        return new PackageResult(true, packagePath, diagnostics);
    }

    public ImportResult Import(ImportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var diagnostics = new List<CompilerDiagnostic>();
        if (!File.Exists(request.PackagePath))
        {
            diagnostics.Add(new CompilerDiagnostic(
                "pac-import-package-missing",
                DiagnosticSeverity.Error,
                $"PAC import package was not found: {request.PackagePath}",
                request.PackagePath));
            return new ImportResult(false, request.PackagePath, false, diagnostics);
        }

        if (request.Environment.DataverseUrl is null)
        {
            diagnostics.Add(new CompilerDiagnostic(
                "pac-import-missing-environment",
                DiagnosticSeverity.Error,
                "PAC import requires Environment.DataverseUrl.",
                request.PackagePath));
            return new ImportResult(false, request.PackagePath, false, diagnostics);
        }

        if (!EnsurePacAvailable(diagnostics))
        {
            return new ImportResult(false, request.PackagePath, false, diagnostics);
        }

        var arguments = new List<string>
        {
            "solution",
            "import",
            "--path",
            Path.GetFullPath(request.PackagePath),
            "--environment",
            request.Environment.DataverseUrl.ToString()
        };

        if (request.PublishAfterImport)
        {
            arguments.Add("--publish-changes");
        }

        var settingsPath = ResolveDeploymentSettingsPath(request.PackagePath);
        if (!string.IsNullOrWhiteSpace(settingsPath))
        {
            arguments.Add("--settings-file");
            arguments.Add(settingsPath!);
        }

        var importResult = RunPacCommand(
            diagnostics,
            "pac-import-failed",
            arguments,
            Path.GetDirectoryName(Path.GetFullPath(request.PackagePath)),
            $"Imported package {request.PackagePath} into {request.Environment.DataverseUrl}.");

        return new ImportResult(importResult.Success, request.PackagePath, importResult.Success && request.PublishAfterImport, diagnostics);
    }

    private bool EnsurePacAvailable(List<CompilerDiagnostic> diagnostics)
    {
        var probeResult = _processRunner.Run(new ProcessRunRequest("pac", ["help"], Environment.CurrentDirectory));
        if (probeResult.StartException is not null)
        {
            diagnostics.Add(new CompilerDiagnostic(
                "pac-cli-unavailable",
                DiagnosticSeverity.Error,
                $"PAC CLI could not be started: {probeResult.StartException.Message}",
                "pac"));
            return false;
        }

        if (probeResult.ExitCode != 0)
        {
            AppendCommandDiagnostics(diagnostics, "pac-cli-unavailable", probeResult, "PAC CLI availability check failed.");
            return false;
        }

        AppendOutputDiagnostics(diagnostics, "pac-cli-version", probeResult, "Validated PAC CLI availability.");
        return true;
    }

    private CommandOutcome RunPacCommand(
        List<CompilerDiagnostic> diagnostics,
        string failureCode,
        IReadOnlyList<string> arguments,
        string? workingDirectory,
        string successMessage)
    {
        var result = _processRunner.Run(new ProcessRunRequest("pac", arguments, workingDirectory));
        if (result.StartException is not null)
        {
            diagnostics.Add(new CompilerDiagnostic(
                failureCode,
                DiagnosticSeverity.Error,
                $"PAC command could not start: {result.StartException.Message}",
                "pac"));
            return CommandOutcome.Failed;
        }

        AppendOutputDiagnostics(diagnostics, failureCode, result, successMessage);
        if (result.ExitCode != 0)
        {
            diagnostics.Add(new CompilerDiagnostic(
                failureCode,
                DiagnosticSeverity.Error,
                $"PAC command exited with code {result.ExitCode}: {FormatCommand(result.Request)}",
                result.Request.WorkingDirectory));
            return CommandOutcome.Failed;
        }

        return CommandOutcome.Succeeded;
    }

    private static void AppendOutputDiagnostics(
        List<CompilerDiagnostic> diagnostics,
        string code,
        ProcessRunResult result,
        string successMessage)
    {
        diagnostics.Add(new CompilerDiagnostic(
            code,
            DiagnosticSeverity.Info,
            $"{successMessage} Command: {FormatCommand(result.Request)}",
            result.Request.WorkingDirectory));

        if (!string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            diagnostics.Add(new CompilerDiagnostic(
                $"{code}-stdout",
                DiagnosticSeverity.Info,
                result.StandardOutput.Trim(),
                result.Request.WorkingDirectory));
        }

        if (!string.IsNullOrWhiteSpace(result.StandardError))
        {
            diagnostics.Add(new CompilerDiagnostic(
                $"{code}-stderr",
                result.ExitCode == 0 ? DiagnosticSeverity.Warning : DiagnosticSeverity.Error,
                result.StandardError.Trim(),
                result.Request.WorkingDirectory));
        }
    }

    private static void AppendCommandDiagnostics(
        List<CompilerDiagnostic> diagnostics,
        string code,
        ProcessRunResult result,
        string message)
    {
        diagnostics.Add(new CompilerDiagnostic(
            code,
            DiagnosticSeverity.Error,
            $"{message} Command: {FormatCommand(result.Request)}",
            result.Request.WorkingDirectory));

        if (!string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            diagnostics.Add(new CompilerDiagnostic(
                $"{code}-stdout",
                DiagnosticSeverity.Info,
                result.StandardOutput.Trim(),
                result.Request.WorkingDirectory));
        }

        if (!string.IsNullOrWhiteSpace(result.StandardError))
        {
            diagnostics.Add(new CompilerDiagnostic(
                $"{code}-stderr",
                DiagnosticSeverity.Error,
                result.StandardError.Trim(),
                result.Request.WorkingDirectory));
        }
    }

    private static string ResolvePackageBaseName(string inputRoot)
    {
        var manifestPath = Path.Combine(inputRoot, "manifest.json");
        if (File.Exists(manifestPath))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            if (document.RootElement.TryGetProperty("solution", out var solutionElement)
                && TryGetProperty(solutionElement, "uniqueName", out var uniqueNameElement)
                && !string.IsNullOrWhiteSpace(uniqueNameElement.GetString()))
            {
                return uniqueNameElement.GetString()!;
            }
        }

        return new DirectoryInfo(Path.GetFullPath(inputRoot)).Name;
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.TryGetProperty(propertyName, out value))
        {
            return true;
        }

        if (propertyName.Length > 0)
        {
            var alternate = char.ToUpperInvariant(propertyName[0]) + propertyName[1..];
            if (element.TryGetProperty(alternate, out value))
            {
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string? ResolveDeploymentSettingsPath(string packagePath)
    {
        var packageDirectory = Path.GetDirectoryName(Path.GetFullPath(packagePath));
        if (string.IsNullOrWhiteSpace(packageDirectory))
        {
            return null;
        }

        var siblingSettings = Path.Combine(packageDirectory, "package-inputs", "settings", "deployment-settings.json");
        return File.Exists(siblingSettings) ? siblingSettings : null;
    }

    private static string FormatCommand(ProcessRunRequest request) =>
        string.Join(" ", new[] { request.FileName }.Concat(request.Arguments.Select(QuoteIfNeeded)));

    private static string QuoteIfNeeded(string value) =>
        value.Contains(' ', StringComparison.Ordinal) ? $"\"{value}\"" : value;

    private readonly record struct CommandOutcome(bool Success)
    {
        public static CommandOutcome Succeeded { get; } = new(true);

        public static CommandOutcome Failed { get; } = new(false);
    }
}

internal sealed record ProcessRunRequest(string FileName, IReadOnlyList<string> Arguments, string? WorkingDirectory);

internal sealed record ProcessRunResult(
    ProcessRunRequest Request,
    int ExitCode,
    string StandardOutput,
    string StandardError,
    Exception? StartException = null);

internal interface IProcessRunner
{
    ProcessRunResult Run(ProcessRunRequest request);
}

internal sealed class DefaultProcessRunner : IProcessRunner
{
    public ProcessRunResult Run(ProcessRunRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var startInfo = new ProcessStartInfo
        {
            FileName = request.FileName,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = string.IsNullOrWhiteSpace(request.WorkingDirectory)
                ? Environment.CurrentDirectory
                : request.WorkingDirectory
        };

        foreach (var argument in request.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        try
        {
            using var process = new Process { StartInfo = startInfo };
            process.Start();
            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();

            return new ProcessRunResult(request, process.ExitCode, standardOutput, standardError);
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return new ProcessRunResult(request, -1, string.Empty, string.Empty, exception);
        }
    }
}
