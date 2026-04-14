using System.Diagnostics;
using System.IO.Compression;
using DataverseSolutionCompiler.Domain.Abstractions;
using DataverseSolutionCompiler.Domain.Diagnostics;
using DataverseSolutionCompiler.Domain.Model;
using DataverseSolutionCompiler.Domain.Read;

namespace DataverseSolutionCompiler.Readers.Xml;

public sealed class ZipSolutionReader : ISolutionReader
{
    public CanonicalSolution Read(ReadRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SourcePath);

        if (!File.Exists(request.SourcePath))
        {
            throw new FileNotFoundException("Packed Dataverse solution zip not found.", request.SourcePath);
        }

        if (IsClassicExportZip(request.SourcePath))
        {
            return ReadClassicExportZip(request);
        }

        var extractionRoot = Path.Combine(
            Path.GetTempPath(),
            "DataverseSolutionCompiler",
            "zip-read",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(extractionRoot);
        ZipFile.ExtractToDirectory(request.SourcePath, extractionRoot);

        var parsed = XmlCanonicalSolutionParser.Parse(extractionRoot);
        return parsed with
        {
            Diagnostics = parsed.Diagnostics
                .Concat(
                [
                    new CompilerDiagnostic(
                        "zip-reader-extracted",
                        DiagnosticSeverity.Info,
                        "The packed ZIP reader extracted the solution into a temporary folder and delegated to the typed XML parser for the proven families.",
                        request.SourcePath)
                ])
                .ToArray()
        };
    }

    private static CanonicalSolution ReadClassicExportZip(ReadRequest request)
    {
        var normalizedRoot = Path.Combine(
            Path.GetTempPath(),
            "DataverseSolutionCompiler",
            "zip-read-normalized",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(normalizedRoot);

        var unpackResult = RunPacSolutionUnpack(request.SourcePath, normalizedRoot);
        if (unpackResult.StartException is not null)
        {
            throw new InvalidOperationException(
                $"Classic exported solution zips require PAC CLI to normalize into an unpacked folder before parsing: {unpackResult.StartException.Message}");
        }

        if (unpackResult.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"PAC solution unpack failed for classic exported solution zip '{request.SourcePath}'. {FormatPacFailure(unpackResult)}");
        }

        var parsed = XmlCanonicalSolutionParser.Parse(normalizedRoot);
        var diagnostics = parsed.Diagnostics.ToList();
        diagnostics.Add(new CompilerDiagnostic(
            "zip-reader-normalized-classic-export",
            DiagnosticSeverity.Info,
            "The packed ZIP reader detected a classic exported solution zip and normalized it through PAC solution unpack before typed parsing.",
            request.SourcePath));

        if (!string.IsNullOrWhiteSpace(unpackResult.StandardOutput))
        {
            diagnostics.Add(new CompilerDiagnostic(
                "zip-reader-normalized-classic-export-stdout",
                DiagnosticSeverity.Info,
                unpackResult.StandardOutput.Trim(),
                request.SourcePath));
        }

        if (!string.IsNullOrWhiteSpace(unpackResult.StandardError))
        {
            diagnostics.Add(new CompilerDiagnostic(
                "zip-reader-normalized-classic-export-stderr",
                DiagnosticSeverity.Warning,
                unpackResult.StandardError.Trim(),
                request.SourcePath));
        }

        return parsed with { Diagnostics = diagnostics.ToArray() };
    }

    private static bool IsClassicExportZip(string zipPath)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        var entryNames = archive.Entries
            .Select(entry => entry.FullName.Replace('\\', '/').TrimStart('/'))
            .ToArray();

        return entryNames.Any(name => string.Equals(name, "solution.xml", StringComparison.OrdinalIgnoreCase))
            && entryNames.Any(name => string.Equals(name, "customizations.xml", StringComparison.OrdinalIgnoreCase))
            && !entryNames.Any(name => name.StartsWith("Other/", StringComparison.OrdinalIgnoreCase));
    }

    private static PacUnpackResult RunPacSolutionUnpack(string zipPath, string outputFolder)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "pac",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Environment.CurrentDirectory
        };
        startInfo.ArgumentList.Add("solution");
        startInfo.ArgumentList.Add("unpack");
        startInfo.ArgumentList.Add("--zipfile");
        startInfo.ArgumentList.Add(Path.GetFullPath(zipPath));
        startInfo.ArgumentList.Add("--folder");
        startInfo.ArgumentList.Add(Path.GetFullPath(outputFolder));
        startInfo.ArgumentList.Add("--allowDelete");
        startInfo.ArgumentList.Add("true");
        startInfo.ArgumentList.Add("--allowWrite");
        startInfo.ArgumentList.Add("true");
        startInfo.ArgumentList.Add("--clobber");

        try
        {
            using var process = new Process { StartInfo = startInfo };
            process.Start();
            var standardOutput = process.StandardOutput.ReadToEnd();
            var standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();
            return new PacUnpackResult(process.ExitCode, standardOutput, standardError, null);
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return new PacUnpackResult(-1, string.Empty, string.Empty, exception);
        }
    }

    private static string FormatPacFailure(PacUnpackResult result)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            parts.Add($"stdout: {result.StandardOutput.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(result.StandardError))
        {
            parts.Add($"stderr: {result.StandardError.Trim()}");
        }

        parts.Add($"exit code: {result.ExitCode}");
        return string.Join(" ", parts);
    }

    private readonly record struct PacUnpackResult(int ExitCode, string StandardOutput, string StandardError, Exception? StartException);
}
