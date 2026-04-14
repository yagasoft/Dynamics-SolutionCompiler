using System.Text.Json.Serialization;

namespace DataverseSolutionCompiler.Emitters.TrackedSource;

internal sealed record IntentSpecDocument
{
    [JsonPropertyName("specVersion")]
    public string? SpecVersion { get; init; }

    [JsonPropertyName("solution")]
    public IntentSolutionSpec? Solution { get; init; }

    [JsonPropertyName("publisher")]
    public IntentPublisherSpec? Publisher { get; init; }

    [JsonPropertyName("globalOptionSets")]
    public IReadOnlyList<IntentGlobalOptionSetSpec>? GlobalOptionSets { get; init; }

    [JsonPropertyName("environmentVariables")]
    public IReadOnlyList<IntentEnvironmentVariableSpec>? EnvironmentVariables { get; init; }

    [JsonPropertyName("appModules")]
    public IReadOnlyList<IntentAppModuleSpec>? AppModules { get; init; }

    [JsonPropertyName("tables")]
    public IReadOnlyList<IntentTableSpec>? Tables { get; init; }
}

internal sealed record IntentSolutionSpec
{
    [JsonPropertyName("uniqueName")]
    public string? UniqueName { get; init; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("version")]
    public string? Version { get; init; }

    [JsonPropertyName("layeringIntent")]
    public string? LayeringIntent { get; init; }
}

internal sealed record IntentPublisherSpec
{
    [JsonPropertyName("uniqueName")]
    public string? UniqueName { get; init; }

    [JsonPropertyName("prefix")]
    public string? Prefix { get; init; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }
}

internal sealed record IntentGlobalOptionSetSpec
{
    [JsonPropertyName("logicalName")]
    public string? LogicalName { get; init; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("optionSetType")]
    public string? OptionSetType { get; init; }

    [JsonPropertyName("options")]
    public IReadOnlyList<IntentOptionItemSpec>? Options { get; init; }
}

internal sealed record IntentEnvironmentVariableSpec
{
    [JsonPropertyName("schemaName")]
    public string? SchemaName { get; init; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("defaultValue")]
    public string? DefaultValue { get; init; }

    [JsonPropertyName("currentValue")]
    public string? CurrentValue { get; init; }

    [JsonPropertyName("secretStore")]
    public string? SecretStore { get; init; }

    [JsonPropertyName("valueSchema")]
    public string? ValueSchema { get; init; }
}

internal sealed record IntentAppModuleSpec
{
    [JsonPropertyName("uniqueName")]
    public string? UniqueName { get; init; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("siteMap")]
    public IntentSiteMapSpec? SiteMap { get; init; }
}

internal sealed record IntentSiteMapSpec
{
    [JsonPropertyName("areas")]
    public IReadOnlyList<IntentSiteMapAreaSpec>? Areas { get; init; }
}

internal sealed record IntentSiteMapAreaSpec
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("groups")]
    public IReadOnlyList<IntentSiteMapGroupSpec>? Groups { get; init; }
}

internal sealed record IntentSiteMapGroupSpec
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("subAreas")]
    public IReadOnlyList<IntentSiteMapSubAreaSpec>? SubAreas { get; init; }
}

internal sealed record IntentSiteMapSubAreaSpec
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("entity")]
    public string? Entity { get; init; }
}

internal sealed record IntentTableSpec
{
    [JsonPropertyName("logicalName")]
    public string? LogicalName { get; init; }

    [JsonPropertyName("schemaName")]
    public string? SchemaName { get; init; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("entitySetName")]
    public string? EntitySetName { get; init; }

    [JsonPropertyName("ownershipTypeMask")]
    public string? OwnershipTypeMask { get; init; }

    [JsonPropertyName("columns")]
    public IReadOnlyList<IntentTableColumnSpec>? Columns { get; init; }

    [JsonPropertyName("keys")]
    public IReadOnlyList<IntentTableKeySpec>? Keys { get; init; }

    [JsonPropertyName("forms")]
    public IReadOnlyList<IntentFormSpec>? Forms { get; init; }

    [JsonPropertyName("views")]
    public IReadOnlyList<IntentViewSpec>? Views { get; init; }
}

internal sealed record IntentTableColumnSpec
{
    [JsonPropertyName("logicalName")]
    public string? LogicalName { get; init; }

    [JsonPropertyName("schemaName")]
    public string? SchemaName { get; init; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("isSecured")]
    public bool IsSecured { get; init; }

    [JsonPropertyName("targetTable")]
    public string? TargetTable { get; init; }

    [JsonPropertyName("relationshipSchemaName")]
    public string? RelationshipSchemaName { get; init; }

    [JsonPropertyName("globalOptionSet")]
    public string? GlobalOptionSet { get; init; }

    [JsonPropertyName("options")]
    public IReadOnlyList<IntentOptionItemSpec>? Options { get; init; }
}

internal sealed record IntentTableKeySpec
{
    [JsonPropertyName("logicalName")]
    public string? LogicalName { get; init; }

    [JsonPropertyName("schemaName")]
    public string? SchemaName { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("keyAttributes")]
    public IReadOnlyList<string>? KeyAttributes { get; init; }
}

internal sealed record IntentOptionItemSpec
{
    [JsonPropertyName("value")]
    public string? Value { get; init; }

    [JsonPropertyName("label")]
    public string? Label { get; init; }
}

