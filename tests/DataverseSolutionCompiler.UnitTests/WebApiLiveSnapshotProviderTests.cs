using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Azure.Core;
using Azure.Identity;
using FluentAssertions;
using DataverseSolutionCompiler.Diff;
using DataverseSolutionCompiler.Domain.Diff;
using DataverseSolutionCompiler.Domain.Live;
using DataverseSolutionCompiler.Domain.Model;
using DataverseSolutionCompiler.Domain.Planning;
using DataverseSolutionCompiler.Domain.Read;
using DataverseSolutionCompiler.Readers.Live;
using DataverseSolutionCompiler.Readers.Xml;
using Xunit;

namespace DataverseSolutionCompiler.UnitTests;

public sealed class WebApiLiveSnapshotProviderTests
{
    [Fact]
    public async Task ReadAsync_projects_seed_core_schema_and_option_set_families()
    {
        var harness = LiveFixtureHarness.Create("seed-core", pageSolutionComponents: true);

        var snapshot = await harness.ReadAsync(
            ComponentFamily.SolutionShell,
            ComponentFamily.Table,
            ComponentFamily.Column,
            ComponentFamily.Relationship,
            ComponentFamily.OptionSet);

        snapshot.Artifacts.Should().Contain(artifact => artifact.Family == ComponentFamily.Table && artifact.LogicalName == "cdxmeta_workitem");
        snapshot.Artifacts.Should().Contain(artifact => artifact.Family == ComponentFamily.Column && artifact.LogicalName == "cdxmeta_workitem|cdxmeta_details");
        snapshot.Artifacts.Should().Contain(artifact => artifact.Family == ComponentFamily.Relationship && artifact.LogicalName == "cdxmeta_category_workitem");
        snapshot.Artifacts.Should().Contain(artifact => artifact.Family == ComponentFamily.OptionSet && artifact.LogicalName == "cdxmeta_priorityband");
        snapshot.Artifacts.Should().ContainSingle(artifact =>
            artifact.Family == ComponentFamily.OptionSet
            && artifact.LogicalName == "cdxmeta_workitem|cdxmeta_stage"
            && artifact.Properties![ArtifactPropertyKeys.OptionSetType] == "picklist"
            && artifact.Properties![ArtifactPropertyKeys.OptionCount] == "3");
        snapshot.Artifacts.Should().ContainSingle(artifact =>
            artifact.Family == ComponentFamily.OptionSet
            && artifact.LogicalName == "cdxmeta_workitem|cdxmeta_isblocked"
            && artifact.Properties![ArtifactPropertyKeys.OptionSetType] == "boolean"
            && artifact.Properties![ArtifactPropertyKeys.OptionCount] == "2");
        harness.Requests.Should().Contain(request => request.Contains("$skiptoken=page2", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ReadAsync_projects_alternate_key_family()
    {
        var harness = LiveFixtureHarness.Create("seed-alternate-key");

        var snapshot = await harness.ReadAsync(ComponentFamily.Key);

        snapshot.Artifacts.Should().ContainSingle(artifact =>
            artifact.Family == ComponentFamily.Key
            && artifact.LogicalName == "cdxmeta_workitem|cdxmeta_workitem_externalcode");
        var key = snapshot.Artifacts.Single(artifact => artifact.Family == ComponentFamily.Key);
        key.Properties![ArtifactPropertyKeys.EntityLogicalName].Should().Be("cdxmeta_workitem");
        key.Properties![ArtifactPropertyKeys.SchemaName].Should().Be("cdxmeta_WorkItem_ExternalCode");
        key.Properties![ArtifactPropertyKeys.KeyAttributesJson].Should().Be("[\"cdxmeta_externalcode\"]");
        harness.Requests.Should().Contain(request => request.Contains("/solutioncomponents", StringComparison.OrdinalIgnoreCase));
        harness.Requests.Should().Contain(request => request.Contains("$expand=Keys", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ReadAsync_projects_image_configuration_family()
    {
        var harness = LiveFixtureHarness.Create("seed-image-config");

        var snapshot = await harness.ReadAsync(
            ComponentFamily.Table,
            ComponentFamily.Column,
            ComponentFamily.ImageConfiguration);

        snapshot.Artifacts.Should().ContainSingle(artifact =>
            artifact.Family == ComponentFamily.ImageConfiguration
            && artifact.LogicalName == "cdxmeta_photoasset|entity-image");
        snapshot.Artifacts.Should().ContainSingle(artifact =>
            artifact.Family == ComponentFamily.ImageConfiguration
            && artifact.LogicalName == "cdxmeta_photoasset|cdxmeta_profileimage|attribute-image");

        var table = snapshot.Artifacts.Single(artifact => artifact.Family == ComponentFamily.Table && artifact.LogicalName == "cdxmeta_photoasset");
        table.Properties![ArtifactPropertyKeys.IsCustomizable].Should().Be("true");

        var imageColumn = snapshot.Artifacts.Single(artifact =>
            artifact.Family == ComponentFamily.Column
            && artifact.LogicalName == "cdxmeta_photoasset|cdxmeta_profileimage");
        imageColumn.Properties![ArtifactPropertyKeys.IsCustomizable].Should().Be("true");

        harness.Requests.Should().Contain(request => request.Contains("ImageAttributeMetadata", StringComparison.OrdinalIgnoreCase));
        harness.Requests.Should().Contain(request => request.Contains("/solutioncomponents", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ReadAsync_falls_back_to_entity_scoped_image_configuration_when_solution_scope_underreports()
    {
        var harness = LiveFixtureHarness.Create("seed-image-config", omitImageConfigurationScope: true);

        var snapshot = await harness.ReadAsync(ComponentFamily.ImageConfiguration);

        snapshot.Diagnostics.Should().Contain(diagnostic => diagnostic.Code == "live-readback-image-config-fallback");
        snapshot.Artifacts.Should().Contain(artifact => artifact.Family == ComponentFamily.ImageConfiguration && artifact.LogicalName == "cdxmeta_photoasset|entity-image");
        snapshot.Artifacts.Should().Contain(artifact => artifact.Family == ComponentFamily.ImageConfiguration && artifact.LogicalName == "cdxmeta_photoasset|cdxmeta_profileimage|attribute-image");
    }

    [Fact]
    public async Task ReadAsync_projects_app_shell_and_canvas_app_families()
    {
        var advancedUiHarness = LiveFixtureHarness.Create("seed-advanced-ui");
        var advancedUi = await advancedUiHarness.ReadAsync(
            ComponentFamily.AppModule,
            ComponentFamily.AppSetting,
            ComponentFamily.SiteMap,
            ComponentFamily.EnvironmentVariableDefinition,
            ComponentFamily.EnvironmentVariableValue);

        advancedUi.Artifacts.Should().Contain(artifact => artifact.Family == ComponentFamily.AppModule && artifact.LogicalName == "codex_metadata_advanced_ui_924e69cb");
        advancedUi.Artifacts.Should().Contain(artifact => artifact.Family == ComponentFamily.AppSetting && artifact.LogicalName == "codex_metadata_advanced_ui_924e69cb|AppChannel");
        advancedUi.Artifacts.Should().Contain(artifact => artifact.Family == ComponentFamily.SiteMap && artifact.LogicalName == "codex_metadata_advanced_ui_924e69cb");
        advancedUi.Artifacts.Should().Contain(artifact => artifact.Family == ComponentFamily.EnvironmentVariableDefinition && artifact.LogicalName == "cdxmeta_advanceduimode");
        advancedUi.Artifacts.Should().Contain(artifact => artifact.Family == ComponentFamily.EnvironmentVariableValue && artifact.LogicalName == "cdxmeta_advanceduimode");

        var environmentHarness = LiveFixtureHarness.Create("seed-environment");
        var environment = await environmentHarness.ReadAsync(ComponentFamily.CanvasApp);

        environment.Artifacts.Should().ContainSingle(artifact => artifact.Family == ComponentFamily.CanvasApp && artifact.LogicalName == "cat_overview_3dbf5");
        environment.Artifacts.Single(artifact => artifact.Family == ComponentFamily.CanvasApp).Properties![ArtifactPropertyKeys.AppVersion]
            .Should().Be("2023-09-06T20:22:07Z");
    }

    [Fact]
    public async Task ReadAsync_projects_quick_and_card_forms_for_seed_forms()
    {
        var harness = LiveFixtureHarness.Create("seed-forms");

        var snapshot = await harness.ReadAsync(ComponentFamily.Form);
        var source = ReadSourceFixture("seed-forms");
        var expectedForms = source.Artifacts
            .Where(artifact => artifact.Family == ComponentFamily.Form
                && artifact.Properties is not null
                && artifact.Properties.TryGetValue(ArtifactPropertyKeys.FormType, out var formType)
                && (string.Equals(formType, "quick", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(formType, "card", StringComparison.OrdinalIgnoreCase)))
            .OrderBy(artifact => artifact.LogicalName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var actualForms = snapshot.Artifacts
            .Where(artifact => artifact.Family == ComponentFamily.Form
                && artifact.Properties is not null
                && artifact.Properties.TryGetValue(ArtifactPropertyKeys.FormType, out var formType)
                && (string.Equals(formType, "quick", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(formType, "card", StringComparison.OrdinalIgnoreCase)))
            .OrderBy(artifact => artifact.LogicalName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        actualForms.Select(artifact => artifact.LogicalName).Should().Equal(expectedForms.Select(artifact => artifact.LogicalName));
        actualForms.Should().OnlyContain(artifact =>
            string.Equals(artifact.Properties![ArtifactPropertyKeys.FormType], "quick", StringComparison.OrdinalIgnoreCase)
            || string.Equals(artifact.Properties![ArtifactPropertyKeys.FormType], "card", StringComparison.OrdinalIgnoreCase));
        harness.Requests.Should().Contain(request => request.Contains("/systemforms", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ReadAsync_projects_saved_query_visualization_family_for_seed_advanced_ui()
    {
        var harness = LiveFixtureHarness.Create("seed-advanced-ui");

        var snapshot = await harness.ReadAsync(ComponentFamily.Visualization);
        var source = ReadSourceFixture("seed-advanced-ui");
        var expectedVisualization = source.Artifacts.Single(artifact => artifact.Family == ComponentFamily.Visualization);
        var actualVisualization = snapshot.Artifacts.Single(artifact => artifact.Family == ComponentFamily.Visualization);

        actualVisualization.DisplayName.Should().Be(expectedVisualization.DisplayName);
        actualVisualization.Properties![ArtifactPropertyKeys.VisualizationId].Should().Be(expectedVisualization.Properties![ArtifactPropertyKeys.VisualizationId]);
        actualVisualization.Properties![ArtifactPropertyKeys.TargetEntity].Should().Be(expectedVisualization.Properties![ArtifactPropertyKeys.TargetEntity]);
        actualVisualization.Properties![ArtifactPropertyKeys.DataDescriptionXml].Should().Be(expectedVisualization.Properties![ArtifactPropertyKeys.DataDescriptionXml]);
        actualVisualization.Properties![ArtifactPropertyKeys.PresentationDescriptionXml].Should().Be(expectedVisualization.Properties![ArtifactPropertyKeys.PresentationDescriptionXml]);
        actualVisualization.Properties![ArtifactPropertyKeys.ComparisonSignature].Should().Be(expectedVisualization.Properties![ArtifactPropertyKeys.ComparisonSignature]);
        harness.Requests.Should().Contain(request => request.Contains("/solutioncomponents", StringComparison.OrdinalIgnoreCase));
        harness.Requests.Should().Contain(request => request.Contains("/savedqueryvisualizations", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("seed-app-shell")]
    [InlineData("seed-advanced-ui")]
    public async Task ReadAsync_projects_web_resource_family_for_app_shell_seeds(string fixtureName)
    {
        var harness = LiveFixtureHarness.Create(fixtureName);

        var snapshot = await harness.ReadAsync(ComponentFamily.WebResource);
        var source = ReadSourceFixture(fixtureName);
        var expectedWebResources = source.Artifacts
            .Where(artifact => artifact.Family == ComponentFamily.WebResource)
            .OrderBy(artifact => artifact.LogicalName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var actualWebResources = snapshot.Artifacts
            .Where(artifact => artifact.Family == ComponentFamily.WebResource)
            .OrderBy(artifact => artifact.LogicalName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        actualWebResources.Should().HaveCount(expectedWebResources.Length);
        foreach (var expected in expectedWebResources)
        {
            var actual = actualWebResources.Single(artifact =>
                string.Equals(artifact.LogicalName, expected.LogicalName, StringComparison.OrdinalIgnoreCase));
            actual.DisplayName.Should().Be(expected.DisplayName);
            actual.Properties![ArtifactPropertyKeys.WebResourceType].Should().Be(expected.Properties![ArtifactPropertyKeys.WebResourceType]);
            actual.Properties![ArtifactPropertyKeys.ByteLength].Should().Be(expected.Properties![ArtifactPropertyKeys.ByteLength]);
            actual.Properties![ArtifactPropertyKeys.ContentHash].Should().Be(expected.Properties![ArtifactPropertyKeys.ContentHash]);
        }

        harness.Requests.Should().Contain(request => request.Contains("/solutioncomponents", StringComparison.OrdinalIgnoreCase));
        harness.Requests.Should().Contain(request => request.Contains("/webresourceset", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("seed-app-shell")]
    [InlineData("seed-advanced-ui")]
    public async Task ReadAsync_projects_site_map_definition_detail_for_app_shell_seeds(string fixtureName)
    {
        var harness = LiveFixtureHarness.Create(fixtureName);

        var snapshot = await harness.ReadAsync(ComponentFamily.SiteMap);
        var source = ReadSourceFixture(fixtureName);
        var expectedSiteMap = source.Artifacts.Single(artifact => artifact.Family == ComponentFamily.SiteMap);
        var actualSiteMap = snapshot.Artifacts.Single(artifact => artifact.Family == ComponentFamily.SiteMap);

        actualSiteMap.DisplayName.Should().Be(expectedSiteMap.DisplayName);
        actualSiteMap.Properties![ArtifactPropertyKeys.AreaCount].Should().Be(expectedSiteMap.Properties![ArtifactPropertyKeys.AreaCount]);
        actualSiteMap.Properties![ArtifactPropertyKeys.SiteMapDefinitionJson].Should().Be(expectedSiteMap.Properties![ArtifactPropertyKeys.SiteMapDefinitionJson]);
        actualSiteMap.Properties![ArtifactPropertyKeys.ComparisonSignature].Should().Be(expectedSiteMap.Properties![ArtifactPropertyKeys.ComparisonSignature]);
        switch (fixtureName)
        {
            case "seed-app-shell":
                actualSiteMap.Properties![ArtifactPropertyKeys.SiteMapDefinitionJson].Should().Contain("\"client\":\"Web\"");
                actualSiteMap.Properties![ArtifactPropertyKeys.SiteMapDefinitionJson].Should().Contain("\"vectorIcon\":\"/WebResources/cdxmeta_/shell/icon.svg\"");
                actualSiteMap.Properties![ArtifactPropertyKeys.SiteMapDefinitionJson].Should().Contain("\"passParams\":true");
                break;
            case "seed-advanced-ui":
                actualSiteMap.Properties![ArtifactPropertyKeys.SiteMapDefinitionJson].Should().Contain("\"icon\":\"/WebResources/cdxmeta_/advancedui/icon.svg\"");
                actualSiteMap.Properties![ArtifactPropertyKeys.SiteMapDefinitionJson].Should().Contain("\"passParams\":false");
                actualSiteMap.Properties![ArtifactPropertyKeys.SiteMapDefinitionJson].Should().Contain("\"availableOffline\":false");
                break;
        }
        harness.Requests.Should().Contain(request => request.Contains("/solutioncomponents", StringComparison.OrdinalIgnoreCase));
        harness.Requests.Should().Contain(request => request.Contains("/sitemaps", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ReadAsync_projects_supported_site_map_dashboard_targets_with_app_scope()
    {
        const string dashboardId = "3c5d4df8-4c0d-4d57-9e8f-6d4b3a8d5812";
        const string appId = "e1d1df92-5e88-4cff-8562-3d0f3f7164d0";
        var requests = new List<string>();
        static HttpResponseMessage CreateJsonResponse(JsonNode body) =>
            new(HttpStatusCode.OK)
            {
                Content = new StringContent(body.ToJsonString())
                {
                    Headers =
                    {
                        ContentType = new MediaTypeHeaderValue("application/json")
                    }
                }
            };

        using var client = new HttpClient(new StaticResponseHandler(request =>
        {
            var relative = request.RequestUri?.PathAndQuery.TrimStart('/') ?? string.Empty;
            requests.Add(relative);

            if (relative.Contains("solutions?$select=", StringComparison.OrdinalIgnoreCase))
            {
                return CreateJsonResponse(new JsonObject
                {
                    ["value"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["solutionid"] = "49916849-57f1-ee11-9048-000d3ab5d944",
                            ["friendlyname"] = "Codex Metadata App Shell",
                            ["uniquename"] = "CodexMetadataSeedAppShell",
                            ["version"] = "1.0.0.0",
                            ["ismanaged"] = false
                        }
                    }
                });
            }

            if (relative.Contains("solutioncomponents", StringComparison.OrdinalIgnoreCase))
            {
                return CreateJsonResponse(new JsonObject
                {
                    ["value"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["componenttype"] = 62,
                            ["objectid"] = "72b8c2f0-f2ab-4cd9-b59e-26d5139c4f24"
                        }
                    }
                });
            }

            if (relative.Contains("sitemaps", StringComparison.OrdinalIgnoreCase))
            {
                return CreateJsonResponse(new JsonObject
                {
                    ["value"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["sitemapid"] = "72b8c2f0-f2ab-4cd9-b59e-26d5139c4f24",
                            ["sitemapname"] = "Codex Metadata Shell",
                            ["sitemapnameunique"] = "codex_metadata_shell_dd96cf20",
                            ["sitemapxml"] = $"<SiteMap><Area Id=\"area_codex_metadata_shell\" Title=\"Codex Metadata\"><Group Id=\"group_codex_metadata_shell\" Title=\"Shell\"><SubArea Id=\"subarea_codex_metadata_shell\" Title=\"Metadata Dashboard\" Url=\"/main.aspx?appid={appId}&amp;pagetype=dashboard&amp;id={dashboardId}\" Client=\"Web\" PassParams=\"true\" Icon=\"/WebResources/cdxmeta_/shell/icon.svg\" VectorIcon=\"/WebResources/cdxmeta_/shell/icon.svg\" /></Group></Area></SiteMap>"
                        }
                    }
                });
            }

            return CreateJsonResponse(new JsonObject
            {
                ["value"] = new JsonArray()
            });
        }))
        {
            BaseAddress = new Uri("https://example.crm.dynamics.com/")
        };

        var reader = new DataverseWebApiLiveReader(client, new FakeTokenCredential());
        var request = new ReadbackRequest(
            new EnvironmentProfile("dev", new Uri("https://example.crm.dynamics.com")),
            "CodexMetadataSeedAppShell",
            [ComponentFamily.SiteMap]);

        var snapshot = await reader.ReadAsync(request, CancellationToken.None);

        var siteMap = snapshot.Artifacts.Single(artifact =>
            artifact.Family == ComponentFamily.SiteMap
            && artifact.LogicalName == "codex_metadata_shell_dd96cf20");
        siteMap.Properties![ArtifactPropertyKeys.SiteMapDefinitionJson].Should().Contain($"\"dashboard\":\"{dashboardId}\"");
        siteMap.Properties![ArtifactPropertyKeys.SiteMapDefinitionJson].Should().Contain($"\"appId\":\"{appId}\"");
        siteMap.Properties![ArtifactPropertyKeys.SiteMapDefinitionJson].Should().Contain("\"client\":\"Web\"");
        siteMap.Properties![ArtifactPropertyKeys.SiteMapDefinitionJson].Should().NotContain("/main.aspx?appid=");
        requests.Should().Contain(requestPath => requestPath.Contains("solutioncomponents", StringComparison.OrdinalIgnoreCase));
        requests.Should().Contain(requestPath => requestPath.Contains("sitemaps", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ReadAsync_projects_supported_site_map_custom_page_targets_with_record_context()
    {
        const string customPage = "cdxmeta_shellhome";
        const string appId = "e1d1df92-5e88-4cff-8562-3d0f3f7164d0";
        const string contextRecordId = "bd7616fe-3f95-4d6a-b4cb-9e788425f721";
        var requests = new List<string>();
        static HttpResponseMessage CreateJsonResponse(JsonNode body) =>
            new(HttpStatusCode.OK)
            {
                Content = new StringContent(body.ToJsonString())
                {
                    Headers =
                    {
                        ContentType = new MediaTypeHeaderValue("application/json")
                    }
                }
            };

        using var client = new HttpClient(new StaticResponseHandler(request =>
        {
            var relative = request.RequestUri?.PathAndQuery.TrimStart('/') ?? string.Empty;
            requests.Add(relative);

            if (relative.Contains("solutions?$select=", StringComparison.OrdinalIgnoreCase))
            {
                return CreateJsonResponse(new JsonObject
                {
                    ["value"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["solutionid"] = "49916849-57f1-ee11-9048-000d3ab5d944",
                            ["friendlyname"] = "Codex Metadata App Shell",
                            ["uniquename"] = "CodexMetadataSeedAppShell",
                            ["version"] = "1.0.0.0",
                            ["ismanaged"] = false
                        }
                    }
                });
            }

            if (relative.Contains("solutioncomponents", StringComparison.OrdinalIgnoreCase))
            {
                return CreateJsonResponse(new JsonObject
                {
                    ["value"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["componenttype"] = 62,
                            ["objectid"] = "72b8c2f0-f2ab-4cd9-b59e-26d5139c4f24"
                        }
                    }
                });
            }

            if (relative.Contains("sitemaps", StringComparison.OrdinalIgnoreCase))
            {
                return CreateJsonResponse(new JsonObject
                {
                    ["value"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["sitemapid"] = "72b8c2f0-f2ab-4cd9-b59e-26d5139c4f24",
                            ["sitemapname"] = "Codex Metadata Shell",
                            ["sitemapnameunique"] = "codex_metadata_shell_dd96cf20",
                            ["sitemapxml"] = $"<SiteMap><Area Id=\"area_codex_metadata_shell\" Title=\"Codex Metadata\"><Group Id=\"group_codex_metadata_shell\" Title=\"Shell\"><SubArea Id=\"subarea_codex_metadata_shell\" Title=\"Metadata Home\" Url=\"/main.aspx?appid={appId}&amp;pagetype=custom&amp;name={customPage}&amp;entityName=account&amp;recordId=%7B{contextRecordId}%7D\" Client=\"Web\" PassParams=\"true\" Icon=\"/WebResources/cdxmeta_/shell/icon.svg\" VectorIcon=\"/WebResources/cdxmeta_/shell/icon.svg\" /></Group></Area></SiteMap>"
                        }
                    }
                });
            }

            return CreateJsonResponse(new JsonObject
            {
                ["value"] = new JsonArray()
            });
        }))
        {
            BaseAddress = new Uri("https://example.crm.dynamics.com/")
        };

        var reader = new DataverseWebApiLiveReader(client, new FakeTokenCredential());
        var request = new ReadbackRequest(
            new EnvironmentProfile("dev", new Uri("https://example.crm.dynamics.com")),
            "CodexMetadataSeedAppShell",
            [ComponentFamily.SiteMap]);

        var snapshot = await reader.ReadAsync(request, CancellationToken.None);

        var siteMap = snapshot.Artifacts.Single(artifact =>
            artifact.Family == ComponentFamily.SiteMap
            && artifact.LogicalName == "codex_metadata_shell_dd96cf20");
        siteMap.Properties![ArtifactPropertyKeys.SiteMapDefinitionJson].Should().Contain($"\"customPage\":\"{customPage}\"");
        siteMap.Properties![ArtifactPropertyKeys.SiteMapDefinitionJson].Should().Contain("\"customPageEntityName\":\"account\"");
        siteMap.Properties![ArtifactPropertyKeys.SiteMapDefinitionJson].Should().Contain($"\"customPageRecordId\":\"{contextRecordId}\"");
        siteMap.Properties![ArtifactPropertyKeys.SiteMapDefinitionJson].Should().Contain($"\"appId\":\"{appId}\"");
        siteMap.Properties![ArtifactPropertyKeys.SiteMapDefinitionJson].Should().Contain("\"client\":\"Web\"");
        siteMap.Properties![ArtifactPropertyKeys.SiteMapDefinitionJson].Should().NotContain("/main.aspx?appid=");
        requests.Should().Contain(requestPath => requestPath.Contains("solutioncomponents", StringComparison.OrdinalIgnoreCase));
        requests.Should().Contain(requestPath => requestPath.Contains("sitemaps", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ReadAsync_projects_supported_site_map_entity_list_targets()
    {
        const string appId = "e1d1df92-5e88-4cff-8562-3d0f3f7164d0";
        const string viewId = "0cc7bf59-5fb4-4f11-a3b2-9170a9d6ef42";
        var requests = new List<string>();
        static HttpResponseMessage CreateJsonResponse(JsonNode body) =>
            new(HttpStatusCode.OK)
            {
                Content = new StringContent(body.ToJsonString())
                {
                    Headers =
                    {
                        ContentType = new MediaTypeHeaderValue("application/json")
                    }
                }
            };

        using var client = new HttpClient(new StaticResponseHandler(request =>
        {
            var relative = request.RequestUri?.PathAndQuery.TrimStart('/') ?? string.Empty;
            requests.Add(relative);

            if (relative.Contains("solutions?$select=", StringComparison.OrdinalIgnoreCase))
            {
                return CreateJsonResponse(new JsonObject
                {
                    ["value"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["solutionid"] = "49916849-57f1-ee11-9048-000d3ab5d944",
                            ["friendlyname"] = "Codex Metadata App Shell",
                            ["uniquename"] = "CodexMetadataSeedAppShell",
                            ["version"] = "1.0.0.0",
                            ["ismanaged"] = false
                        }
                    }
                });
            }

            if (relative.Contains("solutioncomponents", StringComparison.OrdinalIgnoreCase))
            {
                return CreateJsonResponse(new JsonObject
                {
                    ["value"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["componenttype"] = 62,
                            ["objectid"] = "72b8c2f0-f2ab-4cd9-b59e-26d5139c4f24"
                        }
                    }
                });
            }

            if (relative.Contains("sitemaps", StringComparison.OrdinalIgnoreCase))
            {
                return CreateJsonResponse(new JsonObject
                {
                    ["value"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["sitemapid"] = "72b8c2f0-f2ab-4cd9-b59e-26d5139c4f24",
                            ["sitemapname"] = "Codex Metadata Shell",
                            ["sitemapnameunique"] = "codex_metadata_shell_dd96cf20",
                            ["sitemapxml"] = $"<SiteMap><Area Id=\"area_codex_metadata_shell\" Title=\"Codex Metadata\"><Group Id=\"group_codex_metadata_shell\" Title=\"Shell\"><SubArea Id=\"subarea_codex_metadata_shell\" Title=\"Metadata Accounts\" Url=\"/main.aspx?appid={appId}&amp;pagetype=entitylist&amp;etn=account&amp;viewid=%7B{viewId}%7D&amp;viewtype=1039\" Client=\"Web\" PassParams=\"true\" Icon=\"/WebResources/cdxmeta_/shell/icon.svg\" VectorIcon=\"/WebResources/cdxmeta_/shell/icon.svg\" /></Group></Area></SiteMap>"
                        }
                    }
                });
            }

            return CreateJsonResponse(new JsonObject
            {
                ["value"] = new JsonArray()
            });
        }))
        {
            BaseAddress = new Uri("https://example.crm.dynamics.com/")
        };

        var reader = new DataverseWebApiLiveReader(client, new FakeTokenCredential());
        var request = new ReadbackRequest(
            new EnvironmentProfile("dev", new Uri("https://example.crm.dynamics.com")),
            "CodexMetadataSeedAppShell",
            [ComponentFamily.SiteMap]);

        var snapshot = await reader.ReadAsync(request, CancellationToken.None);

        var siteMap = snapshot.Artifacts.Single(artifact =>
            artifact.Family == ComponentFamily.SiteMap
            && artifact.LogicalName == "codex_metadata_shell_dd96cf20");
        siteMap.Properties![ArtifactPropertyKeys.SiteMapDefinitionJson].Should().Contain("\"entity\":\"account\"");
        siteMap.Properties![ArtifactPropertyKeys.SiteMapDefinitionJson].Should().Contain($"\"viewId\":\"{viewId}\"");
        siteMap.Properties![ArtifactPropertyKeys.SiteMapDefinitionJson].Should().Contain("\"viewType\":\"savedquery\"");
        siteMap.Properties![ArtifactPropertyKeys.SiteMapDefinitionJson].Should().Contain($"\"appId\":\"{appId}\"");
        siteMap.Properties![ArtifactPropertyKeys.SiteMapDefinitionJson].Should().NotContain("/main.aspx?appid=");
        requests.Should().Contain(requestPath => requestPath.Contains("solutioncomponents", StringComparison.OrdinalIgnoreCase));
        requests.Should().Contain(requestPath => requestPath.Contains("sitemaps", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ReadAsync_projects_supported_site_map_entity_record_targets()
    {
        const string appId = "e1d1df92-5e88-4cff-8562-3d0f3f7164d0";
        const string recordId = "bd7616fe-3f95-4d6a-b4cb-9e788425f721";
        const string formId = "a77ba3f0-df52-46a1-a0a2-2c4fd6e25cdf";
        var requests = new List<string>();
        static HttpResponseMessage CreateJsonResponse(JsonNode body) =>
            new(HttpStatusCode.OK)
            {
                Content = new StringContent(body.ToJsonString())
                {
                    Headers =
                    {
                        ContentType = new MediaTypeHeaderValue("application/json")
                    }
                }
            };

        using var client = new HttpClient(new StaticResponseHandler(request =>
        {
            var relative = request.RequestUri?.PathAndQuery.TrimStart('/') ?? string.Empty;
            requests.Add(relative);

            if (relative.Contains("solutions?$select=", StringComparison.OrdinalIgnoreCase))
            {
                return CreateJsonResponse(new JsonObject
                {
                    ["value"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["solutionid"] = "49916849-57f1-ee11-9048-000d3ab5d944",
                            ["friendlyname"] = "Codex Metadata App Shell",
                            ["uniquename"] = "CodexMetadataSeedAppShell",
                            ["version"] = "1.0.0.0",
                            ["ismanaged"] = false
                        }
                    }
                });
            }

            if (relative.Contains("solutioncomponents", StringComparison.OrdinalIgnoreCase))
            {
                return CreateJsonResponse(new JsonObject
                {
                    ["value"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["componenttype"] = 62,
                            ["objectid"] = "72b8c2f0-f2ab-4cd9-b59e-26d5139c4f24"
                        }
                    }
                });
            }

            if (relative.Contains("sitemaps", StringComparison.OrdinalIgnoreCase))
            {
                return CreateJsonResponse(new JsonObject
                {
                    ["value"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["sitemapid"] = "72b8c2f0-f2ab-4cd9-b59e-26d5139c4f24",
                            ["sitemapname"] = "Codex Metadata Shell",
                            ["sitemapnameunique"] = "codex_metadata_shell_dd96cf20",
                            ["sitemapxml"] = $"<SiteMap><Area Id=\"area_codex_metadata_shell\" Title=\"Codex Metadata\"><Group Id=\"group_codex_metadata_shell\" Title=\"Shell\"><SubArea Id=\"subarea_codex_metadata_shell\" Title=\"Metadata Record\" Url=\"/main.aspx?appid={appId}&amp;pagetype=entityrecord&amp;etn=account&amp;id=%7B{recordId}%7D&amp;extraqs=formid%3D%7B{formId}%7D\" Client=\"Web\" PassParams=\"true\" Icon=\"/WebResources/cdxmeta_/shell/icon.svg\" VectorIcon=\"/WebResources/cdxmeta_/shell/icon.svg\" /></Group></Area></SiteMap>"
                        }
                    }
                });
            }

            return CreateJsonResponse(new JsonObject
            {
                ["value"] = new JsonArray()
            });
        }))
        {
            BaseAddress = new Uri("https://example.crm.dynamics.com/")
        };

        var reader = new DataverseWebApiLiveReader(client, new FakeTokenCredential());
        var request = new ReadbackRequest(
            new EnvironmentProfile("dev", new Uri("https://example.crm.dynamics.com")),
            "CodexMetadataSeedAppShell",
            [ComponentFamily.SiteMap]);

        var snapshot = await reader.ReadAsync(request, CancellationToken.None);

        var siteMap = snapshot.Artifacts.Single(artifact =>
            artifact.Family == ComponentFamily.SiteMap
            && artifact.LogicalName == "codex_metadata_shell_dd96cf20");
        siteMap.Properties![ArtifactPropertyKeys.SiteMapDefinitionJson].Should().Contain("\"entity\":\"account\"");
        siteMap.Properties![ArtifactPropertyKeys.SiteMapDefinitionJson].Should().Contain($"\"recordId\":\"{recordId}\"");
        siteMap.Properties![ArtifactPropertyKeys.SiteMapDefinitionJson].Should().Contain($"\"formId\":\"{formId}\"");
        siteMap.Properties![ArtifactPropertyKeys.SiteMapDefinitionJson].Should().Contain($"\"appId\":\"{appId}\"");
        siteMap.Properties![ArtifactPropertyKeys.SiteMapDefinitionJson].Should().NotContain("/main.aspx?appid=");
        requests.Should().Contain(requestPath => requestPath.Contains("solutioncomponents", StringComparison.OrdinalIgnoreCase));
        requests.Should().Contain(requestPath => requestPath.Contains("sitemaps", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ReadAsync_canonicalizes_unsupported_site_map_urls_and_keeps_overlap_clean()
    {
        const string appId = "e1d1df92-5e88-4cff-8562-3d0f3f7164d0";
        const string dashboardId = "3c5d4df8-4c0d-4d57-9e8f-6d4b3a8d5812";
        const string contextRecordId = "bd7616fe-3f95-4d6a-b4cb-9e788425f721";
        var expectedRawUrl = $"/main.aspx?appid={appId}&extraqs=entityName%3Daccount%26recordId%3D{contextRecordId}&id={dashboardId}&pagetype=dashboard&showWelcome=true";
        var sourceRoot = Path.Combine(Path.GetTempPath(), $"dsc-live-site-map-raw-url-{Guid.NewGuid():N}");
        CanonicalSolution source;

        try
        {
            CopyDirectory(
                Path.Combine("C:\\Git\\Dataverse-Solution-KB", "fixtures", "skill-corpus", "examples", "seed-app-shell", "unpacked"),
                sourceRoot);

            var sourceSiteMapPath = Path.Combine(sourceRoot, "AppModuleSiteMaps", "codex_metadata_shell_dd96cf20", "AppModuleSiteMap.xml");
            var sourceXml = File.ReadAllText(sourceSiteMapPath)
                .Replace(
                    "Url=\"$webresource:cdxmeta_/shell/landing.html\"",
                    $"Url=\"/main.aspx?id=%7B{dashboardId}%7D&amp;appid=%7B{appId}%7D&amp;extraqs=entityName%3DAccount%26recordId%3D%7B{contextRecordId}%7D&amp;showWelcome=1&amp;pagetype=dashboard\"",
                    StringComparison.Ordinal);
            File.WriteAllText(sourceSiteMapPath, sourceXml);

            source = new XmlSolutionReader().Read(new ReadRequest(sourceRoot)) with
            {
                Artifacts = new XmlSolutionReader().Read(new ReadRequest(sourceRoot)).Artifacts
                    .Where(artifact => artifact.Family == ComponentFamily.SiteMap)
                    .ToArray()
            };

            var requests = new List<string>();
            static HttpResponseMessage CreateJsonResponse(JsonNode body) =>
                new(HttpStatusCode.OK)
                {
                    Content = new StringContent(body.ToJsonString())
                    {
                        Headers =
                        {
                            ContentType = new MediaTypeHeaderValue("application/json")
                        }
                    }
                };

            using var client = new HttpClient(new StaticResponseHandler(request =>
            {
                var relative = request.RequestUri?.PathAndQuery.TrimStart('/') ?? string.Empty;
                requests.Add(relative);

                if (relative.Contains("solutions?$select=", StringComparison.OrdinalIgnoreCase))
                {
                    return CreateJsonResponse(new JsonObject
                    {
                        ["value"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["solutionid"] = "49916849-57f1-ee11-9048-000d3ab5d944",
                                ["friendlyname"] = "Codex Metadata App Shell",
                                ["uniquename"] = "CodexMetadataSeedAppShell",
                                ["version"] = "1.0.0.0",
                                ["ismanaged"] = false
                            }
                        }
                    });
                }

                if (relative.Contains("solutioncomponents", StringComparison.OrdinalIgnoreCase))
                {
                    return CreateJsonResponse(new JsonObject
                    {
                        ["value"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["componenttype"] = 62,
                                ["objectid"] = "72b8c2f0-f2ab-4cd9-b59e-26d5139c4f24"
                            }
                        }
                    });
                }

                if (relative.Contains("sitemaps", StringComparison.OrdinalIgnoreCase))
                {
                    return CreateJsonResponse(new JsonObject
                    {
                        ["value"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["sitemapid"] = "72b8c2f0-f2ab-4cd9-b59e-26d5139c4f24",
                                ["sitemapname"] = "Codex Metadata Shell",
                                ["sitemapnameunique"] = "codex_metadata_shell_dd96cf20",
                                ["sitemapxml"] = $"<SiteMap><Area Id=\"area_codex_metadata_shell\" Title=\"Codex Metadata\"><Group Id=\"group_codex_metadata_shell\" Title=\"Shell\"><SubArea Id=\"subarea_codex_metadata_shell\" Title=\"Metadata Shell\" Url=\"/main.aspx?pagetype=dashboard&amp;showWelcome=1&amp;extraqs=recordId%3D%7B{contextRecordId}%7D%26entityName%3DAccount&amp;appid=%7B{appId}%7D&amp;id={dashboardId}\" Client=\"Web\" PassParams=\"true\" Icon=\"/WebResources/cdxmeta_/shell/icon.svg\" VectorIcon=\"/WebResources/cdxmeta_/shell/icon.svg\" /></Group></Area></SiteMap>"
                            }
                        }
                    });
                }

                return CreateJsonResponse(new JsonObject
                {
                    ["value"] = new JsonArray()
                });
            }))
            {
                BaseAddress = new Uri("https://example.crm.dynamics.com/")
            };

            var reader = new DataverseWebApiLiveReader(client, new FakeTokenCredential());
            var request = new ReadbackRequest(
                new EnvironmentProfile("dev", new Uri("https://example.crm.dynamics.com")),
                "CodexMetadataSeedAppShell",
                [ComponentFamily.SiteMap]);

            var snapshot = await reader.ReadAsync(request, CancellationToken.None);

            var siteMap = snapshot.Artifacts.Single(artifact =>
                artifact.Family == ComponentFamily.SiteMap
                && artifact.LogicalName == "codex_metadata_shell_dd96cf20");
            var subArea = JsonNode.Parse(siteMap.Properties![ArtifactPropertyKeys.SiteMapDefinitionJson])!["areas"]![0]!["groups"]![0]!["subAreas"]![0]!;
            subArea["url"]!.GetValue<string>().Should().Be(expectedRawUrl);
            siteMap.Properties![ArtifactPropertyKeys.WebResourceSubAreaCount].Should().Be("0");
            subArea["dashboard"].Should().BeNull();

            var report = new StableOverlapDriftComparer().Compare(source, snapshot, new CompareRequest());
            report.HasBlockingDrift.Should().BeFalse();
            report.Findings.Should().BeEmpty();

            requests.Should().Contain(requestPath => requestPath.Contains("solutioncomponents", StringComparison.OrdinalIgnoreCase));
            requests.Should().Contain(requestPath => requestPath.Contains("sitemaps", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (Directory.Exists(sourceRoot))
            {
                Directory.Delete(sourceRoot, recursive: true);
            }
        }

        static void CopyDirectory(string sourceDirectory, string destinationDirectory)
        {
            Directory.CreateDirectory(destinationDirectory);

            foreach (var directory in Directory.GetDirectories(sourceDirectory))
            {
                CopyDirectory(directory, Path.Combine(destinationDirectory, Path.GetFileName(directory)));
            }

            foreach (var file in Directory.GetFiles(sourceDirectory))
            {
                File.Copy(file, Path.Combine(destinationDirectory, Path.GetFileName(file)), overwrite: true);
            }
        }
    }

    [Fact]
    public async Task ReadAsync_projects_standalone_custom_control_family_for_seed_advanced_ui()
    {
        var harness = LiveFixtureHarness.Create("seed-advanced-ui");

        var snapshot = await harness.ReadAsync(ComponentFamily.CustomControl);

        snapshot.Artifacts.Should().ContainSingle(artifact =>
            artifact.Family == ComponentFamily.CustomControl
            && artifact.LogicalName == "cat_powercat.customizabletextfield");
        var artifact = snapshot.Artifacts.Single(item => item.Family == ComponentFamily.CustomControl);
        artifact.Properties![ArtifactPropertyKeys.Version].Should().Be("0.0.2");
        artifact.Properties![ArtifactPropertyKeys.Namespace].Should().Be("PowerCAT");
        artifact.Properties![ArtifactPropertyKeys.ConstructorName].Should().Be("CustomizableTextField");
        artifact.Properties![ArtifactPropertyKeys.ControlType].Should().Be("virtual");
        artifact.Properties![ArtifactPropertyKeys.ApiVersion].Should().Be("1.3.5");
        artifact.Properties![ArtifactPropertyKeys.PropertyCount].Should().Be("7");
        artifact.Properties![ArtifactPropertyKeys.FeatureCount].Should().Be("1");
        artifact.Properties![ArtifactPropertyKeys.ResourceCount].Should().Be("3");
        artifact.Properties![ArtifactPropertyKeys.SupportedPlatformsJson].Should().Be("[\"Canvas\",\"CustomPage\",\"Model\"]");
        artifact.Properties![ArtifactPropertyKeys.PropertyNamesJson].Should().Contain("Value");
        artifact.Properties![ArtifactPropertyKeys.ResourcePathsJson].Should().Contain("bundle.js");
        artifact.Properties![ArtifactPropertyKeys.PlatformLibrariesJson].Should().Contain("React:16.8.6");
        harness.Requests.Should().Contain(request => request.Contains("/solutioncomponents", StringComparison.OrdinalIgnoreCase));
        harness.Requests.Should().Contain(request => request.Contains("/customcontrols", StringComparison.OrdinalIgnoreCase));
        snapshot.Diagnostics.Should().Contain(diagnostic => diagnostic.Code == "live-readback-customcontrol-source-asymmetry");
    }

    [Fact]
    public async Task ReadAsync_projects_entity_analytics_configuration_family()
    {
        var harness = LiveFixtureHarness.Create("seed-entity-analytics");

        var snapshot = await harness.ReadAsync(ComponentFamily.EntityAnalyticsConfiguration);

        snapshot.Artifacts.Should().ContainSingle(artifact =>
            artifact.Family == ComponentFamily.EntityAnalyticsConfiguration
            && artifact.LogicalName == "contact");
        var artifact = snapshot.Artifacts.Single(item => item.Family == ComponentFamily.EntityAnalyticsConfiguration);
        artifact.Properties![ArtifactPropertyKeys.EntityDataSource].Should().Be("dataverse");
        artifact.Properties![ArtifactPropertyKeys.IsEnabledForAdls].Should().Be("true");
        artifact.Properties![ArtifactPropertyKeys.IsEnabledForTimeSeries].Should().Be("false");
        harness.Requests.Should().Contain(request => request.Contains("/entityanalyticsconfigs", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ReadAsync_projects_ai_families()
    {
        var harness = LiveFixtureHarness.Create("seed-ai-families");

        var snapshot = await harness.ReadAsync(
            ComponentFamily.AiProjectType,
            ComponentFamily.AiProject,
            ComponentFamily.AiConfiguration);

        snapshot.Artifacts.Should().ContainSingle(artifact => artifact.Family == ComponentFamily.AiProjectType && artifact.LogicalName == "document_automation");
        snapshot.Artifacts.Should().ContainSingle(artifact => artifact.Family == ComponentFamily.AiProject && artifact.LogicalName == "invoice_processing");
        snapshot.Artifacts.Should().ContainSingle(artifact => artifact.Family == ComponentFamily.AiConfiguration && artifact.LogicalName == "invoice_processing_training");
        harness.Requests.Should().Contain(request => request.Contains("/msdyn_aitemplates", StringComparison.OrdinalIgnoreCase));
        harness.Requests.Should().Contain(request => request.Contains("/msdyn_aimodels", StringComparison.OrdinalIgnoreCase));
        harness.Requests.Should().Contain(request => request.Contains("/msdyn_aiconfigurations", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ReadAsync_projects_plugin_registration_families()
    {
        var harness = LiveFixtureHarness.Create("seed-plugin-registration");

        var snapshot = await harness.ReadAsync(
            ComponentFamily.PluginAssembly,
            ComponentFamily.PluginType,
            ComponentFamily.PluginStep,
            ComponentFamily.PluginStepImage);

        snapshot.Artifacts.Should().ContainSingle(artifact =>
            artifact.Family == ComponentFamily.PluginAssembly
            && artifact.LogicalName == "Codex.Metadata.Plugins, Version=1.0.0.0, Culture=neutral, PublicKeyToken=9d006cbbfeff5098");
        snapshot.Artifacts.Should().ContainSingle(artifact =>
            artifact.Family == ComponentFamily.PluginType
            && artifact.LogicalName == "Codex.Metadata.Plugins.AccountUpdateTrace");
        snapshot.Artifacts.Should().ContainSingle(artifact =>
            artifact.Family == ComponentFamily.PluginStep
            && artifact.LogicalName == "Codex.Metadata.Plugins.AccountUpdateTrace|Update|account|20|0|Account Update Trace Step");
        snapshot.Artifacts.Should().ContainSingle(artifact =>
            artifact.Family == ComponentFamily.PluginStepImage
            && artifact.LogicalName == "Codex.Metadata.Plugins.AccountUpdateTrace|Update|account|20|0|Account Update Trace Step|Account PreImage|preimage|0");
        harness.Requests.Should().Contain(request => request.Contains("/pluginassemblies", StringComparison.OrdinalIgnoreCase));
        harness.Requests.Should().Contain(request => request.Contains("/plugintypes", StringComparison.OrdinalIgnoreCase));
        harness.Requests.Should().Contain(request => request.Contains("/sdkmessageprocessingsteps", StringComparison.OrdinalIgnoreCase));
        harness.Requests.Should().Contain(request => request.Contains("/sdkmessageprocessingstepimages", StringComparison.OrdinalIgnoreCase));
        harness.Requests.Should().Contain(request => request.Contains("/sdkmessages", StringComparison.OrdinalIgnoreCase));
        harness.Requests.Should().Contain(request => request.Contains("/sdkmessagefilters", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("seed-code-plugin-classic")]
    [InlineData("seed-code-plugin-package")]
    [InlineData("seed-code-plugin-imperative")]
    [InlineData("seed-code-plugin-helper")]
    [InlineData("seed-code-plugin-imperative-service")]
    public async Task ReadAsync_projects_code_first_plugin_registration_families(string fixtureName)
    {
        var harness = LiveFixtureHarness.Create(fixtureName);

        var snapshot = await harness.ReadAsync(
            ComponentFamily.PluginAssembly,
            ComponentFamily.PluginType,
            ComponentFamily.PluginStep,
            ComponentFamily.PluginStepImage);
        var source = ReadSourceFixture(fixtureName);

        var expected = source.Artifacts
            .Where(artifact => artifact.Family is ComponentFamily.PluginAssembly or ComponentFamily.PluginType or ComponentFamily.PluginStep or ComponentFamily.PluginStepImage)
            .OrderBy(artifact => artifact.Family)
            .ThenBy(artifact => artifact.LogicalName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var actual = snapshot.Artifacts
            .Where(artifact => artifact.Family is ComponentFamily.PluginAssembly or ComponentFamily.PluginType or ComponentFamily.PluginStep or ComponentFamily.PluginStepImage)
            .OrderBy(artifact => artifact.Family)
            .ThenBy(artifact => artifact.LogicalName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        actual.Select(artifact => (artifact.Family, artifact.LogicalName))
            .Should()
            .Equal(expected.Select(artifact => (artifact.Family, artifact.LogicalName)));
        harness.Requests.Should().Contain(request => request.Contains("/pluginassemblies", StringComparison.OrdinalIgnoreCase));
        harness.Requests.Should().Contain(request => request.Contains("/plugintypes", StringComparison.OrdinalIgnoreCase));
        harness.Requests.Should().Contain(request => request.Contains("/sdkmessageprocessingsteps", StringComparison.OrdinalIgnoreCase));
        harness.Requests.Should().Contain(request => request.Contains("/sdkmessageprocessingstepimages", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ReadAsync_projects_helper_based_custom_workflow_activity_and_plugin_type_side_by_side()
    {
        var harness = LiveFixtureHarness.Create("seed-code-plugin-helper");

        var snapshot = await harness.ReadAsync(
            ComponentFamily.PluginAssembly,
            ComponentFamily.PluginType,
            ComponentFamily.PluginStep,
            ComponentFamily.PluginStepImage);

        snapshot.Artifacts.Count(artifact => artifact.Family == ComponentFamily.PluginType).Should().Be(2);
        snapshot.Artifacts.Should().Contain(artifact =>
            artifact.Family == ComponentFamily.PluginType
            && artifact.LogicalName == "Codex.Metadata.CodeFirst.Helper.AccountDescriptionActivity"
            && artifact.Properties![ArtifactPropertyKeys.WorkflowActivityGroupName] == "Codex.Metadata.CodeFirst.Helper (1.0.0.0)");
        snapshot.Artifacts.Should().Contain(artifact =>
            artifact.Family == ComponentFamily.PluginType
            && artifact.LogicalName == "Codex.Metadata.CodeFirst.Helper.AccountCreateDescriptionPlugin");
        snapshot.Artifacts.Should().ContainSingle(artifact =>
            artifact.Family == ComponentFamily.PluginStep
            && artifact.LogicalName == "Codex.Metadata.CodeFirst.Helper.AccountCreateDescriptionPlugin|Create|account|20|0|Account Create Description Helper Stamp");
    }

    [Fact]
    public async Task ReadAsync_projects_custom_workflow_activity_plugin_families()
    {
        var harness = LiveFixtureHarness.Create("seed-code-workflow-activity-classic");

        var snapshot = await harness.ReadAsync(
            ComponentFamily.PluginAssembly,
            ComponentFamily.PluginType);

        snapshot.Artifacts.Should().ContainSingle(artifact => artifact.Family == ComponentFamily.PluginAssembly);
        var pluginType = snapshot.Artifacts.Single(artifact => artifact.Family == ComponentFamily.PluginType);
        pluginType.LogicalName.Should().Be("Codex.Metadata.CodeFirst.WorkflowActivity.Classic.AccountDescriptionActivity");
        pluginType.Properties![ArtifactPropertyKeys.WorkflowActivityGroupName].Should().Be("Codex.Metadata.CodeFirst.WorkflowActivity.Classic (1.0.0.0)");
        snapshot.Artifacts.Should().NotContain(artifact => artifact.Family == ComponentFamily.PluginStep);
        harness.Requests.Should().Contain(request => request.Contains("/pluginassemblies", StringComparison.OrdinalIgnoreCase));
        harness.Requests.Should().Contain(request => request.Contains("/plugintypes", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("seed-workflow-classic", "workflow", "Create")]
    [InlineData("seed-workflow-action", "customAction", "cdxmeta_AccountStampAction")]
    public async Task ReadAsync_projects_workflow_family_for_supported_workflow_seeds(
        string fixtureName,
        string expectedWorkflowKind,
        string expectedTriggerMessage)
    {
        var harness = LiveFixtureHarness.Create(fixtureName);

        var snapshot = await harness.ReadAsync(ComponentFamily.Workflow);
        var source = ReadSourceFixture(fixtureName);
        var expected = source.Artifacts.Single(artifact => artifact.Family == ComponentFamily.Workflow);
        var actual = snapshot.Artifacts.Single(artifact => artifact.Family == ComponentFamily.Workflow);

        actual.LogicalName.Should().Be(expected.LogicalName);
        actual.DisplayName.Should().Be(expected.DisplayName);
        actual.Properties![ArtifactPropertyKeys.WorkflowId].Should().Be(expected.Properties![ArtifactPropertyKeys.WorkflowId]);
        actual.Properties![ArtifactPropertyKeys.WorkflowKind].Should().Be(expectedWorkflowKind);
        actual.Properties![ArtifactPropertyKeys.TriggerMessageName].Should().Be(expectedTriggerMessage);
        actual.Properties![ArtifactPropertyKeys.XamlHash].Should().Be(expected.Properties![ArtifactPropertyKeys.XamlHash]);
        (actual.Properties!.TryGetValue(ArtifactPropertyKeys.ClientDataHash, out var actualClientDataHash) ? actualClientDataHash : null)
            .Should().Be(expected.Properties!.GetValueOrDefault(ArtifactPropertyKeys.ClientDataHash));
        var expectedActionMetadata = expected.Properties!.GetValueOrDefault(ArtifactPropertyKeys.WorkflowActionMetadataJson);
        var actualActionMetadata = actual.Properties!.TryGetValue(ArtifactPropertyKeys.WorkflowActionMetadataJson, out var actualWorkflowActionMetadata)
            ? actualWorkflowActionMetadata
            : null;
        if (string.IsNullOrWhiteSpace(expectedActionMetadata))
        {
            actualActionMetadata.Should().BeNull();
        }
        else
        {
            JsonNode.DeepEquals(JsonNode.Parse(actualActionMetadata!), JsonNode.Parse(expectedActionMetadata!)).Should().BeTrue();
        }
        harness.Requests.Should().Contain(request => request.Contains("/solutioncomponents", StringComparison.OrdinalIgnoreCase));
        harness.Requests.Should().Contain(request => request.Contains("/workflows", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ReadAsync_projects_service_endpoint_and_connector_families()
    {
        var harness = LiveFixtureHarness.Create("seed-service-endpoint-connector");

        var snapshot = await harness.ReadAsync(
            ComponentFamily.ServiceEndpoint,
            ComponentFamily.Connector);

        snapshot.Artifacts.Should().ContainSingle(artifact =>
            artifact.Family == ComponentFamily.ServiceEndpoint
            && artifact.LogicalName == "codex_webhook_endpoint");
        snapshot.Artifacts.Should().ContainSingle(artifact =>
            artifact.Family == ComponentFamily.Connector
            && artifact.LogicalName == "shared-offerings-connector");

        var serviceEndpoint = snapshot.Artifacts.Single(artifact => artifact.Family == ComponentFamily.ServiceEndpoint);
        serviceEndpoint.Properties![ArtifactPropertyKeys.NamespaceAddress].Should().Be("https://hooks.contoso.example");
        serviceEndpoint.Properties![ArtifactPropertyKeys.EndpointPath].Should().Be("/dataverse/codex");

        var connector = snapshot.Artifacts.Single(artifact => artifact.Family == ComponentFamily.Connector);
        connector.DisplayName.Should().Be("Codex Shared Connector");
        connector.Properties![ArtifactPropertyKeys.ConnectorInternalId].Should().Be("shared-offerings-connector");
        connector.Properties![ArtifactPropertyKeys.CapabilitiesJson].Should().Be("[\"actions\",\"cloud\"]");

        harness.Requests.Should().Contain(request => request.Contains("/serviceendpoints", StringComparison.OrdinalIgnoreCase));
        harness.Requests.Should().Contain(request => request.Contains("/connectors", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ReadAsync_projects_process_policy_families()
    {
        var harness = LiveFixtureHarness.Create("seed-process-policy");

        var snapshot = await harness.ReadAsync(
            ComponentFamily.DuplicateRule,
            ComponentFamily.DuplicateRuleCondition,
            ComponentFamily.RoutingRule,
            ComponentFamily.RoutingRuleItem,
            ComponentFamily.MobileOfflineProfile,
            ComponentFamily.MobileOfflineProfileItem);

        snapshot.Artifacts.Should().ContainSingle(artifact =>
            artifact.Family == ComponentFamily.DuplicateRule
            && artifact.LogicalName == "dre67df5ba444cf6a6b4092b00952064b3b91ddc3e81f6d3746c2169ae4ed2c367");
        snapshot.Artifacts.Should().ContainSingle(artifact =>
            artifact.Family == ComponentFamily.DuplicateRuleCondition
            && artifact.LogicalName == "dre67df5ba444cf6a6b4092b00952064b3b91ddc3e81f6d3746c2169ae4ed2c367|name|name|0");
        snapshot.Artifacts.Should().ContainSingle(artifact =>
            artifact.Family == ComponentFamily.RoutingRule
            && artifact.LogicalName == "codex metadata routing rule");
        snapshot.Artifacts.Should().ContainSingle(artifact =>
            artifact.Family == ComponentFamily.RoutingRuleItem
            && artifact.LogicalName == "codex metadata routing rule|route all");
        snapshot.Artifacts.Should().ContainSingle(artifact =>
            artifact.Family == ComponentFamily.MobileOfflineProfile
            && artifact.LogicalName == "codex metadata mobile offline profile");
        snapshot.Artifacts.Should().ContainSingle(artifact =>
            artifact.Family == ComponentFamily.MobileOfflineProfileItem
            && artifact.LogicalName == "codex metadata mobile offline profile|account");

        harness.Requests.Should().Contain(request => request.Contains("/duplicaterules", StringComparison.OrdinalIgnoreCase));
        harness.Requests.Should().Contain(request => request.Contains("/duplicateruleconditions", StringComparison.OrdinalIgnoreCase));
        harness.Requests.Should().Contain(request => request.Contains("/routingrules", StringComparison.OrdinalIgnoreCase));
        harness.Requests.Should().Contain(request => request.Contains("/routingruleitems", StringComparison.OrdinalIgnoreCase));
        harness.Requests.Should().Contain(request => request.Contains("/mobileofflineprofiles", StringComparison.OrdinalIgnoreCase));
        harness.Requests.Should().Contain(request => request.Contains("/mobileofflineprofileitems", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ReadAsync_projects_security_definition_families_and_reports_role_privileges_as_best_effort()
    {
        var harness = LiveFixtureHarness.Create("seed-process-security");

        var snapshot = await harness.ReadAsync(
            ComponentFamily.Role,
            ComponentFamily.RolePrivilege,
            ComponentFamily.FieldSecurityProfile,
            ComponentFamily.FieldPermission,
            ComponentFamily.ConnectionRole);

        snapshot.Artifacts.Should().ContainSingle(artifact =>
            artifact.Family == ComponentFamily.Role
            && artifact.LogicalName == "codex metadata seed role");
        snapshot.Artifacts.Should().ContainSingle(artifact =>
            artifact.Family == ComponentFamily.FieldSecurityProfile
            && artifact.LogicalName == "codex metadata seed field security");
        snapshot.Artifacts.Should().ContainSingle(artifact =>
            artifact.Family == ComponentFamily.FieldPermission
            && artifact.LogicalName == "codex metadata seed field security|cdxmeta_workitem|cdxmeta_details");
        snapshot.Artifacts.Should().ContainSingle(artifact =>
            artifact.Family == ComponentFamily.ConnectionRole
            && artifact.LogicalName == "codex metadata seed connection role");
        snapshot.Artifacts.Should().NotContain(artifact => artifact.Family == ComponentFamily.RolePrivilege);
        snapshot.Diagnostics.Should().Contain(diagnostic => diagnostic.Code == "live-readback-role-privilege-best-effort");

        harness.Requests.Should().Contain(request => request.Contains("/roles", StringComparison.OrdinalIgnoreCase));
        harness.Requests.Should().Contain(request => request.Contains("/fieldsecurityprofiles", StringComparison.OrdinalIgnoreCase));
        harness.Requests.Should().Contain(request => request.Contains("/fieldpermissions", StringComparison.OrdinalIgnoreCase));
        harness.Requests.Should().Contain(request => request.Contains("/connectionroles", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ReadAsync_falls_back_to_entity_scoped_forms_and_views_when_solution_scope_underreports()
    {
        var harness = LiveFixtureHarness.Create("seed-forms", entityOnlyUiScope: true);

        var snapshot = await harness.ReadAsync(ComponentFamily.Form, ComponentFamily.View);

        snapshot.Diagnostics.Should().Contain(diagnostic => diagnostic.Code == "live-readback-form-fallback");
        snapshot.Diagnostics.Should().Contain(diagnostic => diagnostic.Code == "live-readback-view-fallback");
        snapshot.Artifacts.Should().Contain(artifact => artifact.Family == ComponentFamily.Form && artifact.LogicalName == "cdxmeta_workitem|main|c67be8a4-c475-4041-90e6-78e3ed79b018");
        snapshot.Artifacts.Should().Contain(artifact => artifact.Family == ComponentFamily.View && artifact.LogicalName == "cdxmeta_workitem|Active Work Items");
        harness.Requests.Should().Contain(request => request.Contains("objecttypecode eq 'cdxmeta_workitem'", StringComparison.OrdinalIgnoreCase));
        harness.Requests.Should().Contain(request => request.Contains("returnedtypecode eq 'cdxmeta_workitem'", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ReadAsync_returns_error_when_solution_is_missing()
    {
        var harness = LiveFixtureHarness.Create("seed-core", missingSolution: true);

        var snapshot = await harness.ReadAsync(ComponentFamily.Table);

        snapshot.Artifacts.Should().BeEmpty();
        snapshot.Diagnostics.Should().ContainSingle(diagnostic => diagnostic.Code == "live-readback-solution-not-found");
    }

    [Fact]
    public async Task ReadAsync_returns_error_when_authentication_fails()
    {
        var request = new ReadbackRequest(
            new EnvironmentProfile("dev", new Uri("https://example.crm.dynamics.com")),
            "CodexMetadataSeedCore",
            [ComponentFamily.Table]);
        using var client = new HttpClient(new StaticResponseHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));
        var reader = new DataverseWebApiLiveReader(client, new ThrowingTokenCredential());

        var snapshot = await reader.ReadAsync(request, CancellationToken.None);

        snapshot.Artifacts.Should().BeEmpty();
        snapshot.Diagnostics.Should().ContainSingle(diagnostic => diagnostic.Code == "live-readback-auth-failure");
    }

    [Fact]
    public async Task ReadAsync_keeps_web_resource_when_content_bytes_are_not_decodable()
    {
        var requests = new List<string>();
        static HttpResponseMessage CreateJsonResponse(JsonNode body) =>
            new(HttpStatusCode.OK)
            {
                Content = new StringContent(body.ToJsonString())
                {
                    Headers =
                    {
                        ContentType = new MediaTypeHeaderValue("application/json")
                    }
                }
            };
        using var client = new HttpClient(new StaticResponseHandler(request =>
        {
            var relative = request.RequestUri?.PathAndQuery.TrimStart('/') ?? string.Empty;
            requests.Add(relative);

            if (relative.Contains("solutions?$select=", StringComparison.OrdinalIgnoreCase))
            {
                return CreateJsonResponse(new JsonObject
                {
                    ["value"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["solutionid"] = "49916849-57f1-ee11-9048-000d3ab5d944",
                            ["friendlyname"] = "Codex Metadata App Shell",
                            ["uniquename"] = "CodexMetadataSeedAppShell",
                            ["version"] = "1.0.0.0",
                            ["ismanaged"] = false
                        }
                    }
                });
            }

            if (relative.Contains("solutioncomponents", StringComparison.OrdinalIgnoreCase))
            {
                return CreateJsonResponse(new JsonObject
                {
                    ["value"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["componenttype"] = 61,
                            ["objectid"] = "a0ba2262-5637-f111-88b3-0022489b9600"
                        }
                    }
                });
            }

            if (relative.Contains("webresourceset", StringComparison.OrdinalIgnoreCase))
            {
                return CreateJsonResponse(new JsonObject
                {
                    ["value"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["webresourceid"] = "a0ba2262-5637-f111-88b3-0022489b9600",
                            ["name"] = "cdxmeta_/shell/landing.html",
                            ["displayname"] = "Codex Metadata Shell Landing HTML",
                            ["description"] = "Neutral HTML landing page for the app-shell seed.",
                            ["webresourcetype"] = 1,
                            ["content"] = "not-base64"
                        }
                    }
                });
            }

            return CreateJsonResponse(new JsonObject
            {
                ["value"] = new JsonArray()
            });
        }));
        var reader = new DataverseWebApiLiveReader(client, new FakeTokenCredential());
        var request = new ReadbackRequest(
            new EnvironmentProfile("dev", new Uri("https://example.crm.dynamics.com")),
            "CodexMetadataSeedAppShell",
            [ComponentFamily.WebResource]);

        var snapshot = await reader.ReadAsync(request, CancellationToken.None);

        snapshot.Artifacts.Should().ContainSingle(artifact =>
            artifact.Family == ComponentFamily.WebResource
            && artifact.LogicalName == "cdxmeta_/shell/landing.html");
        snapshot.Artifacts.Single(artifact => artifact.Family == ComponentFamily.WebResource)
            .Properties!
            .Should()
            .NotContainKey(ArtifactPropertyKeys.ContentHash);
        snapshot.Diagnostics.Should().Contain(diagnostic => diagnostic.Code == "live-readback-webresource-content-best-effort");
        requests.Should().Contain(requestPath => requestPath.Contains("webresourceset", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ReadAsync_retries_solution_projection_without_publisher_columns_when_the_org_rejects_them()
    {
        var requests = new List<string>();
        using var client = new HttpClient(new StaticResponseHandler(request =>
        {
            var relative = request.RequestUri?.PathAndQuery.TrimStart('/') ?? string.Empty;
            requests.Add(relative);

            if (relative.Contains("solutions?$select=solutionid,friendlyname,uniquename,version,ismanaged,publisheruniquename,publishercustomizationprefix,publisherfriendlyname", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent(
                        """
                        {
                          "error": {
                            "code": "0x80060888",
                            "message": "Could not find a property named 'publisheruniquename' on type 'Microsoft.Dynamics.CRM.solution'."
                          }
                        }
                        """)
                    {
                        Headers =
                        {
                            ContentType = new MediaTypeHeaderValue("application/json")
                        }
                    }
                };
            }

            if (relative.Contains("solutions?$select=solutionid,friendlyname,uniquename,version,ismanaged", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """
                        {
                          "value": [
                            {
                              "solutionid": "49916849-57f1-ee11-9048-000d3ab5d944",
                              "friendlyname": "Codex Metadata Seed Image Config",
                              "uniquename": "CodexMetadataSeedImageConfig",
                              "version": "1.0.0.0",
                              "ismanaged": false
                            }
                          ]
                        }
                        """)
                    {
                        Headers =
                        {
                            ContentType = new MediaTypeHeaderValue("application/json")
                        }
                    }
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"value\":[]}")
                {
                    Headers =
                    {
                        ContentType = new MediaTypeHeaderValue("application/json")
                    }
                }
            };
        }));
        var reader = new DataverseWebApiLiveReader(client, new FakeTokenCredential());
        var request = new ReadbackRequest(
            new EnvironmentProfile("dev", new Uri("https://example.crm.dynamics.com")),
            "CodexMetadataSeedImageConfig",
            [ComponentFamily.SolutionShell]);

        var snapshot = await reader.ReadAsync(request, CancellationToken.None);

        snapshot.Artifacts.Should().ContainSingle(artifact =>
            artifact.Family == ComponentFamily.SolutionShell
            && artifact.LogicalName == "CodexMetadataSeedImageConfig");
        requests.Should().Contain(query => query.Contains("publisheruniquename", StringComparison.OrdinalIgnoreCase));
        requests.Should().Contain(query =>
            query.Contains("solutions?$select=solutionid,friendlyname,uniquename,version,ismanaged", StringComparison.OrdinalIgnoreCase)
            && !query.Contains("publisheruniquename", StringComparison.OrdinalIgnoreCase));
        snapshot.Diagnostics.Should().NotContain(diagnostic => diagnostic.Code == "live-readback-http-failure");
    }

    [Fact]
    public async Task ReadAsync_keeps_partial_artifacts_when_one_family_endpoint_fails()
    {
        var harness = LiveFixtureHarness.Create("seed-core", failingPathFragment: "/ManyToOneRelationships");

        var snapshot = await harness.ReadAsync(ComponentFamily.Table, ComponentFamily.Relationship);

        snapshot.Artifacts.Should().Contain(artifact => artifact.Family == ComponentFamily.Table && artifact.LogicalName == "cdxmeta_workitem");
        snapshot.Diagnostics.Should().Contain(diagnostic => diagnostic.Code == "live-readback-http-failure" && diagnostic.Message.Contains("Relationship", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ReadAsync_reports_import_map_families_as_unsupported_without_false_drift()
    {
        var source = ReadSourceFixture("seed-import-map") with
        {
            Artifacts = ReadSourceFixture("seed-import-map").Artifacts
                .Where(artifact => artifact.Family is ComponentFamily.ImportMap or ComponentFamily.DataSourceMapping)
                .ToArray()
        };
        var harness = LiveFixtureHarness.Create("seed-import-map");

        var snapshot = await harness.ReadAsync(ComponentFamily.ImportMap, ComponentFamily.DataSourceMapping);
        var report = new StableOverlapDriftComparer().Compare(source, snapshot, new CompareRequest());

        snapshot.Artifacts.Should().BeEmpty();
        snapshot.Diagnostics.Should().Contain(diagnostic =>
            diagnostic.Code == "live-readback-import-map-source-first"
            && diagnostic.Message.Contains("ImportMap", StringComparison.OrdinalIgnoreCase));
        report.HasBlockingDrift.Should().BeFalse();
        report.Findings.Should().BeEmpty();
    }

    [Fact]
    public async Task ReadAsync_reports_similarity_and_sla_families_as_source_first_without_false_drift()
    {
        var source = ReadSourceFixture("source-only-sla") with
        {
            Artifacts = ReadSourceFixture("source-only-sla").Artifacts
                .Where(artifact => artifact.Family is ComponentFamily.Sla or ComponentFamily.SlaItem)
                .Concat(ReadSourceFixture("source-only-similarity-rule").Artifacts
                    .Where(artifact => artifact.Family == ComponentFamily.SimilarityRule))
                .ToArray()
        };
        var harness = LiveFixtureHarness.Create("seed-process-policy");

        var snapshot = await harness.ReadAsync(ComponentFamily.Sla, ComponentFamily.SlaItem, ComponentFamily.SimilarityRule);
        var report = new StableOverlapDriftComparer().Compare(source, snapshot, new CompareRequest());

        snapshot.Artifacts.Should().BeEmpty();
        snapshot.Diagnostics.Should().Contain(diagnostic =>
            diagnostic.Code == "live-readback-source-first-process-policy"
            && diagnostic.Message.Contains("Sla", StringComparison.OrdinalIgnoreCase));
        report.HasBlockingDrift.Should().BeFalse();
        report.Findings.Should().BeEmpty();
    }

    [Fact]
    public async Task ReadAsync_reports_ribbon_as_unsupported_without_false_drift()
    {
        var source = ReadSourceFixture("seed-advanced-ui") with
        {
            Artifacts = ReadSourceFixture("seed-advanced-ui").Artifacts
                .Where(artifact => artifact.Family == ComponentFamily.Ribbon)
                .ToArray()
        };
        var harness = LiveFixtureHarness.Create("seed-advanced-ui", sourceScope: source);

        var snapshot = await harness.ReadAsync(ComponentFamily.Ribbon);
        var report = new StableOverlapDriftComparer().Compare(source, snapshot, new CompareRequest());

        snapshot.Artifacts.Should().BeEmpty();
        snapshot.Diagnostics.Should().Contain(diagnostic =>
            diagnostic.Code == "live-readback-unsupported-families"
            && diagnostic.Message.Contains("Ribbon", StringComparison.OrdinalIgnoreCase));
        report.HasBlockingDrift.Should().BeFalse();
        report.Findings.Should().BeEmpty();
    }

    [Theory]
    [InlineData("seed-alternate-key")]
    [InlineData("seed-core")]
    [InlineData("seed-forms")]
    [InlineData("seed-app-shell")]
    [InlineData("seed-advanced-ui")]
    [InlineData("seed-environment")]
    [InlineData("seed-entity-analytics")]
    [InlineData("seed-ai-families")]
    [InlineData("seed-image-config")]
    [InlineData("seed-plugin-registration")]
    [InlineData("seed-code-plugin-classic")]
    [InlineData("seed-code-plugin-package")]
    [InlineData("seed-code-plugin-imperative")]
    [InlineData("seed-code-plugin-helper")]
    [InlineData("seed-code-plugin-imperative-service")]
    [InlineData("seed-code-workflow-activity-classic")]
    [InlineData("seed-workflow-classic")]
    [InlineData("seed-workflow-action")]
    [InlineData("seed-process-policy")]
    [InlineData("seed-process-security")]
    [InlineData("seed-service-endpoint-connector")]
    public async Task Source_and_live_overlap_compare_cleanly_for_supported_families(string fixtureName)
    {
        var source = ReadSourceFixture(fixtureName);
        var filteredSource = FilterSourceToSupportedFamilies(source, fixtureName);
        var requestedFamilies = filteredSource.Artifacts
            .Select(artifact => artifact.Family)
            .Distinct()
            .ToArray();
        var harness = LiveFixtureHarness.Create(fixtureName, sourceScope: filteredSource);
        var snapshot = await harness.ReadAsync(requestedFamilies);

        var report = new StableOverlapDriftComparer().Compare(filteredSource, snapshot, new CompareRequest());

        report.HasBlockingDrift.Should().BeFalse();
    }

    private static CanonicalSolution ReadSourceFixture(string fixtureName)
    {
        if (string.Equals(fixtureName, "seed-code-plugin-classic", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fixtureName, "seed-code-plugin-package", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fixtureName, "seed-code-plugin-imperative", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fixtureName, "seed-code-plugin-helper", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fixtureName, "seed-code-plugin-imperative-service", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fixtureName, "seed-code-workflow-activity-classic", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fixtureName, "seed-code-workflow-activity-package", StringComparison.OrdinalIgnoreCase))
        {
            return new DataverseSolutionCompiler.Compiler.CompilerKernel().Compile(
                new DataverseSolutionCompiler.Domain.Compilation.CompilationRequest(
                    Path.Combine(
                        "C:\\Git\\Dataverse-Solution-KB",
                        "fixtures",
                        "skill-corpus",
                        "examples",
                        fixtureName),
                    Array.Empty<string>())).Solution;
        }

        var reader = new XmlSolutionReader();
        return reader.Read(new ReadRequest(Path.Combine(
            "C:\\Git\\Dataverse-Solution-KB",
            "fixtures",
            "skill-corpus",
            "examples",
            fixtureName,
            "unpacked")));
    }

    private static CanonicalSolution FilterSourceToSupportedFamilies(CanonicalSolution source, string fixtureName)
    {
        var supportedFamilies = fixtureName switch
        {
            "seed-advanced-ui" => new HashSet<ComponentFamily>
            {
                ComponentFamily.SolutionShell,
                ComponentFamily.Visualization,
                ComponentFamily.AppModule,
                ComponentFamily.AppSetting,
                ComponentFamily.SiteMap,
                ComponentFamily.WebResource,
                ComponentFamily.EnvironmentVariableDefinition,
                ComponentFamily.EnvironmentVariableValue
            },
            "seed-app-shell" => new HashSet<ComponentFamily>
            {
                ComponentFamily.SolutionShell,
                ComponentFamily.AppModule,
                ComponentFamily.AppSetting,
                ComponentFamily.SiteMap,
                ComponentFamily.WebResource,
                ComponentFamily.EnvironmentVariableDefinition,
                ComponentFamily.EnvironmentVariableValue
            },
            "seed-environment" => new HashSet<ComponentFamily>
            {
                ComponentFamily.SolutionShell,
                ComponentFamily.CanvasApp
            },
            "seed-alternate-key" => new HashSet<ComponentFamily>
            {
                ComponentFamily.SolutionShell,
                ComponentFamily.Table,
                ComponentFamily.Column,
                ComponentFamily.Key
            },
            "seed-entity-analytics" => new HashSet<ComponentFamily>
            {
                ComponentFamily.SolutionShell,
                ComponentFamily.EntityAnalyticsConfiguration
            },
            "seed-image-config" => new HashSet<ComponentFamily>
            {
                ComponentFamily.SolutionShell,
                ComponentFamily.Table,
                ComponentFamily.Column,
                ComponentFamily.ImageConfiguration
            },
            "seed-ai-families" => new HashSet<ComponentFamily>
            {
                ComponentFamily.SolutionShell,
                ComponentFamily.AiProjectType,
                ComponentFamily.AiProject,
                ComponentFamily.AiConfiguration
            },
            "seed-plugin-registration" => new HashSet<ComponentFamily>
            {
                ComponentFamily.SolutionShell,
                ComponentFamily.PluginAssembly,
                ComponentFamily.PluginType,
                ComponentFamily.PluginStep,
                ComponentFamily.PluginStepImage
            },
            "seed-code-plugin-classic" or "seed-code-plugin-package" or "seed-code-plugin-imperative" or "seed-code-plugin-helper" or "seed-code-plugin-imperative-service" => new HashSet<ComponentFamily>
            {
                ComponentFamily.SolutionShell,
                ComponentFamily.PluginAssembly,
                ComponentFamily.PluginType,
                ComponentFamily.PluginStep,
                ComponentFamily.PluginStepImage
            },
            "seed-code-workflow-activity-classic" => new HashSet<ComponentFamily>
            {
                ComponentFamily.SolutionShell,
                ComponentFamily.PluginAssembly,
                ComponentFamily.PluginType
            },
            "seed-workflow-classic" => new HashSet<ComponentFamily>
            {
                ComponentFamily.SolutionShell,
                ComponentFamily.Workflow
            },
            "seed-workflow-action" => new HashSet<ComponentFamily>
            {
                ComponentFamily.SolutionShell,
                ComponentFamily.Workflow
            },
            "seed-process-policy" => new HashSet<ComponentFamily>
            {
                ComponentFamily.SolutionShell,
                ComponentFamily.DuplicateRule,
                ComponentFamily.DuplicateRuleCondition,
                ComponentFamily.RoutingRule,
                ComponentFamily.RoutingRuleItem,
                ComponentFamily.MobileOfflineProfile,
                ComponentFamily.MobileOfflineProfileItem
            },
            "seed-process-security" => new HashSet<ComponentFamily>
            {
                ComponentFamily.SolutionShell,
                ComponentFamily.Role,
                ComponentFamily.FieldSecurityProfile,
                ComponentFamily.FieldPermission,
                ComponentFamily.ConnectionRole
            },
            "seed-service-endpoint-connector" => new HashSet<ComponentFamily>
            {
                ComponentFamily.SolutionShell,
                ComponentFamily.ServiceEndpoint,
                ComponentFamily.Connector
            },
            _ => new HashSet<ComponentFamily>
            {
                ComponentFamily.SolutionShell,
                ComponentFamily.Table,
                ComponentFamily.Column,
                ComponentFamily.Relationship,
                ComponentFamily.OptionSet,
                ComponentFamily.Form,
                ComponentFamily.View
            }
        };

        return source with
        {
            Artifacts = source.Artifacts.Where(artifact => supportedFamilies.Contains(artifact.Family)).ToArray()
        };
    }
}

internal sealed class LiveFixtureHarness
{
    private readonly string _fixtureName;
    private readonly JsonObject _readback;
    private readonly JsonObject? _solution;
    private readonly CanonicalSolution? _sourceScope;
    private readonly bool _entityOnlyUiScope;
    private readonly bool _pageSolutionComponents;
    private readonly bool _omitImageConfigurationScope;
    private readonly bool _missingSolution;
    private readonly string? _failingPathFragment;
    private readonly Dictionary<string, Guid> _entityIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Guid> _globalOptionSetIds = new(StringComparer.OrdinalIgnoreCase);

    private LiveFixtureHarness(
        string fixtureName,
        JsonObject readback,
        JsonObject? solution,
        CanonicalSolution? sourceScope,
        bool entityOnlyUiScope,
        bool pageSolutionComponents,
        bool omitImageConfigurationScope,
        bool missingSolution,
        string? failingPathFragment)
    {
        _fixtureName = fixtureName;
        _readback = readback;
        _solution = solution;
        _sourceScope = sourceScope;
        _entityOnlyUiScope = entityOnlyUiScope;
        _pageSolutionComponents = pageSolutionComponents;
        _omitImageConfigurationScope = omitImageConfigurationScope;
        _missingSolution = missingSolution;
        _failingPathFragment = failingPathFragment;
    }

    public List<string> Requests { get; } = [];

    public static LiveFixtureHarness Create(
        string fixtureName,
        CanonicalSolution? sourceScope = null,
        bool entityOnlyUiScope = false,
        bool pageSolutionComponents = false,
        bool omitImageConfigurationScope = false,
        bool missingSolution = false,
        string? failingPathFragment = null)
    {
        var root = Path.Combine(
            "C:\\Git\\Dataverse-Solution-KB",
            "fixtures",
            "skill-corpus",
            "examples",
            fixtureName,
            "readback");

        var readback = ParseObject(Path.Combine(root, "readback.json"));
        var solutionPath = Path.Combine(root, "solution.json");
        var solution = File.Exists(solutionPath) ? ParseObject(solutionPath) : readback["solution"]?.DeepClone() as JsonObject;

        return new LiveFixtureHarness(
            fixtureName,
            readback,
            solution,
            sourceScope,
            entityOnlyUiScope,
            pageSolutionComponents,
            omitImageConfigurationScope,
            missingSolution,
            failingPathFragment);
    }

    public async Task<LiveSnapshot> ReadAsync(params ComponentFamily[] families)
    {
        using var client = new HttpClient(new FixtureHandler(this))
        {
            BaseAddress = new Uri("https://example.crm.dynamics.com/")
        };
        var request = new ReadbackRequest(
            new EnvironmentProfile("dev", new Uri("https://example.crm.dynamics.com")),
            _solution?["uniquename"]?.GetValue<string>(),
            families.Length == 0 ? null : families);

        var reader = new DataverseWebApiLiveReader(client, new FakeTokenCredential());
        return await reader.ReadAsync(request, CancellationToken.None);
    }

    internal HttpResponseMessage Handle(HttpRequestMessage request)
    {
        Requests.Add(Uri.UnescapeDataString(request.RequestUri!.PathAndQuery));

        if (!string.IsNullOrWhiteSpace(_failingPathFragment)
            && request.RequestUri.PathAndQuery.Contains(_failingPathFragment, StringComparison.OrdinalIgnoreCase))
        {
            return JsonResponse(HttpStatusCode.InternalServerError, new JsonObject
            {
                ["error"] = new JsonObject
                {
                    ["message"] = "Fixture-injected failure."
                }
            });
        }

        var path = request.RequestUri.AbsolutePath;
        var query = Uri.UnescapeDataString(request.RequestUri.Query);

        if (path.EndsWith("/solutions", StringComparison.OrdinalIgnoreCase))
        {
            return Envelope(_missingSolution || _solution is null ? [] : [_solution.DeepClone()]);
        }

        if (path.EndsWith("/solutioncomponents", StringComparison.OrdinalIgnoreCase))
        {
            return HandleSolutionComponents(request.RequestUri);
        }

        if (path.EndsWith("/EntityDefinitions", StringComparison.OrdinalIgnoreCase))
        {
            return Envelope(GetEntityDefinitionRows());
        }

        if (TryExtractEntityLogicalName(path, out var entityLogicalName))
        {
            return HandleEntityRequest(entityLogicalName, path, query);
        }

        if (path.EndsWith("/GlobalOptionSetDefinitions", StringComparison.OrdinalIgnoreCase))
        {
            return Envelope(GetGlobalOptionSets());
        }

        if (path.EndsWith("/systemforms", StringComparison.OrdinalIgnoreCase))
        {
            return Envelope(GetForms(query));
        }

        if (path.EndsWith("/savedqueries", StringComparison.OrdinalIgnoreCase))
        {
            return Envelope(GetViews(query));
        }

        if (path.EndsWith("/savedqueryvisualizations", StringComparison.OrdinalIgnoreCase))
        {
            return Envelope(FilterRowsBySourceScope(
                GetArtifactArray("saved-query-visualizations.json", "saved_query_visualizations"),
                ComponentFamily.Visualization,
                row => row["savedqueryvisualizationid"]?.GetValue<string>(),
                row => $"{NormalizeLogicalName(row["primaryentitytypecode"]?.GetValue<string>())}|{row["name"]?.GetValue<string>()}"));
        }

        if (path.EndsWith("/appmodules", StringComparison.OrdinalIgnoreCase))
        {
            return Envelope(GetArtifactArray("app-modules.json", "app_modules"));
        }

        if (path.EndsWith("/appsettings", StringComparison.OrdinalIgnoreCase))
        {
            return Envelope(GetArtifactArray("app-settings.json", "app_settings"));
        }

        if (path.EndsWith("/sitemaps", StringComparison.OrdinalIgnoreCase))
        {
            return Envelope(GetArtifactArray("site-maps.json", "site_maps"));
        }

        if (path.EndsWith("/webresourceset", StringComparison.OrdinalIgnoreCase))
        {
            return Envelope(FilterRowsBySourceScope(
                GetArtifactArray("web-resources.json", "web_resources"),
                ComponentFamily.WebResource,
                row => row["webresourceid"]?.GetValue<string>(),
                row => row["name"]?.GetValue<string>()));
        }

        if (path.EndsWith("/customcontrols", StringComparison.OrdinalIgnoreCase))
        {
            return Envelope(GetArtifactArray("custom-controls.json", "custom_controls"));
        }

        if (path.EndsWith("/environmentvariabledefinitions", StringComparison.OrdinalIgnoreCase))
        {
            return Envelope(GetArtifactArray("environment-variable-definitions.json", "environment_variable_definitions"));
        }

        if (path.EndsWith("/environmentvariablevalues", StringComparison.OrdinalIgnoreCase))
        {
            return Envelope(GetArtifactArray("environment-variable-values.json", "environment_variable_values"));
        }

        if (path.EndsWith("/canvasapps", StringComparison.OrdinalIgnoreCase))
        {
            return Envelope(GetArtifactArray("canvas-apps.json", "canvas_apps"));
        }

        if (path.EndsWith("/entityanalyticsconfigs", StringComparison.OrdinalIgnoreCase))
        {
            return Envelope(GetArtifactArray("entity-analytics-configurations.json", "entity_analytics_configurations"));
        }

        if (path.EndsWith("/msdyn_aitemplates", StringComparison.OrdinalIgnoreCase))
        {
            return Envelope(GetArtifactArray("ai-project-types.json", "ai_project_types"));
        }

        if (path.EndsWith("/msdyn_aimodels", StringComparison.OrdinalIgnoreCase))
        {
            return Envelope(GetArtifactArray("ai-projects.json", "ai_projects"));
        }

        if (path.EndsWith("/msdyn_aiconfigurations", StringComparison.OrdinalIgnoreCase))
        {
            return Envelope(GetArtifactArray("ai-configurations.json", "ai_configurations"));
        }

        if (path.EndsWith("/pluginassemblies", StringComparison.OrdinalIgnoreCase))
        {
            return Envelope(GetArtifactArray("plugin-assemblies.json", "plugin_assemblies"));
        }

        if (path.EndsWith("/plugintypes", StringComparison.OrdinalIgnoreCase))
        {
            return Envelope(GetArtifactArray("plugin-types.json", "plugin_types"));
        }

        if (path.EndsWith("/sdkmessageprocessingsteps", StringComparison.OrdinalIgnoreCase))
        {
            return Envelope(GetArtifactArray("plugin-steps.json", "plugin_steps"));
        }

        if (path.EndsWith("/sdkmessageprocessingstepimages", StringComparison.OrdinalIgnoreCase))
        {
            return Envelope(GetArtifactArray("plugin-step-images.json", "plugin_step_images"));
        }

        if (path.EndsWith("/sdkmessages", StringComparison.OrdinalIgnoreCase))
        {
            return Envelope(GetArtifactArray("sdkmessages.json", "sdkmessages"));
        }

        if (path.EndsWith("/sdkmessagefilters", StringComparison.OrdinalIgnoreCase))
        {
            return Envelope(GetArtifactArray("sdkmessagefilters.json", "sdkmessagefilters"));
        }

        if (path.EndsWith("/serviceendpoints", StringComparison.OrdinalIgnoreCase))
        {
            return Envelope(GetArtifactArray("service-endpoints.json", "service_endpoints"));
        }

        if (path.EndsWith("/connectors", StringComparison.OrdinalIgnoreCase))
        {
            return Envelope(GetArtifactArray("connectors.json", "connectors"));
        }

        if (path.EndsWith("/duplicaterules", StringComparison.OrdinalIgnoreCase))
        {
            return Envelope(GetArtifactArray("duplicate-rules.json", "duplicate_rules"));
        }

        if (path.EndsWith("/duplicateruleconditions", StringComparison.OrdinalIgnoreCase))
        {
            return Envelope(GetArtifactArray("duplicate-rule-conditions.json", "duplicate_rule_conditions"));
        }

        if (path.EndsWith("/routingrules", StringComparison.OrdinalIgnoreCase))
        {
            return Envelope(GetArtifactArray("routing-rules.json", "routing_rules"));
        }

        if (path.EndsWith("/routingruleitems", StringComparison.OrdinalIgnoreCase))
        {
            return Envelope(GetArtifactArray("routing-rule-items.json", "routing_rule_items"));
        }

        if (path.EndsWith("/mobileofflineprofiles", StringComparison.OrdinalIgnoreCase))
        {
            return Envelope(GetArtifactArray("mobile-offline-profiles.json", "mobile_offline_profiles"));
        }

        if (path.EndsWith("/mobileofflineprofileitems", StringComparison.OrdinalIgnoreCase))
        {
            return Envelope(GetArtifactArray("mobile-offline-profile-items.json", "mobile_offline_profile_items"));
        }

        if (path.EndsWith("/roles", StringComparison.OrdinalIgnoreCase))
        {
            return Envelope(GetArtifactArray("roles.json", "roles"));
        }

        if (path.EndsWith("/fieldsecurityprofiles", StringComparison.OrdinalIgnoreCase))
        {
            return Envelope(GetArtifactArray("field-security-profiles.json", "field_security_profiles"));
        }

        if (path.EndsWith("/fieldpermissions", StringComparison.OrdinalIgnoreCase))
        {
            return Envelope(GetArtifactArray("field-permissions.json", "field_permissions"));
        }

        if (path.EndsWith("/connectionroles", StringComparison.OrdinalIgnoreCase))
        {
            return Envelope(GetArtifactArray("connection-roles.json", "connection_roles"));
        }

        if (path.EndsWith("/workflows", StringComparison.OrdinalIgnoreCase))
        {
            return Envelope(GetArtifactArray("workflows.json", "workflows"));
        }

        return JsonResponse(HttpStatusCode.NotFound, new JsonObject
        {
            ["error"] = new JsonObject
            {
                ["message"] = $"{request.RequestUri.PathAndQuery} was not mapped by the fixture handler."
            }
        });
    }

    private HttpResponseMessage HandleSolutionComponents(Uri uri)
    {
        var rows = BuildSolutionComponentRows();
        if (_pageSolutionComponents && !uri.Query.Contains("$skiptoken=page2", StringComparison.OrdinalIgnoreCase))
        {
            var firstPage = new JsonArray();
            foreach (var row in rows.Take(3))
            {
                firstPage.Add(row.DeepClone());
            }

            return JsonResponse(HttpStatusCode.OK, new JsonObject
            {
                ["value"] = firstPage,
                ["@odata.nextLink"] = "https://example.crm.dynamics.com/api/data/v9.2/solutioncomponents?$skiptoken=page2"
            });
        }

        if (_pageSolutionComponents && uri.Query.Contains("$skiptoken=page2", StringComparison.OrdinalIgnoreCase))
        {
            rows = rows.Skip(3).ToArray();
        }

        return Envelope(rows);
    }

    private JsonNode[] BuildSolutionComponentRows()
    {
        var rows = new List<JsonNode>();
        var entities = GetEntityNames();
        foreach (var entity in entities)
        {
            var id = StableGuid($"entity:{entity}");
            _entityIds[entity] = id;
            rows.Add(new JsonObject
            {
                ["componenttype"] = 1,
                ["objectid"] = id.ToString("D"),
                ["logicalname"] = entity
            });
        }

        foreach (var optionSet in GetGlobalOptionSets().OfType<JsonObject>())
        {
            var name = optionSet["option_set_name"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var id = StableGuid($"global-option-set:{name}");
            _globalOptionSetIds[name] = id;
            rows.Add(new JsonObject
            {
                ["componenttype"] = 9,
                ["objectid"] = id.ToString("D"),
                ["name"] = name
            });
        }

        foreach (var key in FilterRowsBySourceScope(GetArtifactArray("entity-keys.json", "entity_keys"), ComponentFamily.Key, row => row["entitykeymetadataid"]?.GetValue<string>(), row => row["logical_name"]?.GetValue<string>()))
        {
            rows.Add(new JsonObject
            {
                ["componenttype"] = 14,
                ["objectid"] = key["entitykeymetadataid"]!.GetValue<string>(),
                ["logicalname"] = key["logical_name"]?.GetValue<string>(),
                ["schemaname"] = key["schemaname"]?.GetValue<string>()
            });
        }

        if (!_entityOnlyUiScope)
        {
            foreach (var form in FilterRowsBySourceScope(GetAllForms(), ComponentFamily.Form, row => row["formid"]?.GetValue<string>(), row => $"{row["objecttypecode"]?.GetValue<string>()}|{row["name"]?.GetValue<string>()}"))
            {
                rows.Add(new JsonObject
                {
                    ["componenttype"] = 60,
                    ["objectid"] = form["formid"]!.GetValue<string>()
                });
            }

            foreach (var view in FilterRowsBySourceScope(GetAllViews(), ComponentFamily.View, row => row["savedqueryid"]?.GetValue<string>(), row => $"{row["returnedtypecode"]?.GetValue<string>()}|{row["name"]?.GetValue<string>()}"))
            {
                rows.Add(new JsonObject
                {
                    ["componenttype"] = 26,
                    ["objectid"] = view["savedqueryid"]!.GetValue<string>()
                });
            }

            foreach (var visualization in FilterRowsBySourceScope(
                         GetArtifactArray("saved-query-visualizations.json", "saved_query_visualizations"),
                         ComponentFamily.Visualization,
                         row => row["savedqueryvisualizationid"]?.GetValue<string>(),
                         row => $"{NormalizeLogicalName(row["primaryentitytypecode"]?.GetValue<string>())}|{row["name"]?.GetValue<string>()}"))
            {
                rows.Add(new JsonObject
                {
                    ["componenttype"] = 59,
                    ["objectid"] = visualization["savedqueryvisualizationid"]!.GetValue<string>()
                });
            }
        }

        foreach (var appModule in FilterRowsBySourceScope(GetArtifactArray("app-modules.json", "app_modules"), ComponentFamily.AppModule, row => row["appmoduleid"]?.GetValue<string>(), row => row["uniquename"]?.GetValue<string>()))
        {
            rows.Add(new JsonObject
            {
                ["componenttype"] = 80,
                ["objectid"] = appModule["appmoduleid"]!.GetValue<string>()
            });
        }

        foreach (var siteMap in FilterRowsBySourceScope(GetArtifactArray("site-maps.json", "site_maps"), ComponentFamily.SiteMap, row => row["sitemapid"]?.GetValue<string>(), row => row["sitemapnameunique"]?.GetValue<string>()))
        {
            rows.Add(new JsonObject
            {
                ["componenttype"] = 62,
                ["objectid"] = siteMap["sitemapid"]!.GetValue<string>()
            });
        }

        foreach (var webResource in FilterRowsBySourceScope(GetArtifactArray("web-resources.json", "web_resources"), ComponentFamily.WebResource, row => row["webresourceid"]?.GetValue<string>(), row => row["name"]?.GetValue<string>()))
        {
            rows.Add(new JsonObject
            {
                ["componenttype"] = 61,
                ["objectid"] = webResource["webresourceid"]!.GetValue<string>()
            });
        }

        foreach (var workflow in FilterRowsBySourceScope(
                     GetArtifactArray("workflows.json", "workflows"),
                     ComponentFamily.Workflow,
                     row => row["workflowid"]?.GetValue<string>(),
                     row => row["uniquename"]?.GetValue<string>() ?? row["name"]?.GetValue<string>()))
        {
            rows.Add(new JsonObject
            {
                ["componenttype"] = 29,
                ["objectid"] = workflow["workflowid"]!.GetValue<string>()
            });
        }

        foreach (var customControl in GetArtifactArray("custom-controls.json", "custom_controls").OfType<JsonObject>())
        {
            if (customControl["customcontrolid"] is null)
            {
                continue;
            }

            rows.Add(new JsonObject
            {
                ["componenttype"] = 66,
                ["objectid"] = customControl["customcontrolid"]!.GetValue<string>()
            });
        }

        foreach (var definition in FilterRowsBySourceScope(GetArtifactArray("environment-variable-definitions.json", "environment_variable_definitions"), ComponentFamily.EnvironmentVariableDefinition, row => row["environmentvariabledefinitionid"]?.GetValue<string>(), row => row["schemaname"]?.GetValue<string>()))
        {
            rows.Add(new JsonObject
            {
                ["componenttype"] = 380,
                ["objectid"] = definition["environmentvariabledefinitionid"]!.GetValue<string>()
            });
        }

        foreach (var value in FilterRowsBySourceScope(GetArtifactArray("environment-variable-values.json", "environment_variable_values"), ComponentFamily.EnvironmentVariableValue, row => row["environmentvariablevalueid"]?.GetValue<string>(), row => row["schemaname"]?.GetValue<string>()))
        {
            rows.Add(new JsonObject
            {
                ["componenttype"] = 381,
                ["objectid"] = value["environmentvariablevalueid"]!.GetValue<string>()
            });
        }

        foreach (var canvasApp in FilterRowsBySourceScope(GetArtifactArray("canvas-apps.json", "canvas_apps"), ComponentFamily.CanvasApp, row => row["canvasappid"]?.GetValue<string>(), row => row["name"]?.GetValue<string>()))
        {
            rows.Add(new JsonObject
            {
                ["componenttype"] = 300,
                ["objectid"] = canvasApp["canvasappid"]!.GetValue<string>()
            });
        }

        foreach (var analyticsConfiguration in FilterRowsBySourceScope(GetArtifactArray("entity-analytics-configurations.json", "entity_analytics_configurations"), ComponentFamily.EntityAnalyticsConfiguration, row => row["entityanalyticsconfigid"]?.GetValue<string>(), row => row["parententitylogicalname"]?.GetValue<string>()))
        {
            rows.Add(new JsonObject
            {
                ["componenttype"] = 430,
                ["objectid"] = analyticsConfiguration["entityanalyticsconfigid"]!.GetValue<string>()
            });
        }

        if (!_omitImageConfigurationScope)
        {
            foreach (var entity in entities)
            {
                var metadata = GetEntityMetadata(entity);
                if (metadata is null)
                {
                    continue;
                }

                var primaryImageAttribute = metadata["PrimaryImageAttribute"]?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(primaryImageAttribute))
                {
                    rows.Add(new JsonObject
                    {
                        ["componenttype"] = 432,
                        ["objectid"] = StableGuid($"entity-image:{entity}").ToString("D"),
                        ["logical_name"] = $"{entity}|entity-image",
                        ["parententitylogicalname"] = entity
                    });
                }

                foreach (var imageAttribute in GetEntityImageAttributeRows(entity, metadata).OfType<JsonObject>())
                {
                    var attributeLogicalName = imageAttribute["LogicalName"]?.GetValue<string>();
                    if (string.IsNullOrWhiteSpace(attributeLogicalName))
                    {
                        continue;
                    }

                    rows.Add(new JsonObject
                    {
                        ["componenttype"] = 431,
                        ["objectid"] = StableGuid($"attribute-image:{entity}|{attributeLogicalName}").ToString("D"),
                        ["logical_name"] = $"{entity}|{attributeLogicalName}|attribute-image",
                        ["parententitylogicalname"] = entity,
                        ["attributelogicalname"] = attributeLogicalName
                    });
                }
            }
        }

        foreach (var aiProjectType in FilterRowsBySourceScope(GetArtifactArray("ai-project-types.json", "ai_project_types"), ComponentFamily.AiProjectType, row => row["msdyn_aitemplateid"]?.GetValue<string>(), row => row["msdyn_uniquename"]?.GetValue<string>()))
        {
            rows.Add(new JsonObject
            {
                ["componenttype"] = 400,
                ["objectid"] = aiProjectType["msdyn_aitemplateid"]!.GetValue<string>()
            });
        }

        foreach (var aiProject in FilterRowsBySourceScope(GetArtifactArray("ai-projects.json", "ai_projects"), ComponentFamily.AiProject, row => row["msdyn_aimodelid"]?.GetValue<string>(), row => GetAiProjectLogicalName(row)))
        {
            rows.Add(new JsonObject
            {
                ["componenttype"] = 401,
                ["objectid"] = aiProject["msdyn_aimodelid"]!.GetValue<string>()
            });
        }

        foreach (var aiConfiguration in FilterRowsBySourceScope(GetArtifactArray("ai-configurations.json", "ai_configurations"), ComponentFamily.AiConfiguration, row => row["msdyn_aiconfigurationid"]?.GetValue<string>(), row => GetAiConfigurationLogicalName(row)))
        {
            rows.Add(new JsonObject
            {
                ["componenttype"] = 402,
                ["objectid"] = aiConfiguration["msdyn_aiconfigurationid"]!.GetValue<string>()
            });
        }

        foreach (var assembly in FilterRowsBySourceScope(GetArtifactArray("plugin-assemblies.json", "plugin_assemblies"), ComponentFamily.PluginAssembly, row => row["pluginassemblyid"]?.GetValue<string>(), row => row["logical_name"]?.GetValue<string>() ?? row["full_name"]?.GetValue<string>()))
        {
            rows.Add(new JsonObject
            {
                ["componenttype"] = 91,
                ["objectid"] = assembly["pluginassemblyid"]!.GetValue<string>()
            });
        }

        foreach (var pluginType in FilterRowsBySourceScope(GetArtifactArray("plugin-types.json", "plugin_types"), ComponentFamily.PluginType, row => row["plugintypeid"]?.GetValue<string>(), row => row["logical_name"]?.GetValue<string>() ?? row["typename"]?.GetValue<string>()))
        {
            rows.Add(new JsonObject
            {
                ["componenttype"] = 90,
                ["objectid"] = pluginType["plugintypeid"]!.GetValue<string>()
            });
        }

        foreach (var step in FilterRowsBySourceScope(GetArtifactArray("plugin-steps.json", "plugin_steps"), ComponentFamily.PluginStep, row => row["sdkmessageprocessingstepid"]?.GetValue<string>(), row => row["logical_name"]?.GetValue<string>()))
        {
            rows.Add(new JsonObject
            {
                ["componenttype"] = 92,
                ["objectid"] = step["sdkmessageprocessingstepid"]!.GetValue<string>()
            });
        }

        foreach (var image in FilterRowsBySourceScope(GetArtifactArray("plugin-step-images.json", "plugin_step_images"), ComponentFamily.PluginStepImage, row => row["sdkmessageprocessingstepimageid"]?.GetValue<string>(), row => row["logical_name"]?.GetValue<string>()))
        {
            rows.Add(new JsonObject
            {
                ["componenttype"] = 93,
                ["objectid"] = image["sdkmessageprocessingstepimageid"]!.GetValue<string>()
            });
        }

        foreach (var serviceEndpoint in FilterRowsBySourceScope(GetArtifactArray("service-endpoints.json", "service_endpoints"), ComponentFamily.ServiceEndpoint, row => row["serviceendpointid"]?.GetValue<string>(), row => row["logical_name"]?.GetValue<string>() ?? row["name"]?.GetValue<string>()))
        {
            rows.Add(new JsonObject
            {
                ["componenttype"] = 95,
                ["objectid"] = serviceEndpoint["serviceendpointid"]!.GetValue<string>()
            });
        }

        foreach (var connector in FilterRowsBySourceScope(GetArtifactArray("connectors.json", "connectors"), ComponentFamily.Connector, row => row["connectorid"]?.GetValue<string>(), row => row["logical_name"]?.GetValue<string>() ?? row["connectorinternalid"]?.GetValue<string>() ?? row["name"]?.GetValue<string>()))
        {
            rows.Add(new JsonObject
            {
                ["componenttype"] = connector["solution_component_type"]?.GetValue<int>() ?? 371,
                ["objectid"] = connector["connectorid"]!.GetValue<string>()
            });
        }

        foreach (var duplicateRule in FilterRowsBySourceScope(GetArtifactArray("duplicate-rules.json", "duplicate_rules"), ComponentFamily.DuplicateRule, row => row["duplicateruleid"]?.GetValue<string>(), row => row["uniquename"]?.GetValue<string>() ?? row["name"]?.GetValue<string>()))
        {
            rows.Add(new JsonObject
            {
                ["componenttype"] = 44,
                ["objectid"] = duplicateRule["duplicateruleid"]!.GetValue<string>()
            });
        }

        foreach (var routingRule in FilterRowsBySourceScope(GetArtifactArray("routing-rules.json", "routing_rules"), ComponentFamily.RoutingRule, row => row["routingruleid"]?.GetValue<string>(), row => NormalizeLogicalName(row["name"]?.GetValue<string>())))
        {
            rows.Add(new JsonObject
            {
                ["componenttype"] = 150,
                ["objectid"] = routingRule["routingruleid"]!.GetValue<string>()
            });
        }

        foreach (var mobileOfflineProfile in FilterRowsBySourceScope(GetArtifactArray("mobile-offline-profiles.json", "mobile_offline_profiles"), ComponentFamily.MobileOfflineProfile, row => row["mobileofflineprofileid"]?.GetValue<string>(), row => NormalizeLogicalName(row["name"]?.GetValue<string>())))
        {
            rows.Add(new JsonObject
            {
                ["componenttype"] = 161,
                ["objectid"] = mobileOfflineProfile["mobileofflineprofileid"]!.GetValue<string>()
            });
        }

        foreach (var role in FilterRowsBySourceScope(GetArtifactArray("roles.json", "roles"), ComponentFamily.Role, row => row["roleid"]?.GetValue<string>(), row => NormalizeLogicalName(row["name"]?.GetValue<string>())))
        {
            rows.Add(new JsonObject
            {
                ["componenttype"] = 20,
                ["objectid"] = role["roleid"]!.GetValue<string>()
            });
        }

        foreach (var fieldSecurityProfile in FilterRowsBySourceScope(GetArtifactArray("field-security-profiles.json", "field_security_profiles"), ComponentFamily.FieldSecurityProfile, row => row["fieldsecurityprofileid"]?.GetValue<string>(), row => NormalizeLogicalName(row["name"]?.GetValue<string>())))
        {
            rows.Add(new JsonObject
            {
                ["componenttype"] = 70,
                ["objectid"] = fieldSecurityProfile["fieldsecurityprofileid"]!.GetValue<string>()
            });
        }

        foreach (var connectionRole in FilterRowsBySourceScope(GetArtifactArray("connection-roles.json", "connection_roles"), ComponentFamily.ConnectionRole, row => row["connectionroleid"]?.GetValue<string>(), row => NormalizeLogicalName(row["name"]?.GetValue<string>())))
        {
            rows.Add(new JsonObject
            {
                ["componenttype"] = 63,
                ["objectid"] = connectionRole["connectionroleid"]!.GetValue<string>()
            });
        }

        return rows.ToArray();
    }

    private HttpResponseMessage HandleEntityRequest(string entityLogicalName, string path, string query)
    {
        var metadata = GetEntityMetadata(entityLogicalName);
        if (metadata is null)
        {
            return Envelope([]);
        }

        if (path.Contains("/Attributes/Microsoft.Dynamics.CRM.ImageAttributeMetadata", StringComparison.OrdinalIgnoreCase))
        {
            return Envelope(GetEntityImageAttributeRows(entityLogicalName, metadata));
        }

        if (path.Contains("/Attributes/Microsoft.Dynamics.CRM.", StringComparison.OrdinalIgnoreCase))
        {
            return Envelope(GetEntityOptionSetRows(entityLogicalName, metadata, path));
        }

        if (path.EndsWith("/Attributes", StringComparison.OrdinalIgnoreCase))
        {
            return Envelope(metadata["Attributes"] as JsonArray ?? new JsonArray());
        }

        if (path.EndsWith("/ManyToOneRelationships", StringComparison.OrdinalIgnoreCase))
        {
            return Envelope(metadata["Relationships"]?["many_to_one"] as JsonArray ?? new JsonArray());
        }

        if (path.EndsWith("/OneToManyRelationships", StringComparison.OrdinalIgnoreCase))
        {
            return Envelope(metadata["Relationships"]?["one_to_many"] as JsonArray ?? new JsonArray());
        }

        if (path.EndsWith("/ManyToManyRelationships", StringComparison.OrdinalIgnoreCase))
        {
            return Envelope(metadata["Relationships"]?["many_to_many"] as JsonArray ?? new JsonArray());
        }

        if (query.Contains("$select", StringComparison.OrdinalIgnoreCase))
        {
            return JsonResponse(HttpStatusCode.OK, metadata.DeepClone()!);
        }

        return JsonResponse(HttpStatusCode.OK, metadata.DeepClone()!);
    }

    private IEnumerable<JsonObject> FilterRowsBySourceScope(
        JsonArray rows,
        ComponentFamily family,
        Func<JsonObject, string?> idSelector,
        Func<JsonObject, string?> logicalSelector)
    {
        if (_sourceScope is null)
        {
            return rows.OfType<JsonObject>();
        }

        var allowedLogicalNames = _sourceScope.Artifacts
            .Where(artifact => artifact.Family == family)
            .Select(artifact => artifact.LogicalName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var allowedIds = _sourceScope.Artifacts
            .Where(artifact => artifact.Family == family && artifact.Properties is not null)
            .Select(artifact => family switch
            {
                ComponentFamily.Form => artifact.Properties!.TryGetValue(ArtifactPropertyKeys.FormId, out var formId) ? formId : null,
                ComponentFamily.View => artifact.Properties!.TryGetValue(ArtifactPropertyKeys.ViewId, out var viewId) ? viewId : null,
                _ => null
            })
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return rows
            .OfType<JsonObject>()
            .Where(row =>
            {
                var logicalName = logicalSelector(row);
                var id = idSelector(row);
                return (!string.IsNullOrWhiteSpace(logicalName) && allowedLogicalNames.Contains(logicalName!))
                    || (!string.IsNullOrWhiteSpace(id) && allowedIds.Contains(id!));
            });
    }

    private JsonArray GetEntityDefinitionRows()
    {
        var array = new JsonArray();
        foreach (var entity in GetEntityNames())
        {
            var metadata = GetEntityMetadata(entity);
            if (metadata is null)
            {
                continue;
            }

            array.Add(new JsonObject
            {
                ["LogicalName"] = entity,
                ["MetadataId"] = StableGuid($"entity:{entity}").ToString("D")
            });
        }

        return array;
    }

    private JsonArray GetGlobalOptionSets() =>
        GetArtifactArray("global-option-sets.json", "global_option_sets");

    private JsonArray GetAllForms()
    {
        var array = new JsonArray();
        foreach (var entity in GetEntityNames())
        {
            foreach (var form in GetFormsForEntity(entity))
            {
                if (form is not null)
                {
                    array.Add(form.DeepClone());
                }
            }
        }

        return array;
    }

    private JsonArray GetAllViews()
    {
        var array = new JsonArray();
        foreach (var entity in GetEntityNames())
        {
            foreach (var view in GetViewsForEntity(entity))
            {
                if (view is not null)
                {
                    array.Add(view.DeepClone());
                }
            }
        }

        return array;
    }

    private JsonArray GetForms(string query)
    {
        if (query.Contains("objecttypecode eq", StringComparison.OrdinalIgnoreCase))
        {
            var entity = ExtractQuotedValue(query);
            return GetFormsForEntity(entity);
        }

        return GetAllForms();
    }

    private JsonArray GetViews(string query)
    {
        if (query.Contains("returnedtypecode eq", StringComparison.OrdinalIgnoreCase))
        {
            var entity = ExtractQuotedValue(query);
            return GetViewsForEntity(entity);
        }

        return GetAllViews();
    }

    private JsonArray GetEntityOptionSetRows(string entityLogicalName, JsonObject metadata, string path)
    {
        var optionSets = metadata["OptionSets"] as JsonArray ?? new JsonArray();
        var desiredType = path.Contains("BooleanAttributeMetadata", StringComparison.OrdinalIgnoreCase) ? "boolean"
            : path.Contains("StateAttributeMetadata", StringComparison.OrdinalIgnoreCase) ? "state"
            : path.Contains("StatusAttributeMetadata", StringComparison.OrdinalIgnoreCase) ? "status"
            : "picklist";

        var results = new JsonArray();
        foreach (var optionSet in optionSets.OfType<JsonObject>().Where(row => string.Equals(row["option_set_type"]?.GetValue<string>(), desiredType, StringComparison.OrdinalIgnoreCase)))
        {
            var attributeLogicalName = optionSet["attribute_logical_name"]?.GetValue<string>();
            var attribute = metadata["Attributes"]?.AsArray().OfType<JsonObject>().FirstOrDefault(candidate =>
                string.Equals(candidate["LogicalName"]?.GetValue<string>(), attributeLogicalName, StringComparison.OrdinalIgnoreCase));

            var row = attribute?.DeepClone() as JsonObject ?? new JsonObject();
            row["OptionSet"] = optionSet.DeepClone();
            if (row["LogicalName"] is null && !string.IsNullOrWhiteSpace(attributeLogicalName))
            {
                row["LogicalName"] = attributeLogicalName;
            }

            results.Add(row);
        }

        return results;
    }

    private JsonArray GetEntityImageAttributeRows(string entityLogicalName, JsonObject metadata)
    {
        var results = new JsonArray();
        foreach (var attribute in (metadata["Attributes"] as JsonArray ?? new JsonArray()).OfType<JsonObject>())
        {
            var attributeType = attribute["AttributeType"]?.GetValue<string>();
            if (!string.Equals(attributeType, "Image", StringComparison.OrdinalIgnoreCase)
                && attribute["CanStoreFullImage"] is null
                && attribute["IsPrimaryImage"] is null)
            {
                continue;
            }

            results.Add(attribute.DeepClone());
        }

        return results;
    }

    private JsonObject? GetEntityMetadata(string entityLogicalName)
    {
        var path = Path.Combine(
            "C:\\Git\\Dataverse-Solution-KB",
            "fixtures",
            "skill-corpus",
            "examples",
            _fixtureName,
            "readback",
            "entities",
            entityLogicalName,
            "metadata.json");

        if (File.Exists(path))
        {
            return ParseObject(path);
        }

        var entity = _readback["entities"]?[entityLogicalName];
        return entity?.DeepClone() as JsonObject;
    }

    private IEnumerable<string> GetEntityNames()
    {
        var allNames = _readback["entities"] is JsonObject entities
            ? entities.Select(property => property.Key).OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray()
            : Array.Empty<string>();
        if (_sourceScope is null)
        {
            return allNames;
        }

        var sourceEntities = _sourceScope.Artifacts
            .Where(artifact => artifact.Family == ComponentFamily.Table)
            .Select(artifact => artifact.LogicalName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return allNames.Where(sourceEntities.Contains);
    }

    private JsonArray GetFormsForEntity(string? entityLogicalName)
    {
        if (string.IsNullOrWhiteSpace(entityLogicalName))
        {
            return [];
        }

        var path = Path.Combine(
            "C:\\Git\\Dataverse-Solution-KB",
            "fixtures",
            "skill-corpus",
            "examples",
            _fixtureName,
            "readback",
            "entities",
            entityLogicalName,
            "forms.json");

        if (File.Exists(path))
        {
            return ParseArray(path);
        }

        return _readback["entities"]?[entityLogicalName]?["forms"]?.DeepClone() as JsonArray ?? new JsonArray();
    }

    private JsonArray GetViewsForEntity(string? entityLogicalName)
    {
        if (string.IsNullOrWhiteSpace(entityLogicalName))
        {
            return [];
        }

        var path = Path.Combine(
            "C:\\Git\\Dataverse-Solution-KB",
            "fixtures",
            "skill-corpus",
            "examples",
            _fixtureName,
            "readback",
            "entities",
            entityLogicalName,
            "views.json");

        if (File.Exists(path))
        {
            return ParseArray(path);
        }

        return _readback["entities"]?[entityLogicalName]?["views"]?.DeepClone() as JsonArray ?? new JsonArray();
    }

    private JsonArray GetArtifactArray(string artifactFileName, string aggregatePropertyName)
    {
        var root = Path.Combine(
            "C:\\Git\\Dataverse-Solution-KB",
            "fixtures",
            "skill-corpus",
            "examples",
            _fixtureName,
            "readback",
            "artifacts",
            artifactFileName);

        if (File.Exists(root))
        {
            return ParseArray(root);
        }

        return _readback[aggregatePropertyName]?.DeepClone() as JsonArray ?? new JsonArray();
    }

    private static bool TryExtractEntityLogicalName(string path, out string entityLogicalName)
    {
        const string marker = "EntityDefinitions(LogicalName='";
        var start = path.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            entityLogicalName = string.Empty;
            return false;
        }

        start += marker.Length;
        var end = path.IndexOf("')", start, StringComparison.OrdinalIgnoreCase);
        if (end < 0)
        {
            entityLogicalName = string.Empty;
            return false;
        }

        entityLogicalName = path[start..end];
        return true;
    }

    private static string? ExtractQuotedValue(string query)
    {
        var first = query.IndexOf('\'');
        var second = query.IndexOf('\'', first + 1);
        return first >= 0 && second > first ? query[(first + 1)..second] : null;
    }

    private static string? GetAiProjectLogicalName(JsonNode? row) =>
        NormalizeLogicalName(GetJsonObjectString(row, "msdyn_modelcreationcontext", "logicalName"));

    private static string? GetAiConfigurationLogicalName(JsonNode? row) =>
        NormalizeLogicalName(GetJsonObjectString(row, "msdyn_resourceinfo", "logicalName"));

    private static string? GetJsonObjectString(JsonNode? row, string propertyName, string nestedPropertyName)
    {
        var raw = row?[propertyName]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(raw)?[nestedPropertyName]?.GetValue<string>();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? NormalizeLogicalName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().ToLowerInvariant();
    }

    private static JsonObject ParseObject(string path) =>
        JsonNode.Parse(File.ReadAllText(path))?.AsObject()
        ?? throw new InvalidOperationException($"Fixture JSON object '{path}' could not be parsed.");

    private static JsonArray ParseArray(string path) =>
        JsonNode.Parse(File.ReadAllText(path)) switch
        {
            JsonArray array => array,
            JsonObject obj => obj.AsObject().Select(property => property.Value).OfType<JsonArray>().FirstOrDefault()
                ?? new JsonArray(obj.DeepClone()),
            _ => throw new InvalidOperationException($"Fixture JSON array '{path}' could not be parsed.")
        };

    private static Guid StableGuid(string value)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(value));
        return new Guid(bytes);
    }

    private static HttpResponseMessage Envelope(IEnumerable<JsonNode?> rows)
    {
        var array = new JsonArray();
        foreach (var row in rows)
        {
            array.Add(row?.DeepClone());
        }

        return JsonResponse(HttpStatusCode.OK, new JsonObject
        {
            ["value"] = array
        });
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, JsonNode body) =>
        new(statusCode)
        {
            Content = new StringContent(body.ToJsonString())
            {
                Headers =
                {
                    ContentType = new MediaTypeHeaderValue("application/json")
                }
            }
        };
}

internal sealed class FixtureHandler(LiveFixtureHarness harness) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(harness.Handle(request));
}

internal sealed class StaticResponseHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(responder(request));
}

internal sealed class FakeTokenCredential : TokenCredential
{
    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
        new("fixture-token", DateTimeOffset.UtcNow.AddMinutes(5));

    public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
        ValueTask.FromResult(GetToken(requestContext, cancellationToken));
}

internal sealed class ThrowingTokenCredential : TokenCredential
{
    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
        throw new AuthenticationFailedException("Fixture auth failure.");

    public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
        throw new AuthenticationFailedException("Fixture auth failure.");
}
