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

        if (request.SourceKind == ReadSourceKind.PackedZip || Path.GetExtension(request.SourcePath).Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            return new ZipSolutionReader().Read(request with { SourceKind = ReadSourceKind.PackedZip });
        }

        if (!Directory.Exists(request.SourcePath))
        {
            throw new DirectoryNotFoundException($"XML solution folder not found: {request.SourcePath}");
        }

        return XmlCanonicalSolutionParser.Parse(request.SourcePath);
    }
}