internal sealed record IntentFormSpec
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("tabs")]
    public IReadOnlyList<IntentFormTabSpec>? Tabs { get; init; }

    [JsonPropertyName("headerFields")]
    public IReadOnlyList<string>? HeaderFields { get; init; }
}

internal sealed record IntentFormTabSpec
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("label")]
    public string? Label { get; init; }

    [JsonPropertyName("sections")]
    public IReadOnlyList<IntentFormSectionSpec>? Sections { get; init; }
}

internal sealed record IntentFormSectionSpec
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("label")]
    public string? Label { get; init; }

    [JsonPropertyName("fields")]
    public IReadOnlyList<string>? Fields { get; init; }
}

internal sealed record IntentViewSpec
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("layoutColumns")]
    public IReadOnlyList<string>? LayoutColumns { get; init; }

    [JsonPropertyName("fetchAttributes")]
    public IReadOnlyList<string>? FetchAttributes { get; init; }

    [JsonPropertyName("filters")]
    public IReadOnlyList<IntentViewFilterSpec>? Filters { get; init; }

    [JsonPropertyName("orders")]
    public IReadOnlyList<IntentViewOrderSpec>? Orders { get; init; }
}

internal sealed record IntentViewFilterSpec
{
    [JsonPropertyName("attribute")]
    public string? Attribute { get; init; }

    [JsonPropertyName("operator")]
    public string? Operator { get; init; }

    [JsonPropertyName("value")]
    public string? Value { get; init; }
}

internal sealed record IntentViewOrderSpec
{
    [JsonPropertyName("attribute")]
    public string? Attribute { get; init; }

    [JsonPropertyName("descending")]
    public bool Descending { get; init; }
}

internal sealed record ReverseGenerationReport
{
    [JsonPropertyName("inputKind")]
    public string? InputKind { get; init; }

    [JsonPropertyName("isPartial")]
    public bool IsPartial { get; init; }

    [JsonPropertyName("supportedFamiliesEmitted")]
    public IReadOnlyList<string>? SupportedFamiliesEmitted { get; init; }

    [JsonPropertyName("unsupportedFamiliesOmitted")]
    public IReadOnlyList<IntentReportEntry>? UnsupportedFamiliesOmitted { get; init; }

    [JsonPropertyName("preservedIdsIncluded")]
    public IReadOnlyList<PreservedIdEntry>? PreservedIdsIncluded { get; init; }
}

internal static class ReverseGenerationReportCategories
{
    public const string UnsupportedFamily = "unsupportedFamily";
    public const string UnsupportedShape = "unsupportedShape";
    public const string PlatformGeneratedArtifact = "platformGeneratedArtifact";
    public const string MissingSourceFidelity = "missingSourceFidelity";
}

internal sealed record IntentReportEntry(
    [property: JsonPropertyName("family")] string Family,
    [property: JsonPropertyName("artifact")] string Artifact,
    [property: JsonPropertyName("reason")] string Reason,
    [property: JsonPropertyName("category")] string Category = ReverseGenerationReportCategories.UnsupportedShape);

internal sealed record PreservedIdEntry(
    [property: JsonPropertyName("family")] string Family,
    [property: JsonPropertyName("artifact")] string Artifact,
    [property: JsonPropertyName("id")] string Id);

internal sealed record OptionEntry
{
    [JsonPropertyName("value")]
    public string? Value { get; init; }

    [JsonPropertyName("label")]
    public string? Label { get; init; }
}

internal sealed record ViewFilterDefinition
{
    [JsonPropertyName("attribute")]
    public string? Attribute { get; init; }

    [JsonPropertyName("operator")]
    public string? Operator { get; init; }

    [JsonPropertyName("value")]
    public string? Value { get; init; }
}

internal sealed record ViewOrderDefinition
{
    [JsonPropertyName("attribute")]
    public string? Attribute { get; init; }

    [JsonPropertyName("descending")]
    public string? Descending { get; init; }
}

internal sealed record IntentFormDefinition
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("tabs")]
    public IReadOnlyList<IntentFormTabDefinition> Tabs { get; init; } = [];

    [JsonPropertyName("headerFields")]
    public IReadOnlyList<string> HeaderFields { get; init; } = [];
}

internal sealed record IntentFormTabDefinition
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("label")]
    public string? Label { get; init; }

    [JsonPropertyName("sections")]
    public IReadOnlyList<IntentFormSectionDefinition> Sections { get; init; } = [];
}

internal sealed record IntentFormSectionDefinition
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("label")]
    public string? Label { get; init; }

    [JsonPropertyName("fields")]
    public IReadOnlyList<string> Fields { get; init; } = [];
}

internal sealed record SiteMapDefinition
{
    [JsonPropertyName("areas")]
    public IReadOnlyList<SiteMapAreaDefinition> Areas { get; init; } = [];
}

internal sealed record SiteMapAreaDefinition
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("groups")]
    public IReadOnlyList<SiteMapGroupDefinition> Groups { get; init; } = [];
}

internal sealed record SiteMapGroupDefinition
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("subAreas")]
    public IReadOnlyList<SiteMapSubAreaDefinition> SubAreas { get; init; } = [];
}

internal sealed record SiteMapSubAreaDefinition
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("entity")]
    public string? Entity { get; init; }

    [JsonPropertyName("url")]
    public string? Url { get; init; }
}
