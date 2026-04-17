using FluentAssertions;
using System.Diagnostics;
using System.Text.Json.Nodes;
using DataverseSolutionCompiler.Domain.Model;
using DataverseSolutionCompiler.Domain.Read;
using DataverseSolutionCompiler.Readers.Xml;
using Xunit;

namespace DataverseSolutionCompiler.UnitTests;

public sealed class XmlSolutionReaderTests
{
    [Fact]
    public void Reader_parses_seed_core_into_typed_schema_artifacts()
    {
        var solution = ReadFixture("seed-core");

        solution.Identity.UniqueName.Should().Be("CodexMetadataSeedCore");
        solution.Publisher.UniqueName.Should().Be("CodexMetadata");

        var table = FindArtifact(solution, ComponentFamily.Table, "cdxmeta_workitem");
        table.Properties![ArtifactPropertyKeys.PrimaryIdAttribute].Should().Be("cdxmeta_workitemid");
        table.Properties![ArtifactPropertyKeys.PrimaryNameAttribute].Should().Be("cdxmeta_workitemname");
        table.Properties![ArtifactPropertyKeys.OwnershipTypeMask].Should().Be("UserOwned");

        var detailsColumn = FindArtifact(solution, ComponentFamily.Column, "cdxmeta_workitem|cdxmeta_details");
        detailsColumn.Properties![ArtifactPropertyKeys.AttributeType].Should().Be("ntext");
        detailsColumn.Properties![ArtifactPropertyKeys.IsSecured].Should().Be("true");

        var relationship = FindArtifact(solution, ComponentFamily.Relationship, "cdxmeta_category_workitem");
        relationship.Properties![ArtifactPropertyKeys.RelationshipType].Should().Be("OneToMany");
        relationship.Properties![ArtifactPropertyKeys.ReferencingEntity].Should().Be("cdxmeta_workitem");

        var globalChoice = FindArtifact(solution, ComponentFamily.OptionSet, "cdxmeta_priorityband");
        globalChoice.Properties![ArtifactPropertyKeys.IsGlobal].Should().Be("true");
        globalChoice.Properties![ArtifactPropertyKeys.OptionCount].Should().Be("3");

        var globalChoiceColumn = FindArtifact(solution, ComponentFamily.Column, "cdxmeta_workitem|cdxmeta_priorityband");
        globalChoiceColumn.Properties![ArtifactPropertyKeys.AttributeType].Should().Be("picklist");
        globalChoiceColumn.Properties![ArtifactPropertyKeys.IsGlobal].Should().Be("true");
        globalChoiceColumn.Properties![ArtifactPropertyKeys.OptionSetName].Should().Be("cdxmeta_priorityband");

        var localChoice = FindArtifact(solution, ComponentFamily.OptionSet, "cdxmeta_workitem|cdxmeta_stage");
        localChoice.Properties![ArtifactPropertyKeys.IsGlobal].Should().Be("false");
        localChoice.Properties![ArtifactPropertyKeys.OptionSetType].Should().Be("picklist");
    }

    [Fact]
    public void Reader_parses_seed_alternate_key_into_typed_schema_detail_artifacts()
    {
        var solution = ReadFixture("seed-alternate-key");

        solution.Identity.UniqueName.Should().Be("CodexMetadataSeedAlternateKey");

        var key = FindArtifact(solution, ComponentFamily.Key, "cdxmeta_workitem|cdxmeta_workitem_externalcode");
        key.DisplayName.Should().Be("Work Item External Code");
        key.Properties![ArtifactPropertyKeys.EntityLogicalName].Should().Be("cdxmeta_workitem");
        key.Properties![ArtifactPropertyKeys.SchemaName].Should().Be("cdxmeta_WorkItem_ExternalCode");
        key.Properties![ArtifactPropertyKeys.KeyAttributesJson].Should().Be("[\"cdxmeta_externalcode\"]");
        key.Properties![ArtifactPropertyKeys.IndexStatus].Should().Be("Active");
    }

    [Fact]
    public void Reader_parses_seed_forms_into_semantic_form_and_view_artifacts()
    {
        var solution = ReadFixture("seed-forms");

        var form = FindArtifact(solution, ComponentFamily.Form, "cdxmeta_workitem|main|c67be8a4-c475-4041-90e6-78e3ed79b018");
        form.Properties![ArtifactPropertyKeys.QuickFormCount].Should().Be("1");
        form.Properties![ArtifactPropertyKeys.SubgridCount].Should().Be("1");
        form.Properties![ArtifactPropertyKeys.HeaderControlCount].Should().Be("3");

        var view = FindArtifact(solution, ComponentFamily.View, "cdxmeta_workitem|Active Work Items");
        view.Properties![ArtifactPropertyKeys.TargetEntity].Should().Be("cdxmeta_workitem");
        view.Properties![ArtifactPropertyKeys.QueryType].Should().Be("0");
        view.Properties![ArtifactPropertyKeys.LayoutColumnsJson].Should().Be("[\"cdxmeta_workitemname\",\"createdon\"]");
    }

