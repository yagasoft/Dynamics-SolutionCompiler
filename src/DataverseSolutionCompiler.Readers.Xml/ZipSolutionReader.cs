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
}
