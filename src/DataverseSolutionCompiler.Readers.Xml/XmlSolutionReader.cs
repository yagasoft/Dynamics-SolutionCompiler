using DataverseSolutionCompiler.Domain.Abstractions;
using DataverseSolutionCompiler.Domain.Capabilities;
using DataverseSolutionCompiler.Domain.Diagnostics;
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

        var artifacts = InventoryFolder(request.SourcePath).ToArray();
        var diagnostics = new[]
        {
            new CompilerDiagnostic(
                "xml-reader-structural",
                DiagnosticSeverity.Info,
                "The XML reader currently inventories unpacked Dataverse source structurally while the typed family parsers are still being implemented.",
                request.SourcePath)
        };

        return new CanonicalSolution(
            new SolutionIdentity(
                new DirectoryInfo(request.SourcePath).Name.ToLowerInvariant(),
                new DirectoryInfo(request.SourcePath).Name,
                "0.1.0",
                LayeringIntent.Hybrid),
            new PublisherDefinition("dsc", "dsc", "dsc", "Dataverse Solution Compiler"),
            artifacts,
            [],
            [],
            diagnostics);
    }

    private static IEnumerable<FamilyArtifact> InventoryFolder(string root)
    {
        if (File.Exists(Path.Combine(root, "Other", "Solution.xml")))
        {
            yield return new FamilyArtifact(ComponentFamily.SolutionShell, "solution", "Solution.xml", Path.Combine(root, "Other", "Solution.xml"));
        }

        if (File.Exists(Path.Combine(root, "Other", "Customizations.xml")))
        {
            yield return new FamilyArtifact(ComponentFamily.LegacyAsset, "customizations", "Customizations.xml", Path.Combine(root, "Other", "Customizations.xml"));
        }

        foreach (var directory in Directory.GetDirectories(root))
        {
            var name = Path.GetFileName(directory);
            var family = name switch
            {
                "Entities" => ComponentFamily.Table,
                "AppModules" => ComponentFamily.AppModule,
                "SiteMaps" => ComponentFamily.SiteMap,
                "WebResources" => ComponentFamily.WebResource,
                "CanvasApps" => ComponentFamily.CanvasApp,
                "OptionSets" => ComponentFamily.OptionSet,
                "Workflows" => ComponentFamily.Workflow,
                "DuplicateRules" => ComponentFamily.DuplicateRule,
                "RoutingRules" => ComponentFamily.RoutingRule,
                "MobileOfflineProfiles" => ComponentFamily.MobileOfflineProfile,
                _ => (ComponentFamily?)null
            };

            if (family is not null)
            {
                yield return new FamilyArtifact(family.Value, name.ToLowerInvariant(), name, directory);
            }
        }
    }
}