    [Fact]
    public void Reader_parses_seed_advanced_ui_app_shell_families()
    {
        var solution = ReadFixture("seed-advanced-ui");

        var appModule = FindArtifact(solution, ComponentFamily.AppModule, "codex_metadata_advanced_ui_924e69cb");
        appModule.Properties![ArtifactPropertyKeys.RoleMapCount].Should().Be("2");
        appModule.Properties![ArtifactPropertyKeys.AppSettingCount].Should().Be("1");

        var appSetting = FindArtifact(solution, ComponentFamily.AppSetting, "codex_metadata_advanced_ui_924e69cb|AppChannel");
        appSetting.Properties![ArtifactPropertyKeys.Value].Should().Be("1");

        var siteMap = FindArtifact(solution, ComponentFamily.SiteMap, "codex_metadata_advanced_ui_924e69cb");
        siteMap.Properties![ArtifactPropertyKeys.AreaCount].Should().Be("1");
        siteMap.Properties![ArtifactPropertyKeys.WebResourceSubAreaCount].Should().Be("1");
        siteMap.Properties![ArtifactPropertyKeys.SiteMapDefinitionJson].Should().Contain("\"webResource\":\"cdxmeta_/advancedui/landing.html\"");
        siteMap.Properties![ArtifactPropertyKeys.SiteMapDefinitionJson].Should().Contain("\"icon\":\"/WebResources/cdxmeta_/advancedui/icon.svg\"");
        siteMap.Properties![ArtifactPropertyKeys.SiteMapDefinitionJson].Should().Contain("\"passParams\":false");
        siteMap.Properties![ArtifactPropertyKeys.SiteMapDefinitionJson].Should().Contain("\"availableOffline\":false");
        siteMap.Properties![ArtifactPropertyKeys.SiteMapDefinitionJson].Should().NotContain("$webresource:");

        var visualization = FindArtifact(solution, ComponentFamily.Visualization, "account|Accounts by Industry");
        visualization.Properties![ArtifactPropertyKeys.TargetEntity].Should().Be("account");
        visualization.Properties![ArtifactPropertyKeys.ChartTypesJson].Should().Be("[\"Bar\"]");

        var webResource = FindArtifact(solution, ComponentFamily.WebResource, "cdxmeta_/advancedui/landing.html");
        webResource.Properties![ArtifactPropertyKeys.AssetSourcePath].Should().Be("WebResources/cdxmeta_/advancedui/landing.html");
        webResource.Properties![ArtifactPropertyKeys.ContentHash].Should().NotBeNullOrWhiteSpace();

        var ribbon = FindArtifact(solution, ComponentFamily.Ribbon, "account");
        ribbon.Properties![ArtifactPropertyKeys.MetadataSourcePath].Should().Be("Entities/Account/RibbonDiff.xml");
        ribbon.Properties![ArtifactPropertyKeys.EntityLogicalName].Should().Be("account");
        ribbon.Properties![ArtifactPropertyKeys.ContentHash].Should().NotBeNullOrWhiteSpace();

        var definition = FindArtifact(solution, ComponentFamily.EnvironmentVariableDefinition, "cdxmeta_AdvancedUiMode");
        definition.Properties![ArtifactPropertyKeys.DefaultValue].Should().Be("scaffold");
        definition.Properties![ArtifactPropertyKeys.ValueSchema].Should().Be("string");

        var value = FindArtifact(solution, ComponentFamily.EnvironmentVariableValue, "cdxmeta_AdvancedUiMode");
        value.Properties![ArtifactPropertyKeys.Value].Should().Be("seed");
    }

    [Fact]
    public void Reader_parses_seed_app_shell_site_map_adjunct_fields()
    {
        var solution = ReadFixture("seed-app-shell");

        var siteMap = FindArtifact(solution, ComponentFamily.SiteMap, "codex_metadata_shell_dd96cf20");
        siteMap.Properties![ArtifactPropertyKeys.SiteMapDefinitionJson].Should().Contain("\"client\":\"Web\"");
        siteMap.Properties![ArtifactPropertyKeys.SiteMapDefinitionJson].Should().Contain("\"passParams\":true");
        siteMap.Properties![ArtifactPropertyKeys.SiteMapDefinitionJson].Should().Contain("\"icon\":\"/WebResources/cdxmeta_/shell/icon.svg\"");
        siteMap.Properties![ArtifactPropertyKeys.SiteMapDefinitionJson].Should().Contain("\"vectorIcon\":\"/WebResources/cdxmeta_/shell/icon.svg\"");
    }

