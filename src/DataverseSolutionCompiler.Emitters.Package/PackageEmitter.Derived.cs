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
            new XAttribute("version", "9.2.0.0"),
            new XAttribute("SolutionPackageVersion", "9.2"),
            new XAttribute("languagecode", "1033"),
            new XAttribute("generatedBy", "DataverseSolutionCompiler"),
            new XAttribute(XNamespace.Xmlns + "xsi", XsiNamespace),
            new XElement("SolutionManifest",
                new XElement("UniqueName", model.Identity.UniqueName),
                CreateLocalizedNames(model.Identity.DisplayName),
                CreateDescriptions(GetSolutionDescription(model)),
                new XElement("Version", model.Identity.Version),
                new XElement("Managed", model.Identity.LayeringIntent == LayeringIntent.ManagedRelease ? "1" : "0"),
                new XElement("Publisher",
                    new XElement("UniqueName", model.Publisher.UniqueName),
                    CreateLocalizedNames(model.Publisher.DisplayName),
                    CreateDescriptions(GetPublisherDescription(model)),
                    new XElement("CustomizationPrefix", model.Publisher.Prefix),
                    new XElement("CustomizationOptionValuePrefix", "72727")),
                new XElement("RootComponents", rootComponents)));

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
                new XElement("Entities"),
                new XElement("Roles"),
                new XElement("Workflows")),
            emittedFiles,
            "Minimal customizations shell for derived compiler intent.");
    }

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

        foreach (var environmentVariable in model.Artifacts.Where(artifact => artifact.Family == ComponentFamily.EnvironmentVariableDefinition).OrderBy(artifact => artifact.LogicalName, StringComparer.OrdinalIgnoreCase))
        {
            yield return new XElement("RootComponent",
                new XAttribute("type", "380"),
                new XAttribute("schemaName", environmentVariable.LogicalName),
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
                        new XAttribute("Name", entityLogicalName),
                        CreateLocalizedNames(table.DisplayName ?? entitySchemaName),
                        new XElement("LocalizedCollectionNames",
                            new XElement("LocalizedCollectionName",
                                new XAttribute("description", $"{table.DisplayName ?? entitySchemaName}s"),
                                new XAttribute("languagecode", "1033"))),
                        CreateDescriptions(GetProperty(table, ArtifactPropertyKeys.Description)),
                        new XElement("EntitySetName", GetProperty(table, ArtifactPropertyKeys.EntitySetName) ?? $"{entityLogicalName}s"),
                        new XElement("OwnershipTypeMask", GetProperty(table, ArtifactPropertyKeys.OwnershipTypeMask) ?? "UserOwned"),
                        new XElement("IsCustomizable", string.Equals(GetProperty(table, ArtifactPropertyKeys.IsCustomizable), "true", StringComparison.OrdinalIgnoreCase) ? "1" : "0"),
                        new XElement("attributes", columns.Select(column => BuildAttributeElement(column, localOptionSets))),
                        keys.Length == 0 ? null : new XElement("keys", keys.Select(BuildEntityKeyElement)))));

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
        var element = new XElement("attribute",
            new XAttribute("PhysicalName", schemaName),
            new XElement("Type", attributeType),
            new XElement("Name", logicalName),
            new XElement("LogicalName", logicalName),
            new XElement("RequiredLevel", "none"),
            new XElement("DisplayMask", BuildDisplayMask(column)),
            new XElement("IsCustomField", string.Equals(GetProperty(column, ArtifactPropertyKeys.IsCustomField), "true", StringComparison.OrdinalIgnoreCase) ? "1" : "0"),
            new XElement("IsSecured", string.Equals(GetProperty(column, ArtifactPropertyKeys.IsSecured), "true", StringComparison.OrdinalIgnoreCase) ? "1" : "0"),
            new XElement("IsCustomizable", string.Equals(GetProperty(column, ArtifactPropertyKeys.IsCustomizable), "true", StringComparison.OrdinalIgnoreCase) ? "1" : "0"),
            new XElement("IsLogical", string.Equals(GetProperty(column, ArtifactPropertyKeys.IsLogical), "true", StringComparison.OrdinalIgnoreCase) ? "1" : "0"),
            new XElement("IntroducedVersion", "1.0.0.0"),
            new XElement("displaynames",
                new XElement("displayname",
                    new XAttribute("description", column.DisplayName ?? schemaName),
                    new XAttribute("languagecode", "1033"))),
            CreateDescriptions(GetProperty(column, ArtifactPropertyKeys.Description)));

        if (localOptionSets.TryGetValue(column.LogicalName, out var localOptionSet))
        {
            element.Add(BuildLocalOptionSetElement(localOptionSet));
        }
        else if (string.Equals(GetProperty(column, ArtifactPropertyKeys.IsGlobal), "true", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(GetProperty(column, ArtifactPropertyKeys.OptionSetName)))
        {
            element.Add(new XElement("optionset",
                new XAttribute("Name", GetProperty(column, ArtifactPropertyKeys.OptionSetName)!),
                new XElement("OptionSetType", GetProperty(column, ArtifactPropertyKeys.OptionSetType) ?? "picklist"),
                new XElement("IsGlobal", "1")));
        }

        return element;
    }

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

        var tabs = (definition.Tabs ?? [])
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
                    new XAttribute("headerdensity", "HighWithControls"),
                    new XElement("tabs", tabs),
                    header),
                new XElement("IsCustomizable", "1"),
                new XElement("CanBeDeleted", "1"),
                CreateLocalizedNames(form.DisplayName ?? form.LogicalName),
                CreateDescriptions(GetProperty(form, ArtifactPropertyKeys.Description))));

        WriteXml(packageRoot, $"{entityDirectory}/FormXml/main/{{{formId}}}.xml", root, emittedFiles, $"Synthesized main form for {form.DisplayName ?? form.LogicalName}.");
    }

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
            new XElement("rows", (section.Fields ?? []).Select((field, fieldIndex) => BuildFieldRowElement(formId, columnLookup, field, tabIndex, sectionIndex, fieldIndex))));

    private static XElement BuildFieldRowElement(
        string formId,
        IReadOnlyDictionary<string, string> columnLookup,
        string field,
        int tabIndex,
        int sectionIndex,
        int fieldIndex) =>
        new("row",
            new XElement("cell",
                new XAttribute("id", StableXmlGuid(formId, "cell", tabIndex, sectionIndex, fieldIndex)),
                new XAttribute("showlabel", "true"),
                new XElement("labels", new XElement("label", new XAttribute("description", ResolveFieldLabel(columnLookup, field)), new XAttribute("languagecode", "1033"))),
                new XElement("control",
                    new XAttribute("id", field),
                    new XAttribute("datafieldname", field),
                    new XAttribute("classid", "{4273EDBD-AC1D-40d3-9FB2-095C621B552D}"))));

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
        var relationshipGroups = model.Artifacts
            .Where(artifact => artifact.Family == ComponentFamily.Relationship)
            .GroupBy(artifact => GetProperty(artifact, ArtifactPropertyKeys.ReferencedEntity) ?? artifact.LogicalName, StringComparer.OrdinalIgnoreCase);

        foreach (var relationshipGroup in relationshipGroups.OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            var root = new XElement("EntityRelationships",
                new XAttribute(XNamespace.Xmlns + "xsi", XsiNamespace),
                relationshipGroup
                    .OrderBy(artifact => artifact.LogicalName, StringComparer.OrdinalIgnoreCase)
                    .Select(artifact => new XElement("EntityRelationship",
                        new XAttribute("Name", artifact.LogicalName),
                        new XElement("EntityRelationshipType", GetProperty(artifact, ArtifactPropertyKeys.RelationshipType) ?? "OneToMany"),
                        new XElement("IsCustomizable", "1"),
                        new XElement("IntroducedVersion", "1.0.0.0"),
                        new XElement("ReferencingEntityName", GetProperty(artifact, ArtifactPropertyKeys.ReferencingEntity) ?? string.Empty),
                        new XElement("ReferencedEntityName", GetProperty(artifact, ArtifactPropertyKeys.ReferencedEntity) ?? string.Empty),
                        new XElement("ReferencingAttributeName", GetProperty(artifact, ArtifactPropertyKeys.ReferencingAttribute) ?? string.Empty),
                        new XElement("RelationshipDescription", CreateDescriptions(GetProperty(artifact, ArtifactPropertyKeys.Description))))));

            WriteXml(
                packageRoot,
                $"Other/Relationships/{relationshipGroup.Key}.xml",
                root,
                emittedFiles,
                $"Synthesized relationship shell for {relationshipGroup.Key}.");
        }
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

    private static void WriteDerivedAppModules(CanonicalSolution model, string packageRoot, List<EmittedArtifact> emittedFiles)
    {
        foreach (var appModule in model.Artifacts.Where(artifact => artifact.Family == ComponentFamily.AppModule).OrderBy(artifact => artifact.LogicalName, StringComparer.OrdinalIgnoreCase))
        {
            var componentTypes = JsonSerializer.Deserialize<List<string>>(GetProperty(appModule, ArtifactPropertyKeys.ComponentTypesJson) ?? "[]", DerivedJsonOptions) ?? [];
            var root = new XElement("AppModule",
                new XElement("UniqueName", appModule.LogicalName),
                new XElement("IntroducedVersion", "1.0.0.0"),
                new XElement("statecode", "0"),
                new XElement("statuscode", "1"),
                new XElement("FormFactor", "1"),
                new XElement("ClientType", "4"),
                new XElement("NavigationType", "0"),
                new XElement("AppModuleComponents",
                    componentTypes.Select(componentType => new XElement("AppModuleComponent", new XAttribute("type", componentType), new XAttribute("schemaName", appModule.LogicalName)))),
                new XElement("AppModuleRoleMaps"),
                CreateLocalizedNames(appModule.DisplayName ?? appModule.LogicalName),
                CreateDescriptions(GetProperty(appModule, ArtifactPropertyKeys.Description)),
                new XElement("appsettings"));

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
            new XAttribute("Entity", subArea.Entity ?? string.Empty),
            new XAttribute("Client", "Web"),
            new XAttribute("PassParams", "true"),
            new XElement("Titles", new XElement("Title", new XAttribute("LCID", "1033"), new XAttribute("Title", subArea.Title ?? $"SubArea {subAreaIndex + 1}"))));

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
}
