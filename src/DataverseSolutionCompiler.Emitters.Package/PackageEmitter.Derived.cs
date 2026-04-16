using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using DataverseSolutionCompiler.Domain.Diagnostics;
using DataverseSolutionCompiler.Domain.Emission;
using DataverseSolutionCompiler.Domain.Model;

namespace DataverseSolutionCompiler.Emitters.Package;

public sealed partial class PackageEmitter
{
    private static readonly JsonSerializerOptions DerivedJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly XNamespace XsiNamespace = "http://www.w3.org/2001/XMLSchema-instance";
    private const string DefaultAppModuleIconWebResourceId = "953b9fac-1e5e-e611-80d6-00155ded156f";
    private const string QuickViewControlClassId = "{5C5600E0-1D6E-4205-A272-BE80DA87FD42}";
    private const string SubgridControlClassId = "{E7A81278-8635-4d9e-8D4D-59480B391C5B}";

    private static void WriteDerivedPackageInputTree(
        CanonicalSolution model,
        string packageRoot,
        List<EmittedArtifact> emittedFiles,
        List<CompilerDiagnostic> diagnostics)
    {
        WriteSolutionFiles(model, packageRoot, emittedFiles);
        WriteDerivedEntityFiles(model, packageRoot, emittedFiles);
        WriteDerivedRelationshipFiles(model, packageRoot, emittedFiles);
        WriteDerivedGlobalOptionSets(model, packageRoot, emittedFiles);
        WriteDerivedVisualizations(model, packageRoot, emittedFiles);
        WriteDerivedAppModules(model, packageRoot, emittedFiles);
        WriteDerivedEnvironmentVariables(model, packageRoot, emittedFiles);

        diagnostics.Add(new CompilerDiagnostic(
            "package-emitter-deployment-settings-omitted",
            DiagnosticSeverity.Info,
            "Deployment settings were omitted for derived JSON intent because JSON v1 intentionally does not author deployment-settings output.",
            packageRoot));
    }

    private static void WriteSolutionFiles(CanonicalSolution model, string packageRoot, List<EmittedArtifact> emittedFiles)
    {
        var rootComponents = BuildRootComponents(model);
        var manifest = new XElement("ImportExportXml",
            new XAttribute("version", "9.2.26033.170"),
            new XAttribute("SolutionPackageVersion", "9.2"),
            new XAttribute("languagecode", "1033"),
            new XAttribute("generatedBy", "CrmLive"),
            new XAttribute(XNamespace.Xmlns + "xsi", XsiNamespace),
            new XAttribute("OrganizationVersion", "9.2.26033.170"),
            new XAttribute("OrganizationSchemaType", "Standard"),
            new XAttribute("CRMServerServiceabilityVersion", "9.2.26033.00170"),
            new XElement("SolutionManifest",
                new XElement("UniqueName", model.Identity.UniqueName),
                CreateLocalizedNames(model.Identity.DisplayName),
                CreateDescriptions(GetSolutionDescription(model)),
                new XElement("Version", model.Identity.Version),
                new XElement("Managed", model.Identity.LayeringIntent == LayeringIntent.ManagedRelease ? "1" : "0"),
                CreatePublisherElement(model),
                new XElement("RootComponents", rootComponents),
                new XElement("MissingDependencies")));

        WriteXml(
            packageRoot,
            "Other/Solution.xml",
            manifest,
            emittedFiles,
            "Synthesized unpacked solution manifest for derived compiler intent.");

        WriteXml(
            packageRoot,
            "Other/Customizations.xml",
            new XElement("ImportExportXml",
                new XAttribute(XNamespace.Xmlns + "xsi", XsiNamespace),
                new XAttribute("OrganizationVersion", "9.2.26033.170"),
                new XAttribute("OrganizationSchemaType", "Standard"),
                new XAttribute("CRMServerServiceabilityVersion", "9.2.26033.00170"),
                new XElement("Entities"),
                new XElement("Roles"),
                new XElement("Workflows"),
                new XElement("FieldSecurityProfiles"),
                new XElement("Templates"),
                new XElement("EntityMaps"),
                BuildCustomizationsEntityRelationshipsElement(model),
                new XElement("OrganizationSettings"),
                new XElement("optionsets"),
                BuildOptionalCustomizationsComponentShells(model),
                new XElement("SavedQueryVisualizations"),
                new XElement("WebResources"),
                new XElement("CustomControls"),
                new XElement("AppModuleSiteMaps"),
                new XElement("AppModules"),
                new XElement("EntityDataProviders"),
                new XElement("Languages", new XElement("Language", "1033"))),
            emittedFiles,
            "Minimal customizations shell for derived compiler intent.");
    }

    private static IEnumerable<XElement> BuildOptionalCustomizationsComponentShells(CanonicalSolution model)
    {
        if (HasFamily(model, ComponentFamily.CanvasApp))
        {
            yield return new XElement("CanvasApps");
        }

        if (HasFamily(model, ComponentFamily.ServiceEndpoint))
        {
            yield return new XElement("ServiceEndpoints");
        }

        if (HasFamily(model, ComponentFamily.Connector))
        {
            yield return new XElement("Connectors");
        }

        if (HasAnyFamily(model, ComponentFamily.RoutingRule, ComponentFamily.RoutingRuleItem))
        {
            yield return new XElement("RoutingRules");
        }

        if (HasAnyFamily(model, ComponentFamily.MobileOfflineProfile, ComponentFamily.MobileOfflineProfileItem))
        {
            yield return new XElement("MobileOfflineProfiles");
        }
    }

    private static bool HasFamily(CanonicalSolution model, ComponentFamily family) =>
        model.Artifacts.Any(artifact => artifact.Family == family);

    private static bool HasAnyFamily(CanonicalSolution model, params ComponentFamily[] families) =>
        model.Artifacts.Any(artifact => families.Contains(artifact.Family));

    private static IEnumerable<XElement> BuildRootComponents(CanonicalSolution model)
    {
        foreach (var table in model.Artifacts.Where(artifact => artifact.Family == ComponentFamily.Table).OrderBy(artifact => artifact.LogicalName, StringComparer.OrdinalIgnoreCase))
        {
            yield return new XElement("RootComponent",
                new XAttribute("type", "1"),
                new XAttribute("schemaName", table.LogicalName),
                new XAttribute("behavior", "0"));
        }

        foreach (var key in model.Artifacts.Where(artifact => artifact.Family == ComponentFamily.Key).OrderBy(artifact => artifact.LogicalName, StringComparer.OrdinalIgnoreCase))
        {
            yield return new XElement("RootComponent",
                new XAttribute("type", "14"),
                new XAttribute("schemaName", GetProperty(key, ArtifactPropertyKeys.SchemaName) ?? key.LogicalName.Split('|').Last()),
                new XAttribute("behavior", "0"));
        }

        foreach (var optionSet in model.Artifacts.Where(artifact => artifact.Family == ComponentFamily.OptionSet && string.Equals(GetProperty(artifact, ArtifactPropertyKeys.IsGlobal), "true", StringComparison.OrdinalIgnoreCase)).OrderBy(artifact => artifact.LogicalName, StringComparer.OrdinalIgnoreCase))
        {
            yield return new XElement("RootComponent",
                new XAttribute("type", "9"),
                new XAttribute("schemaName", optionSet.LogicalName),
                new XAttribute("behavior", "0"));
        }

        foreach (var appModule in model.Artifacts.Where(artifact => artifact.Family == ComponentFamily.AppModule).OrderBy(artifact => artifact.LogicalName, StringComparer.OrdinalIgnoreCase))
        {
            yield return new XElement("RootComponent",
                new XAttribute("type", "80"),
                new XAttribute("schemaName", appModule.LogicalName),
                new XAttribute("behavior", "0"));
        }

        foreach (var siteMap in model.Artifacts.Where(artifact => artifact.Family == ComponentFamily.SiteMap).OrderBy(artifact => artifact.LogicalName, StringComparer.OrdinalIgnoreCase))
        {
            yield return new XElement("RootComponent",
                new XAttribute("type", "62"),
                new XAttribute("schemaName", siteMap.LogicalName),
                new XAttribute("behavior", "0"));
        }

    }

