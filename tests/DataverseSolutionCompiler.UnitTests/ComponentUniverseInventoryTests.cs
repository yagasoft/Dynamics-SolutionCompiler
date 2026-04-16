using System.Text.Json.Nodes;
using FluentAssertions;
using Xunit;

namespace DataverseSolutionCompiler.UnitTests;

public sealed class ComponentUniverseInventoryTests
{
    private const string RepoRoot = @"C:\Git\Dataverse-Solution-KB";
    private static readonly string InventoryPath = Path.Combine(
        RepoRoot,
        "fixtures",
        "skill-corpus",
        "references",
        "solutioncomponent-componenttype-inventory.json");
    private static readonly string CoverageMatrixPath = Path.Combine(
        RepoRoot,
        "fixtures",
        "skill-corpus",
        "references",
        "component-coverage-matrix.md");

    [Fact]
    public void Inventory_accounts_for_official_and_local_observed_component_types_once()
    {
        var document = LoadInventory();
        var officialComponentTypes = document["officialComponentTypeIds"]!
            .AsArray()
            .Select(node => node!.GetValue<int>())
            .ToArray();
        var localObservedOnlyComponentTypes = document["localObservedOnlyComponentTypes"]!
            .AsArray()
            .Select(node => node!["componentType"]!.GetValue<int>())
            .ToArray();
        var entryComponentTypes = LoadEntries()
            .Select(entry => entry["componentType"]!.GetValue<int>())
            .ToArray();

        entryComponentTypes.Should().OnlyHaveUniqueItems();
        entryComponentTypes.Should().BeEquivalentTo(
            officialComponentTypes.Concat(localObservedOnlyComponentTypes),
            options => options.WithoutStrictOrdering());
    }

    [Fact]
    public void Inventory_uses_allowed_classifications_and_maps_owner_entries()
    {
        var allowedClassifications = new HashSet<string>(StringComparer.Ordinal)
        {
            "owner",
            "subordinate",
            "internal-only",
            "unknown"
        };

        foreach (var entry in LoadEntries())
        {
            var classification = entry["classification"]!.GetValue<string>();
            allowedClassifications.Should().Contain(classification);

            if (!string.Equals(classification, "owner", StringComparison.Ordinal))
            {
                continue;
            }

            entry["mappedOwnerFamily"]?.GetValue<string>().Should().NotBeNullOrWhiteSpace();
            entry["coverageRow"]?.GetValue<string>().Should().NotBeNullOrWhiteSpace();
            entry["coverageStatus"]?.GetValue<string>().Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void Coverage_matrix_contains_every_owner_row_from_inventory()
    {
        var coverageMatrix = File.ReadAllText(CoverageMatrixPath);
        var ownerRows = LoadEntries()
            .Where(entry => string.Equals(entry["classification"]!.GetValue<string>(), "owner", StringComparison.Ordinal))
            .Select(entry => entry["coverageRow"]!.GetValue<string>())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        ownerRows.Should().NotBeEmpty();
        foreach (var ownerRow in ownerRows)
        {
            coverageMatrix.Should().Contain($"| {ownerRow} |");
        }
    }

    [Fact]
    public void Inventory_has_no_planned_owner_rows_after_backlog_closure()
    {
        var plannedOwnerRows = LoadEntries()
            .Where(entry => string.Equals(entry["classification"]!.GetValue<string>(), "owner", StringComparison.Ordinal))
            .Where(entry => string.Equals(entry["coverageStatus"]?.GetValue<string>(), "planned", StringComparison.Ordinal))
            .Select(entry => $"{entry["componentType"]!.GetValue<int>()}:{entry["mappedOwnerFamily"]!.GetValue<string>()}")
            .ToArray();

        plannedOwnerRows.Should().BeEmpty("the audited owner-family universe is now fully closed into support or explicit boundaries");
    }

    private static JsonObject LoadInventory() =>
        JsonNode.Parse(File.ReadAllText(InventoryPath))?.AsObject()
        ?? throw new InvalidOperationException("Component universe inventory could not be parsed.");

    private static IReadOnlyList<JsonObject> LoadEntries() =>
        LoadInventory()["entries"]!
            .AsArray()
            .Select(node => node!.AsObject())
            .ToArray();
}
