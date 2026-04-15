using FluentAssertions;
using DataverseSolutionCompiler.Diff;
using DataverseSolutionCompiler.Domain.Diff;
using DataverseSolutionCompiler.Domain.Diagnostics;
using DataverseSolutionCompiler.Domain.Live;
using DataverseSolutionCompiler.Domain.Model;
using DataverseSolutionCompiler.Domain.Planning;
using DataverseSolutionCompiler.Domain.Read;
using DataverseSolutionCompiler.Readers.Xml;
using Xunit;

namespace DataverseSolutionCompiler.UnitTests;

public sealed class StableOverlapDriftComparerTests
{
    [Theory]
    [InlineData("seed-alternate-key")]
    [InlineData("seed-core")]
    [InlineData("seed-forms")]
    [InlineData("seed-advanced-ui")]
    [InlineData("seed-environment")]
    [InlineData("seed-import-map")]
    [InlineData("seed-reporting-legacy")]
    [InlineData("seed-entity-analytics")]
    [InlineData("seed-image-config")]
    [InlineData("seed-ai-families")]
    [InlineData("seed-plugin-registration")]
    [InlineData("seed-process-policy")]
    [InlineData("seed-process-security")]
    [InlineData("seed-service-endpoint-connector")]
    public void Compare_reports_no_drift_when_live_matches_typed_source(string fixtureName)
    {
        var source = ReadFixture(fixtureName);
        var live = MatchingSnapshot(source);

        var report = new StableOverlapDriftComparer().Compare(source, live, new CompareRequest());

        report.Findings.Should().BeEmpty();
    }

    [Fact]
    public void Compare_reports_missing_live_artifacts()
    {
        var source = new CanonicalSolution(
            new SolutionIdentity("sample", "Sample", "1.0.0", LayeringIntent.Hybrid),
            new PublisherDefinition("dsc", "dsc", "dsc", "Dataverse Solution Compiler"),
            [new FamilyArtifact(ComponentFamily.Table, "account")],
            [],
            [],
            []);

        var live = MatchingSnapshot(source) with { Artifacts = [] };

        var report = new StableOverlapDriftComparer().Compare(source, live, new CompareRequest());

        report.Findings.Should().ContainSingle();
        report.Findings[0].Category.Should().Be(DriftCategory.MissingInLive);
    }

    [Fact]
    public void Compare_reports_family_semantic_mismatch()
    {
        var source = ReadFixture("seed-advanced-ui");
        var mutatedArtifacts = source.Artifacts.Select(artifact =>
            artifact.Family == ComponentFamily.EnvironmentVariableValue && artifact.LogicalName == "cdxmeta_AdvancedUiMode"
                ? artifact with
                {
                    Properties = artifact.Properties!.ToDictionary(
                        pair => pair.Key,
                        pair => pair.Key == ArtifactPropertyKeys.Value ? "live" : pair.Value,
                        StringComparer.Ordinal)
                }
                : artifact).ToArray();

        var live = MatchingSnapshot(source) with { Artifacts = mutatedArtifacts };

        var report = new StableOverlapDriftComparer().Compare(source, live, new CompareRequest());

        report.Findings.Should().ContainSingle();
        report.Findings[0].Category.Should().Be(DriftCategory.Mismatch);
        report.Findings[0].Description.Should().Contain(ArtifactPropertyKeys.Value);
    }

