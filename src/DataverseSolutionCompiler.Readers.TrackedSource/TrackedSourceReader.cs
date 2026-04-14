using DataverseSolutionCompiler.Domain.Abstractions;
using DataverseSolutionCompiler.Domain.Diagnostics;
using DataverseSolutionCompiler.Domain.Model;
using DataverseSolutionCompiler.Domain.Read;

namespace DataverseSolutionCompiler.Readers.TrackedSource;

public sealed class TrackedSourceReader : ISolutionReader
{
    public CanonicalSolution Read(ReadRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SourcePath);

        if (!Directory.Exists(request.SourcePath))
        {
            throw new DirectoryNotFoundException($"Tracked source folder not found: {request.SourcePath}");
        }

        var manifestPath = Path.Combine(request.SourcePath, "manifest.json");
        var artifacts = new List<FamilyArtifact>();
        if (File.Exists(manifestPath))
        {
            artifacts.Add(new FamilyArtifact(ComponentFamily.SolutionShell, "tracked-source-manifest", "manifest.json", manifestPath));
        }

        return new CanonicalSolution(
            new SolutionIdentity("tracked-source", "Tracked Source", "0.1.0", LayeringIntent.Hybrid),
            new PublisherDefinition("dsc", "dsc", "dsc", "Dataverse Solution Compiler"),
            artifacts,
            [],
            [],
            [
                new CompilerDiagnostic(
                    "tracked-source-reader-bootstrap",
                    DiagnosticSeverity.Info,
                    "Tracked source reader bootstrap is active; schema-specific parsers will deepen in later milestones.",
                    request.SourcePath)
            ]);
    }
}