    private static void WriteDerivedEntityFiles(CanonicalSolution model, string packageRoot, List<EmittedArtifact> emittedFiles)
    {
        var allColumns = model.Artifacts.Where(artifact => artifact.Family == ComponentFamily.Column).ToArray();
        var localOptionSets = model.Artifacts
            .Where(artifact => artifact.Family == ComponentFamily.OptionSet && !string.Equals(GetProperty(artifact, ArtifactPropertyKeys.IsGlobal), "true", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(artifact => artifact.LogicalName, artifact => artifact, StringComparer.OrdinalIgnoreCase);

        foreach (var table in model.Artifacts.Where(artifact => artifact.Family == ComponentFamily.Table).OrderBy(artifact => artifact.LogicalName, StringComparer.OrdinalIgnoreCase))
        {
            var entityLogicalName = GetProperty(table, ArtifactPropertyKeys.EntityLogicalName) ?? table.LogicalName;
            var entitySchemaName = GetProperty(table, ArtifactPropertyKeys.SchemaName) ?? table.LogicalName;
            var primaryImageAttribute = GetProperty(table, ArtifactPropertyKeys.PrimaryImageAttribute);
            var entityDirectory = $"Entities/{entitySchemaName}";
            var columns = allColumns
                .Where(column => string.Equals(GetProperty(column, ArtifactPropertyKeys.EntityLogicalName), entityLogicalName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(column => GetProperty(column, ArtifactPropertyKeys.SchemaName) ?? column.LogicalName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var keys = model.Artifacts
                .Where(artifact => artifact.Family == ComponentFamily.Key
                    && string.Equals(GetProperty(artifact, ArtifactPropertyKeys.EntityLogicalName), entityLogicalName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(artifact => GetProperty(artifact, ArtifactPropertyKeys.SchemaName) ?? artifact.LogicalName, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var entityXml = new XElement("Entity",
                new XAttribute(XNamespace.Xmlns + "xsi", XsiNamespace),
                new XElement("Name",
                    new XAttribute("LocalizedName", table.DisplayName ?? entitySchemaName),
                    new XAttribute("OriginalName", table.DisplayName ?? entitySchemaName),
                    entitySchemaName),
                new XElement("EntityInfo",
                    new XElement("entity",
                        new XAttribute("Name", entitySchemaName),
                        CreateLocalizedNames(table.DisplayName ?? entitySchemaName),
                        new XElement("LocalizedCollectionNames",
                            new XElement("LocalizedCollectionName",
                                new XAttribute("description", $"{table.DisplayName ?? entitySchemaName}s"),
                                new XAttribute("languagecode", "1033"))),
                        CreateDescriptions(GetProperty(table, ArtifactPropertyKeys.Description)),
                        new XElement("EntitySetName", GetProperty(table, ArtifactPropertyKeys.EntitySetName) ?? $"{entityLogicalName}s"),
                        string.IsNullOrWhiteSpace(primaryImageAttribute) ? null : new XElement("PrimaryImageAttribute", primaryImageAttribute),
                        new XElement("IsDuplicateCheckSupported", "0"),
                        new XElement("IsBusinessProcessEnabled", "0"),
                        new XElement("IsRequiredOffline", "0"),
                        new XElement("IsInteractionCentricEnabled", "0"),
                        new XElement("IsCollaboration", "0"),
                        new XElement("AutoRouteToOwnerQueue", "0"),
                        new XElement("IsConnectionsEnabled", "0"),
                        new XElement("IsDocumentManagementEnabled", "0"),
                        new XElement("AutoCreateAccessTeams", "0"),
                        new XElement("IsOneNoteIntegrationEnabled", "0"),
                        new XElement("IsKnowledgeManagementEnabled", "0"),
                        new XElement("IsSLAEnabled", "0"),
                        new XElement("IsDocumentRecommendationsEnabled", "0"),
                        new XElement("IsBPFEntity", "0"),
                        new XElement("OwnershipTypeMask", GetProperty(table, ArtifactPropertyKeys.OwnershipTypeMask) ?? "UserOwned"),
                        new XElement("IsAuditEnabled", "0"),
                        new XElement("IsRetrieveAuditEnabled", "0"),
                        new XElement("IsRetrieveMultipleAuditEnabled", "0"),
                        new XElement("IsActivity", "0"),
                        new XElement("ActivityTypeMask"),
                        new XElement("IsActivityParty", "0"),
                        new XElement("IsReplicated", "0"),
                        new XElement("IsReplicationUserFiltered", "0"),
                        new XElement("IsMailMergeEnabled", "0"),
                        new XElement("IsVisibleInMobile", "0"),
                        new XElement("IsVisibleInMobileClient", "0"),
                        new XElement("IsReadOnlyInMobileClient", "0"),
                        new XElement("IsOfflineInMobileClient", "0"),
                        new XElement("DaysSinceRecordLastModified", "0"),
                        new XElement("MobileOfflineFilters"),
                        new XElement("IsMapiGridEnabled", "1"),
                        new XElement("IsReadingPaneEnabled", "1"),
                        new XElement("IsQuickCreateEnabled", "0"),
                        new XElement("SyncToExternalSearchIndex", "0"),
                        new XElement("IntroducedVersion", "1.0.0.0"),
                        new XElement("IsCustomizable", string.Equals(GetProperty(table, ArtifactPropertyKeys.IsCustomizable), "true", StringComparison.OrdinalIgnoreCase) ? "1" : "0"),
                        new XElement("IsRenameable", "1"),
                        new XElement("IsMappable", "1"),
                        new XElement("CanModifyAuditSettings", "1"),
                        new XElement("CanModifyMobileVisibility", "1"),
                        new XElement("CanModifyMobileClientVisibility", "1"),
                        new XElement("CanModifyMobileClientReadOnly", "1"),
                        new XElement("CanModifyMobileClientOffline", "1"),
                        new XElement("CanModifyConnectionSettings", "1"),
                        new XElement("CanModifyDuplicateDetectionSettings", "1"),
                        new XElement("CanModifyMailMergeSettings", "1"),
                        new XElement("CanModifyQueueSettings", "1"),
                        new XElement("CanCreateAttributes", "1"),
                        new XElement("CanCreateForms", "1"),
                        new XElement("CanCreateCharts", "1"),
                        new XElement("CanCreateViews", "1"),
                        new XElement("CanModifyAdditionalSettings", "1"),
                        new XElement("CanEnableSyncToExternalSearchIndex", "1"),
                        new XElement("EnforceStateTransitions", "0"),
                        new XElement("CanChangeHierarchicalRelationship", "1"),
                        new XElement("EntityHelpUrlEnabled", "0"),
                        new XElement("ChangeTrackingEnabled", "0"),
                        new XElement("CanChangeTrackingBeEnabled", "1"),
                        new XElement("IsEnabledForExternalChannels", "0"),
                        new XElement("IsMSTeamsIntegrationEnabled", "0"),
                        new XElement("IsSolutionAware", "0"),
                        new XElement("attributes", columns.Select(column => BuildAttributeElement(column, localOptionSets))),
                        keys.Length == 0 ? null : new XElement("keys", keys.Select(BuildEntityKeyElement)))),
                new XElement("FormXml"),
                new XElement("SavedQueries"),
                new XElement("RibbonDiffXml"));

            WriteXml(
                packageRoot,
                $"{entityDirectory}/Entity.xml",
                entityXml,
                emittedFiles,
                $"Synthesized entity metadata for {entityLogicalName}.");

            foreach (var form in model.Artifacts.Where(artifact => artifact.Family == ComponentFamily.Form
                && string.Equals(GetProperty(artifact, ArtifactPropertyKeys.EntityLogicalName), entityLogicalName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(artifact => artifact.LogicalName, StringComparer.OrdinalIgnoreCase))
            {
                WriteDerivedFormFile(packageRoot, entityDirectory, form, columns, emittedFiles);
            }

            foreach (var view in model.Artifacts.Where(artifact => artifact.Family == ComponentFamily.View
                && string.Equals(GetProperty(artifact, ArtifactPropertyKeys.EntityLogicalName), entityLogicalName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(artifact => artifact.LogicalName, StringComparer.OrdinalIgnoreCase))
            {
                WriteDerivedViewFile(packageRoot, entityDirectory, view, table, emittedFiles);
            }
        }
    }

    private static XElement BuildAttributeElement(FamilyArtifact column, IReadOnlyDictionary<string, FamilyArtifact> localOptionSets)
    {
        var logicalName = column.LogicalName.Split('|').Last();
        var schemaName = GetProperty(column, ArtifactPropertyKeys.SchemaName) ?? logicalName;
        var attributeType = NormalizeGeneratedAttributeType(GetProperty(column, ArtifactPropertyKeys.AttributeType));
        if (string.Equals(attributeType, "image", StringComparison.OrdinalIgnoreCase))
        {
            return BuildImageAttributeElement(column, logicalName, schemaName);
        }

        var isPrimaryKey = string.Equals(GetProperty(column, ArtifactPropertyKeys.IsPrimaryKey), "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(attributeType, "primarykey", StringComparison.OrdinalIgnoreCase);
        var isPrimaryName = string.Equals(GetProperty(column, ArtifactPropertyKeys.IsPrimaryName), "true", StringComparison.OrdinalIgnoreCase);
        var element = new XElement("attribute",
            new XAttribute("PhysicalName", schemaName),
            new XElement("Type", attributeType),
            new XElement("Name", logicalName),
            new XElement("LogicalName", logicalName),
            new XElement("RequiredLevel", isPrimaryKey ? "systemrequired" : "none"),
            new XElement("DisplayMask", BuildDisplayMask(column)),
            new XElement("ImeMode", "auto"),
            new XElement("ValidForUpdateApi", isPrimaryKey ? "0" : "1"),
            new XElement("ValidForReadApi", "1"),
            new XElement("ValidForCreateApi", "1"),
            new XElement("IsCustomField", string.Equals(GetProperty(column, ArtifactPropertyKeys.IsCustomField), "true", StringComparison.OrdinalIgnoreCase) ? "1" : "0"),
            new XElement("IsAuditEnabled", isPrimaryKey ? "0" : "1"),
            new XElement("IsSecured", string.Equals(GetProperty(column, ArtifactPropertyKeys.IsSecured), "true", StringComparison.OrdinalIgnoreCase) ? "1" : "0"),
            new XElement("IsCustomizable", string.Equals(GetProperty(column, ArtifactPropertyKeys.IsCustomizable), "true", StringComparison.OrdinalIgnoreCase) ? "1" : "0"),
            new XElement("IsRenameable", "1"),
            new XElement("CanModifySearchSettings", "1"),
            new XElement("CanModifyRequirementLevelSettings", isPrimaryKey ? "0" : "1"),
            new XElement("CanModifyAdditionalSettings", "1"),
            new XElement("SourceType", "0"),
            new XElement("IsGlobalFilterEnabled", "0"),
            new XElement("IsSortableEnabled", "0"),
            new XElement("CanModifyGlobalFilterSettings", "1"),
            new XElement("CanModifyIsSortableSettings", "1"),
            new XElement("IsDataSourceSecret", "0"),
            new XElement("AutoNumberFormat"),
            new XElement("IsSearchable", isPrimaryName ? "1" : "0"),
            new XElement("IsFilterable", isPrimaryKey ? "1" : "0"),
            new XElement("IsRetrievable", "1"),
            new XElement("IsLocalizable", "0"),
            new XElement("IsLogical", string.Equals(GetProperty(column, ArtifactPropertyKeys.IsLogical), "true", StringComparison.OrdinalIgnoreCase) ? "1" : "0"),
            new XElement("IntroducedVersion", "1.0.0.0"),
            new XElement("displaynames",
                new XElement("displayname",
                    new XAttribute("description", column.DisplayName ?? schemaName),
                    new XAttribute("languagecode", "1033"))),
            CreateDescriptions(GetProperty(column, ArtifactPropertyKeys.Description)));

        if (string.Equals(attributeType, "nvarchar", StringComparison.OrdinalIgnoreCase))
        {
            element.Add(
                new XElement("Format", "text"),
                new XElement("MaxLength", isPrimaryName ? "200" : "100"),
                new XElement("Length", isPrimaryName ? "400" : "200"));
        }
        else if (string.Equals(attributeType, "ntext", StringComparison.OrdinalIgnoreCase))
        {
            element.Add(
                new XElement("Format", "textarea"),
                new XElement("MaxLength", "2000"));
        }
        else if (string.Equals(attributeType, "int", StringComparison.OrdinalIgnoreCase))
        {
            element.Add(
                new XElement("Format", "none"),
                new XElement("MinValue", "-2147483648"),
                new XElement("MaxValue", "2147483647"));
        }
        if (localOptionSets.TryGetValue(column.LogicalName, out var localOptionSet))
        {
            element.Add(BuildLocalOptionSetElement(localOptionSet));
        }
        else if (string.Equals(GetProperty(column, ArtifactPropertyKeys.IsGlobal), "true", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(GetProperty(column, ArtifactPropertyKeys.OptionSetName)))
        {
            element.Add(new XElement("OptionSetName", GetProperty(column, ArtifactPropertyKeys.OptionSetName)!));
        }

        return element;
    }

    private static XElement BuildImageAttributeElement(FamilyArtifact column, string logicalName, string schemaName) =>
        new("attribute",
            new XAttribute("PhysicalName", schemaName),
            new XElement("Type", "image"),
            new XElement("Name", schemaName),
            new XElement("LogicalName", logicalName),
            new XElement("DisplayMask", BuildDisplayMask(column)),
            new XElement("IsCustomField", string.Equals(GetProperty(column, ArtifactPropertyKeys.IsCustomField), "true", StringComparison.OrdinalIgnoreCase) ? "1" : "0"),
            new XElement("IsSecured", string.Equals(GetProperty(column, ArtifactPropertyKeys.IsSecured), "true", StringComparison.OrdinalIgnoreCase) ? "1" : "0"),
            new XElement("IsCustomizable", string.Equals(GetProperty(column, ArtifactPropertyKeys.IsCustomizable), "true", StringComparison.OrdinalIgnoreCase) ? "1" : "0"),
            new XElement("CanStoreFullImage", string.Equals(GetProperty(column, ArtifactPropertyKeys.CanStoreFullImage), "true", StringComparison.OrdinalIgnoreCase) ? "1" : "0"),
            new XElement("IsPrimaryImage", string.Equals(GetProperty(column, ArtifactPropertyKeys.IsPrimaryImage), "true", StringComparison.OrdinalIgnoreCase) ? "1" : "0"),
            new XElement("displaynames",
                new XElement("displayname",
                    new XAttribute("description", column.DisplayName ?? schemaName),
                    new XAttribute("languagecode", "1033"))),
            CreateDescriptions(GetProperty(column, ArtifactPropertyKeys.Description)));

    private static XElement BuildEntityKeyElement(FamilyArtifact key)
    {
        var schemaName = GetProperty(key, ArtifactPropertyKeys.SchemaName) ?? key.LogicalName.Split('|').Last();
        var logicalName = key.LogicalName.Split('|').Last();
        var keyAttributes = JsonSerializer.Deserialize<List<string>>(GetProperty(key, ArtifactPropertyKeys.KeyAttributesJson) ?? "[]", DerivedJsonOptions) ?? [];

        return new XElement("key",
            new XAttribute("Name", schemaName),
            new XElement("LogicalName", logicalName),
            new XElement("SchemaName", schemaName),
            new XElement("displaynames",
                new XElement("displayname",
                    new XAttribute("description", key.DisplayName ?? schemaName),
                    new XAttribute("languagecode", "1033"))),
            CreateDescriptions(GetProperty(key, ArtifactPropertyKeys.Description)),
            new XElement("KeyAttributes", keyAttributes.Select(attribute => new XElement("KeyAttribute", attribute))),
            new XElement("EntityKeyIndexStatus", GetProperty(key, ArtifactPropertyKeys.IndexStatus) ?? "Active"));
    }

    private static XElement BuildLocalOptionSetElement(FamilyArtifact optionSet)
    {
        var optionsElement = ParseOptionElements(optionSet);
        return new XElement("optionset",
            new XAttribute("Name", GetProperty(optionSet, ArtifactPropertyKeys.OptionSetName) ?? optionSet.LogicalName.Split('|').Last()),
            new XElement("OptionSetType", GetProperty(optionSet, ArtifactPropertyKeys.OptionSetType) ?? "picklist"),
            new XElement("IntroducedVersion", "1.0.0.0"),
            new XElement("IsCustomizable", "1"),
            CreateLocalizedNames(optionSet.DisplayName ?? optionSet.LogicalName.Split('|').Last()),
            CreateDescriptions(GetProperty(optionSet, ArtifactPropertyKeys.Description)),
            optionsElement);
    }

    private static XElement ParseOptionElements(FamilyArtifact optionSet)
    {
        var optionSetType = (GetProperty(optionSet, ArtifactPropertyKeys.OptionSetType) ?? "picklist").ToLowerInvariant();
        var rawOptions = GetProperty(optionSet, ArtifactPropertyKeys.OptionsJson);
        var options = string.IsNullOrWhiteSpace(rawOptions)
            ? []
            : JsonSerializer.Deserialize<List<GeneratedOptionValue>>(rawOptions, DerivedJsonOptions) ?? [];
        return optionSetType switch
        {
            "bit" or "picklist" => new XElement("options",
                options.Select(option => new XElement("option",
                    new XAttribute("value", option.Value ?? string.Empty),
                    new XAttribute("ExternalValue", string.Empty),
                    new XAttribute("IsHidden", option.IsHidden ?? "0"),
                    new XElement("labels",
                        new XElement("label",
                            new XAttribute("description", option.Label ?? option.Value ?? string.Empty),
                            new XAttribute("languagecode", "1033")))))),
            _ => new XElement("options")
        };
    }

    private static string BuildEntityColumnKey(string entityLogicalName, string columnLogicalName) =>
        $"{entityLogicalName}|{columnLogicalName}";

    private static void WriteDerivedFormFile(
        string packageRoot,
        string entityDirectory,
        FamilyArtifact form,
        IReadOnlyList<FamilyArtifact> columns,
        List<EmittedArtifact> emittedFiles)
    {
        var definition = JsonSerializer.Deserialize<GeneratedFormDefinition>(
            GetProperty(form, ArtifactPropertyKeys.FormDefinitionJson) ?? "{}",
            DerivedJsonOptions) ?? new GeneratedFormDefinition();
        var formId = GetProperty(form, ArtifactPropertyKeys.FormId) ?? Guid.NewGuid().ToString("D");
        var columnLookup = columns.ToDictionary(
            column => column.LogicalName.Split('|').Last(),
            column => column.DisplayName ?? column.LogicalName.Split('|').Last(),
            StringComparer.OrdinalIgnoreCase);
        var formType = GetProperty(form, ArtifactPropertyKeys.FormType);
        var isCardForm = string.Equals(formType, "card", StringComparison.OrdinalIgnoreCase);

        var tabs = isCardForm
            ? [BuildCardTabElement(formId, columnLookup, definition)]
            : (definition.Tabs ?? [])
                .Select((tab, tabIndex) => BuildTabElement(formId, columnLookup, tab, tabIndex))
                .ToArray();

        XElement? header = null;
        if (definition.HeaderFields is { Count: > 0 })
        {
            header = new XElement("header",
                new XAttribute("id", StableXmlGuid(formId, "header")),
                new XAttribute("columns", "111"),
                new XAttribute("celllabelposition", "Top"),
                new XAttribute("labelwidth", "115"),
                new XElement("rows",
                    new XElement("row",
                        definition.HeaderFields.Select((field, index) => new XElement("cell",
                            new XAttribute("id", StableXmlGuid(formId, "header-cell", index)),
                            new XAttribute("showlabel", "true"),
                            new XElement("labels", new XElement("label", new XAttribute("description", ResolveFieldLabel(columnLookup, field)), new XAttribute("languagecode", "1033"))),
                            new XElement("control",
                                new XAttribute("id", $"header_{field}"),
                                new XAttribute("datafieldname", field),
                                new XAttribute("classid", "{4273EDBD-AC1D-40d3-9FB2-095C621B552D}")))))));
        }

        var root = new XElement("forms",
            new XAttribute(XNamespace.Xmlns + "xsi", XsiNamespace),
            new XElement("systemform",
                new XElement("formid", $"{{{formId}}}"),
                new XElement("IntroducedVersion", "1.0.0.0"),
                new XElement("FormPresentation", GetProperty(form, ArtifactPropertyKeys.FormTypeCode) ?? "1"),
                new XElement("FormActivationState", "1"),
                new XElement("form",
                    isCardForm ? null : new XAttribute("headerdensity", "HighWithControls"),
                    new XElement("tabs", tabs),
                    isCardForm ? null : header),
                new XElement("IsCustomizable", "1"),
                new XElement("CanBeDeleted", "1"),
                CreateLocalizedNames(form.DisplayName ?? form.LogicalName),
                CreateDescriptions(GetProperty(form, ArtifactPropertyKeys.Description))));

        var formDirectory = GetFormDirectoryName(GetProperty(form, ArtifactPropertyKeys.FormType));
        WriteXml(packageRoot, $"{entityDirectory}/FormXml/{formDirectory}/{{{formId}}}.xml", root, emittedFiles, $"Synthesized {formDirectory} form for {form.DisplayName ?? form.LogicalName}.");
    }

    private static XElement BuildCardTabElement(
        string formId,
        IReadOnlyDictionary<string, string> columnLookup,
        GeneratedFormDefinition definition)
    {
        var controls = (definition.Tabs ?? [])
            .SelectMany(tab => tab.Sections ?? [])
            .SelectMany(EnumerateGeneratedFormControls)
            .ToArray();

        return new XElement("tab",
            new XAttribute("name", "general"),
            new XAttribute("verticallayout", "true"),
            new XAttribute("id", StableXmlGuid(formId, "card-tab")),
            new XAttribute("IsUserDefined", "0"),
            new XElement("labels", new XElement("label", new XAttribute("description", string.Empty), new XAttribute("languagecode", "1033"))),
            new XElement("columns",
                new XElement("column",
                    new XAttribute("width", "25%"),
                    new XElement("sections", BuildCardColorStripSection(formId))),
                new XElement("column",
                    new XAttribute("width", "75%"),
                    new XElement("sections",
                        BuildCardHeaderSection(formId),
                        BuildCardDetailsSection(formId, columnLookup, controls),
                        BuildCardFooterSection(formId)))));
    }

    private static XElement BuildCardColorStripSection(string formId) =>
        new("section",
            new XAttribute("name", "ColorStrip"),
            new XAttribute("showlabel", "false"),
            new XAttribute("showbar", "false"),
            new XAttribute("columns", "1"),
            new XAttribute("IsUserDefined", "0"),
            new XAttribute("id", StableXmlGuid(formId, "card-color-strip")),
            new XElement("labels", new XElement("label", new XAttribute("description", "ColorStrip"), new XAttribute("languagecode", "1033"))));

    private static XElement BuildCardHeaderSection(string formId) =>
        new("section",
            new XAttribute("name", "CardHeader"),
            new XAttribute("showlabel", "false"),
            new XAttribute("showbar", "false"),
            new XAttribute("columns", "111"),
            new XAttribute("IsUserDefined", "0"),
            new XAttribute("id", StableXmlGuid(formId, "card-header")),
            new XElement("labels", new XElement("label", new XAttribute("description", "Header"), new XAttribute("languagecode", "1033"))));

    private static XElement BuildCardDetailsSection(
        string formId,
        IReadOnlyDictionary<string, string> columnLookup,
        IReadOnlyList<GeneratedFormControl> controls) =>
        new("section",
            new XAttribute("name", "CardDetails"),
            new XAttribute("showlabel", "false"),
            new XAttribute("showbar", "false"),
            new XAttribute("columns", "1"),
            new XAttribute("IsUserDefined", "0"),
            new XAttribute("id", StableXmlGuid(formId, "card-details")),
            new XElement("labels", new XElement("label", new XAttribute("description", "Details"), new XAttribute("languagecode", "1033"))),
            controls.Count == 0
                ? null
                : new XElement("rows", controls.Select((control, controlIndex) => BuildControlRowElement(formId, columnLookup, control, 0, 0, controlIndex))));

    private static XElement BuildCardFooterSection(string formId) =>
        new("section",
            new XAttribute("name", "CardFooter"),
            new XAttribute("showlabel", "false"),
            new XAttribute("showbar", "false"),
            new XAttribute("columns", "1111"),
            new XAttribute("IsUserDefined", "0"),
            new XAttribute("id", StableXmlGuid(formId, "card-footer")),
            new XElement("labels", new XElement("label", new XAttribute("description", "Footer"), new XAttribute("languagecode", "1033"))));

    private static XElement BuildTabElement(
        string formId,
        IReadOnlyDictionary<string, string> columnLookup,
        GeneratedFormTab tab,
        int tabIndex) =>
        new("tab",
            new XAttribute("name", tab.Name ?? $"tab_{tabIndex + 1}"),
            new XAttribute("id", StableXmlGuid(formId, "tab", tabIndex)),
            new XAttribute("IsUserDefined", "1"),
            new XAttribute("expanded", "true"),
            new XAttribute("showlabel", "true"),
            new XElement("labels", new XElement("label", new XAttribute("description", tab.Label ?? tab.Name ?? $"Tab {tabIndex + 1}"), new XAttribute("languagecode", "1033"))),
            new XElement("columns",
                new XElement("column",
                    new XAttribute("width", "100%"),
                    new XElement("sections", (tab.Sections ?? []).Select((section, sectionIndex) => BuildSectionElement(formId, columnLookup, section, tabIndex, sectionIndex))))));

    private static XElement BuildSectionElement(
        string formId,
        IReadOnlyDictionary<string, string> columnLookup,
        GeneratedFormSection section,
        int tabIndex,
        int sectionIndex) =>
        new("section",
            new XAttribute("name", section.Name ?? $"section_{sectionIndex + 1}"),
            new XAttribute("id", StableXmlGuid(formId, "section", tabIndex, sectionIndex)),
            new XAttribute("IsUserDefined", "1"),
            new XAttribute("showlabel", "true"),
            new XAttribute("showbar", "false"),
            new XAttribute("columns", "1"),
            new XElement("labels", new XElement("label", new XAttribute("description", section.Label ?? section.Name ?? $"Section {sectionIndex + 1}"), new XAttribute("languagecode", "1033"))),
            new XElement("rows", EnumerateGeneratedFormControls(section).Select((control, controlIndex) => BuildControlRowElement(formId, columnLookup, control, tabIndex, sectionIndex, controlIndex))));

    private static XElement BuildControlRowElement(
        string formId,
        IReadOnlyDictionary<string, string> columnLookup,
        GeneratedFormControl control,
        int tabIndex,
        int sectionIndex,
        int controlIndex) =>
        new("row",
            new XElement("cell",
                new XAttribute("id", StableXmlGuid(formId, "cell", tabIndex, sectionIndex, controlIndex)),
                new XAttribute("showlabel", "true"),
                new XElement("labels", new XElement("label", new XAttribute("description", ResolveGeneratedControlLabel(columnLookup, control)), new XAttribute("languagecode", "1033"))),
                BuildGeneratedControlElement(control)));

    private static XElement BuildGeneratedControlElement(GeneratedFormControl control)
    {
        var kind = NormalizeGeneratedFormControlKind(control.Kind);
        return kind switch
        {
            "quickView" => new XElement("control",
                new XAttribute("id", control.Field ?? "quickview"),
                new XAttribute("datafieldname", control.Field ?? string.Empty),
                new XAttribute("classid", QuickViewControlClassId),
                new XElement("parameters",
                    string.IsNullOrWhiteSpace(control.QuickFormEntity) || string.IsNullOrWhiteSpace(control.QuickFormId)
                        ? null
                        : new XElement("QuickForms", $"<QuickFormIds><QuickFormId entityname=\"{control.QuickFormEntity}\">{control.QuickFormId}</QuickFormId></QuickFormIds>"),
                    string.IsNullOrWhiteSpace(control.ControlMode) ? null : new XElement("ControlMode", control.ControlMode))),
            "subgrid" => new XElement("control",
                new XAttribute("id", control.RelationshipName ?? control.TargetTable ?? "subgrid"),
                new XAttribute("classid", SubgridControlClassId),
                new XElement("parameters",
                    string.IsNullOrWhiteSpace(control.DefaultViewId) ? null : new XElement("ViewId", $"{{{NormalizeGuid(control.DefaultViewId) ?? control.DefaultViewId}}}"),
                    control.IsUserView.HasValue ? new XElement("IsUserView", control.IsUserView.Value ? "true" : "false") : null,
                    string.IsNullOrWhiteSpace(control.RelationshipName) ? null : new XElement("RelationshipName", control.RelationshipName),
                    string.IsNullOrWhiteSpace(control.TargetTable) ? null : new XElement("TargetEntityType", control.TargetTable),
                    string.IsNullOrWhiteSpace(control.AutoExpand) ? null : new XElement("AutoExpand", control.AutoExpand),
                    control.EnableQuickFind.HasValue ? new XElement("EnableQuickFind", control.EnableQuickFind.Value ? "true" : "false") : null,
                    control.EnableViewPicker.HasValue ? new XElement("EnableViewPicker", control.EnableViewPicker.Value ? "true" : "false") : null,
                    control.EnableJumpBar.HasValue ? new XElement("EnableJumpBar", control.EnableJumpBar.Value ? "true" : "false") : null,
                    control.EnableChartPicker.HasValue ? new XElement("EnableChartPicker", control.EnableChartPicker.Value ? "true" : "false") : null,
                    control.RecordsPerPage.HasValue ? new XElement("RecordsPerPage", control.RecordsPerPage.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)) : null)),
            _ => new XElement("control",
                new XAttribute("id", control.Field ?? "field"),
                new XAttribute("datafieldname", control.Field ?? string.Empty),
                new XAttribute("classid", "{4273EDBD-AC1D-40d3-9FB2-095C621B552D}"))
        };
    }

    private static IEnumerable<GeneratedFormControl> EnumerateGeneratedFormControls(GeneratedFormSection section)
    {
        if (section.Controls is { Count: > 0 })
        {
            foreach (var control in section.Controls)
            {
                if (control is not null)
                {
                    yield return control;
                }
            }

            yield break;
        }

        foreach (var field in section.Fields ?? [])
        {
            if (string.IsNullOrWhiteSpace(field))
            {
                continue;
            }

            yield return new GeneratedFormControl
            {
                Kind = "field",
                Field = field
            };
        }
    }

    private static string ResolveGeneratedControlLabel(IReadOnlyDictionary<string, string> columnLookup, GeneratedFormControl control)
    {
        if (!string.IsNullOrWhiteSpace(control.Label))
        {
            return control.Label;
        }

        return NormalizeGeneratedFormControlKind(control.Kind) switch
        {
            "subgrid" => control.TargetTable ?? control.RelationshipName ?? "Subgrid",
            _ => ResolveFieldLabel(columnLookup, control.Field ?? "field")
        };
    }

    private static string NormalizeGeneratedFormControlKind(string? value) =>
        value?.Trim() switch
        {
            var text when text is not null && text.Equals("quickView", StringComparison.OrdinalIgnoreCase) => "quickView",
            var text when text is not null && text.Equals("subgrid", StringComparison.OrdinalIgnoreCase) => "subgrid",
            _ => "field"
        };

    private static string GetFormDirectoryName(string? formType) =>
        formType?.Trim().ToLowerInvariant() switch
        {
            "quick" => "quick",
            "card" => "card",
            _ => "main"
        };

    private static void WriteDerivedViewFile(
        string packageRoot,
        string entityDirectory,
        FamilyArtifact view,
        FamilyArtifact table,
        List<EmittedArtifact> emittedFiles)
    {
        var layoutColumns = JsonSerializer.Deserialize<List<string>>(GetProperty(view, ArtifactPropertyKeys.LayoutColumnsJson) ?? "[]", DerivedJsonOptions) ?? [];
        var fetchAttributes = JsonSerializer.Deserialize<List<string>>(GetProperty(view, ArtifactPropertyKeys.FetchAttributesJson) ?? "[]", DerivedJsonOptions) ?? [];
        var filters = JsonSerializer.Deserialize<List<GeneratedViewFilter>>(GetProperty(view, ArtifactPropertyKeys.FiltersJson) ?? "[]", DerivedJsonOptions) ?? [];
        var orders = JsonSerializer.Deserialize<List<GeneratedViewOrder>>(GetProperty(view, ArtifactPropertyKeys.OrdersJson) ?? "[]", DerivedJsonOptions) ?? [];
        var viewId = GetProperty(view, ArtifactPropertyKeys.ViewId) ?? Guid.NewGuid().ToString("D");
        var primaryIdAttribute = GetProperty(table, ArtifactPropertyKeys.PrimaryIdAttribute) ?? $"{table.LogicalName}id";

        var root = new XElement("savedqueries",
            new XAttribute(XNamespace.Xmlns + "xsi", XsiNamespace),
            new XElement("savedquery",
                new XElement("IsCustomizable", "1"),
                new XElement("CanBeDeleted", "1"),
                new XElement("isquickfindquery", "0"),
                new XElement("isprivate", "0"),
                new XElement("isdefault", "0"),
                new XElement("savedqueryid", $"{{{viewId}}}"),
                new XElement("layoutxml",
                    new XElement("grid",
                        new XAttribute("name", "resultset"),
                        new XAttribute("jump", layoutColumns.FirstOrDefault() ?? primaryIdAttribute),
                        new XAttribute("select", "1"),
                        new XAttribute("icon", "1"),
                        new XAttribute("preview", "1"),
                        new XElement("row",
                            new XAttribute("name", "result"),
                            new XAttribute("id", primaryIdAttribute),
                            layoutColumns.Select(column => new XElement("cell", new XAttribute("name", column), new XAttribute("width", "150")))))),
                new XElement("querytype", GetProperty(view, ArtifactPropertyKeys.QueryType) ?? "0"),
                new XElement("fetchxml",
                    new XElement("fetch",
                        new XAttribute("version", "1.0"),
                        new XAttribute("mapping", "logical"),
                        new XElement("entity",
                            new XAttribute("name", GetProperty(view, ArtifactPropertyKeys.TargetEntity) ?? GetProperty(table, ArtifactPropertyKeys.EntityLogicalName) ?? table.LogicalName),
                            fetchAttributes.Select(attribute => new XElement("attribute", new XAttribute("name", attribute))),
                            orders.Select(order => new XElement("order", new XAttribute("attribute", order.Attribute ?? string.Empty), new XAttribute("descending", order.Descending ?? "false"))),
                            filters.Count == 0
                                ? null
                                : new XElement("filter",
                                    new XAttribute("type", "and"),
                                    filters.Select(filter => new XElement("condition",
                                        new XAttribute("attribute", filter.Attribute ?? string.Empty),
                                        new XAttribute("operator", filter.Operator ?? string.Empty),
                                        new XAttribute("value", filter.Value ?? string.Empty))))))),
                new XElement("IntroducedVersion", "1.0.0.0"),
                CreateLocalizedNames(view.DisplayName ?? view.LogicalName)));

        WriteXml(packageRoot, $"{entityDirectory}/SavedQueries/{{{viewId}}}.xml", root, emittedFiles, $"Synthesized saved query for {view.DisplayName ?? view.LogicalName}.");
    }

    private static void WriteDerivedRelationshipFiles(CanonicalSolution model, string packageRoot, List<EmittedArtifact> emittedFiles)
    {
        var tableSchemaByLogicalName = BuildTableSchemaLookup(model);
        var columnsByEntityAndLogicalName = BuildColumnLookup(model);
        var relationshipGroups = model.Artifacts
            .Where(artifact => artifact.Family == ComponentFamily.Relationship)
            .GroupBy(artifact => GetProperty(artifact, ArtifactPropertyKeys.ReferencedEntity) ?? artifact.LogicalName, StringComparer.OrdinalIgnoreCase);

        foreach (var relationshipGroup in relationshipGroups.OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            var referencedEntityLogicalName = relationshipGroup.Key;
            var referencedEntitySchemaName = tableSchemaByLogicalName.TryGetValue(referencedEntityLogicalName, out var referencedSchemaName)
                ? referencedSchemaName
                : referencedEntityLogicalName;
            var root = new XElement("EntityRelationships",
                new XAttribute(XNamespace.Xmlns + "xsi", XsiNamespace),
                relationshipGroup
                    .OrderBy(artifact => artifact.LogicalName, StringComparer.OrdinalIgnoreCase)
                    .Select(artifact => BuildEntityRelationshipElement(artifact, referencedEntitySchemaName, tableSchemaByLogicalName, columnsByEntityAndLogicalName)));

            WriteXml(
                packageRoot,
                $"Other/Relationships/{referencedEntitySchemaName}.xml",
                root,
                emittedFiles,
                $"Synthesized relationship shell for {referencedEntitySchemaName}.");
        }
    }

    private static XElement BuildCustomizationsEntityRelationshipsElement(CanonicalSolution model)
    {
        var tableSchemaByLogicalName = BuildTableSchemaLookup(model);
        var columnsByEntityAndLogicalName = BuildColumnLookup(model);
        var relationshipElements = model.Artifacts
            .Where(artifact => artifact.Family == ComponentFamily.Relationship)
            .OrderBy(artifact => artifact.LogicalName, StringComparer.OrdinalIgnoreCase)
            .Select(artifact =>
            {
                var referencedEntityLogicalName = GetProperty(artifact, ArtifactPropertyKeys.ReferencedEntity) ?? artifact.LogicalName;
                var referencedEntitySchemaName = tableSchemaByLogicalName.TryGetValue(referencedEntityLogicalName, out var referencedSchemaName)
                    ? referencedSchemaName
                    : referencedEntityLogicalName;
                return BuildEntityRelationshipElement(artifact, referencedEntitySchemaName, tableSchemaByLogicalName, columnsByEntityAndLogicalName);
            });

        return new XElement("EntityRelationships", relationshipElements);
    }

    private static Dictionary<string, string> BuildTableSchemaLookup(CanonicalSolution model) =>
        model.Artifacts
            .Where(artifact => artifact.Family == ComponentFamily.Table)
            .ToDictionary(
                artifact => GetProperty(artifact, ArtifactPropertyKeys.EntityLogicalName) ?? artifact.LogicalName,
                artifact => GetProperty(artifact, ArtifactPropertyKeys.SchemaName) ?? artifact.LogicalName,
                StringComparer.OrdinalIgnoreCase);

    private static Dictionary<string, FamilyArtifact> BuildColumnLookup(CanonicalSolution model) =>
        model.Artifacts
            .Where(artifact => artifact.Family == ComponentFamily.Column)
            .GroupBy(
                artifact => BuildEntityColumnKey(
                    GetProperty(artifact, ArtifactPropertyKeys.EntityLogicalName) ?? string.Empty,
                    GetProperty(artifact, ArtifactPropertyKeys.AttributeLogicalName) ?? artifact.LogicalName.Split('|').Last()),
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First(),
                StringComparer.OrdinalIgnoreCase);

    private static XElement BuildEntityRelationshipElement(
        FamilyArtifact artifact,
        string referencedEntitySchemaName,
        IReadOnlyDictionary<string, string> tableSchemaByLogicalName,
        IReadOnlyDictionary<string, FamilyArtifact> columnsByEntityAndLogicalName)
    {
        var referencingEntityLogicalName = GetProperty(artifact, ArtifactPropertyKeys.ReferencingEntity) ?? string.Empty;
        var referencingEntitySchemaName = tableSchemaByLogicalName.TryGetValue(referencingEntityLogicalName, out var referencingSchemaName)
            ? referencingSchemaName
            : referencingEntityLogicalName;
        var referencingAttributeLogicalName = GetProperty(artifact, ArtifactPropertyKeys.ReferencingAttribute) ?? string.Empty;
        var referencingColumn = columnsByEntityAndLogicalName.TryGetValue(BuildEntityColumnKey(referencingEntityLogicalName, referencingAttributeLogicalName), out var column)
            ? column
            : null;
        var referencingAttributeSchemaName = referencingColumn is null
            ? referencingAttributeLogicalName
            : GetProperty(referencingColumn, ArtifactPropertyKeys.SchemaName) ?? referencingAttributeLogicalName;
        var lookupDisplayName = referencingColumn?.DisplayName ?? referencingAttributeSchemaName;

        return new XElement("EntityRelationship",
            new XAttribute("Name", artifact.LogicalName),
            new XElement("EntityRelationshipType", GetProperty(artifact, ArtifactPropertyKeys.RelationshipType) ?? "OneToMany"),
            new XElement("IsCustomizable", "1"),
            new XElement("IntroducedVersion", "1.0.0.0"),
            new XElement("IsHierarchical", "0"),
            new XElement("ReferencingEntityName", referencingEntitySchemaName),
            new XElement("ReferencedEntityName", referencedEntitySchemaName),
            new XElement("CascadeAssign", "NoCascade"),
            new XElement("CascadeDelete", "RemoveLink"),
            new XElement("CascadeArchive", "RemoveLink"),
            new XElement("CascadeReparent", "NoCascade"),
            new XElement("CascadeShare", "NoCascade"),
            new XElement("CascadeUnshare", "NoCascade"),
            new XElement("CascadeRollupView", "NoCascade"),
            new XElement("IsValidForAdvancedFind", "1"),
            new XElement("ReferencingAttributeName", referencingAttributeSchemaName),
            new XElement("RelationshipDescription", CreateDescriptions(GetProperty(artifact, ArtifactPropertyKeys.Description))),
            new XElement("EntityRelationshipRoles",
                new XElement("EntityRelationshipRole",
                    new XElement("NavPaneDisplayOption", "UseCollectionName"),
                    new XElement("NavPaneArea", "Details"),
                    new XElement("NavPaneOrder", "10000"),
                    new XElement("NavigationPropertyName", referencingAttributeSchemaName),
                    new XElement("CustomLabels",
                        new XElement("CustomLabel",
                            new XAttribute("description", lookupDisplayName),
                            new XAttribute("languagecode", "1033"))),
                    new XElement("RelationshipRoleType", "1")),
                new XElement("EntityRelationshipRole",
                    new XElement("NavigationPropertyName", artifact.LogicalName),
                    new XElement("RelationshipRoleType", "0"))));
    }

    private static void WriteDerivedGlobalOptionSets(CanonicalSolution model, string packageRoot, List<EmittedArtifact> emittedFiles)
    {
        foreach (var optionSet in model.Artifacts.Where(artifact => artifact.Family == ComponentFamily.OptionSet
            && string.Equals(GetProperty(artifact, ArtifactPropertyKeys.IsGlobal), "true", StringComparison.OrdinalIgnoreCase))
            .OrderBy(artifact => artifact.LogicalName, StringComparer.OrdinalIgnoreCase))
        {
            var root = new XElement("optionset",
                new XAttribute("Name", optionSet.LogicalName),
                new XAttribute("localizedName", optionSet.DisplayName ?? optionSet.LogicalName),
                new XAttribute("description", GetProperty(optionSet, ArtifactPropertyKeys.Description) ?? string.Empty),
                new XAttribute(XNamespace.Xmlns + "xsi", XsiNamespace),
                new XElement("OptionSetType", GetProperty(optionSet, ArtifactPropertyKeys.OptionSetType) ?? "picklist"),
                new XElement("IsGlobal", "1"),
                new XElement("IntroducedVersion", "1.0.0.0"),
                new XElement("IsCustomizable", "1"),
                CreateLocalizedNames(optionSet.DisplayName ?? optionSet.LogicalName),
                CreateDescriptions(GetProperty(optionSet, ArtifactPropertyKeys.Description)),
                ParseOptionElements(optionSet));

            WriteXml(
                packageRoot,
                $"OptionSets/{optionSet.LogicalName}.xml",
                root,
                emittedFiles,
                $"Synthesized global option set for {optionSet.LogicalName}.");
        }
    }

    private static void WriteDerivedVisualizations(CanonicalSolution model, string packageRoot, List<EmittedArtifact> emittedFiles)
    {
        var tablesByLogicalName = model.Artifacts
            .Where(artifact => artifact.Family == ComponentFamily.Table)
            .ToDictionary(
                artifact => GetProperty(artifact, ArtifactPropertyKeys.EntityLogicalName) ?? artifact.LogicalName,
                artifact => GetProperty(artifact, ArtifactPropertyKeys.SchemaName) ?? artifact.LogicalName,
                StringComparer.OrdinalIgnoreCase);

        foreach (var visualization in model.Artifacts
                     .Where(artifact => artifact.Family == ComponentFamily.Visualization)
                     .OrderBy(artifact => artifact.LogicalName, StringComparer.OrdinalIgnoreCase))
        {
            var targetEntity = GetProperty(visualization, ArtifactPropertyKeys.TargetEntity)
                ?? GetProperty(visualization, ArtifactPropertyKeys.EntityLogicalName)
                ?? visualization.LogicalName.Split('|').FirstOrDefault()
                ?? "visualization";
            var entitySchemaName = tablesByLogicalName.TryGetValue(targetEntity, out var schemaName)
                ? schemaName
                : targetEntity;
            var visualizationId = NormalizeGuid(GetProperty(visualization, ArtifactPropertyKeys.VisualizationId)) ?? Guid.NewGuid().ToString("D");

            var root = new XElement("visualization",
                new XAttribute("unmodified", "1"),
                new XAttribute(XNamespace.Xmlns + "xsi", XsiNamespace),
                new XElement("savedqueryvisualizationid", $"{{{visualizationId}}}"),
                ParseXmlFragment(GetProperty(visualization, ArtifactPropertyKeys.DataDescriptionXml), "datadescription"),
                ParseXmlFragment(GetProperty(visualization, ArtifactPropertyKeys.PresentationDescriptionXml), "presentationdescription"),
                new XElement("isdefault", "0"),
                CreateLocalizedNames(visualization.DisplayName ?? visualization.LogicalName),
                CreateDescriptions(GetProperty(visualization, ArtifactPropertyKeys.Description)),
                new XElement("IntroducedVersion", GetProperty(visualization, ArtifactPropertyKeys.IntroducedVersion) ?? "1.0.0.0"));

            WriteXml(
                packageRoot,
                $"Entities/{entitySchemaName}/Visualizations/{{{visualizationId}}}.xml",
                root,
                emittedFiles,
                $"Synthesized visualization for {visualization.DisplayName ?? visualization.LogicalName}.");
        }
    }

    private static void WriteDerivedAppModules(CanonicalSolution model, string packageRoot, List<EmittedArtifact> emittedFiles)
    {
        foreach (var appModule in model.Artifacts.Where(artifact => artifact.Family == ComponentFamily.AppModule).OrderBy(artifact => artifact.LogicalName, StringComparer.OrdinalIgnoreCase))
        {
            var componentTypes = JsonSerializer.Deserialize<List<string>>(GetProperty(appModule, ArtifactPropertyKeys.ComponentTypesJson) ?? "[]", DerivedJsonOptions) ?? [];
            var roleIds = JsonSerializer.Deserialize<List<string>>(GetProperty(appModule, ArtifactPropertyKeys.RoleIdsJson) ?? "[]", DerivedJsonOptions) ?? [];
            var appSettings = model.Artifacts
                .Where(artifact => artifact.Family == ComponentFamily.AppSetting
                    && string.Equals(GetProperty(artifact, ArtifactPropertyKeys.ParentAppModuleUniqueName), appModule.LogicalName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(artifact => GetProperty(artifact, ArtifactPropertyKeys.SettingDefinitionUniqueName) ?? artifact.LogicalName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var root = new XElement("AppModule",
                new XElement("UniqueName", appModule.LogicalName),
                new XElement("IntroducedVersion", "1.0.0.0"),
                new XElement("WebResourceId", DefaultAppModuleIconWebResourceId),
                new XElement("OptimizedFor"),
                new XElement("statecode", "0"),
                new XElement("statuscode", "1"),
                new XElement("FormFactor", "1"),
                new XElement("ClientType", "4"),
                new XElement("NavigationType", "0"),
                new XElement("AppModuleComponents",
                    componentTypes.Select(componentType => new XElement("AppModuleComponent", new XAttribute("type", componentType), new XAttribute("schemaName", appModule.LogicalName)))),
                new XElement("AppModuleRoleMaps",
                    roleIds
                        .Select(NormalizeGuid)
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                        .Select(roleId => new XElement("Role", new XAttribute("id", $"{{{roleId}}}")))),
                CreateLocalizedNames(appModule.DisplayName ?? appModule.LogicalName),
                CreateDescriptions(GetProperty(appModule, ArtifactPropertyKeys.Description)),
                new XElement("appsettings",
                    appSettings.Select(appSetting => new XElement("appsetting",
                        new XAttribute("settingdefinitionid.uniquename", GetProperty(appSetting, ArtifactPropertyKeys.SettingDefinitionUniqueName) ?? appSetting.LogicalName),
                        new XElement("iscustomizable", "1"),
                        new XElement("value", GetProperty(appSetting, ArtifactPropertyKeys.Value) ?? string.Empty)))));

            WriteXml(
                packageRoot,
                $"AppModules/{appModule.LogicalName}/AppModule.xml",
                root,
                emittedFiles,
                $"Synthesized app module shell for {appModule.LogicalName}.");
        }

        foreach (var siteMap in model.Artifacts.Where(artifact => artifact.Family == ComponentFamily.SiteMap).OrderBy(artifact => artifact.LogicalName, StringComparer.OrdinalIgnoreCase))
        {
            var definition = JsonSerializer.Deserialize<GeneratedSiteMapDefinition>(
                GetProperty(siteMap, ArtifactPropertyKeys.SiteMapDefinitionJson) ?? "{}",
                DerivedJsonOptions) ?? new GeneratedSiteMapDefinition();

            var root = new XElement("AppModuleSiteMap",
                new XAttribute(XNamespace.Xmlns + "xsi", XsiNamespace),
                new XElement("SiteMapUniqueName", siteMap.LogicalName),
                new XElement("EnableCollapsibleGroups", "False"),
                new XElement("ShowHome", "True"),
                new XElement("ShowPinned", "True"),
                new XElement("ShowRecents", "True"),
                new XElement("SiteMap",
                    new XAttribute("IntroducedVersion", "7.0.0.0"),
                    (definition.Areas ?? []).Select((area, areaIndex) => BuildAreaElement(area, areaIndex))),
                CreateLocalizedNames(siteMap.DisplayName ?? siteMap.LogicalName));

            WriteXml(
                packageRoot,
                $"AppModuleSiteMaps/{siteMap.LogicalName}/AppModuleSiteMap.xml",
                root,
                emittedFiles,
                $"Synthesized site map for {siteMap.LogicalName}.");
        }
    }

    private static XElement BuildAreaElement(GeneratedSiteMapArea area, int areaIndex) =>
        new("Area",
            new XAttribute("Id", area.Id ?? $"area_{areaIndex + 1}"),
            new XAttribute("ShowGroups", "true"),
            new XAttribute("ResourceId", "SitemapDesigner.NewArea"),
            new XAttribute("IntroducedVersion", "7.0.0.0"),
            new XElement("Titles", new XElement("Title", new XAttribute("LCID", "1033"), new XAttribute("Title", area.Title ?? $"Area {areaIndex + 1}"))),
            (area.Groups ?? []).Select((group, groupIndex) => BuildGroupElement(group, groupIndex)));

    private static XElement BuildGroupElement(GeneratedSiteMapGroup group, int groupIndex) =>
        new("Group",
            new XAttribute("Id", group.Id ?? $"group_{groupIndex + 1}"),
            new XAttribute("ResourceId", "SitemapDesigner.NewGroup"),
            new XAttribute("IntroducedVersion", "7.0.0.0"),
            new XElement("Titles", new XElement("Title", new XAttribute("LCID", "1033"), new XAttribute("Title", group.Title ?? $"Group {groupIndex + 1}"))),
            (group.SubAreas ?? []).Select((subArea, subAreaIndex) => BuildSubAreaElement(subArea, subAreaIndex)));

    private static XElement BuildSubAreaElement(GeneratedSiteMapSubArea subArea, int subAreaIndex) =>
        new("SubArea",
            new XAttribute("Id", subArea.Id ?? $"subarea_{subAreaIndex + 1}"),
            new XAttribute("ResourceId", "SitemapDesigner.NewSubArea"),
            string.IsNullOrWhiteSpace(subArea.Entity) || !string.IsNullOrWhiteSpace(subArea.ViewId) || !string.IsNullOrWhiteSpace(subArea.RecordId)
                ? null
                : new XAttribute("Entity", subArea.Entity),
            BuildSiteMapSubAreaUrl(subArea) is { Length: > 0 } subAreaUrl ? new XAttribute("Url", subAreaUrl) : null,
            string.IsNullOrWhiteSpace(subArea.Client) ? null : new XAttribute("Client", subArea.Client),
            subArea.PassParams.HasValue ? new XAttribute("PassParams", subArea.PassParams.Value ? "true" : "false") : null,
            subArea.AvailableOffline.HasValue ? new XAttribute("AvailableOffline", subArea.AvailableOffline.Value ? "true" : "false") : null,
            string.IsNullOrWhiteSpace(subArea.Icon) ? null : new XAttribute("Icon", subArea.Icon),
            string.IsNullOrWhiteSpace(subArea.VectorIcon) ? null : new XAttribute("VectorIcon", subArea.VectorIcon),
            new XElement("Titles", new XElement("Title", new XAttribute("LCID", "1033"), new XAttribute("Title", subArea.Title ?? $"SubArea {subAreaIndex + 1}"))));

    private static string? BuildSiteMapSubAreaUrl(GeneratedSiteMapSubArea subArea)
    {
        if (!string.IsNullOrWhiteSpace(subArea.WebResource))
        {
            return $"$webresource:{subArea.WebResource}";
        }

        if (!string.IsNullOrWhiteSpace(subArea.CustomPage))
        {
            var normalizedCustomPage = NormalizeLogicalName(subArea.CustomPage) ?? subArea.CustomPage.Trim();
            var normalizedContextEntityName = NormalizeLogicalName(subArea.CustomPageEntityName);
            var normalizedContextRecordId = NormalizeGuid(subArea.CustomPageRecordId);
            var normalizedAppId = NormalizeGuid(subArea.AppId);
            var queryParameters = new List<string>
            {
                "pagetype=custom",
                $"name={normalizedCustomPage}"
            };

            if (!string.IsNullOrWhiteSpace(normalizedContextEntityName))
            {
                queryParameters.Add($"entityName={normalizedContextEntityName}");
            }

            if (!string.IsNullOrWhiteSpace(normalizedContextRecordId))
            {
                queryParameters.Add($"recordId={normalizedContextRecordId}");
            }

            if (!string.IsNullOrWhiteSpace(normalizedAppId))
            {
                queryParameters.Add($"appid={normalizedAppId}");
            }

            return $"/main.aspx?{string.Join("&", queryParameters)}";
        }

        if (!string.IsNullOrWhiteSpace(subArea.ViewId) && !string.IsNullOrWhiteSpace(subArea.Entity))
        {
            var normalizedEntity = NormalizeLogicalName(subArea.Entity) ?? subArea.Entity.Trim();
            var normalizedViewId = NormalizeGuid(subArea.ViewId) ?? subArea.ViewId.Trim();
            var normalizedViewType = NormalizeSiteMapViewType(subArea.ViewType);
            var normalizedAppId = NormalizeGuid(subArea.AppId);
            var queryParameters = new List<string>();
            if (!string.IsNullOrWhiteSpace(normalizedAppId))
            {
                queryParameters.Add($"appid={normalizedAppId}");
            }

            queryParameters.Add("pagetype=entitylist");
            queryParameters.Add($"etn={normalizedEntity}");
            queryParameters.Add($"viewid={normalizedViewId}");
            if (!string.IsNullOrWhiteSpace(normalizedViewType))
            {
                queryParameters.Add($"viewtype={normalizedViewType}");
            }

            return $"/main.aspx?{string.Join("&", queryParameters)}";
        }

        if (!string.IsNullOrWhiteSpace(subArea.RecordId) && !string.IsNullOrWhiteSpace(subArea.Entity))
        {
            var normalizedEntity = NormalizeLogicalName(subArea.Entity) ?? subArea.Entity.Trim();
            var normalizedRecordId = NormalizeGuid(subArea.RecordId) ?? subArea.RecordId.Trim();
            var normalizedFormId = NormalizeGuid(subArea.FormId);
            var normalizedAppId = NormalizeGuid(subArea.AppId);
            var queryParameters = new List<string>();
            if (!string.IsNullOrWhiteSpace(normalizedAppId))
            {
                queryParameters.Add($"appid={normalizedAppId}");
            }

            queryParameters.Add("pagetype=entityrecord");
            queryParameters.Add($"etn={normalizedEntity}");
            queryParameters.Add($"id={normalizedRecordId}");
            if (!string.IsNullOrWhiteSpace(normalizedFormId))
            {
                queryParameters.Add($"extraqs=formid%3D{normalizedFormId}");
            }

            return $"/main.aspx?{string.Join("&", queryParameters)}";
        }

        if (!string.IsNullOrWhiteSpace(subArea.Dashboard))
        {
            var normalizedDashboard = NormalizeGuid(subArea.Dashboard) ?? subArea.Dashboard.Trim();
            var normalizedAppId = NormalizeGuid(subArea.AppId);
            return string.IsNullOrWhiteSpace(normalizedAppId)
                ? $"/main.aspx?pagetype=dashboard&id={normalizedDashboard}"
                : $"/main.aspx?appid={normalizedAppId}&pagetype=dashboard&id={normalizedDashboard}";
        }

        return string.IsNullOrWhiteSpace(subArea.Url) ? null : subArea.Url;
    }

    private static XElement ParseXmlFragment(string? xml, string fallbackElementName)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            return new XElement(fallbackElementName);
        }

        try
        {
            return XElement.Parse(xml, LoadOptions.PreserveWhitespace);
        }
        catch
        {
            return new XElement(fallbackElementName);
        }
    }

    private static void WriteDerivedEnvironmentVariables(CanonicalSolution model, string packageRoot, List<EmittedArtifact> emittedFiles)
    {
        var valueArtifacts = model.Artifacts
            .Where(artifact => artifact.Family == ComponentFamily.EnvironmentVariableValue)
            .ToDictionary(artifact => artifact.LogicalName, artifact => artifact, StringComparer.OrdinalIgnoreCase);

        foreach (var definition in model.Artifacts.Where(artifact => artifact.Family == ComponentFamily.EnvironmentVariableDefinition).OrderBy(artifact => artifact.LogicalName, StringComparer.OrdinalIgnoreCase))
        {
            var directory = $"environmentvariabledefinitions/{definition.LogicalName}";
            var root = new XElement("environmentvariabledefinition",
                new XAttribute("schemaname", definition.LogicalName),
                new XElement("defaultvalue", GetProperty(definition, ArtifactPropertyKeys.DefaultValue) ?? string.Empty),
                new XElement("description",
                    new XAttribute("default", GetProperty(definition, ArtifactPropertyKeys.Description) ?? string.Empty),
                    new XElement("label", new XAttribute("description", GetProperty(definition, ArtifactPropertyKeys.Description) ?? string.Empty), new XAttribute("languagecode", "1033"))),
                new XElement("displayname",
                    new XAttribute("default", definition.DisplayName ?? definition.LogicalName),
                    new XElement("label", new XAttribute("description", definition.DisplayName ?? definition.LogicalName), new XAttribute("languagecode", "1033"))),
                new XElement("introducedversion", "1.0.0.0"),
                new XElement("iscustomizable", "1"),
                new XElement("isrequired", "0"),
                new XElement("secretstore", GetProperty(definition, ArtifactPropertyKeys.SecretStore) ?? "0"),
                new XElement("valueschema", GetProperty(definition, ArtifactPropertyKeys.ValueSchema) ?? string.Empty),
                new XElement("type", GetProperty(definition, ArtifactPropertyKeys.AttributeType) ?? "100000000"));

            WriteXml(packageRoot, $"{directory}/environmentvariabledefinition.xml", root, emittedFiles, $"Synthesized environment variable definition for {definition.LogicalName}.");

            if (valueArtifacts.TryGetValue(definition.LogicalName, out var value))
            {
                WriteJson(
                    packageRoot,
                    $"{directory}/environmentvariablevalues.json",
                    new
                    {
                        environmentvariablevalues = new
                        {
                            environmentvariablevalue = new
                            {
                                iscustomizable = "1",
                                value = GetProperty(value, ArtifactPropertyKeys.Value)
                            }
                        }
                    },
                    emittedFiles,
                    $"Synthesized environment variable value for {definition.LogicalName}.",
                    EmittedArtifactRole.PackageInput);
            }
        }
    }

    private static string? NormalizeGuid(string? value) =>
        Guid.TryParse(value, out var guid) ? guid.ToString("D") : null;

    private static string? NormalizeSiteMapViewType(string? value) =>
        value?.Trim() switch
        {
            "1039" or "savedquery" => "1039",
            "4230" or "userquery" => "4230",
            _ => null
        };

    private static string? NormalizeLogicalName(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();

    private static void WriteXml(string packageRoot, string relativePath, XElement root, List<EmittedArtifact> emittedFiles, string description)
    {
        var fullPath = GetContainedPath(packageRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var document = new XDocument(new XDeclaration("1.0", "utf-8", null), root);
        var xml = document.ToString(SaveOptions.DisableFormatting).Replace("><", ">\n<", StringComparison.Ordinal) + "\n";
        File.WriteAllText(fullPath, xml, Utf8NoBom);
        emittedFiles.Add(new EmittedArtifact($"package-inputs/{relativePath.Replace('\\', '/')}", EmittedArtifactRole.PackageInput, description));
    }

    private static XElement CreateLocalizedNames(string description) =>
        new("LocalizedNames", new XElement("LocalizedName", new XAttribute("description", description), new XAttribute("languagecode", "1033")));

    private static XElement CreateDescriptions(string? description) =>
        new("Descriptions", new XElement("Description", new XAttribute("description", description ?? string.Empty), new XAttribute("languagecode", "1033")));

    private static XElement CreatePublisherElement(CanonicalSolution model) =>
        new("Publisher",
            new XElement("UniqueName", model.Publisher.UniqueName),
            CreateLocalizedNames(model.Publisher.DisplayName),
            CreateDescriptions(GetPublisherDescription(model)),
            CreateNilElement("EMailAddress"),
            CreateNilElement("SupportingWebsiteUrl"),
            new XElement("CustomizationPrefix", model.Publisher.Prefix),
            new XElement("CustomizationOptionValuePrefix", "72727"),
            new XElement("Addresses",
                CreatePublisherAddress("1"),
                CreatePublisherAddress("2")));

    private static XElement CreatePublisherAddress(string addressNumber) =>
        new("Address",
            new XElement("AddressNumber", addressNumber),
            new XElement("AddressTypeCode", "1"),
            CreateNilElement("City"),
            CreateNilElement("County"),
            CreateNilElement("Country"),
            CreateNilElement("Fax"),
            CreateNilElement("FreightTermsCode"),
            CreateNilElement("ImportSequenceNumber"),
            CreateNilElement("Latitude"),
            CreateNilElement("Line1"),
            CreateNilElement("Line2"),
            CreateNilElement("Line3"),
            CreateNilElement("Longitude"),
            CreateNilElement("Name"),
            CreateNilElement("PostalCode"),
            CreateNilElement("PostOfficeBox"),
            CreateNilElement("PrimaryContactName"),
            new XElement("ShippingMethodCode", "1"),
            CreateNilElement("StateOrProvince"),
            CreateNilElement("Telephone1"),
            CreateNilElement("Telephone2"),
            CreateNilElement("Telephone3"),
            CreateNilElement("TimeZoneRuleVersionNumber"),
            CreateNilElement("UPSZone"),
            CreateNilElement("UTCOffset"),
            CreateNilElement("UTCConversionTimeZoneCode"));

    private static XElement CreateNilElement(string name) =>
        new(name, new XAttribute(XsiNamespace + "nil", "true"));

    private static string BuildDisplayMask(FamilyArtifact column)
    {
        if (string.Equals(GetProperty(column, ArtifactPropertyKeys.IsPrimaryName), "true", StringComparison.OrdinalIgnoreCase))
        {
            return "PrimaryName|ValidForAdvancedFind|ValidForForm|ValidForGrid";
        }

        return "ValidForAdvancedFind|ValidForForm|ValidForGrid";
    }

    private static string NormalizeGeneratedAttributeType(string? value) =>
        (value ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "string" => "nvarchar",
            "memo" => "ntext",
            "boolean" => "bit",
            "picklist" => "picklist",
            "integer" => "int",
            "image" => "image",
            "primarykey" => "primarykey",
            var other => other
        };

    private static string ResolveFieldLabel(IReadOnlyDictionary<string, string> columnLookup, string field) =>
        columnLookup.TryGetValue(field, out var label) ? label : field;

    private static string StableXmlGuid(string seed, params object[] suffixParts)
    {
        var raw = string.Join("|", new[] { seed }.Concat(suffixParts.Select(part => part.ToString() ?? string.Empty)));
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw));
        return $"{{{new Guid(hash[..16]).ToString("D")}}}";
    }

    private static string? GetSolutionDescription(CanonicalSolution model) =>
        model.Artifacts.FirstOrDefault(artifact => artifact.Family == ComponentFamily.SolutionShell)?.Properties is { } properties
            && properties.TryGetValue(ArtifactPropertyKeys.Description, out var description)
                ? description
                : null;

    private static string? GetPublisherDescription(CanonicalSolution model) =>
        model.Artifacts.FirstOrDefault(artifact => artifact.Family == ComponentFamily.Publisher)?.Properties is { } properties
            && properties.TryGetValue(ArtifactPropertyKeys.Description, out var description)
                ? description
                : null;
}

internal sealed record GeneratedOptionValue
{
    [JsonPropertyName("value")]
    public string? Value { get; init; }

    [JsonPropertyName("label")]
    public string? Label { get; init; }

    [JsonPropertyName("isHidden")]
    public string? IsHidden { get; init; }
}

internal sealed record GeneratedFormDefinition
{
    [JsonPropertyName("tabs")]
    public IReadOnlyList<GeneratedFormTab>? Tabs { get; init; }

    [JsonPropertyName("headerFields")]
    public IReadOnlyList<string>? HeaderFields { get; init; }
}

internal sealed record GeneratedFormTab
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("label")]
    public string? Label { get; init; }

    [JsonPropertyName("sections")]
    public IReadOnlyList<GeneratedFormSection>? Sections { get; init; }
}

internal sealed record GeneratedFormSection
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("label")]
    public string? Label { get; init; }

    [JsonPropertyName("fields")]
    public IReadOnlyList<string>? Fields { get; init; }

    [JsonPropertyName("controls")]
    public IReadOnlyList<GeneratedFormControl>? Controls { get; init; }
}

internal sealed record GeneratedFormControl
{
    [JsonPropertyName("kind")]
    public string? Kind { get; init; }

    [JsonPropertyName("field")]
    public string? Field { get; init; }

    [JsonPropertyName("label")]
    public string? Label { get; init; }

    [JsonPropertyName("quickFormEntity")]
    public string? QuickFormEntity { get; init; }

    [JsonPropertyName("quickFormId")]
    public string? QuickFormId { get; init; }

    [JsonPropertyName("controlMode")]
    public string? ControlMode { get; init; }

    [JsonPropertyName("relationshipName")]
    public string? RelationshipName { get; init; }

    [JsonPropertyName("targetTable")]
    public string? TargetTable { get; init; }

    [JsonPropertyName("defaultViewId")]
    public string? DefaultViewId { get; init; }

    [JsonPropertyName("isUserView")]
    public bool? IsUserView { get; init; }

    [JsonPropertyName("autoExpand")]
    public string? AutoExpand { get; init; }

    [JsonPropertyName("enableQuickFind")]
    public bool? EnableQuickFind { get; init; }

    [JsonPropertyName("enableViewPicker")]
    public bool? EnableViewPicker { get; init; }

    [JsonPropertyName("enableJumpBar")]
    public bool? EnableJumpBar { get; init; }

    [JsonPropertyName("enableChartPicker")]
    public bool? EnableChartPicker { get; init; }

    [JsonPropertyName("recordsPerPage")]
    public int? RecordsPerPage { get; init; }
}

internal sealed record GeneratedViewFilter
{
    [JsonPropertyName("attribute")]
    public string? Attribute { get; init; }

    [JsonPropertyName("operator")]
    public string? Operator { get; init; }

    [JsonPropertyName("value")]
    public string? Value { get; init; }
}

internal sealed record GeneratedViewOrder
{
    [JsonPropertyName("attribute")]
    public string? Attribute { get; init; }

    [JsonPropertyName("descending")]
    public string? Descending { get; init; }
}

internal sealed record GeneratedSiteMapDefinition
{
    [JsonPropertyName("areas")]
    public IReadOnlyList<GeneratedSiteMapArea>? Areas { get; init; }
}

internal sealed record GeneratedSiteMapArea
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("groups")]
    public IReadOnlyList<GeneratedSiteMapGroup>? Groups { get; init; }
}

internal sealed record GeneratedSiteMapGroup
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("subAreas")]
    public IReadOnlyList<GeneratedSiteMapSubArea>? SubAreas { get; init; }
}

internal sealed record GeneratedSiteMapSubArea
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("entity")]
    public string? Entity { get; init; }

    [JsonPropertyName("viewId")]
    public string? ViewId { get; init; }

    [JsonPropertyName("viewType")]
    public string? ViewType { get; init; }

    [JsonPropertyName("recordId")]
    public string? RecordId { get; init; }

    [JsonPropertyName("formId")]
    public string? FormId { get; init; }

    [JsonPropertyName("url")]
    public string? Url { get; init; }

    [JsonPropertyName("webResource")]
    public string? WebResource { get; init; }

    [JsonPropertyName("dashboard")]
    public string? Dashboard { get; init; }

    [JsonPropertyName("customPage")]
    public string? CustomPage { get; init; }

    [JsonPropertyName("customPageEntityName")]
    public string? CustomPageEntityName { get; init; }

    [JsonPropertyName("customPageRecordId")]
    public string? CustomPageRecordId { get; init; }

    [JsonPropertyName("appId")]
    public string? AppId { get; init; }

    [JsonPropertyName("client")]
    public string? Client { get; init; }

    [JsonPropertyName("passParams")]
    public bool? PassParams { get; init; }

    [JsonPropertyName("availableOffline")]
    public bool? AvailableOffline { get; init; }

    [JsonPropertyName("icon")]
    public string? Icon { get; init; }

    [JsonPropertyName("vectorIcon")]
    public string? VectorIcon { get; init; }
}
