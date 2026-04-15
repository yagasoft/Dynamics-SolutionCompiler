using DataverseSolutionCompiler.Domain.Abstractions;
using DataverseSolutionCompiler.Domain.Model;
using DataverseSolutionCompiler.Domain.Read;

namespace DataverseSolutionCompiler.Readers.Xml;

public sealed class XmlSolutionReader : ISolutionReader
{
    public CanonicalSolution Read(ReadRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SourcePath);

        var normalizedSourcePath = NormalizeSourcePath(request.SourcePath);

        if (request.SourceKind == ReadSourceKind.PackedZip || Path.GetExtension(normalizedSourcePath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            return new ZipSolutionReader().Read(request with { SourceKind = ReadSourceKind.PackedZip, SourcePath = normalizedSourcePath });
        }

        if (!Directory.Exists(normalizedSourcePath))
        {
            throw new DirectoryNotFoundException($"XML solution folder not found: {normalizedSourcePath}");
        }

        return XmlCanonicalSolutionParser.Parse(normalizedSourcePath);
    }

    private static string NormalizeSourcePath(string sourcePath)
    {
        if (File.Exists(sourcePath))
        {
            return sourcePath;
        }

        if (!Directory.Exists(sourcePath))
        {
            return sourcePath;
        }

        if (File.Exists(Path.Combine(sourcePath, "Other", "Solution.xml")))
        {
            return sourcePath;
        }

        var unpackedCandidate = Path.Combine(sourcePath, "unpacked");
        if (Directory.Exists(unpackedCandidate)
            && File.Exists(Path.Combine(unpackedCandidate, "Other", "Solution.xml")))
        {
            return unpackedCandidate;
        }

        return sourcePath;
    }
}
