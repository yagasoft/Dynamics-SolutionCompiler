using FluentAssertions;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
    [InlineData("seed-app-shell")]
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
    public void Compare_reports_missing_live_for_local_boolean_option_set_artifacts()
    {
        var sourceArtifact = ReadFixture("seed-core").Artifacts.Single(artifact =>
            artifact.Family == ComponentFamily.OptionSet
            && artifact.LogicalName == "cdxmeta_workitem|cdxmeta_isblocked");
        var source = new CanonicalSolution(
            new SolutionIdentity("schema-detail", "Schema detail", "1.0.0", LayeringIntent.Hybrid),
            new PublisherDefinition("dsc", "dsc", "dsc", "Dataverse Solution Compiler"),
            [sourceArtifact],
            [],
            [],
            []);

        var live = new LiveSnapshot(new EnvironmentProfile("dev"), source.Identity.UniqueName, [], []);

        var report = new StableOverlapDriftComparer().Compare(source, live, new CompareRequest());

        report.Findings.Should().ContainSingle(finding =>
            finding.Category == DriftCategory.MissingInLive
            && finding.Family == ComponentFamily.OptionSet);
    }

    [Fact]
    public void Compare_reports_missing_live_for_quick_and_card_forms()
    {
        var sourceArtifacts = ReadFixture("seed-forms").Artifacts
            .Where(artifact => artifact.Family == ComponentFamily.Form
                && artifact.Properties is not null
                && artifact.Properties.TryGetValue(ArtifactPropertyKeys.FormType, out var formType)
                && (string.Equals(formType, "quick", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(formType, "card", StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        var source = new CanonicalSolution(
            new SolutionIdentity("forms", "Forms", "1.0.0", LayeringIntent.Hybrid),
            new PublisherDefinition("dsc", "dsc", "dsc", "Dataverse Solution Compiler"),
            sourceArtifacts,
            [],
            [],
            []);

        var live = new LiveSnapshot(new EnvironmentProfile("dev"), source.Identity.UniqueName, [], []);

        var report = new StableOverlapDriftComparer().Compare(source, live, new CompareRequest());

        report.Findings.Should().HaveCount(sourceArtifacts.Length);
        report.Findings.Should().OnlyContain(finding =>
            finding.Category == DriftCategory.MissingInLive
            && finding.Family == ComponentFamily.Form);
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
    public void Compare_reports_visualization_mismatch_when_chart_definition_changes()
    {
        var source = ReadFixture("seed-advanced-ui");
        var live = MatchingSnapshot(source) with
        {
            Artifacts = source.Artifacts.Select(artifact =>
            {
                if (artifact.Family != ComponentFamily.Visualization)
                {
                    return artifact with { Evidence = EvidenceKind.Readback };
                }

                var chartTypesJson = "[\"Column\"]";
                var summaryJson = JsonSerializer.Serialize(new
                {
                    targetEntity = GetProperty(artifact, ArtifactPropertyKeys.TargetEntity),
                    chartTypes = JsonSerializer.Deserialize<string[]>(chartTypesJson),
                    groupByColumns = JsonSerializer.Deserialize<string[]>(GetProperty(artifact, ArtifactPropertyKeys.GroupByColumnsJson) ?? "[]"),
                    measureAliases = JsonSerializer.Deserialize<string[]>(GetProperty(artifact, ArtifactPropertyKeys.MeasureAliasesJson) ?? "[]"),
                    titleNames = JsonSerializer.Deserialize<string[]>(GetProperty(artifact, ArtifactPropertyKeys.TitleNamesJson) ?? "[]")
                });

                return artifact with
                {
                    Evidence = EvidenceKind.Readback,
                    Properties = artifact.Properties!.ToDictionary(
                        pair => pair.Key,
                        pair => pair.Key switch
                        {
                            var key when key == ArtifactPropertyKeys.ChartTypesJson => chartTypesJson,
                            var key when key == ArtifactPropertyKeys.SummaryJson => summaryJson,
                            var key when key == ArtifactPropertyKeys.ComparisonSignature => ComputeSignature(summaryJson),
                            _ => pair.Value
                        },
                        StringComparer.Ordinal)
                };
            }).ToArray()
        };

        var report = new StableOverlapDriftComparer().Compare(source, live, new CompareRequest());

        report.Findings.Should().ContainSingle(finding =>
            finding.Category == DriftCategory.Mismatch
            && finding.Family == ComponentFamily.Visualization
            && finding.Description.Contains(ArtifactPropertyKeys.ComparisonSignature, StringComparison.Ordinal));
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
    public void Compare_does_not_report_missing_live_for_source_first_ribbon_families()
    {
        var source = ReadFixture("seed-advanced-ui") with
        {
            Artifacts = ReadFixture("seed-advanced-ui").Artifacts
                .Where(artifact => artifact.Family == ComponentFamily.Ribbon)
                .ToArray()
        };

        var live = new LiveSnapshot(new EnvironmentProfile("dev"), source.Identity.UniqueName, [], []);

        var report = new StableOverlapDriftComparer().Compare(source, live, new CompareRequest());

        report.Findings.Should().BeEmpty();
    }

    [Fact]
    public void Compare_emits_best_effort_diagnostic_when_live_app_module_role_maps_are_underreported()
    {
        var source = ReadFixture("seed-app-shell");
        var live = MatchingSnapshot(source) with
        {
            Artifacts = source.Artifacts.Select(artifact =>
                artifact.Family == ComponentFamily.AppModule
                    ? artifact with
                    {
                        Evidence = EvidenceKind.Readback,
                        Properties = artifact.Properties!.ToDictionary(
                            pair => pair.Key,
                            pair => pair.Key switch
                            {
                                var key when key == ArtifactPropertyKeys.RoleIdsJson => "[]",
                                var key when key == ArtifactPropertyKeys.RoleMapCount => "0",
                                _ => pair.Value
                            },
                            StringComparer.Ordinal)
                    }
                    : artifact with { Evidence = EvidenceKind.Readback }).ToArray()
        };

        var report = new StableOverlapDriftComparer().Compare(source, live, new CompareRequest());

        report.Findings.Should().BeEmpty();
        report.Diagnostics.Should().Contain(diagnostic =>
            diagnostic.Code == "stable-overlap-appmodule-rolemap-best-effort"
            && diagnostic.Location == "codex_metadata_shell_dd96cf20");
    }

    [Fact]
    public void Compare_emits_best_effort_diagnostic_for_source_asymmetric_custom_controls()
    {
        var source = ReadFixture("seed-advanced-ui");
        var live = MatchingSnapshot(source) with
        {
            Artifacts =
            [
                .. source.Artifacts.Select(artifact => artifact with { Evidence = EvidenceKind.Readback }),
                new FamilyArtifact(
                    ComponentFamily.CustomControl,
                    "cat_powercat.customizabletextfield",
                    "cat_PowerCAT.CustomizableTextField",
                    "customcontrols/cat_powercat.customizabletextfield",
                    EvidenceKind.Readback,
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        [ArtifactPropertyKeys.Version] = "0.0.2",
                        [ArtifactPropertyKeys.ComparisonSignature] = "customcontrol-signature"
                    })
            ]
        };

        var report = new StableOverlapDriftComparer().Compare(source, live, new CompareRequest());

        report.Findings.Should().BeEmpty();
        report.Diagnostics.Should().Contain(diagnostic =>
            diagnostic.Code == "stable-overlap-customcontrol-source-asymmetry"
            && diagnostic.Location == "cat_powercat.customizabletextfield");
    }

    [Fact]
    public void Compare_reports_site_map_navigation_detail_mismatch_when_definition_changes()
    {
        var source = ReadFixture("seed-app-shell");
        var live = MatchingSnapshot(source) with
        {
            Artifacts = source.Artifacts.Select(artifact =>
            {
                if (artifact.Family != ComponentFamily.SiteMap)
                {
                    return artifact with { Evidence = EvidenceKind.Readback };
                }

                var definitionJson = GetProperty(artifact, ArtifactPropertyKeys.SiteMapDefinitionJson)!
                    .Replace("cdxmeta_/shell/landing.html", "cdxmeta_/shell/drift.html", StringComparison.Ordinal);
                return artifact with
                {
                    Evidence = EvidenceKind.Readback,
                    Properties = artifact.Properties!.ToDictionary(
                        pair => pair.Key,
                        pair => pair.Key switch
                        {
                            var key when key == ArtifactPropertyKeys.SiteMapDefinitionJson => definitionJson,
                            var key when key == ArtifactPropertyKeys.ComparisonSignature => ComputeSignature(definitionJson),
                            _ => pair.Value
                        },
                        StringComparer.Ordinal)
                };
            }).ToArray()
        };

        var report = new StableOverlapDriftComparer().Compare(source, live, new CompareRequest());

        report.HasBlockingDrift.Should().BeFalse();
        report.Findings.Should().ContainSingle(finding =>
            finding.Category == DriftCategory.Mismatch
            && finding.Description.Contains(ArtifactPropertyKeys.ComparisonSignature, StringComparison.Ordinal));
    }

    [Fact]
    public void Compare_reports_site_map_adjunct_mismatch_when_icon_changes()
    {
        var source = ReadFixture("seed-app-shell");
        var live = MatchingSnapshot(source) with
        {
            Artifacts = source.Artifacts.Select(artifact =>
            {
                if (artifact.Family != ComponentFamily.SiteMap)
                {
                    return artifact with { Evidence = EvidenceKind.Readback };
                }

                var definitionJson = GetProperty(artifact, ArtifactPropertyKeys.SiteMapDefinitionJson)!
                    .Replace("icon.svg", "icon-drift.svg", StringComparison.Ordinal);
                return artifact with
                {
                    Evidence = EvidenceKind.Readback,
                    Properties = artifact.Properties!.ToDictionary(
                        pair => pair.Key,
                        pair => pair.Key switch
                        {
                            var key when key == ArtifactPropertyKeys.SiteMapDefinitionJson => definitionJson,
                            var key when key == ArtifactPropertyKeys.ComparisonSignature => ComputeSignature(definitionJson),
                            _ => pair.Value
                        },
                        StringComparer.Ordinal)
                };
            }).ToArray()
        };

        var report = new StableOverlapDriftComparer().Compare(source, live, new CompareRequest());

        report.HasBlockingDrift.Should().BeFalse();
        report.Findings.Should().ContainSingle(finding =>
            finding.Category == DriftCategory.Mismatch
            && finding.Description.Contains(ArtifactPropertyKeys.ComparisonSignature, StringComparison.Ordinal));
    }

    [Fact]
    public void Compare_reports_site_map_dashboard_target_mismatch_when_dashboard_changes()
    {
        const string sourceDashboardId = "3c5d4df8-4c0d-4d57-9e8f-6d4b3a8d5812";
        const string liveDashboardId = "d42c7cc7-1e81-42d5-8711-7a7861fbded9";

        var source = ReadFixture("seed-app-shell") with
        {
            Artifacts = ReadFixture("seed-app-shell").Artifacts.Select(artifact =>
            {
                if (artifact.Family != ComponentFamily.SiteMap)
                {
                    return artifact;
                }

                var definitionJson = GetProperty(artifact, ArtifactPropertyKeys.SiteMapDefinitionJson)!
                    .Replace("\"webResource\":\"cdxmeta_/shell/landing.html\"", $"\"dashboard\":\"{sourceDashboardId}\"", StringComparison.Ordinal);
                return artifact with
                {
                    Properties = artifact.Properties!.ToDictionary(
                        pair => pair.Key,
                        pair => pair.Key switch
                        {
                            var key when key == ArtifactPropertyKeys.SiteMapDefinitionJson => definitionJson,
                            var key when key == ArtifactPropertyKeys.ComparisonSignature => ComputeSignature(definitionJson),
                            _ => pair.Value
                        },
                        StringComparer.Ordinal)
                };
            }).ToArray()
        };

        var live = MatchingSnapshot(source) with
        {
            Artifacts = source.Artifacts.Select(artifact =>
            {
                if (artifact.Family != ComponentFamily.SiteMap)
                {
                    return artifact with { Evidence = EvidenceKind.Readback };
                }

                var definitionJson = GetProperty(artifact, ArtifactPropertyKeys.SiteMapDefinitionJson)!
                    .Replace(sourceDashboardId, liveDashboardId, StringComparison.Ordinal);
                return artifact with
                {
                    Evidence = EvidenceKind.Readback,
                    Properties = artifact.Properties!.ToDictionary(
                        pair => pair.Key,
                        pair => pair.Key switch
                        {
                            var key when key == ArtifactPropertyKeys.SiteMapDefinitionJson => definitionJson,
                            var key when key == ArtifactPropertyKeys.ComparisonSignature => ComputeSignature(definitionJson),
                            _ => pair.Value
                        },
                        StringComparer.Ordinal)
                };
            }).ToArray()
        };

        var report = new StableOverlapDriftComparer().Compare(source, live, new CompareRequest());

        report.HasBlockingDrift.Should().BeFalse();
        report.Findings.Should().ContainSingle(finding =>
            finding.Category == DriftCategory.Mismatch
            && finding.Description.Contains(ArtifactPropertyKeys.ComparisonSignature, StringComparison.Ordinal));
    }

    [Fact]
    public void Compare_reports_site_map_dashboard_app_scope_mismatch_when_app_changes()
    {
        const string dashboardId = "3c5d4df8-4c0d-4d57-9e8f-6d4b3a8d5812";
        const string sourceAppId = "e1d1df92-5e88-4cff-8562-3d0f3f7164d0";
        const string liveAppId = "6bf4c2c2-a514-453a-bff8-7b24798b4e52";

        var source = ReadFixture("seed-app-shell") with
        {
            Artifacts = ReadFixture("seed-app-shell").Artifacts.Select(artifact =>
            {
                if (artifact.Family != ComponentFamily.SiteMap)
                {
                    return artifact;
                }

                var definitionJson = GetProperty(artifact, ArtifactPropertyKeys.SiteMapDefinitionJson)!
                    .Replace(
                        "\"webResource\":\"cdxmeta_/shell/landing.html\"",
                        $"\"dashboard\":\"{dashboardId}\",\"appId\":\"{sourceAppId}\"",
                        StringComparison.Ordinal);
                return artifact with
                {
                    Properties = artifact.Properties!.ToDictionary(
                        pair => pair.Key,
                        pair => pair.Key switch
                        {
                            var key when key == ArtifactPropertyKeys.SiteMapDefinitionJson => definitionJson,
                            var key when key == ArtifactPropertyKeys.ComparisonSignature => ComputeSignature(definitionJson),
                            _ => pair.Value
                        },
                        StringComparer.Ordinal)
                };
            }).ToArray()
        };

        var live = MatchingSnapshot(source) with
        {
            Artifacts = source.Artifacts.Select(artifact =>
            {
                if (artifact.Family != ComponentFamily.SiteMap)
                {
                    return artifact with { Evidence = EvidenceKind.Readback };
                }

                var definitionJson = GetProperty(artifact, ArtifactPropertyKeys.SiteMapDefinitionJson)!
                    .Replace(sourceAppId, liveAppId, StringComparison.Ordinal);
                return artifact with
                {
                    Evidence = EvidenceKind.Readback,
                    Properties = artifact.Properties!.ToDictionary(
                        pair => pair.Key,
                        pair => pair.Key switch
                        {
                            var key when key == ArtifactPropertyKeys.SiteMapDefinitionJson => definitionJson,
                            var key when key == ArtifactPropertyKeys.ComparisonSignature => ComputeSignature(definitionJson),
                            _ => pair.Value
                        },
                        StringComparer.Ordinal)
                };
            }).ToArray()
        };

        var report = new StableOverlapDriftComparer().Compare(source, live, new CompareRequest());

        report.HasBlockingDrift.Should().BeFalse();
        report.Findings.Should().ContainSingle(finding =>
            finding.Category == DriftCategory.Mismatch
            && finding.Description.Contains(ArtifactPropertyKeys.ComparisonSignature, StringComparison.Ordinal));
    }

    [Fact]
    public void Compare_reports_site_map_custom_page_target_mismatch_when_app_changes()
    {
        const string customPage = "cdxmeta_shellhome";
        const string sourceAppId = "e1d1df92-5e88-4cff-8562-3d0f3f7164d0";
        const string liveAppId = "6bf4c2c2-a514-453a-bff8-7b24798b4e52";

        var source = ReadFixture("seed-app-shell") with
        {
            Artifacts = ReadFixture("seed-app-shell").Artifacts.Select(artifact =>
            {
                if (artifact.Family != ComponentFamily.SiteMap)
                {
                    return artifact;
                }

                var definitionJson = GetProperty(artifact, ArtifactPropertyKeys.SiteMapDefinitionJson)!
                    .Replace(
                        "\"webResource\":\"cdxmeta_/shell/landing.html\"",
                        $"\"customPage\":\"{customPage}\",\"appId\":\"{sourceAppId}\"",
                        StringComparison.Ordinal);
                return artifact with
                {
                    Properties = artifact.Properties!.ToDictionary(
                        pair => pair.Key,
                        pair => pair.Key switch
                        {
                            var key when key == ArtifactPropertyKeys.SiteMapDefinitionJson => definitionJson,
                            var key when key == ArtifactPropertyKeys.ComparisonSignature => ComputeSignature(definitionJson),
                            _ => pair.Value
                        },
                        StringComparer.Ordinal)
                };
            }).ToArray()
        };

        var live = MatchingSnapshot(source) with
        {
            Artifacts = source.Artifacts.Select(artifact =>
            {
                if (artifact.Family != ComponentFamily.SiteMap)
                {
                    return artifact with { Evidence = EvidenceKind.Readback };
                }

                var definitionJson = GetProperty(artifact, ArtifactPropertyKeys.SiteMapDefinitionJson)!
                    .Replace(sourceAppId, liveAppId, StringComparison.Ordinal);
                return artifact with
                {
                    Evidence = EvidenceKind.Readback,
                    Properties = artifact.Properties!.ToDictionary(
                        pair => pair.Key,
                        pair => pair.Key switch
                        {
                            var key when key == ArtifactPropertyKeys.SiteMapDefinitionJson => definitionJson,
                            var key when key == ArtifactPropertyKeys.ComparisonSignature => ComputeSignature(definitionJson),
                            _ => pair.Value
                        },
                        StringComparer.Ordinal)
                };
            }).ToArray()
        };

        var report = new StableOverlapDriftComparer().Compare(source, live, new CompareRequest());

        report.HasBlockingDrift.Should().BeFalse();
        report.Findings.Should().ContainSingle(finding =>
            finding.Category == DriftCategory.Mismatch
            && finding.Description.Contains(ArtifactPropertyKeys.ComparisonSignature, StringComparison.Ordinal));
    }

    [Fact]
    public void Compare_reports_site_map_custom_page_context_mismatch_when_record_changes()
    {
        const string customPage = "cdxmeta_shellhome";
        const string sourceRecordId = "bd7616fe-3f95-4d6a-b4cb-9e788425f721";
        const string liveRecordId = "f5c7b814-d5ba-4d53-b3bc-588c4403ef25";

        var source = ReadFixture("seed-app-shell") with
        {
            Artifacts = ReadFixture("seed-app-shell").Artifacts.Select(artifact =>
            {
                if (artifact.Family != ComponentFamily.SiteMap)
                {
                    return artifact;
                }

                var definitionJson = GetProperty(artifact, ArtifactPropertyKeys.SiteMapDefinitionJson)!
                    .Replace(
                        "\"webResource\":\"cdxmeta_/shell/landing.html\"",
                        $"\"customPage\":\"{customPage}\",\"customPageEntityName\":\"account\",\"customPageRecordId\":\"{sourceRecordId}\"",
                        StringComparison.Ordinal);
                return artifact with
                {
                    Properties = artifact.Properties!.ToDictionary(
                        pair => pair.Key,
                        pair => pair.Key switch
                        {
                            var key when key == ArtifactPropertyKeys.SiteMapDefinitionJson => definitionJson,
                            var key when key == ArtifactPropertyKeys.ComparisonSignature => ComputeSignature(definitionJson),
                            _ => pair.Value
                        },
                        StringComparer.Ordinal)
                };
            }).ToArray()
        };

        var live = MatchingSnapshot(source) with
        {
            Artifacts = source.Artifacts.Select(artifact =>
            {
                if (artifact.Family != ComponentFamily.SiteMap)
                {
                    return artifact with { Evidence = EvidenceKind.Readback };
                }

                var definitionJson = GetProperty(artifact, ArtifactPropertyKeys.SiteMapDefinitionJson)!
                    .Replace(sourceRecordId, liveRecordId, StringComparison.Ordinal);
                return artifact with
                {
                    Evidence = EvidenceKind.Readback,
                    Properties = artifact.Properties!.ToDictionary(
                        pair => pair.Key,
                        pair => pair.Key switch
                        {
                            var key when key == ArtifactPropertyKeys.SiteMapDefinitionJson => definitionJson,
                            var key when key == ArtifactPropertyKeys.ComparisonSignature => ComputeSignature(definitionJson),
                            _ => pair.Value
                        },
                        StringComparer.Ordinal)
                };
            }).ToArray()
        };

        var report = new StableOverlapDriftComparer().Compare(source, live, new CompareRequest());

        report.HasBlockingDrift.Should().BeFalse();
        report.Findings.Should().ContainSingle(finding =>
            finding.Category == DriftCategory.Mismatch
            && finding.Description.Contains(ArtifactPropertyKeys.ComparisonSignature, StringComparison.Ordinal));
    }

    [Fact]
    public void Compare_reports_site_map_entity_list_target_mismatch_when_view_changes()
    {
        const string sourceViewId = "0cc7bf59-5fb4-4f11-a3b2-9170a9d6ef42";
        const string liveViewId = "7a6e49c5-fb7b-43c1-9093-0f26f6c0110c";
        const string appId = "e1d1df92-5e88-4cff-8562-3d0f3f7164d0";

        var source = ReadFixture("seed-app-shell") with
        {
            Artifacts = ReadFixture("seed-app-shell").Artifacts.Select(artifact =>
            {
                if (artifact.Family != ComponentFamily.SiteMap)
                {
                    return artifact;
                }

                var definitionJson = GetProperty(artifact, ArtifactPropertyKeys.SiteMapDefinitionJson)!
                    .Replace(
                        "\"webResource\":\"cdxmeta_/shell/landing.html\"",
                        $"\"entity\":\"account\",\"viewId\":\"{sourceViewId}\",\"viewType\":\"savedquery\",\"appId\":\"{appId}\"",
                        StringComparison.Ordinal);
                return artifact with
                {
                    Properties = artifact.Properties!.ToDictionary(
                        pair => pair.Key,
                        pair => pair.Key switch
                        {
                            var key when key == ArtifactPropertyKeys.SiteMapDefinitionJson => definitionJson,
                            var key when key == ArtifactPropertyKeys.ComparisonSignature => ComputeSignature(definitionJson),
                            _ => pair.Value
                        },
                        StringComparer.Ordinal)
                };
            }).ToArray()
        };

        var live = MatchingSnapshot(source) with
        {
            Artifacts = source.Artifacts.Select(artifact =>
            {
                if (artifact.Family != ComponentFamily.SiteMap)
                {
                    return artifact with { Evidence = EvidenceKind.Readback };
                }

                var definitionJson = GetProperty(artifact, ArtifactPropertyKeys.SiteMapDefinitionJson)!
                    .Replace(sourceViewId, liveViewId, StringComparison.Ordinal);
                return artifact with
                {
                    Evidence = EvidenceKind.Readback,
                    Properties = artifact.Properties!.ToDictionary(
                        pair => pair.Key,
                        pair => pair.Key switch
                        {
                            var key when key == ArtifactPropertyKeys.SiteMapDefinitionJson => definitionJson,
                            var key when key == ArtifactPropertyKeys.ComparisonSignature => ComputeSignature(definitionJson),
                            _ => pair.Value
                        },
                        StringComparer.Ordinal)
                };
            }).ToArray()
        };

        var report = new StableOverlapDriftComparer().Compare(source, live, new CompareRequest());

        report.HasBlockingDrift.Should().BeFalse();
        report.Findings.Should().ContainSingle(finding =>
            finding.Category == DriftCategory.Mismatch
            && finding.Description.Contains(ArtifactPropertyKeys.ComparisonSignature, StringComparison.Ordinal));
    }

    [Fact]
    public void Compare_reports_site_map_entity_record_target_mismatch_when_record_changes()
    {
        const string recordId = "bd7616fe-3f95-4d6a-b4cb-9e788425f721";
        const string liveRecordId = "34b8f6d6-f45d-4ee5-a56f-b9784a1f5261";
        const string formId = "a77ba3f0-df52-46a1-a0a2-2c4fd6e25cdf";
        const string appId = "e1d1df92-5e88-4cff-8562-3d0f3f7164d0";

        var source = ReadFixture("seed-app-shell") with
        {
            Artifacts = ReadFixture("seed-app-shell").Artifacts.Select(artifact =>
            {
                if (artifact.Family != ComponentFamily.SiteMap)
                {
                    return artifact;
                }

                var definitionJson = GetProperty(artifact, ArtifactPropertyKeys.SiteMapDefinitionJson)!
                    .Replace(
                        "\"webResource\":\"cdxmeta_/shell/landing.html\"",
                        $"\"entity\":\"account\",\"recordId\":\"{recordId}\",\"formId\":\"{formId}\",\"appId\":\"{appId}\"",
                        StringComparison.Ordinal);
                return artifact with
                {
                    Properties = artifact.Properties!.ToDictionary(
                        pair => pair.Key,
                        pair => pair.Key switch
                        {
                            var key when key == ArtifactPropertyKeys.SiteMapDefinitionJson => definitionJson,
                            var key when key == ArtifactPropertyKeys.ComparisonSignature => ComputeSignature(definitionJson),
                            _ => pair.Value
                        },
                        StringComparer.Ordinal)
                };
            }).ToArray()
        };

        var live = MatchingSnapshot(source) with
        {
            Artifacts = source.Artifacts.Select(artifact =>
            {
                if (artifact.Family != ComponentFamily.SiteMap)
                {
                    return artifact with { Evidence = EvidenceKind.Readback };
                }

                var definitionJson = GetProperty(artifact, ArtifactPropertyKeys.SiteMapDefinitionJson)!
                    .Replace(recordId, liveRecordId, StringComparison.Ordinal);
                return artifact with
                {
                    Evidence = EvidenceKind.Readback,
                    Properties = artifact.Properties!.ToDictionary(
                        pair => pair.Key,
                        pair => pair.Key switch
                        {
                            var key when key == ArtifactPropertyKeys.SiteMapDefinitionJson => definitionJson,
                            var key when key == ArtifactPropertyKeys.ComparisonSignature => ComputeSignature(definitionJson),
                            _ => pair.Value
                        },
                        StringComparer.Ordinal)
                };
            }).ToArray()
        };

        var report = new StableOverlapDriftComparer().Compare(source, live, new CompareRequest());

        report.HasBlockingDrift.Should().BeFalse();
        report.Findings.Should().ContainSingle(finding =>
            finding.Category == DriftCategory.Mismatch
            && finding.Description.Contains(ArtifactPropertyKeys.ComparisonSignature, StringComparison.Ordinal));
    }

    [Fact]
    public void Compare_reports_site_map_raw_url_boundary_mismatch_when_canonical_raw_url_changes()
    {
        const string appId = "e1d1df92-5e88-4cff-8562-3d0f3f7164d0";
        const string dashboardId = "3c5d4df8-4c0d-4d57-9e8f-6d4b3a8d5812";
        const string contextRecordId = "bd7616fe-3f95-4d6a-b4cb-9e788425f721";
        var sourceRawUrl = $"/main.aspx?appid={appId}&extraqs=entityName%3Daccount%26recordId%3D{contextRecordId}&id={dashboardId}&pagetype=dashboard&showWelcome=true";
        var liveRawUrl = $"/main.aspx?appid={appId}&extraqs=entityName%3Daccount%26recordId%3D{contextRecordId}&id={dashboardId}&pagetype=dashboard&showWelcome=false";

        var source = ReadFixture("seed-app-shell") with
        {
            Artifacts = ReadFixture("seed-app-shell").Artifacts.Select(artifact =>
            {
                if (artifact.Family != ComponentFamily.SiteMap)
                {
                    return artifact;
                }

                var definitionJson = GetProperty(artifact, ArtifactPropertyKeys.SiteMapDefinitionJson)!
                    .Replace(
                        "\"webResource\":\"cdxmeta_/shell/landing.html\"",
                        $"\"url\":\"{sourceRawUrl}\"",
                        StringComparison.Ordinal);
                return artifact with
                {
                    Properties = artifact.Properties!.ToDictionary(
                        pair => pair.Key,
                        pair => pair.Key switch
                        {
                            var key when key == ArtifactPropertyKeys.SiteMapDefinitionJson => definitionJson,
                            var key when key == ArtifactPropertyKeys.ComparisonSignature => ComputeSignature(definitionJson),
                            var key when key == ArtifactPropertyKeys.WebResourceSubAreaCount => "0",
                            _ => pair.Value
                        },
                        StringComparer.Ordinal)
                };
            }).ToArray()
        };

        var live = MatchingSnapshot(source) with
        {
            Artifacts = source.Artifacts.Select(artifact =>
            {
                if (artifact.Family != ComponentFamily.SiteMap)
                {
                    return artifact with { Evidence = EvidenceKind.Readback };
                }

                var definitionJson = GetProperty(artifact, ArtifactPropertyKeys.SiteMapDefinitionJson)!
                    .Replace(sourceRawUrl, liveRawUrl, StringComparison.Ordinal);
                return artifact with
                {
                    Evidence = EvidenceKind.Readback,
                    Properties = artifact.Properties!.ToDictionary(
                        pair => pair.Key,
                        pair => pair.Key switch
                        {
                            var key when key == ArtifactPropertyKeys.SiteMapDefinitionJson => definitionJson,
                            var key when key == ArtifactPropertyKeys.ComparisonSignature => ComputeSignature(definitionJson),
                            _ => pair.Value
                        },
                        StringComparer.Ordinal)
                };
            }).ToArray()
        };

        var report = new StableOverlapDriftComparer().Compare(source, live, new CompareRequest());

        report.HasBlockingDrift.Should().BeFalse();
        report.Findings.Should().ContainSingle(finding =>
            finding.Category == DriftCategory.Mismatch
            && finding.Description.Contains(ArtifactPropertyKeys.ComparisonSignature, StringComparison.Ordinal));
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

    private static string? GetProperty(FamilyArtifact artifact, string key) =>
        artifact.Properties is not null && artifact.Properties.TryGetValue(key, out var value) ? value : null;

    private static string ComputeSignature(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
