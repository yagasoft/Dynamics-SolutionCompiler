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

        using var archive = ZipFile.OpenRead(request.SourcePath);
        var artifacts = archive.Entries
            .Select(MapEntryToArtifact)
            .Where(artifact => artifact is not null)
            .Cast<FamilyArtifact>()
            .Distinct()
            .ToArray();

        return new CanonicalSolution(
            new SolutionIdentity(
                Path.GetFileNameWithoutExtension(request.SourcePath).ToLowerInvariant(),
                Path.GetFileNameWithoutExtension(request.SourcePath),
                "0.1.0",
                LayeringIntent.Hybrid),
            new PublisherDefinition("dsc", "dsc", "dsc", "Dataverse Solution Compiler"),
            artifacts,
            [],
            [],
            [
                new CompilerDiagnostic(
                    "zip-reader-structural",
                    DiagnosticSeverity.Info,
                    "The packed ZIP reader inventories known Dataverse entry families without unpacking them to tracked source yet.",
                    request.SourcePath)
            ]);
    }

    private static FamilyArtifact? MapEntryToArtifact(ZipArchiveEntry entry)
    {
        if (entry.FullName.StartsWith("Entities/", StringComparison.OrdinalIgnoreCase))
        {
            return new FamilyArtifact(ComponentFamily.Table, "entities", "Entities", entry.FullName);
        }

        if (entry.FullName.StartsWith("CanvasApps/", StringComparison.OrdinalIgnoreCase))
        {
            return new FamilyArtifact(ComponentFamily.CanvasApp, "canvasapps", "CanvasApps", entry.FullName);
        }

        if (entry.FullName.Equals("Other/Solution.xml", StringComparison.OrdinalIgnoreCase))
        {
            return new FamilyArtifact(ComponentFamily.SolutionShell, "solution", "Solution.xml", entry.FullName);
        }

        if (entry.FullName.Equals("Other/Customizations.xml", StringComparison.OrdinalIgnoreCase))
        {
            return new FamilyArtifact(ComponentFamily.LegacyAsset, "customizations", "Customizations.xml", entry.FullName);
        }

        return null;
    }
}
