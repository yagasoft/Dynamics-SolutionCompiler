using DataverseSolutionCompiler.Domain.Diagnostics;
using DataverseSolutionCompiler.Domain.Model;

namespace DataverseSolutionCompiler.Readers.Xml;

internal sealed partial class XmlCanonicalSolutionParser
{
    private readonly string _root;
    private readonly List<FamilyArtifact> _artifacts = [];
    private readonly List<CompilerDiagnostic> _diagnostics = [];

    private XmlCanonicalSolutionParser(string root)
    {
        _root = root;
    }

    public static CanonicalSolution Parse(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        return new XmlCanonicalSolutionParser(root).Parse();
    }

    private CanonicalSolution Parse()
    {
        var solutionIdentity = ParseSolutionIdentity(out var publisher);

        ParseEntities();
        ParseRelationships();
        ParseGlobalOptionSets();
        ParseAppModules();
        ParseSiteMaps();
        ParseWebResources();
        ParseEnvironmentVariables();
        ParseImportMaps();
        ParseAiFamilies();
        ParseEntityAnalyticsConfigurations();
        ParsePluginRegistrationFamilies();
        ParseServiceEndpointsAndConnectors();
        ParseProcessPolicyFamilies();
        ParseSecurityFamilies();
        ParseCanvasApps();
        ParseLegacyArtifacts();

        _diagnostics.Add(new CompilerDiagnostic(
            "xml-reader-typed-families",
            DiagnosticSeverity.Info,
            "The XML reader now parses the strongest proven Dataverse source families into stable typed artifacts while later families remain partial.",
            _root));

        return new CanonicalSolution(
            solutionIdentity,
            publisher,
            _artifacts
                .OrderBy(artifact => artifact.Family)
                .ThenBy(artifact => artifact.LogicalName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(artifact => artifact.SourcePath, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            [],
            [],
            _diagnostics.ToArray());
    }

    private SolutionIdentity ParseSolutionIdentity(out PublisherDefinition publisher)
    {
        var solutionPath = Path.Combine(_root, "Other", "Solution.xml");
        if (!File.Exists(solutionPath))
        {
            publisher = new PublisherDefinition("dsc", "dsc", "dsc", "Dataverse Solution Compiler");
            return new SolutionIdentity(
                new DirectoryInfo(_root).Name.ToLowerInvariant(),
                new DirectoryInfo(_root).Name,
                "0.1.0",
                LayeringIntent.Hybrid);
        }

        var root = LoadRoot(solutionPath);
        var manifest = root.ElementLocal("SolutionManifest");
        var uniqueName = Text(manifest?.ElementLocal("UniqueName")) ?? new DirectoryInfo(_root).Name.ToLowerInvariant();
        var displayName = LocalizedDescription(manifest?.ElementLocal("LocalizedNames")) ?? uniqueName;
        var version = Text(manifest?.ElementLocal("Version")) ?? "0.1.0";
        var managed = NormalizeBoolean(Text(manifest?.ElementLocal("Managed"))) == "true";
        var publisherElement = manifest?.ElementLocal("Publisher");

        var publisherUniqueName = Text(publisherElement?.ElementLocal("UniqueName")) ?? "dsc";
        var publisherPrefix = Text(publisherElement?.ElementLocal("CustomizationPrefix")) ?? "dsc";
        var publisherDisplayName = LocalizedDescription(publisherElement?.ElementLocal("LocalizedNames")) ?? publisherUniqueName;

        publisher = new PublisherDefinition(
            publisherUniqueName,
            publisherPrefix,
            publisherPrefix,
            publisherDisplayName);

        AddArtifact(
            ComponentFamily.Publisher,
            publisherUniqueName,
            publisherDisplayName,
            solutionPath,
            CreateProperties(
                (ArtifactPropertyKeys.MetadataSourcePath, RelativePath(solutionPath)),
                (ArtifactPropertyKeys.PublisherPrefix, publisherPrefix),
                (ArtifactPropertyKeys.PublisherDisplayName, publisherDisplayName),
                (ArtifactPropertyKeys.Description, LocalizedDescription(publisherElement?.ElementLocal("Descriptions")))));

        AddArtifact(
            ComponentFamily.SolutionShell,
            uniqueName,
            displayName,
            solutionPath,
            CreateProperties(
                (ArtifactPropertyKeys.MetadataSourcePath, RelativePath(solutionPath)),
                (ArtifactPropertyKeys.Managed, managed ? "true" : "false"),
                (ArtifactPropertyKeys.PublisherUniqueName, publisherUniqueName),
                (ArtifactPropertyKeys.PublisherPrefix, publisherPrefix),
                (ArtifactPropertyKeys.PublisherDisplayName, publisherDisplayName),
                (ArtifactPropertyKeys.Description, LocalizedDescription(manifest?.ElementLocal("Descriptions")))));

        return new SolutionIdentity(
            uniqueName,
            displayName,
            version,
            managed ? LayeringIntent.ManagedRelease : LayeringIntent.UnmanagedDevelopment);
    }

    private void ParseLegacyArtifacts()
    {
        var customizationsPath = Path.Combine(_root, "Other", "Customizations.xml");
        if (File.Exists(customizationsPath))
        {
            AddArtifact(ComponentFamily.LegacyAsset, "customizations", "Customizations.xml", customizationsPath);
        }
    }

    private void AddArtifact(
        ComponentFamily family,
        string logicalName,
        string? displayName,
        string sourcePath,
        IReadOnlyDictionary<string, string>? properties = null,
        EvidenceKind evidence = EvidenceKind.Source)
    {
        if (string.IsNullOrWhiteSpace(logicalName))
        {
            return;
        }

        _artifacts.Add(new FamilyArtifact(
            family,
            logicalName,
            displayName,
            sourcePath,
            evidence,
            properties));
    }

    private string RelativePath(string path) =>
        Path.GetRelativePath(_root, path).Replace('\\', '/');
}