    [Fact]
    public void Compare_suppresses_live_only_platform_noise_columns()
    {
        var source = ReadFixture("seed-core");
        var liveArtifacts = source.Artifacts.Concat(
        [
            new FamilyArtifact(
                ComponentFamily.Column,
                "cdxmeta_workitem|owneridname",
                "Owner",
                Properties: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [ArtifactPropertyKeys.EntityLogicalName] = "cdxmeta_workitem",
                    [ArtifactPropertyKeys.IsCustomField] = "false",
                    [ArtifactPropertyKeys.AttributeType] = "lookup"
                }),
            new FamilyArtifact(
                ComponentFamily.Column,
                "cdxmeta_workitem|versionnumber",
                "Version Number",
                Properties: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [ArtifactPropertyKeys.EntityLogicalName] = "cdxmeta_workitem",
                    [ArtifactPropertyKeys.IsCustomField] = "false",
                    [ArtifactPropertyKeys.AttributeType] = "bigint"
                })
        ]).ToArray();

        var live = MatchingSnapshot(source) with { Artifacts = liveArtifacts };

        var report = new StableOverlapDriftComparer().Compare(source, live, new CompareRequest());

        report.Findings.Should().BeEmpty();
    }

    [Fact]
    public void Compare_excludes_best_effort_artifacts_by_default()
    {
        var source = new CanonicalSolution(
            new SolutionIdentity("sample", "Sample", "1.0.0", LayeringIntent.Hybrid),
            new PublisherDefinition("dsc", "dsc", "dsc", "Dataverse Solution Compiler"),
            [new FamilyArtifact(ComponentFamily.WebResource, "sample.js", Evidence: EvidenceKind.BestEffort)],
            [],
            [],
            []);

        var live = MatchingSnapshot(source);

        var report = new StableOverlapDriftComparer().Compare(source, live, new CompareRequest());

        report.Findings.Should().BeEmpty();
    }

    [Fact]
    public void Compare_does_not_report_missing_live_for_source_first_import_map_families()
    {
        var source = ReadFixture("seed-import-map") with
        {
            Artifacts = ReadFixture("seed-import-map").Artifacts
                .Where(artifact => artifact.Family is ComponentFamily.ImportMap or ComponentFamily.DataSourceMapping)
                .ToArray()
        };

        var live = new LiveSnapshot(new EnvironmentProfile("dev"), source.Identity.UniqueName, [], []);

        var report = new StableOverlapDriftComparer().Compare(source, live, new CompareRequest());

        report.Findings.Should().BeEmpty();
    }

    [Fact]
    public void Compare_does_not_report_missing_live_for_source_first_similarity_and_sla_families()
    {
        var slaArtifacts = ReadFixture("source-only-sla").Artifacts
            .Where(artifact => artifact.Family is ComponentFamily.Sla or ComponentFamily.SlaItem);
        var similarityArtifacts = ReadFixture("source-only-similarity-rule").Artifacts
            .Where(artifact => artifact.Family == ComponentFamily.SimilarityRule);
        var source = new CanonicalSolution(
            new SolutionIdentity("source-only-policy", "Source-only policy", "1.0.0", LayeringIntent.Hybrid),
            new PublisherDefinition("dsc", "dsc", "dsc", "Dataverse Solution Compiler"),
            slaArtifacts.Concat(similarityArtifacts).ToArray(),
            [],
            [],
            []);

        var live = new LiveSnapshot(new EnvironmentProfile("dev"), source.Identity.UniqueName, [], []);

        var report = new StableOverlapDriftComparer().Compare(source, live, new CompareRequest());

        report.Findings.Should().BeEmpty();
    }

    [Fact]
    public void Compare_does_not_report_missing_live_for_source_first_reporting_legacy_families()
    {
        var source = ReadFixture("seed-reporting-legacy") with
        {
            Artifacts = ReadFixture("seed-reporting-legacy").Artifacts
                .Where(artifact => artifact.Family is ComponentFamily.Report or ComponentFamily.Template or ComponentFamily.DisplayString or ComponentFamily.Attachment or ComponentFamily.LegacyAsset)
                .ToArray()
        };

        var live = new LiveSnapshot(new EnvironmentProfile("dev"), source.Identity.UniqueName, [], []);

        var report = new StableOverlapDriftComparer().Compare(source, live, new CompareRequest());

        report.Findings.Should().BeEmpty();
    }

    [Fact]
    public void Compare_normalizes_key_attribute_order_and_ignores_index_status()
    {
        var source = new CanonicalSolution(
            new SolutionIdentity("sample", "Sample", "1.0.0", LayeringIntent.Hybrid),
            new PublisherDefinition("dsc", "dsc", "dsc", "Dataverse Solution Compiler"),
            [
                new FamilyArtifact(
                    ComponentFamily.Key,
                    "account|account_externalcode",
                    "Account External Code",
                    Properties: new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        [ArtifactPropertyKeys.EntityLogicalName] = "account",
                        [ArtifactPropertyKeys.SchemaName] = "account_ExternalCode",
                        [ArtifactPropertyKeys.KeyAttributesJson] = "[\"accountnumber\",\"externalcode\"]",
                        [ArtifactPropertyKeys.IndexStatus] = "Active"
                    })
            ],
            [],
            [],
            []);

        var live = MatchingSnapshot(source) with
        {
            Artifacts =
            [
                new FamilyArtifact(
                    ComponentFamily.Key,
                    "account|account_externalcode",
                    "Account External Code",
                    Evidence: EvidenceKind.Readback,
                    Properties: new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        [ArtifactPropertyKeys.EntityLogicalName] = "account",
                        [ArtifactPropertyKeys.SchemaName] = "account_ExternalCode",
                        [ArtifactPropertyKeys.KeyAttributesJson] = "[\"externalcode\",\"accountnumber\"]",
                        [ArtifactPropertyKeys.IndexStatus] = "Pending"
                    })
            ]
        };

        var report = new StableOverlapDriftComparer().Compare(source, live, new CompareRequest());

        report.Findings.Should().BeEmpty();
    }

    [Fact]
    public void Compare_reports_blocking_drift_when_is_customizable_changes()
    {
        var source = ReadFixture("seed-image-config");
        var mutatedArtifacts = source.Artifacts.Select(artifact =>
        {
            if (artifact.Family == ComponentFamily.Table && artifact.LogicalName == "cdxmeta_photoasset")
            {
                return artifact with
                {
                    Properties = artifact.Properties!.ToDictionary(
                        pair => pair.Key,
                        pair => pair.Key == ArtifactPropertyKeys.IsCustomizable ? "false" : pair.Value,
                        StringComparer.Ordinal)
                };
            }

            return artifact;
        }).ToArray();

        var live = MatchingSnapshot(source) with { Artifacts = mutatedArtifacts };

        var report = new StableOverlapDriftComparer().Compare(source, live, new CompareRequest());

        report.HasBlockingDrift.Should().BeTrue();
        report.Findings.Should().ContainSingle(finding =>
            finding.Category == DriftCategory.Mismatch
            && finding.Description.Contains(ArtifactPropertyKeys.IsCustomizable, StringComparison.Ordinal));
    }

    [Fact]
    public void Compare_normalizes_connector_capabilities_order()
    {
        var source = new CanonicalSolution(
            new SolutionIdentity("sample", "Sample", "1.0.0", LayeringIntent.Hybrid),
            new PublisherDefinition("dsc", "dsc", "dsc", "Dataverse Solution Compiler"),
            [
                new FamilyArtifact(
                    ComponentFamily.Connector,
                    "shared-offerings-connector",
                    "Codex Shared Connector",
                    Properties: new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        [ArtifactPropertyKeys.Name] = "codex_shared_connector",
                        [ArtifactPropertyKeys.ConnectorInternalId] = "shared-offerings-connector",
                        [ArtifactPropertyKeys.CapabilitiesJson] = "[\"actions\",\"cloud\"]"
                    })
            ],
            [],
            [],
            []);

        var live = MatchingSnapshot(source) with
        {
            Artifacts =
            [
                new FamilyArtifact(
                    ComponentFamily.Connector,
                    "shared-offerings-connector",
                    "Codex Shared Connector",
                    Evidence: EvidenceKind.Readback,
                    Properties: new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        [ArtifactPropertyKeys.Name] = "codex_shared_connector",
                        [ArtifactPropertyKeys.ConnectorInternalId] = "shared-offerings-connector",
                        [ArtifactPropertyKeys.CapabilitiesJson] = "[\"cloud\",\"actions\"]"
                    })
            ]
        };

        var report = new StableOverlapDriftComparer().Compare(source, live, new CompareRequest());

        report.Findings.Should().BeEmpty();
    }

    private static CanonicalSolution ReadFixture(string fixtureName)
    {
        var reader = new XmlSolutionReader();
        return reader.Read(new ReadRequest(Path.Combine(
            "C:\\Git\\Dataverse-Solution-KB",
            "fixtures",
            "skill-corpus",
            "examples",
            fixtureName,
            "unpacked")));
    }

    private static LiveSnapshot MatchingSnapshot(CanonicalSolution source) =>
        new(
            new EnvironmentProfile("dev"),
            source.Identity.UniqueName,
            source.Artifacts,
            [new CompilerDiagnostic("live-match", DiagnosticSeverity.Info, "fixture-backed live match")]);
}
