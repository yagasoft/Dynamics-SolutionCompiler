using System.Text.Json.Nodes;
using DataverseSolutionCompiler.Domain.Diagnostics;
using DataverseSolutionCompiler.Domain.Model;

namespace DataverseSolutionCompiler.Readers.Live;

internal sealed partial class DataverseWebApiLiveReader
{
    private async Task<IReadOnlyList<FamilyArtifact>> ReadImageConfigurationArtifactsAsync(
        string entityLogicalName,
        JsonObject entity,
        SolutionComponentScope scope,
        ICollection<CompilerDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        var primaryImageAttribute = NormalizeLogicalName(GetString(entity, "PrimaryImageAttribute"));
        var imageAttributeRows = await ReadEntityImageAttributeRowsAsync(entityLogicalName, entity, cancellationToken).ConfigureAwait(false);
        if (imageAttributeRows.Count == 0 && string.IsNullOrWhiteSpace(primaryImageAttribute))
        {
            return [];
        }

        var artifacts = new List<FamilyArtifact>();
        var primaryImageRow = imageAttributeRows.FirstOrDefault(row =>
            string.Equals(
                NormalizeLogicalName(GetString(row, "LogicalName")),
                primaryImageAttribute,
                StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(primaryImageAttribute))
        {
            var entityScopeMissing = scope.EntityImageConfigurationEntities.Count == 0
                || !scope.EntityImageConfigurationEntities.Contains(entityLogicalName);
            if (entityScopeMissing && _options.EnableEntityScopedUiFallback)
            {
                diagnostics.Add(new CompilerDiagnostic(
                    "live-readback-image-config-fallback",
                    DiagnosticSeverity.Warning,
                    $"Falling back to entity-scoped metadata for entity image configuration on '{entityLogicalName}' because solution component type 432 was under-reported.",
                    entityLogicalName));
            }

            artifacts.Add(CreateEntityImageConfigurationArtifact(entityLogicalName, primaryImageAttribute!, primaryImageRow));
        }

        foreach (var row in imageAttributeRows)
        {
            var attributeLogicalName = NormalizeLogicalName(GetString(row, "LogicalName"));
            if (string.IsNullOrWhiteSpace(attributeLogicalName))
            {
                continue;
            }

            var logicalName = BuildAttributeImageConfigurationLogicalName(entityLogicalName, attributeLogicalName)!;
            var attributeScopeMissing = scope.AttributeImageConfigurationLogicalNames.Count == 0
                || !scope.AttributeImageConfigurationLogicalNames.Contains(logicalName);
            if (attributeScopeMissing && _options.EnableEntityScopedUiFallback)
            {
                diagnostics.Add(new CompilerDiagnostic(
                    "live-readback-image-config-fallback",
                    DiagnosticSeverity.Warning,
                    $"Falling back to entity-scoped metadata for attribute image configuration '{entityLogicalName}.{attributeLogicalName}' because solution component type 431 was under-reported.",
                    logicalName));
            }

            artifacts.Add(CreateAttributeImageConfigurationArtifact(entityLogicalName, primaryImageAttribute, row));
        }

        return artifacts
            .GroupBy(artifact => artifact.LogicalName, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(artifact => artifact.LogicalName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private async Task<IReadOnlyList<JsonObject>> ReadEntityImageAttributeRowsAsync(
        string entityLogicalName,
        JsonObject entity,
        CancellationToken cancellationToken)
    {
        var rows = await GetCollectionAsync(
            $"EntityDefinitions(LogicalName='{EscapeODataLiteral(entityLogicalName)}')/Attributes/Microsoft.Dynamics.CRM.ImageAttributeMetadata?$select=LogicalName,SchemaName,DisplayName,AttributeType,IsSecured,IsPrimaryName,IsLogical,IsCustomAttribute,IsCustomizable,CanStoreFullImage,IsPrimaryImage",
            cancellationToken).ConfigureAwait(false);

        return rows.Count > 0
            ? rows.OfType<JsonObject>().ToArray()
            : ReadObjects(entity, "Attributes").Where(IsImageAttributeRow).ToArray();
    }

    private static bool IsImageAttributeRow(JsonObject row) =>
        string.Equals(NormalizeLogicalName(GetString(row, "AttributeType")), "image", StringComparison.OrdinalIgnoreCase)
        || GetProperty(row, "CanStoreFullImage") is not null
        || GetProperty(row, "IsPrimaryImage") is not null;

    private static FamilyArtifact CreateEntityImageConfigurationArtifact(
        string entityLogicalName,
        string primaryImageAttribute,
        JsonObject? primaryImageRow) =>
        new(
            ComponentFamily.ImageConfiguration,
            $"{entityLogicalName}|entity-image",
            GetString(primaryImageRow, "DisplayName") ?? primaryImageAttribute,
            $"EntityDefinitions(LogicalName='{entityLogicalName}')/ImageConfiguration",
            EvidenceKind.Readback,
            CreateProperties(
                (ArtifactPropertyKeys.EntityLogicalName, entityLogicalName),
                (ArtifactPropertyKeys.ImageConfigurationScope, "entity"),
                (ArtifactPropertyKeys.PrimaryImageAttribute, primaryImageAttribute),
                (ArtifactPropertyKeys.ImageAttributeLogicalName, primaryImageAttribute),
                (ArtifactPropertyKeys.CanStoreFullImage, NormalizeBoolean(GetString(primaryImageRow, "CanStoreFullImage"))),
                (ArtifactPropertyKeys.IsPrimaryImage, "true")));

    private static FamilyArtifact CreateAttributeImageConfigurationArtifact(
        string entityLogicalName,
        string? primaryImageAttribute,
        JsonObject row)
    {
        var attributeLogicalName = NormalizeLogicalName(GetString(row, "LogicalName"))!;
        return new FamilyArtifact(
            ComponentFamily.ImageConfiguration,
            BuildAttributeImageConfigurationLogicalName(entityLogicalName, attributeLogicalName)!,
            GetString(row, "DisplayName") ?? attributeLogicalName,
            $"EntityDefinitions(LogicalName='{entityLogicalName}')/Attributes/{attributeLogicalName}/ImageConfiguration",
            EvidenceKind.Readback,
            CreateProperties(
                (ArtifactPropertyKeys.EntityLogicalName, entityLogicalName),
                (ArtifactPropertyKeys.ImageConfigurationScope, "attribute"),
                (ArtifactPropertyKeys.PrimaryImageAttribute, primaryImageAttribute),
                (ArtifactPropertyKeys.ImageAttributeLogicalName, attributeLogicalName),
                (ArtifactPropertyKeys.CanStoreFullImage, NormalizeBoolean(GetString(row, "CanStoreFullImage"))),
                (ArtifactPropertyKeys.IsPrimaryImage, NormalizeBoolean(GetString(row, "IsPrimaryImage")))));
    }

    private static string? BuildAttributeImageConfigurationLogicalName(string? entityLogicalName, string? attributeLogicalName) =>
        string.IsNullOrWhiteSpace(entityLogicalName) || string.IsNullOrWhiteSpace(attributeLogicalName)
            ? null
            : $"{NormalizeLogicalName(entityLogicalName)}|{NormalizeLogicalName(attributeLogicalName)}|attribute-image";

    private static string? BuildEntityImageConfigurationEntityName(string? logicalName)
    {
        if (string.IsNullOrWhiteSpace(logicalName))
        {
            return null;
        }

        const string suffix = "|entity-image";
        return logicalName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
            ? logicalName[..^suffix.Length]
            : NormalizeLogicalName(logicalName);
    }
}
