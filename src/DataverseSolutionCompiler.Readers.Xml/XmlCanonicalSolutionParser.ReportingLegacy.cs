using DataverseSolutionCompiler.Domain.Model;
using System.Globalization;
using System.Xml.Linq;

namespace DataverseSolutionCompiler.Readers.Xml;

internal sealed partial class XmlCanonicalSolutionParser
{
    private void ParseReportingAndLegacySourceArtifacts()
    {
        ParseReportingLegacyDirectory("Reports", ComponentFamily.Report);
        ParseReportingLegacyDirectory("Templates", ComponentFamily.Template);
        ParseReportingLegacyDirectory("DisplayStrings", ComponentFamily.DisplayString);
        ParseReportingLegacyDirectory("Attachments", ComponentFamily.Attachment);
        ParseReportingLegacyDirectory("WebWizard", ComponentFamily.LegacyAsset);
        ParseReportingLegacyDirectory("WebWizards", ComponentFamily.LegacyAsset);
    }

    private void ParseReportingLegacyDirectory(string directoryName, ComponentFamily family)
    {
        var directoryPath = Path.Combine(_root, directoryName);
        if (!Directory.Exists(directoryPath))
        {
            return;
        }

        foreach (var metadataPath in Directory.EnumerateFiles(directoryPath, "*.xml", SearchOption.AllDirectories)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            if (!IsReportingLegacyMetadataPath(metadataPath))
            {
                continue;
            }

            var metadataRelativePath = RelativePath(metadataPath);
            var assetPath = ResolveReportingLegacyAssetPath(metadataPath);
            var assetRelativePath = assetPath is null ? null : RelativePath(assetPath);
            var logicalName = BuildReportingLegacyLogicalName(directoryName, metadataPath);
            var displayName = TryReadReportingLegacyDisplayName(metadataPath) ?? HumanizeReportingLegacyName(logicalName);
            var description = TryReadReportingLegacyDescription(metadataPath);

            AddArtifact(
                family,
                logicalName,
                displayName,
                metadataPath,
                CreateProperties(
                    (ArtifactPropertyKeys.MetadataSourcePath, metadataRelativePath),
                    (ArtifactPropertyKeys.PackageRelativePath, metadataRelativePath),
                    (ArtifactPropertyKeys.AssetSourcePath, assetRelativePath),
                    (ArtifactPropertyKeys.Description, description),
                    (ArtifactPropertyKeys.Name, displayName),
                    (ArtifactPropertyKeys.ByteLength, assetPath is null ? null : new FileInfo(assetPath).Length.ToString(CultureInfo.InvariantCulture)),
                    (ArtifactPropertyKeys.ContentHash, assetPath is null ? null : ComputeFileHash(assetPath))));
        }
    }

    private static bool IsReportingLegacyMetadataPath(string metadataPath)
    {
        if (!metadataPath.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !metadataPath.EndsWith(".rdl", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ResolveReportingLegacyAssetPath(string metadataPath)
    {
        if (!metadataPath.EndsWith(".data.xml", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var candidate = metadataPath[..^".data.xml".Length];
        return File.Exists(candidate) ? candidate : null;
    }

    private static string BuildReportingLegacyLogicalName(string directoryName, string metadataPath)
    {
        var fileName = Path.GetFileName(metadataPath);
        var stem = fileName.EndsWith(".data.xml", StringComparison.OrdinalIgnoreCase)
            ? fileName[..^".data.xml".Length]
            : Path.GetFileNameWithoutExtension(fileName);
        stem = Path.GetFileNameWithoutExtension(stem);

        var parentDirectory = Path.GetFileName(Path.GetDirectoryName(metadataPath));
        if (!string.IsNullOrWhiteSpace(parentDirectory)
            && !string.Equals(parentDirectory, directoryName, StringComparison.OrdinalIgnoreCase))
        {
            return NormalizeLogicalName($"{parentDirectory}_{stem}") ?? $"{parentDirectory}_{stem}".ToLowerInvariant();
        }

        return NormalizeLogicalName(stem) ?? stem.ToLowerInvariant();
    }

    private static string HumanizeReportingLegacyName(string logicalName)
    {
        if (string.IsNullOrWhiteSpace(logicalName))
        {
            return logicalName;
        }

        var words = logicalName
            .Replace('_', ' ')
            .Replace('-', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(segment.ToLowerInvariant()));
        return string.Join(' ', words);
    }

    private static string? TryReadReportingLegacyDisplayName(string metadataPath)
    {
        try
        {
            var root = LoadRoot(metadataPath);
            return LocalizedDescription(root.ElementLocal("LocalizedNames"))
                ?? LocalizedDescription(root.ElementLocal("DisplayName"))
                ?? Text(root.ElementLocal("DisplayName"))
                ?? Text(root.ElementLocal("Title"))
                ?? Text(root.ElementLocal("Name"))
                ?? root.AttributeValue("name");
        }
        catch
        {
            return null;
        }
    }

    private static string? TryReadReportingLegacyDescription(string metadataPath)
    {
        try
        {
            var root = LoadRoot(metadataPath);
            return LocalizedDescription(root.ElementLocal("Descriptions"))
                ?? Text(root.ElementLocal("Description"))
                ?? root.AttributeValue("description");
        }
        catch
        {
            return null;
        }
    }
}