    [Fact]
    public void Reader_parses_supported_site_map_dashboard_targets_with_app_scope()
    {
        const string dashboardId = "3c5d4df8-4c0d-4d57-9e8f-6d4b3a8d5812";
        const string appId = "e1d1df92-5e88-4cff-8562-3d0f3f7164d0";
        var sourceRoot = FixtureRoot("seed-app-shell");
        var tempRoot = Path.Combine(Path.GetTempPath(), $"dsc-site-map-dashboard-{Guid.NewGuid():N}");

        try
        {
            CopyDirectory(sourceRoot, tempRoot);

            var siteMapPath = Path.Combine(tempRoot, "AppModuleSiteMaps", "codex_metadata_shell_dd96cf20", "AppModuleSiteMap.xml");
            var updatedXml = File.ReadAllText(siteMapPath)
                .Replace(
                    "Url=\"$webresource:cdxmeta_/shell/landing.html\"",
                    $"Url=\"/main.aspx?appid={appId}&amp;pagetype=dashboard&amp;id={dashboardId}\"",
                    StringComparison.Ordinal);
            File.WriteAllText(siteMapPath, updatedXml);

            var solution = new XmlSolutionReader().Read(new ReadRequest(tempRoot));
            var siteMap = FindArtifact(solution, ComponentFamily.SiteMap, "codex_metadata_shell_dd96cf20");

            siteMap.Properties![ArtifactPropertyKeys.SiteMapDefinitionJson].Should().Contain($"\"dashboard\":\"{dashboardId}\"");
            siteMap.Properties![ArtifactPropertyKeys.SiteMapDefinitionJson].Should().Contain($"\"appId\":\"{appId}\"");
            siteMap.Properties![ArtifactPropertyKeys.SiteMapDefinitionJson].Should().Contain("\"client\":\"Web\"");
            siteMap.Properties![ArtifactPropertyKeys.SiteMapDefinitionJson].Should().Contain("\"passParams\":true");
            siteMap.Properties![ArtifactPropertyKeys.SiteMapDefinitionJson].Should().NotContain("/main.aspx?appid=");
            siteMap.Properties![ArtifactPropertyKeys.SiteMapDefinitionJson].Should().NotContain("\"webResource\":\"");
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void Reader_parses_supported_site_map_custom_page_targets_with_record_context()
    {
        const string customPage = "cdxmeta_shellhome";
        const string appId = "e1d1df92-5e88-4cff-8562-3d0f3f7164d0";
        const string contextRecordId = "bd7616fe-3f95-4d6a-b4cb-9e788425f721";
        var sourceRoot = FixtureRoot("seed-app-shell");
        var tempRoot = Path.Combine(Path.GetTempPath(), $"dsc-site-map-custom-page-{Guid.NewGuid():N}");

        try
        {
            CopyDirectory(sourceRoot, tempRoot);

            var siteMapPath = Path.Combine(tempRoot, "AppModuleSiteMaps", "codex_metadata_shell_dd96cf20", "AppModuleSiteMap.xml");
            var updatedXml = File.ReadAllText(siteMapPath)
                .Replace(
                    "Url=\"$webresource:cdxmeta_/shell/landing.html\"",
                    $"Url=\"/main.aspx?appid={appId}&amp;pagetype=custom&amp;name={customPage}&amp;entityName=account&amp;recordId=%7B{contextRecordId}%7D\"",
                    StringComparison.Ordinal);
            File.WriteAllText(siteMapPath, updatedXml);

            var solution = new XmlSolutionReader().Read(new ReadRequest(tempRoot));
            var siteMap = FindArtifact(solution, ComponentFamily.SiteMap, "codex_metadata_shell_dd96cf20");

            siteMap.Properties![ArtifactPropertyKeys.SiteMapDefinitionJson].Should().Contain($"\"customPage\":\"{customPage}\"");
            siteMap.Properties![ArtifactPropertyKeys.SiteMapDefinitionJson].Should().Contain("\"customPageEntityName\":\"account\"");
            siteMap.Properties![ArtifactPropertyKeys.SiteMapDefinitionJson].Should().Contain($"\"customPageRecordId\":\"{contextRecordId}\"");
            siteMap.Properties![ArtifactPropertyKeys.SiteMapDefinitionJson].Should().Contain($"\"appId\":\"{appId}\"");
            siteMap.Properties![ArtifactPropertyKeys.SiteMapDefinitionJson].Should().Contain("\"client\":\"Web\"");
            siteMap.Properties![ArtifactPropertyKeys.SiteMapDefinitionJson].Should().NotContain("/main.aspx?appid=");
            siteMap.Properties![ArtifactPropertyKeys.SiteMapDefinitionJson].Should().NotContain("\"webResource\":\"");
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void Reader_parses_supported_site_map_entity_list_targets()
    {
        const string appId = "e1d1df92-5e88-4cff-8562-3d0f3f7164d0";
        const string viewId = "0cc7bf59-5fb4-4f11-a3b2-9170a9d6ef42";
        var sourceRoot = FixtureRoot("seed-app-shell");
        var tempRoot = Path.Combine(Path.GetTempPath(), $"dsc-site-map-entity-list-{Guid.NewGuid():N}");

        try
        {
            CopyDirectory(sourceRoot, tempRoot);

            var siteMapPath = Path.Combine(tempRoot, "AppModuleSiteMaps", "codex_metadata_shell_dd96cf20", "AppModuleSiteMap.xml");
            var updatedXml = File.ReadAllText(siteMapPath)
                .Replace(
                    "Url=\"$webresource:cdxmeta_/shell/landing.html\"",
                    $"Url=\"/main.aspx?appid={appId}&amp;pagetype=entitylist&amp;etn=account&amp;viewid=%7B{viewId}%7D&amp;viewtype=1039\"",
                    StringComparison.Ordinal);
            File.WriteAllText(siteMapPath, updatedXml);

            var solution = new XmlSolutionReader().Read(new ReadRequest(tempRoot));
            var siteMap = FindArtifact(solution, ComponentFamily.SiteMap, "codex_metadata_shell_dd96cf20");

            siteMap.Properties![ArtifactPropertyKeys.SiteMapDefinitionJson].Should().Contain("\"entity\":\"account\"");
            siteMap.Properties![ArtifactPropertyKeys.SiteMapDefinitionJson].Should().Contain($"\"viewId\":\"{viewId}\"");
            siteMap.Properties![ArtifactPropertyKeys.SiteMapDefinitionJson].Should().Contain("\"viewType\":\"savedquery\"");
            siteMap.Properties![ArtifactPropertyKeys.SiteMapDefinitionJson].Should().Contain($"\"appId\":\"{appId}\"");
            siteMap.Properties![ArtifactPropertyKeys.SiteMapDefinitionJson].Should().NotContain("\"url\":\"/main.aspx?appid=");
            siteMap.Properties![ArtifactPropertyKeys.SiteMapDefinitionJson].Should().NotContain("\"webResource\":\"");
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void Reader_parses_supported_site_map_entity_record_targets()
    {
        const string appId = "e1d1df92-5e88-4cff-8562-3d0f3f7164d0";
        const string recordId = "bd7616fe-3f95-4d6a-b4cb-9e788425f721";
        const string formId = "a77ba3f0-df52-46a1-a0a2-2c4fd6e25cdf";
        var sourceRoot = FixtureRoot("seed-app-shell");
        var tempRoot = Path.Combine(Path.GetTempPath(), $"dsc-site-map-entity-record-{Guid.NewGuid():N}");

        try
        {
            CopyDirectory(sourceRoot, tempRoot);

            var siteMapPath = Path.Combine(tempRoot, "AppModuleSiteMaps", "codex_metadata_shell_dd96cf20", "AppModuleSiteMap.xml");
            var updatedXml = File.ReadAllText(siteMapPath)
                .Replace(
                    "Url=\"$webresource:cdxmeta_/shell/landing.html\"",
                    $"Url=\"/main.aspx?appid={appId}&amp;pagetype=entityrecord&amp;etn=account&amp;id=%7B{recordId}%7D&amp;extraqs=formid%3D%7B{formId}%7D\"",
                    StringComparison.Ordinal);
            File.WriteAllText(siteMapPath, updatedXml);

            var solution = new XmlSolutionReader().Read(new ReadRequest(tempRoot));
            var siteMap = FindArtifact(solution, ComponentFamily.SiteMap, "codex_metadata_shell_dd96cf20");

            siteMap.Properties![ArtifactPropertyKeys.SiteMapDefinitionJson].Should().Contain("\"entity\":\"account\"");
            siteMap.Properties![ArtifactPropertyKeys.SiteMapDefinitionJson].Should().Contain($"\"recordId\":\"{recordId}\"");
            siteMap.Properties![ArtifactPropertyKeys.SiteMapDefinitionJson].Should().Contain($"\"formId\":\"{formId}\"");
            siteMap.Properties![ArtifactPropertyKeys.SiteMapDefinitionJson].Should().Contain($"\"appId\":\"{appId}\"");
            siteMap.Properties![ArtifactPropertyKeys.SiteMapDefinitionJson].Should().NotContain("\"url\":\"/main.aspx?appid=");
            siteMap.Properties![ArtifactPropertyKeys.SiteMapDefinitionJson].Should().NotContain("\"webResource\":\"");
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void Reader_preserves_unsupported_site_map_urls_as_canonical_raw_url_boundary()
    {
        const string appId = "e1d1df92-5e88-4cff-8562-3d0f3f7164d0";
        const string dashboardId = "3c5d4df8-4c0d-4d57-9e8f-6d4b3a8d5812";
        const string contextRecordId = "bd7616fe-3f95-4d6a-b4cb-9e788425f721";
        var expectedRawUrl = $"/main.aspx?appid={appId}&extraqs=entityName%3Daccount%26recordId%3D{contextRecordId}&id={dashboardId}&pagetype=dashboard&showWelcome=true";
        var sourceRoot = FixtureRoot("seed-app-shell");
        var tempRoot = Path.Combine(Path.GetTempPath(), $"dsc-site-map-raw-url-{Guid.NewGuid():N}");

        try
        {
            CopyDirectory(sourceRoot, tempRoot);

            var siteMapPath = Path.Combine(tempRoot, "AppModuleSiteMaps", "codex_metadata_shell_dd96cf20", "AppModuleSiteMap.xml");
            var updatedXml = File.ReadAllText(siteMapPath)
                .Replace(
                    "Url=\"$webresource:cdxmeta_/shell/landing.html\"",
                    $"Url=\"/main.aspx?showWelcome=1&amp;id=%7B{dashboardId}%7D&amp;extraqs=recordId%3D%7B{contextRecordId}%7D%26entityName%3DAccount&amp;appid=%7B{appId}%7D&amp;pagetype=dashboard\"",
                    StringComparison.Ordinal);
            File.WriteAllText(siteMapPath, updatedXml);

            var solution = new XmlSolutionReader().Read(new ReadRequest(tempRoot));
            var siteMap = FindArtifact(solution, ComponentFamily.SiteMap, "codex_metadata_shell_dd96cf20");
            var subArea = JsonNode.Parse(siteMap.Properties![ArtifactPropertyKeys.SiteMapDefinitionJson])!["areas"]![0]!["groups"]![0]!["subAreas"]![0]!;

            subArea["url"]!.GetValue<string>().Should().Be(expectedRawUrl);
            siteMap.Properties![ArtifactPropertyKeys.WebResourceSubAreaCount].Should().Be("0");
            subArea["dashboard"].Should().BeNull();
            subArea["customPage"].Should().BeNull();
            subArea["webResource"].Should().BeNull();
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void Reader_parses_seed_environment_canvas_app_metadata_and_assets()
    {
        var solution = ReadFixture("seed-environment");

        var canvasApp = FindArtifact(solution, ComponentFamily.CanvasApp, "cat_overview_3dbf5");
        canvasApp.DisplayName.Should().Be("Overview");
        canvasApp.Properties![ArtifactPropertyKeys.DocumentSourcePath].Should().Be("CanvasApps/cat_overview_3dbf5_DocumentUri.msapp");
        canvasApp.Properties![ArtifactPropertyKeys.BackgroundSourcePath].Should().Be("CanvasApps/cat_overview_3dbf5_BackgroundImageUri");
        canvasApp.Properties![ArtifactPropertyKeys.TagsJson].Should().Contain("primaryFormFactor");
    }

    [Fact]
    public void Reader_parses_seed_import_map_into_typed_environment_configuration_artifacts()
    {
        var solution = ReadFixture("seed-import-map");

        var importMap = FindArtifact(solution, ComponentFamily.ImportMap, "codex_contact_csv_map");
        importMap.DisplayName.Should().Be("Codex Contact CSV Map");
        importMap.Properties![ArtifactPropertyKeys.ImportSource].Should().Be("CSV");
        importMap.Properties![ArtifactPropertyKeys.SourceFormat].Should().Be("text/csv");
        importMap.Properties![ArtifactPropertyKeys.ImportTargetEntity].Should().Be("contact");
        importMap.Properties![ArtifactPropertyKeys.MappingCount].Should().Be("2");

        var fullnameMapping = FindArtifact(solution, ComponentFamily.DataSourceMapping, "codex_contact_csv_map|fullname|fullname|1");
        fullnameMapping.Properties![ArtifactPropertyKeys.ParentImportMapLogicalName].Should().Be("codex_contact_csv_map");
        fullnameMapping.Properties![ArtifactPropertyKeys.SourceEntityName].Should().Be("contacts.csv");
        fullnameMapping.Properties![ArtifactPropertyKeys.SourceAttributeName].Should().Be("fullname");
        fullnameMapping.Properties![ArtifactPropertyKeys.TargetEntityName].Should().Be("contact");
        fullnameMapping.Properties![ArtifactPropertyKeys.TargetAttributeName].Should().Be("fullname");
    }

    [Fact]
    public void Reader_parses_seed_reporting_legacy_into_source_first_artifacts()
    {
        var solution = ReadFixture("seed-reporting-legacy");

        solution.Identity.UniqueName.Should().Be("CodexMetadataSeedReportingLegacy");

        var report = FindArtifact(solution, ComponentFamily.Report, "cdxmeta_account_summary");
        report.DisplayName.Should().Be("Account Summary");
        report.Properties![ArtifactPropertyKeys.MetadataSourcePath].Should().Be("Reports/cdxmeta_account_summary.rdl.data.xml");
        report.Properties![ArtifactPropertyKeys.PackageRelativePath].Should().Be("Reports/cdxmeta_account_summary.rdl.data.xml");
        report.Properties![ArtifactPropertyKeys.AssetSourcePath].Should().Be("Reports/cdxmeta_account_summary.rdl");

        var template = FindArtifact(solution, ComponentFamily.Template, "cdxmeta_welcome_email");
        template.DisplayName.Should().Be("Welcome Email");
        template.Properties![ArtifactPropertyKeys.MetadataSourcePath].Should().Be("Templates/cdxmeta_welcome_email.xml");

        var displayString = FindArtifact(solution, ComponentFamily.DisplayString, "cdxmeta_reporting_labels");
        displayString.DisplayName.Should().Be("Reporting Labels");
        displayString.Properties![ArtifactPropertyKeys.PackageRelativePath].Should().Be("DisplayStrings/cdxmeta_reporting_labels.xml");

        var attachment = FindArtifact(solution, ComponentFamily.Attachment, "cdxmeta_report_payload");
        attachment.DisplayName.Should().Be("Report Payload");
        attachment.Properties![ArtifactPropertyKeys.AssetSourcePath].Should().Be("Attachments/cdxmeta_report_payload.txt");

        var wizard = FindArtifact(solution, ComponentFamily.LegacyAsset, "cdxmeta_onboarding_wizard");
        wizard.DisplayName.Should().Be("Onboarding Wizard");
        wizard.Properties![ArtifactPropertyKeys.MetadataSourcePath].Should().Be("WebWizards/cdxmeta_onboarding_wizard.xml");
    }

    [Fact]
    public void Reader_parses_seed_entity_analytics_into_typed_environment_configuration_artifacts()
    {
        var solution = ReadFixture("seed-entity-analytics");

        var analytics = FindArtifact(solution, ComponentFamily.EntityAnalyticsConfiguration, "contact");
        analytics.DisplayName.Should().Be("contact");
        analytics.Properties![ArtifactPropertyKeys.ParentEntityLogicalName].Should().Be("contact");
        analytics.Properties![ArtifactPropertyKeys.EntityDataSource].Should().Be("dataverse");
        analytics.Properties![ArtifactPropertyKeys.IsEnabledForAdls].Should().Be("true");
        analytics.Properties![ArtifactPropertyKeys.IsEnabledForTimeSeries].Should().Be("false");
    }

    [Fact]
    public void Reader_parses_entity_analytics_from_customizations_export_shape()
    {
        var sourceRoot = FixtureRoot("seed-entity-analytics");
        var tempRoot = Path.Combine(Path.GetTempPath(), $"dsc-entity-analytics-customizations-{Guid.NewGuid():N}");

        try
        {
            CopyDirectory(sourceRoot, tempRoot);

            var analyticsDirectory = Path.Combine(tempRoot, "entityanalyticsconfigs");
            if (Directory.Exists(analyticsDirectory))
            {
                Directory.Delete(analyticsDirectory, recursive: true);
            }

            var otherRoot = Path.Combine(tempRoot, "Other");
            Directory.CreateDirectory(otherRoot);
            File.WriteAllText(
                Path.Combine(otherRoot, "Customizations.xml"),
                """
                <?xml version="1.0" encoding="utf-8"?>
                <ImportExportXml xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
                  <EntityAnalyticsConfigs>
                    <EntityAnalyticsConfig>
                      <parententitylogicalname>contact</parententitylogicalname>
                      <isenabledforadls>1</isenabledforadls>
                      <isenabledfortimeseries>0</isenabledfortimeseries>
                      <entitydatasource></entitydatasource>
                    </EntityAnalyticsConfig>
                  </EntityAnalyticsConfigs>
                </ImportExportXml>
                """);

            var reader = new XmlSolutionReader();
            var solution = reader.Read(new ReadRequest(tempRoot));

            var analytics = FindArtifact(solution, ComponentFamily.EntityAnalyticsConfiguration, "contact");
            analytics.Properties![ArtifactPropertyKeys.ParentEntityLogicalName].Should().Be("contact");
            analytics.Properties![ArtifactPropertyKeys.EntityDataSource].Should().Be("dataverse");
            analytics.Properties![ArtifactPropertyKeys.IsEnabledForAdls].Should().Be("true");
            analytics.Properties![ArtifactPropertyKeys.IsEnabledForTimeSeries].Should().Be("false");
            analytics.SourcePath.Should().EndWith(Path.Combine("Other", "Customizations.xml"));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void Reader_parses_seed_image_config_into_typed_schema_detail_artifacts()
    {
        var solution = ReadFixture("seed-image-config");

        var table = FindArtifact(solution, ComponentFamily.Table, "cdxmeta_photoasset");
        table.Properties![ArtifactPropertyKeys.IsCustomizable].Should().Be("true");

        var caption = FindArtifact(solution, ComponentFamily.Column, "cdxmeta_photoasset|cdxmeta_caption");
        caption.Properties![ArtifactPropertyKeys.IsCustomizable].Should().Be("false");

        var entityImage = FindArtifact(solution, ComponentFamily.ImageConfiguration, "cdxmeta_photoasset|entity-image");
        entityImage.Properties![ArtifactPropertyKeys.PrimaryImageAttribute].Should().Be("cdxmeta_profileimage");
        entityImage.Properties![ArtifactPropertyKeys.ImageConfigurationScope].Should().Be("entity");
        entityImage.Properties![ArtifactPropertyKeys.CanStoreFullImage].Should().Be("true");

        var attributeImage = FindArtifact(solution, ComponentFamily.ImageConfiguration, "cdxmeta_photoasset|cdxmeta_profileimage|attribute-image");
        attributeImage.Properties![ArtifactPropertyKeys.ImageAttributeLogicalName].Should().Be("cdxmeta_profileimage");
        attributeImage.Properties![ArtifactPropertyKeys.ImageConfigurationScope].Should().Be("attribute");
        attributeImage.Properties![ArtifactPropertyKeys.IsPrimaryImage].Should().Be("true");
    }

    [Fact]
    public void Reader_parses_image_configuration_from_customizations_when_entity_xml_omits_it()
    {
        var sourceRoot = FixtureRoot("seed-image-config");
        var tempRoot = Path.Combine(Path.GetTempPath(), $"dsc-image-config-customizations-{Guid.NewGuid():N}");

        try
        {
            CopyDirectory(sourceRoot, tempRoot);

            var entityPath = Path.Combine(tempRoot, "Entities", "cdxmeta_PhotoAsset", "Entity.xml");
            var entityXml = File.ReadAllText(entityPath)
                .Replace("<PrimaryImageAttribute>cdxmeta_profileimage</PrimaryImageAttribute>", string.Empty, StringComparison.Ordinal)
                .Replace("<CanStoreFullImage>1</CanStoreFullImage>", string.Empty, StringComparison.Ordinal)
                .Replace("<IsPrimaryImage>1</IsPrimaryImage>", string.Empty, StringComparison.Ordinal);
            File.WriteAllText(entityPath, entityXml);

            var customizationsPath = Path.Combine(tempRoot, "Other", "Customizations.xml");
            File.WriteAllText(
                customizationsPath,
                """
                <?xml version="1.0" encoding="utf-8"?>
                <ImportExportXml xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
                  <Entities />
                  <EntityImageConfigs>
                    <EntityImageConfig>
                      <parententitylogicalname>cdxmeta_photoasset</parententitylogicalname>
                      <primaryimageattribute>cdxmeta_profileimage</primaryimageattribute>
                    </EntityImageConfig>
                  </EntityImageConfigs>
                  <AttributeImageConfigs>
                    <AttributeImageConfig>
                      <attributelogicalname>cdxmeta_profileimage</attributelogicalname>
                      <parententitylogicalname>cdxmeta_photoasset</parententitylogicalname>
                      <canstorefullimage>1</canstorefullimage>
                    </AttributeImageConfig>
                  </AttributeImageConfigs>
                </ImportExportXml>
                """);

            var reader = new XmlSolutionReader();
            var solution = reader.Read(new ReadRequest(tempRoot));

            var entityImage = FindArtifact(solution, ComponentFamily.ImageConfiguration, "cdxmeta_photoasset|entity-image");
            entityImage.Properties![ArtifactPropertyKeys.PrimaryImageAttribute].Should().Be("cdxmeta_profileimage");
            entityImage.Properties![ArtifactPropertyKeys.CanStoreFullImage].Should().Be("true");

            var attributeImage = FindArtifact(solution, ComponentFamily.ImageConfiguration, "cdxmeta_photoasset|cdxmeta_profileimage|attribute-image");
            attributeImage.Properties![ArtifactPropertyKeys.CanStoreFullImage].Should().Be("true");
            attributeImage.Properties![ArtifactPropertyKeys.IsPrimaryImage].Should().Be("true");
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void Reader_parses_seed_ai_families_into_typed_environment_configuration_artifacts()
    {
        var solution = ReadFixture("seed-ai-families");

        var projectType = FindArtifact(solution, ComponentFamily.AiProjectType, "document_automation");
        projectType.DisplayName.Should().Be("Document Automation");
        projectType.Properties![ArtifactPropertyKeys.Description].Should().Be("Core AI project type for document workflows.");

        var project = FindArtifact(solution, ComponentFamily.AiProject, "invoice_processing");
        project.Properties![ArtifactPropertyKeys.ParentAiProjectTypeLogicalName].Should().Be("document_automation");
        project.Properties![ArtifactPropertyKeys.TargetEntity].Should().Be("invoice");

        var configuration = FindArtifact(solution, ComponentFamily.AiConfiguration, "invoice_processing_training");
        configuration.Properties![ArtifactPropertyKeys.ParentAiProjectLogicalName].Should().Be("invoice_processing");
        configuration.Properties![ArtifactPropertyKeys.ConfigurationKind].Should().Be("training");
        configuration.Properties![ArtifactPropertyKeys.Value].Should().Contain("invoice-train-v1");
    }

    [Fact]
    public void Reader_parses_seed_plugin_registration_into_typed_extensibility_artifacts()
    {
        var solution = ReadFixture("seed-plugin-registration");

        var assembly = FindArtifact(solution, ComponentFamily.PluginAssembly, "Codex.Metadata.Plugins, Version=1.0.0.0, Culture=neutral, PublicKeyToken=9d006cbbfeff5098");
        assembly.DisplayName.Should().Be("Codex.Metadata.Plugins");
        assembly.Properties![ArtifactPropertyKeys.AssemblyFileName].Should().Be("Codex.Metadata.Plugins.dll");
        assembly.Properties![ArtifactPropertyKeys.AssetSourcePath].Should().Be("PluginAssemblies/CodexMetadataPlugins-2F08B2D4-7F38-4B6F-84C8-5AB6FA4B6D10/Codex.Metadata.Plugins.dll");

        var pluginType = FindArtifact(solution, ComponentFamily.PluginType, "Codex.Metadata.Plugins.AccountUpdateTrace");
        pluginType.Properties![ArtifactPropertyKeys.AssemblyFullName].Should().Be("Codex.Metadata.Plugins, Version=1.0.0.0, Culture=neutral, PublicKeyToken=9d006cbbfeff5098");
        pluginType.Properties![ArtifactPropertyKeys.FriendlyName].Should().Be("Account Update Trace");

        var step = FindArtifact(solution, ComponentFamily.PluginStep, "Codex.Metadata.Plugins.AccountUpdateTrace|Update|account|20|0|Account Update Trace Step");
        step.Properties![ArtifactPropertyKeys.MessageName].Should().Be("Update");
        step.Properties![ArtifactPropertyKeys.PrimaryEntity].Should().Be("account");
        step.Properties![ArtifactPropertyKeys.FilteringAttributes].Should().Be("accountnumber,name");

        var image = FindArtifact(solution, ComponentFamily.PluginStepImage, "Codex.Metadata.Plugins.AccountUpdateTrace|Update|account|20|0|Account Update Trace Step|Account PreImage|preimage|0");
        image.Properties![ArtifactPropertyKeys.ParentPluginStepLogicalName].Should().Be("Codex.Metadata.Plugins.AccountUpdateTrace|Update|account|20|0|Account Update Trace Step");
        image.Properties![ArtifactPropertyKeys.MessagePropertyName].Should().Be("Target");
        image.Properties![ArtifactPropertyKeys.SelectedAttributes].Should().Be("accountnumber,name");
    }

    [Fact]
    public void Reader_parses_seed_service_endpoint_connector_into_typed_extensibility_artifacts()
    {
        var solution = ReadFixture("seed-service-endpoint-connector");

        var serviceEndpoint = FindArtifact(solution, ComponentFamily.ServiceEndpoint, "codex_webhook_endpoint");
        serviceEndpoint.DisplayName.Should().Be("codex_webhook_endpoint");
        serviceEndpoint.Properties![ArtifactPropertyKeys.Contract].Should().Be("8");
        serviceEndpoint.Properties![ArtifactPropertyKeys.NamespaceAddress].Should().Be("https://hooks.contoso.example");
        serviceEndpoint.Properties![ArtifactPropertyKeys.EndpointPath].Should().Be("/dataverse/codex");

        var connector = FindArtifact(solution, ComponentFamily.Connector, "shared-offerings-connector");
        connector.DisplayName.Should().Be("Codex Shared Connector");
        connector.Properties![ArtifactPropertyKeys.Name].Should().Be("codex_shared_connector");
        connector.Properties![ArtifactPropertyKeys.ConnectorInternalId].Should().Be("shared-offerings-connector");
        connector.Properties![ArtifactPropertyKeys.ConnectorType].Should().Be("1");
        connector.Properties![ArtifactPropertyKeys.CapabilitiesJson].Should().Be("[\"actions\",\"cloud\"]");
    }

    [Fact]
    public void Reader_parses_seed_process_policy_into_typed_process_policy_artifacts()
    {
        var solution = ReadFixture("seed-process-policy");

        var duplicateRule = FindArtifact(solution, ComponentFamily.DuplicateRule, "dre67df5ba444cf6a6b4092b00952064b3b91ddc3e81f6d3746c2169ae4ed2c367");
        duplicateRule.Properties![ArtifactPropertyKeys.BaseEntityName].Should().Be("account");
        duplicateRule.Properties![ArtifactPropertyKeys.MatchingEntityName].Should().Be("account");
        duplicateRule.Properties![ArtifactPropertyKeys.ExcludeInactiveRecords].Should().Be("true");

        var duplicateRuleCondition = FindArtifact(solution, ComponentFamily.DuplicateRuleCondition, "dre67df5ba444cf6a6b4092b00952064b3b91ddc3e81f6d3746c2169ae4ed2c367|name|name|0");
        duplicateRuleCondition.Properties![ArtifactPropertyKeys.ParentDuplicateRuleLogicalName].Should().Be("dre67df5ba444cf6a6b4092b00952064b3b91ddc3e81f6d3746c2169ae4ed2c367");
        duplicateRuleCondition.Properties![ArtifactPropertyKeys.IgnoreBlankValues].Should().Be("true");

        var routingRule = FindArtifact(solution, ComponentFamily.RoutingRule, "codex metadata routing rule");
        routingRule.Properties![ArtifactPropertyKeys.Description].Should().Be("Neutral routing rule for Dataverse metadata synthesis.");

        var routingRuleItem = FindArtifact(solution, ComponentFamily.RoutingRuleItem, "codex metadata routing rule|route all");
        routingRuleItem.Properties![ArtifactPropertyKeys.ParentRoutingRuleLogicalName].Should().Be("codex metadata routing rule");
        routingRuleItem.Properties![ArtifactPropertyKeys.ConditionXml].Should().Be("<conditions />");

        var mobileOfflineProfile = FindArtifact(solution, ComponentFamily.MobileOfflineProfile, "codex metadata mobile offline profile");
        mobileOfflineProfile.Properties![ArtifactPropertyKeys.Description].Should().Be("Neutral mobile offline profile for Dataverse metadata synthesis.");

        var mobileOfflineProfileItem = FindArtifact(solution, ComponentFamily.MobileOfflineProfileItem, "codex metadata mobile offline profile|account");
        mobileOfflineProfileItem.Properties![ArtifactPropertyKeys.ParentMobileOfflineProfileLogicalName].Should().Be("codex metadata mobile offline profile");
        mobileOfflineProfileItem.Properties![ArtifactPropertyKeys.EntityLogicalName].Should().Be("account");
    }

    [Fact]
    public void Reader_parses_seed_process_security_into_typed_security_artifacts()
    {
        var solution = ReadFixture("seed-process-security");

        var role = FindArtifact(solution, ComponentFamily.Role, "codex metadata seed role");
        role.Properties![ArtifactPropertyKeys.PrivilegeCount].Should().Be("9");

        var rolePrivilege = FindArtifact(solution, ComponentFamily.RolePrivilege, "codex metadata seed role|prvReadPluginAssembly|Global");
        rolePrivilege.Evidence.Should().Be(EvidenceKind.BestEffort);
        rolePrivilege.Properties![ArtifactPropertyKeys.ParentRoleLogicalName].Should().Be("codex metadata seed role");

        var fieldSecurityProfile = FindArtifact(solution, ComponentFamily.FieldSecurityProfile, "codex metadata seed field security");
        fieldSecurityProfile.Properties![ArtifactPropertyKeys.ItemCount].Should().Be("1");

        var fieldPermission = FindArtifact(solution, ComponentFamily.FieldPermission, "codex metadata seed field security|cdxmeta_workitem|cdxmeta_details");
        fieldPermission.Properties![ArtifactPropertyKeys.CanRead].Should().Be("4");
        fieldPermission.Properties![ArtifactPropertyKeys.CanReadUnmasked].Should().Be("0");

        var connectionRole = FindArtifact(solution, ComponentFamily.ConnectionRole, "codex metadata seed connection role");
        connectionRole.Properties![ArtifactPropertyKeys.Category].Should().Be("1");
        connectionRole.Properties![ArtifactPropertyKeys.ObjectTypeMappingsJson].Should().Be("[\"All\"]");
    }

    [Fact]
    public void Reader_parses_classic_workflow_source_backed_artifact()
    {
        var solution = ReadFixture("seed-workflow-classic");

        var workflow = FindArtifact(solution, ComponentFamily.Workflow, "cdxmeta_accountstampworkflow");
        workflow.DisplayName.Should().Be("Codex Metadata Account Stamp Workflow");
        workflow.Properties![ArtifactPropertyKeys.WorkflowId].Should().Be("11111111-1111-1111-1111-111111111111");
        workflow.Properties![ArtifactPropertyKeys.WorkflowKind].Should().Be("workflow");
        workflow.Properties![ArtifactPropertyKeys.PrimaryEntity].Should().Be("account");
        workflow.Properties![ArtifactPropertyKeys.TriggerMessageName].Should().Be("Create");
        workflow.Properties![ArtifactPropertyKeys.PackageRelativePath].Should().Be("Workflows/cdxmeta_AccountStampWorkflow.xaml.data.xml");
        workflow.Properties![ArtifactPropertyKeys.AssetSourceMapJson].Should().Contain("Workflows/cdxmeta_AccountStampWorkflow.xaml");
        workflow.Properties![ArtifactPropertyKeys.XamlHash].Should().NotBeNullOrWhiteSpace();
        workflow.Properties![ArtifactPropertyKeys.ClientDataHash].Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Reader_parses_custom_action_source_backed_artifact_with_action_metadata()
    {
        var solution = ReadFixture("seed-workflow-action");

        var workflow = FindArtifact(solution, ComponentFamily.Workflow, "cdxmeta_accountstampaction");
        workflow.DisplayName.Should().Be("Codex Metadata Account Stamp Action");
        workflow.Properties![ArtifactPropertyKeys.WorkflowKind].Should().Be("customAction");
        workflow.Properties![ArtifactPropertyKeys.PrimaryEntity].Should().Be("account");
        workflow.Properties![ArtifactPropertyKeys.TriggerMessageName].Should().Be("cdxmeta_AccountStampAction");
        workflow.Properties![ArtifactPropertyKeys.WorkflowActionMetadataJson].Should().Contain("\"uniqueName\":\"cdxmeta_AccountStampAction\"");
        workflow.Properties![ArtifactPropertyKeys.WorkflowActionMetadataJson].Should().Contain("\"direction\":\"Out\"");
    }

    [Fact]
    public void Reader_parses_business_process_flow_source_backed_artifact_with_stage_metadata()
    {
        var solution = ReadFixture("seed-workflow-bpf");

        var workflow = FindArtifact(solution, ComponentFamily.Workflow, "cdxmeta_accountsalesflow");
        workflow.DisplayName.Should().Be("Codex Metadata Account Sales Flow");
        workflow.Properties![ArtifactPropertyKeys.WorkflowKind].Should().Be("businessProcessFlow");
        workflow.Properties![ArtifactPropertyKeys.Category].Should().Be("4");
        workflow.Properties![ArtifactPropertyKeys.BusinessProcessType].Should().Be("0");
        workflow.Properties![ArtifactPropertyKeys.ProcessOrder].Should().Be("1");
        workflow.Properties![ArtifactPropertyKeys.ProcessStagesJson].Should().Contain("Qualify");
        workflow.Properties![ArtifactPropertyKeys.ProcessStagesJson].Should().Contain("Develop");
        workflow.Properties![ArtifactPropertyKeys.ProcessStagesJson].Should().Contain("Close");
        workflow.Properties![ArtifactPropertyKeys.PackageRelativePath].Should().Be("Workflows/cdxmeta_AccountSalesFlow.xaml.data.xml");
    }

    [Fact]
    public void Zip_reader_delegates_to_the_same_typed_parser()
    {
        var sourceRoot = FixtureRoot("seed-core");
        var zipPath = Path.Combine(Path.GetTempPath(), $"seed-core-{Guid.NewGuid():N}.zip");

        try
        {
            System.IO.Compression.ZipFile.CreateFromDirectory(sourceRoot, zipPath);

            var reader = new XmlSolutionReader();
            var solution = reader.Read(new ReadRequest(zipPath, ReadSourceKind.PackedZip));

            solution.Artifacts.Should().Contain(artifact => artifact.Family == ComponentFamily.Table && artifact.LogicalName == "cdxmeta_workitem");
            solution.Artifacts.Should().Contain(artifact => artifact.Family == ComponentFamily.OptionSet && artifact.LogicalName == "cdxmeta_priorityband");
        }
        finally
        {
            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }
        }
    }

    [Fact]
    public void Reader_normalizes_classic_export_zip_when_pac_is_available()
    {
        if (!IsPacAvailable())
        {
            return;
        }

        var reader = new XmlSolutionReader();
        var solution = reader.Read(new ReadRequest(Path.Combine(
            "C:\\Git\\Dataverse-Solution-KB",
            "fixtures",
            "skill-corpus",
            "examples",
            "seed-core",
            "export",
            "CodexMetadataSeedCore.zip"),
            ReadSourceKind.PackedZip));

        solution.Artifacts.Should().Contain(artifact => artifact.Family == ComponentFamily.Table && artifact.LogicalName == "cdxmeta_workitem");
        solution.Diagnostics.Should().Contain(diagnostic => diagnostic.Code == "zip-reader-normalized-classic-export");
    }

    [Fact]
    public void Reader_parses_source_only_similarity_rule_and_sla_families_as_best_effort()
    {
        var slaSolution = ReadFixture("source-only-sla");
        var similaritySolution = ReadFixture("source-only-similarity-rule");

        var similarityRule = FindArtifact(similaritySolution, ComponentFamily.SimilarityRule, "codex metadata account similarity rule");
        similarityRule.Evidence.Should().Be(EvidenceKind.BestEffort);
        similarityRule.Properties![ArtifactPropertyKeys.BaseEntityName].Should().Be("account");
        similarityRule.Properties![ArtifactPropertyKeys.MaxKeywords].Should().Be("5");

        var sla = FindArtifact(slaSolution, ComponentFamily.Sla, "codex metadata account sla");
        sla.Evidence.Should().Be(EvidenceKind.BestEffort);
        sla.Properties![ArtifactPropertyKeys.ApplicableFrom].Should().Be("createdon");

        var slaItem = FindArtifact(slaSolution, ComponentFamily.SlaItem, "codex metadata account sla|codex metadata account sla item|account");
        slaItem.Evidence.Should().Be(EvidenceKind.BestEffort);
        slaItem.Properties![ArtifactPropertyKeys.ParentSlaLogicalName].Should().Be("codex metadata account sla");
        slaItem.Properties![ArtifactPropertyKeys.ActionFlowUniqueName].Should().Be("cdxmeta_source_only_sla_action");
    }

    private static CanonicalSolution ReadFixture(string fixtureName)
    {
        var reader = new XmlSolutionReader();
        return reader.Read(new ReadRequest(FixtureRoot(fixtureName)));
    }

    private static string FixtureRoot(string fixtureName) =>
        Path.Combine(
            "C:\\Git\\Dataverse-Solution-KB",
            "fixtures",
            "skill-corpus",
            "examples",
            fixtureName,
            "unpacked");

    private static FamilyArtifact FindArtifact(CanonicalSolution solution, ComponentFamily family, string logicalName) =>
        solution.Artifacts.Single(artifact =>
            artifact.Family == family
            && string.Equals(artifact.LogicalName, logicalName, StringComparison.OrdinalIgnoreCase));

    private static bool IsPacAvailable()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "pac",
                ArgumentList = { "--version" },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return false;
            }

            process.WaitForExit(5000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static void CopyDirectory(string sourceRoot, string destinationRoot)
    {
        Directory.CreateDirectory(destinationRoot);

        foreach (var directory in Directory.GetDirectories(sourceRoot, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(destinationRoot, Path.GetRelativePath(sourceRoot, directory)));
        }

        foreach (var file in Directory.GetFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            var destinationPath = Path.Combine(destinationRoot, Path.GetRelativePath(sourceRoot, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.Copy(file, destinationPath, overwrite: true);
        }
    }
}
