using System.Text.Json;
using DataverseSolutionCompiler.Domain.Diagnostics;
using DataverseSolutionCompiler.Domain.Emission;
using DataverseSolutionCompiler.Domain.Model;

namespace DataverseSolutionCompiler.Emitters.TrackedSource;

public sealed partial class IntentSpecEmitter
{
    private static readonly IReadOnlySet<ComponentFamily> SourceBackedFamilies = new HashSet<ComponentFamily>
    {
        ComponentFamily.WebResource,
        ComponentFamily.CanvasApp,
        ComponentFamily.EntityAnalyticsConfiguration,
        ComponentFamily.AiProjectType,
        ComponentFamily.AiProject,
        ComponentFamily.AiConfiguration,
        ComponentFamily.PluginAssembly,
        ComponentFamily.PluginType,
        ComponentFamily.PluginStep,
        ComponentFamily.PluginStepImage,
        ComponentFamily.ServiceEndpoint,
        ComponentFamily.Connector,
        ComponentFamily.DuplicateRule,
        ComponentFamily.DuplicateRuleCondition,
        ComponentFamily.RoutingRule,
        ComponentFamily.RoutingRuleItem,
        ComponentFamily.MobileOfflineProfile,
        ComponentFamily.MobileOfflineProfileItem,
        ComponentFamily.Role,
        ComponentFamily.RolePrivilege,
        ComponentFamily.FieldSecurityProfile,
        ComponentFamily.FieldPermission,
        ComponentFamily.ConnectionRole,
        ComponentFamily.Report,
        ComponentFamily.Template,
        ComponentFamily.DisplayString,
        ComponentFamily.Attachment,
        ComponentFamily.LegacyAsset
    };

    private static readonly HashSet<string> KnownPlatformFormFieldNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "createdby",
        "createdon",
        "modifiedby",
        "modifiedon",
        "ownerid",
        "owningbusinessunit",
        "owningteam",
        "owninguser",
        "processid",
        "stageid",
        "statecode",
        "statuscode",
        "transactioncurrencyid",
        "versionnumber"
    };

    private static IReadOnlyList<IntentReportEntry> BuildUnsupportedArtifactEntries(IEnumerable<FamilyArtifact> artifacts, IReadOnlySet<string> sourceBackedArtifactKeys) =>
        artifacts
            .Where(artifact => !SupportedFamilies.Contains(artifact.Family))
            .Where(artifact => !SourceBackedFamilies.Contains(artifact.Family))
            .Where(artifact => !sourceBackedArtifactKeys.Contains(BuildArtifactKey(artifact)))
            .Select(artifact => new IntentReportEntry(
                artifact.Family.ToString(),
                artifact.LogicalName,
                "Family is outside the supported reverse-generation subset for intent-spec JSON.",
                ReverseGenerationReportCategories.UnsupportedFamily))
            .ToArray();

    private static IReadOnlyList<IntentReportEntry> BuildUnsupportedDiagnosticEntries(IEnumerable<CompilerDiagnostic> diagnostics) =>
        diagnostics
            .Where(diagnostic => diagnostic.Code.StartsWith("tracked-source-intent-unsupported-", StringComparison.Ordinal))
            .Select(diagnostic => new IntentReportEntry(
                diagnostic.Code["tracked-source-intent-unsupported-".Length..],
                diagnostic.Location ?? diagnostic.Code,
                diagnostic.Message,
                ReverseGenerationReportCategories.UnsupportedFamily))
            .ToArray();

    private static Dictionary<string, FamilyArtifact[]> GroupArtifactsByEntity(IEnumerable<FamilyArtifact> artifacts, ComponentFamily family) =>
        artifacts
            .Where(artifact => artifact.Family == family)
            .GroupBy(artifact => NormalizeLogicalName(GetProperty(artifact, ArtifactPropertyKeys.EntityLogicalName)) ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderBy(artifact => artifact.DisplayName ?? artifact.LogicalName, StringComparer.OrdinalIgnoreCase).ToArray(), StringComparer.OrdinalIgnoreCase);

    private static Dictionary<(string TableLogicalName, string ColumnLogicalName), FamilyArtifact> BuildSupportedRelationshipLookup(
        IEnumerable<FamilyArtifact> artifacts,
        ICollection<IntentReportEntry> unsupportedEntries)
    {
        var lookup = new Dictionary<(string TableLogicalName, string ColumnLogicalName), FamilyArtifact>();
        foreach (var relationship in artifacts
                     .Where(artifact => artifact.Family == ComponentFamily.Relationship)
                     .OrderBy(artifact => artifact.LogicalName, StringComparer.OrdinalIgnoreCase))
        {
            var relationshipType = GetProperty(relationship, ArtifactPropertyKeys.RelationshipType);
            var referencingEntity = NormalizeLogicalName(GetProperty(relationship, ArtifactPropertyKeys.ReferencingEntity));
            var referencingAttribute = NormalizeLogicalName(GetProperty(relationship, ArtifactPropertyKeys.ReferencingAttribute));
            var referencedEntity = NormalizeLogicalName(GetProperty(relationship, ArtifactPropertyKeys.ReferencedEntity));
            if (!string.Equals(relationshipType, "OneToMany", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(referencingEntity)
                || string.IsNullOrWhiteSpace(referencingAttribute)
                || string.IsNullOrWhiteSpace(referencedEntity))
            {
                unsupportedEntries.Add(new IntentReportEntry(
                    ComponentFamily.Relationship.ToString(),
                    relationship.LogicalName,
                    "Only one-to-many lookup relationships with referenced entity and referencing attribute metadata can be folded back into lookup columns."));
                continue;
            }

            var key = (referencingEntity, referencingAttribute);
            if (!lookup.TryAdd(key, relationship))
            {
                unsupportedEntries.Add(new IntentReportEntry(
                    ComponentFamily.Relationship.ToString(),
                    relationship.LogicalName,
                    "Multiple relationships target the same lookup column, so reverse generation could not choose one deterministic lookup contract."));
            }
        }

        return lookup;
    }

    private static List<IntentTableSpec> BuildTableSpecs(
        IEnumerable<FamilyArtifact> artifacts,
        IReadOnlyDictionary<string, FamilyArtifact> localOptionSetsByColumn,
        IReadOnlyDictionary<(string TableLogicalName, string ColumnLogicalName), FamilyArtifact> relationshipsByTableAndAttribute,
        IReadOnlyDictionary<string, FamilyArtifact[]> keysByTable,
        IReadOnlyDictionary<string, FamilyArtifact[]> formsByTable,
        IReadOnlyDictionary<string, FamilyArtifact[]> viewsByTable,
        ICollection<IntentReportEntry> unsupportedEntries,
        ICollection<PreservedIdEntry> preservedIds,
        ISet<string> emittedFamilies)
    {
        var tableSpecs = new List<IntentTableSpec>();
        foreach (var table in artifacts
                     .Where(artifact => artifact.Family == ComponentFamily.Table)
                     .OrderBy(artifact => artifact.LogicalName, StringComparer.OrdinalIgnoreCase))
        {
            var tableLogicalName = NormalizeLogicalName(table.LogicalName);
            if (string.IsNullOrWhiteSpace(tableLogicalName))
            {
                unsupportedEntries.Add(new IntentReportEntry(ComponentFamily.Table.ToString(), table.LogicalName, "Table logical name is missing."));
                continue;
            }

            emittedFamilies.Add(ComponentFamily.Table.ToString());

            var authoredColumns = artifacts
                .Where(artifact => artifact.Family == ComponentFamily.Column
                    && string.Equals(NormalizeLogicalName(GetProperty(artifact, ArtifactPropertyKeys.EntityLogicalName)), tableLogicalName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(artifact => artifact.LogicalName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var imageConfigurations = artifacts
                .Where(artifact => artifact.Family == ComponentFamily.ImageConfiguration
                    && string.Equals(NormalizeLogicalName(GetProperty(artifact, ArtifactPropertyKeys.EntityLogicalName)), tableLogicalName, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            var entityImageConfiguration = imageConfigurations.FirstOrDefault(artifact =>
                string.Equals(GetProperty(artifact, ArtifactPropertyKeys.ImageConfigurationScope), "entity", StringComparison.OrdinalIgnoreCase));
            var imageConfigurationsByAttribute = imageConfigurations
                .Where(artifact => string.Equals(GetProperty(artifact, ArtifactPropertyKeys.ImageConfigurationScope), "attribute", StringComparison.OrdinalIgnoreCase))
                .GroupBy(artifact => NormalizeLogicalName(GetProperty(artifact, ArtifactPropertyKeys.ImageAttributeLogicalName)) ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
            var columnSpecs = new List<IntentTableColumnSpec>();
            var allowedFieldNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                NormalizeLogicalName(GetProperty(table, ArtifactPropertyKeys.PrimaryIdAttribute)) ?? $"{tableLogicalName}id",
                NormalizeLogicalName(GetProperty(table, ArtifactPropertyKeys.PrimaryNameAttribute)) ?? $"{tableLogicalName}name"
            };

            foreach (var column in authoredColumns)
            {
                if (GetBoolProperty(column, ArtifactPropertyKeys.IsPrimaryKey) || GetBoolProperty(column, ArtifactPropertyKeys.IsPrimaryName))
                {
                    continue;
                }

                var columnSpec = BuildColumnSpec(column, localOptionSetsByColumn, relationshipsByTableAndAttribute, imageConfigurationsByAttribute, unsupportedEntries);
                if (columnSpec is null)
                {
                    continue;
                }

                columnSpecs.Add(columnSpec);
                emittedFamilies.Add(ComponentFamily.Column.ToString());
                if (string.Equals(columnSpec.Type, "lookup", StringComparison.OrdinalIgnoreCase))
                {
                    emittedFamilies.Add(ComponentFamily.Relationship.ToString());
                }

                if (!string.IsNullOrWhiteSpace(columnSpec.LogicalName))
                {
                    allowedFieldNames.Add(columnSpec.LogicalName);
                }
            }

            var keySpecs = BuildKeySpecs(keysByTable.GetValueOrDefault(tableLogicalName) ?? [], unsupportedEntries);
            if (keySpecs.Count > 0)
            {
                emittedFamilies.Add(ComponentFamily.Key.ToString());
            }

            if (imageConfigurations.Length > 0)
            {
                emittedFamilies.Add(ComponentFamily.ImageConfiguration.ToString());
            }

            var allowedFormFieldNames = CreateAllowedFormFieldNames(allowedFieldNames);
            var formSpecs = BuildFormSpecs(
                tableLogicalName,
                formsByTable.GetValueOrDefault(tableLogicalName) ?? [],
                artifacts,
                allowedFormFieldNames,
                unsupportedEntries,
                preservedIds);
            if (formSpecs.Count > 0)
            {
                emittedFamilies.Add(ComponentFamily.Form.ToString());
            }

            var viewSpecs = BuildViewSpecs(tableLogicalName, viewsByTable.GetValueOrDefault(tableLogicalName) ?? [], allowedFieldNames, unsupportedEntries, preservedIds);
            if (viewSpecs.Count > 0)
            {
                emittedFamilies.Add(ComponentFamily.View.ToString());
            }

            var visualizationSpecs = BuildVisualizationSpecs(
                tableLogicalName,
                artifacts.Where(artifact => artifact.Family == ComponentFamily.Visualization
                    && string.Equals(GetProperty(artifact, ArtifactPropertyKeys.TargetEntity), tableLogicalName, StringComparison.OrdinalIgnoreCase)),
                artifacts,
                unsupportedEntries,
                preservedIds);
            if (visualizationSpecs.Count > 0)
            {
                emittedFamilies.Add(ComponentFamily.Visualization.ToString());
            }

            tableSpecs.Add(new IntentTableSpec
            {
                LogicalName = tableLogicalName,
                SchemaName = GetProperty(table, ArtifactPropertyKeys.SchemaName) ?? table.DisplayName ?? tableLogicalName,
                DisplayName = table.DisplayName ?? tableLogicalName,
                Description = GetProperty(table, ArtifactPropertyKeys.Description),
                EntitySetName = GetProperty(table, ArtifactPropertyKeys.EntitySetName),
                OwnershipTypeMask = GetProperty(table, ArtifactPropertyKeys.OwnershipTypeMask),
                PrimaryImageAttribute = NormalizeLogicalName(GetProperty(entityImageConfiguration ?? table, ArtifactPropertyKeys.PrimaryImageAttribute)),
                IsCustomizable = GetNullableBoolProperty(table, ArtifactPropertyKeys.IsCustomizable),
                Columns = columnSpecs,
                Keys = keySpecs,
                Forms = formSpecs,
                Views = viewSpecs,
                Visualizations = visualizationSpecs
            });
        }

        return tableSpecs;
    }

    private static IntentTableColumnSpec? BuildColumnSpec(
        FamilyArtifact column,
        IReadOnlyDictionary<string, FamilyArtifact> localOptionSetsByColumn,
        IReadOnlyDictionary<(string TableLogicalName, string ColumnLogicalName), FamilyArtifact> relationshipsByTableAndAttribute,
        IReadOnlyDictionary<string, FamilyArtifact> imageConfigurationsByAttribute,
        ICollection<IntentReportEntry> unsupportedEntries)
    {
        var entityLogicalName = NormalizeLogicalName(GetProperty(column, ArtifactPropertyKeys.EntityLogicalName));
        var columnLogicalName = NormalizeLogicalName(column.LogicalName.Split('|').LastOrDefault());
        if (string.IsNullOrWhiteSpace(entityLogicalName) || string.IsNullOrWhiteSpace(columnLogicalName))
        {
            unsupportedEntries.Add(new IntentReportEntry(ComponentFamily.Column.ToString(), column.LogicalName, "Column identity is incomplete."));
            return null;
        }

        var attributeType = NormalizeAttributeType(GetProperty(column, ArtifactPropertyKeys.AttributeType));
        if (attributeType is null)
        {
            unsupportedEntries.Add(new IntentReportEntry(
                ComponentFamily.Column.ToString(),
                column.LogicalName,
                $"Attribute type '{GetProperty(column, ArtifactPropertyKeys.AttributeType) ?? "(missing)"}' is not supported by the reverse-generated intent subset."));
            return null;
        }

        imageConfigurationsByAttribute.TryGetValue(columnLogicalName, out var imageConfiguration);

        var spec = new IntentTableColumnSpec
        {
            LogicalName = columnLogicalName,
            SchemaName = GetProperty(column, ArtifactPropertyKeys.SchemaName) ?? columnLogicalName,
            DisplayName = column.DisplayName ?? columnLogicalName,
            Description = GetProperty(column, ArtifactPropertyKeys.Description),
            Type = attributeType,
            IsSecured = GetBoolProperty(column, ArtifactPropertyKeys.IsSecured),
            IsCustomizable = GetNullableBoolProperty(column, ArtifactPropertyKeys.IsCustomizable)
        };

        if (string.Equals(attributeType, "image", StringComparison.OrdinalIgnoreCase))
        {
            spec = spec with
            {
                CanStoreFullImage = imageConfiguration is null
                    ? GetNullableBoolProperty(column, ArtifactPropertyKeys.CanStoreFullImage)
                    : GetNullableBoolProperty(imageConfiguration, ArtifactPropertyKeys.CanStoreFullImage),
                IsPrimaryImage = imageConfiguration is null
                    ? GetNullableBoolProperty(column, ArtifactPropertyKeys.IsPrimaryImage)
                    : GetNullableBoolProperty(imageConfiguration, ArtifactPropertyKeys.IsPrimaryImage)
            };
        }

        if (string.Equals(attributeType, "lookup", StringComparison.OrdinalIgnoreCase))
        {
            if (!relationshipsByTableAndAttribute.TryGetValue((entityLogicalName, columnLogicalName), out var relationship))
            {
                    unsupportedEntries.Add(new IntentReportEntry(
                        ComponentFamily.Column.ToString(),
                        column.LogicalName,
                        "Lookup columns require a one-to-many relationship that can be folded back into targetTable and relationshipSchemaName.",
                        ReverseGenerationReportCategories.MissingSourceFidelity));
                return null;
            }

            var targetTable = NormalizeLogicalName(GetProperty(relationship, ArtifactPropertyKeys.ReferencedEntity));
            if (string.IsNullOrWhiteSpace(targetTable))
            {
                unsupportedEntries.Add(new IntentReportEntry(
                    ComponentFamily.Column.ToString(),
                    column.LogicalName,
                    "Lookup column target table could not be reconstructed from the referencing relationship."));
                return null;
            }

            return spec with
            {
                TargetTable = targetTable,
                RelationshipSchemaName = relationship.LogicalName
            };
        }

        if (string.Equals(attributeType, "choice", StringComparison.OrdinalIgnoreCase) || string.Equals(attributeType, "boolean", StringComparison.OrdinalIgnoreCase))
        {
            if (GetBoolProperty(column, ArtifactPropertyKeys.IsGlobal))
            {
                var optionSetName = NormalizeLogicalName(GetProperty(column, ArtifactPropertyKeys.OptionSetName));
                if (string.IsNullOrWhiteSpace(optionSetName))
                {
                    unsupportedEntries.Add(new IntentReportEntry(
                        ComponentFamily.Column.ToString(),
                        column.LogicalName,
                        "Global choice columns require optionSetName so they can point back to a globalOptionSet."));
                    return null;
                }

                spec = spec with { GlobalOptionSet = optionSetName };
            }
            else
            {
                if (!localOptionSetsByColumn.TryGetValue(column.LogicalName, out var optionSetArtifact))
                {
                    unsupportedEntries.Add(new IntentReportEntry(
                        ComponentFamily.Column.ToString(),
                        column.LogicalName,
                        "Local choice and boolean columns require inline option metadata to reverse-generate intent-spec JSON.",
                        ReverseGenerationReportCategories.MissingSourceFidelity));
                    return null;
                }

                spec = spec with { Options = ReadOptionItems(GetProperty(optionSetArtifact, ArtifactPropertyKeys.OptionsJson)) };
            }
        }

        return spec;
    }

    private static List<IntentTableKeySpec> BuildKeySpecs(IEnumerable<FamilyArtifact> keys, ICollection<IntentReportEntry> unsupportedEntries)
    {
        var specs = new List<IntentTableKeySpec>();
        foreach (var key in keys)
        {
            var keyLogicalName = NormalizeLogicalName(key.LogicalName.Split('|').LastOrDefault());
            var schemaName = GetProperty(key, ArtifactPropertyKeys.SchemaName);
            var keyAttributes = ReadStringArray(GetProperty(key, ArtifactPropertyKeys.KeyAttributesJson));
            if (string.IsNullOrWhiteSpace(keyLogicalName) || string.IsNullOrWhiteSpace(schemaName) || keyAttributes.Count == 0)
            {
                unsupportedEntries.Add(new IntentReportEntry(
                    ComponentFamily.Key.ToString(),
                    key.LogicalName,
                    "Alternate keys require logicalName, schemaName, and non-empty keyAttributes for reverse generation."));
                continue;
            }

            specs.Add(new IntentTableKeySpec
            {
                LogicalName = keyLogicalName,
                SchemaName = schemaName,
                Description = GetProperty(key, ArtifactPropertyKeys.Description),
                KeyAttributes = keyAttributes
            });
        }

        return specs;
    }

    private static List<IntentFormSpec> BuildFormSpecs(
        string tableLogicalName,
        IEnumerable<FamilyArtifact> forms,
        IEnumerable<FamilyArtifact> allArtifacts,
        IReadOnlySet<string> allowedFieldNames,
        ICollection<IntentReportEntry> unsupportedEntries,
        ICollection<PreservedIdEntry> preservedIds)
    {
        var specs = new List<IntentFormSpec>();
        foreach (var form in forms)
        {
            if (ShouldEmitAsSourceBacked(form, allArtifacts))
            {
                continue;
            }

            var definition = ReadFormDefinition(form);
            if (definition is null)
            {
                unsupportedEntries.Add(new IntentReportEntry(
                    ComponentFamily.Form.ToString(),
                    form.LogicalName,
                    "Main forms require formDefinitionJson so the layout can be reconstructed.",
                    ReverseGenerationReportCategories.MissingSourceFidelity));
                continue;
            }

            if (!AllFieldsAreSupported(EnumerateSupportedFormFields(definition), allowedFieldNames)
                || !AllFieldsAreSupported(definition.HeaderFields, allowedFieldNames))
            {
                if (ShouldEmitAsSourceBacked(form, allArtifacts))
                {
                    continue;
                }

                unsupportedEntries.Add(new IntentReportEntry(
                    ComponentFamily.Form.ToString(),
                    form.LogicalName,
                    $"Form '{form.DisplayName ?? form.LogicalName}' references fields outside the supported reverse-generated column set for table '{tableLogicalName}'.")); 
                continue;
            }

            var formId = NormalizeGuid(GetProperty(form, ArtifactPropertyKeys.FormId));
            if (!string.IsNullOrWhiteSpace(formId))
            {
                preservedIds.Add(new PreservedIdEntry(ComponentFamily.Form.ToString(), form.LogicalName, formId));
            }

            specs.Add(new IntentFormSpec
            {
                Id = formId,
                Name = definition.Name ?? form.DisplayName ?? form.LogicalName,
                Description = definition.Description,
                Type = NormalizeFormType(GetProperty(form, ArtifactPropertyKeys.FormType)) ?? NormalizeFormType(definition.Type) ?? "main",
                Tabs = definition.Tabs.Select(tab => new IntentFormTabSpec
                {
                    Name = tab.Name,
                    Label = tab.Label,
                    Sections = tab.Sections.Select(section => new IntentFormSectionSpec
                    {
                        Name = section.Name,
                        Label = section.Label,
                        Fields = ShouldUseFieldShorthand(section) ? section.Fields : null,
                        Controls = ShouldUseFieldShorthand(section) ? null : BuildFormControlSpecs(section)
                    }).ToArray()
                }).ToArray(),
                HeaderFields = definition.HeaderFields
            });
        }

        return specs;
    }

    private static List<IntentViewSpec> BuildViewSpecs(
        string tableLogicalName,
        IEnumerable<FamilyArtifact> views,
        IReadOnlySet<string> _allowedFieldNames,
        ICollection<IntentReportEntry> unsupportedEntries,
        ICollection<PreservedIdEntry> preservedIds)
    {
        var specs = new List<IntentViewSpec>();
        foreach (var view in views)
        {
            var queryType = GetProperty(view, ArtifactPropertyKeys.QueryType);
            if (!string.IsNullOrWhiteSpace(queryType) && !string.Equals(queryType, "0", StringComparison.OrdinalIgnoreCase))
            {
                unsupportedEntries.Add(new IntentReportEntry(
                    ComponentFamily.View.ToString(),
                    view.LogicalName,
                    $"View '{view.DisplayName ?? view.LogicalName}' is platform-generated or non-authorable for reverse-generated intent-spec JSON because it reported queryType '{queryType}'.",
                    ReverseGenerationReportCategories.PlatformGeneratedArtifact));
                continue;
            }

            if (GetProperty(view, ArtifactPropertyKeys.CanBeDeleted) is { } canBeDeleted
                && !NormalizeBoolean(canBeDeleted))
            {
                unsupportedEntries.Add(new IntentReportEntry(
                    ComponentFamily.View.ToString(),
                    view.LogicalName,
                    $"View '{view.DisplayName ?? view.LogicalName}' is platform-generated or non-authorable for reverse-generated intent-spec JSON because it is marked CanBeDeleted = 0.",
                    ReverseGenerationReportCategories.PlatformGeneratedArtifact));
                continue;
            }

            var layoutColumns = ReadStringArray(GetProperty(view, ArtifactPropertyKeys.LayoutColumnsJson));
            var fetchAttributes = ReadStringArray(GetProperty(view, ArtifactPropertyKeys.FetchAttributesJson));
            var filters = ReadViewFilters(GetProperty(view, ArtifactPropertyKeys.FiltersJson));
            var orders = ReadViewOrders(GetProperty(view, ArtifactPropertyKeys.OrdersJson));

            if (layoutColumns.Count == 0 || fetchAttributes.Count == 0)
            {
                unsupportedEntries.Add(new IntentReportEntry(
                    ComponentFamily.View.ToString(),
                    view.LogicalName,
                    $"View '{view.DisplayName ?? view.LogicalName}' is missing the layout or fetch metadata needed to reverse-generate a rebuild-safe savedquery view for table '{tableLogicalName}'.",
                    ReverseGenerationReportCategories.MissingSourceFidelity));
                continue;
            }

            var viewId = NormalizeGuid(GetProperty(view, ArtifactPropertyKeys.ViewId));
            if (!string.IsNullOrWhiteSpace(viewId))
            {
                preservedIds.Add(new PreservedIdEntry(ComponentFamily.View.ToString(), view.LogicalName, viewId));
            }

            specs.Add(new IntentViewSpec
            {
                Id = viewId,
                Name = view.DisplayName ?? view.LogicalName,
                Type = "savedquery",
                LayoutColumns = layoutColumns,
                FetchAttributes = fetchAttributes,
                Filters = filters,
                Orders = orders
            });
        }

        return specs;
    }

    private static Dictionary<string, IntentSiteMapSpec> BuildSiteMapSpecs(
        IEnumerable<FamilyArtifact> artifacts,
        ICollection<IntentReportEntry> unsupportedEntries,
        ISet<string> emittedFamilies)
    {
        var siteMaps = new Dictionary<string, IntentSiteMapSpec>(StringComparer.OrdinalIgnoreCase);
        foreach (var siteMap in artifacts
                     .Where(artifact => artifact.Family == ComponentFamily.SiteMap)
                     .OrderBy(artifact => artifact.LogicalName, StringComparer.OrdinalIgnoreCase))
        {
            var spec = BuildSiteMapSpec(siteMap, unsupportedEntries);
            if (spec is null)
            {
                continue;
            }

            emittedFamilies.Add(ComponentFamily.SiteMap.ToString());
            siteMaps[siteMap.LogicalName] = spec;
        }

        return siteMaps;
    }

    private static List<IntentAppModuleSpec> BuildAppModuleSpecs(
        IEnumerable<FamilyArtifact> artifacts,
        IReadOnlyDictionary<string, IntentSiteMapSpec> siteMapsByLogicalName,
        ICollection<IntentReportEntry> unsupportedEntries,
        ISet<string> emittedFamilies)
    {
        var specs = new List<IntentAppModuleSpec>();
        foreach (var appModule in artifacts
                     .Where(artifact => artifact.Family == ComponentFamily.AppModule)
                     .OrderBy(artifact => artifact.LogicalName, StringComparer.OrdinalIgnoreCase))
        {
            if (!siteMapsByLogicalName.TryGetValue(appModule.LogicalName, out var siteMap))
            {
                continue;
            }

            var appSettings = BuildAppSettingSpecs(artifacts, appModule.LogicalName);
            emittedFamilies.Add(ComponentFamily.AppModule.ToString());
            if (appSettings.Count > 0)
            {
                emittedFamilies.Add(ComponentFamily.AppSetting.ToString());
            }
            specs.Add(new IntentAppModuleSpec
            {
                UniqueName = appModule.LogicalName,
                DisplayName = appModule.DisplayName ?? appModule.LogicalName,
                Description = GetProperty(appModule, ArtifactPropertyKeys.Description),
                SiteMap = siteMap,
                AppSettings = appSettings
            });
        }

        return specs;
    }

    private static List<IntentVisualizationSpec> BuildVisualizationSpecs(
        string tableLogicalName,
        IEnumerable<FamilyArtifact> visualizations,
        IEnumerable<FamilyArtifact> allArtifacts,
        ICollection<IntentReportEntry> unsupportedEntries,
        ICollection<PreservedIdEntry> preservedIds)
    {
        var specs = new List<IntentVisualizationSpec>();
        foreach (var visualization in visualizations)
        {
            if (ShouldEmitAsSourceBacked(visualization, allArtifacts))
            {
                continue;
            }

            var dataDescriptionXml = GetProperty(visualization, ArtifactPropertyKeys.DataDescriptionXml);
            var presentationDescriptionXml = GetProperty(visualization, ArtifactPropertyKeys.PresentationDescriptionXml);
            if (string.IsNullOrWhiteSpace(dataDescriptionXml) || string.IsNullOrWhiteSpace(presentationDescriptionXml))
            {
                unsupportedEntries.Add(new IntentReportEntry(
                    ComponentFamily.Visualization.ToString(),
                    visualization.LogicalName,
                    $"Visualization '{visualization.DisplayName ?? visualization.LogicalName}' is missing the metadata needed to reverse-generate a rebuild-safe chart for table '{tableLogicalName}'.",
                    ReverseGenerationReportCategories.MissingSourceFidelity));
                continue;
            }

            var visualizationId = NormalizeGuid(GetProperty(visualization, ArtifactPropertyKeys.VisualizationId));
            if (!string.IsNullOrWhiteSpace(visualizationId))
            {
                preservedIds.Add(new PreservedIdEntry(ComponentFamily.Visualization.ToString(), visualization.LogicalName, visualizationId));
            }

            specs.Add(new IntentVisualizationSpec
            {
                Id = visualizationId,
                Name = visualization.DisplayName ?? visualization.LogicalName,
                Description = GetProperty(visualization, ArtifactPropertyKeys.Description),
                ChartTypes = ReadStringArray(GetProperty(visualization, ArtifactPropertyKeys.ChartTypesJson)),
                GroupByColumns = ReadStringArray(GetProperty(visualization, ArtifactPropertyKeys.GroupByColumnsJson)),
                MeasureAliases = ReadStringArray(GetProperty(visualization, ArtifactPropertyKeys.MeasureAliasesJson)),
                TitleNames = ReadStringArray(GetProperty(visualization, ArtifactPropertyKeys.TitleNamesJson)),
                DataDescriptionXml = dataDescriptionXml,
                PresentationDescriptionXml = presentationDescriptionXml
            });
        }

        return specs;
    }

    private static IntentSiteMapSpec? BuildSiteMapSpec(FamilyArtifact siteMap, ICollection<IntentReportEntry> unsupportedEntries)
    {
        var definitionJson = GetProperty(siteMap, ArtifactPropertyKeys.SiteMapDefinitionJson);
        if (string.IsNullOrWhiteSpace(definitionJson))
        {
            return null;
        }

        var definition = JsonSerializer.Deserialize<SiteMapDefinition>(definitionJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (definition?.Areas is null)
        {
            return null;
        }

        var areas = new List<IntentSiteMapAreaSpec>();
        foreach (var area in definition.Areas)
        {
            var groups = new List<IntentSiteMapGroupSpec>();
            foreach (var group in area.Groups ?? [])
            {
                var subAreas = new List<IntentSiteMapSubAreaSpec>();
                foreach (var subArea in group.SubAreas ?? [])
                {
                    var entity = NormalizeLogicalName(subArea.Entity);
                    var hasUrl = !string.IsNullOrWhiteSpace(subArea.Url);
                    var hasWebResource = !string.IsNullOrWhiteSpace(subArea.WebResource);
                    if (!string.IsNullOrWhiteSpace(entity) == false && !hasUrl && !hasWebResource)
                    {
                        return null;
                    }

                    subAreas.Add(new IntentSiteMapSubAreaSpec
                    {
                        Id = subArea.Id ?? $"subarea_{subAreas.Count + 1}",
                        Title = subArea.Title ?? subArea.Entity ?? subArea.WebResource ?? subArea.Url!,
                        Entity = entity,
                        Url = subArea.Url is { Length: > 0 } url && !url.StartsWith("$webresource:", StringComparison.OrdinalIgnoreCase) ? url : null,
                        WebResource = !string.IsNullOrWhiteSpace(subArea.WebResource)
                            ? subArea.WebResource
                            : subArea.Url is { Length: > 0 } encodedUrl && encodedUrl.StartsWith("$webresource:", StringComparison.OrdinalIgnoreCase)
                                ? encodedUrl["$webresource:".Length..]
                                : null
                    });
                }

                groups.Add(new IntentSiteMapGroupSpec
                {
                    Id = group.Id ?? $"group_{groups.Count + 1}",
                    Title = group.Title ?? group.Id ?? "Group",
                    SubAreas = subAreas
                });
            }

            areas.Add(new IntentSiteMapAreaSpec
            {
                Id = area.Id ?? $"area_{areas.Count + 1}",
                Title = area.Title ?? area.Id ?? "Area",
                Groups = groups
            });
        }

        return new IntentSiteMapSpec { Areas = areas };
    }

    private static List<IntentEnvironmentVariableSpec> BuildEnvironmentVariableSpecs(IEnumerable<FamilyArtifact> artifacts, ICollection<IntentReportEntry> unsupportedEntries)
    {
        var valuesByDefinition = artifacts
            .Where(artifact => artifact.Family == ComponentFamily.EnvironmentVariableValue)
            .ToDictionary(
                artifact => NormalizeLogicalName(GetProperty(artifact, ArtifactPropertyKeys.DefinitionSchemaName) ?? artifact.LogicalName) ?? artifact.LogicalName,
                artifact => artifact,
                StringComparer.OrdinalIgnoreCase);

        var specs = new List<IntentEnvironmentVariableSpec>();
        foreach (var definition in artifacts
                     .Where(artifact => artifact.Family == ComponentFamily.EnvironmentVariableDefinition)
                     .OrderBy(artifact => artifact.LogicalName, StringComparer.OrdinalIgnoreCase))
        {
            var schemaName = definition.LogicalName;
            var type = GetProperty(definition, ArtifactPropertyKeys.AttributeType);
            if (string.IsNullOrWhiteSpace(schemaName) || string.IsNullOrWhiteSpace(type))
            {
                unsupportedEntries.Add(new IntentReportEntry(ComponentFamily.EnvironmentVariableDefinition.ToString(), definition.LogicalName, "Environment variable definitions require schemaName and type for reverse-generated intent-spec JSON."));
                continue;
            }

            specs.Add(new IntentEnvironmentVariableSpec
            {
                SchemaName = schemaName,
                DisplayName = definition.DisplayName ?? schemaName,
                Description = GetProperty(definition, ArtifactPropertyKeys.Description),
                Type = type,
                DefaultValue = GetProperty(definition, ArtifactPropertyKeys.DefaultValue),
                CurrentValue = valuesByDefinition.TryGetValue(NormalizeLogicalName(schemaName) ?? schemaName, out var valueArtifact)
                    ? GetProperty(valueArtifact, ArtifactPropertyKeys.Value)
                    : null,
                SecretStore = GetProperty(definition, ArtifactPropertyKeys.SecretStore),
                ValueSchema = GetProperty(definition, ArtifactPropertyKeys.ValueSchema)
            });
        }

        return specs;
    }

    private static IntentGlobalOptionSetSpec? BuildGlobalOptionSetSpec(FamilyArtifact optionSet)
    {
        var logicalName = NormalizeLogicalName(optionSet.LogicalName);
        return string.IsNullOrWhiteSpace(logicalName)
            ? null
            : new IntentGlobalOptionSetSpec
            {
                LogicalName = logicalName,
                DisplayName = optionSet.DisplayName ?? logicalName,
                Description = GetProperty(optionSet, ArtifactPropertyKeys.Description),
                OptionSetType = "picklist",
                Options = ReadOptionItems(GetProperty(optionSet, ArtifactPropertyKeys.OptionsJson))
            };
    }

    private static IntentFormDefinition? ReadFormDefinition(FamilyArtifact form) =>
        Deserialize<IntentFormDefinition>(GetProperty(form, ArtifactPropertyKeys.FormDefinitionJson));

    private static List<IntentOptionItemSpec> ReadOptionItems(string? json) =>
        (Deserialize<List<OptionEntry>>(json) ?? [])
        .Where(option => !string.IsNullOrWhiteSpace(option.Value))
        .Select(option => new IntentOptionItemSpec
        {
            Value = option.Value!,
            Label = option.Label ?? option.Value!
        })
        .ToList();

    private static List<string> ReadStringArray(string? json) =>
        (Deserialize<List<string>>(json) ?? [])
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Select(value => NormalizeLogicalName(value) ?? value)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    private static List<IntentViewFilterSpec> ReadViewFilters(string? json) =>
        (Deserialize<List<ViewFilterDefinition>>(json) ?? [])
        .Where(filter => !string.IsNullOrWhiteSpace(filter.Attribute) && !string.IsNullOrWhiteSpace(filter.Operator))
        .Select(filter => new IntentViewFilterSpec
        {
            Attribute = NormalizeLogicalName(filter.Attribute) ?? filter.Attribute!,
            Operator = filter.Operator!,
            Value = filter.Value
        })
        .ToList();

    private static List<IntentViewOrderSpec> ReadViewOrders(string? json) =>
        (Deserialize<List<ViewOrderDefinition>>(json) ?? [])
        .Where(order => !string.IsNullOrWhiteSpace(order.Attribute))
        .Select(order => new IntentViewOrderSpec
        {
            Attribute = NormalizeLogicalName(order.Attribute) ?? order.Attribute!,
            Descending = NormalizeBoolean(order.Descending)
        })
        .ToList();

    private static IEnumerable<string?> EnumerateSupportedFormFields(IntentFormDefinition definition) =>
        definition.Tabs
            .SelectMany(tab => tab.Sections)
            .SelectMany(section => (section.Fields ?? []).Concat(
                (section.Controls ?? [])
                    .Where(control => !string.Equals(control.Kind, "subgrid", StringComparison.OrdinalIgnoreCase))
                    .Select(control => control.Field)));

    private static bool AllFieldsAreSupported(IEnumerable<string?> fieldNames, IReadOnlySet<string> allowedFieldNames) =>
        fieldNames
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => NormalizeLogicalName(value) ?? value!)
            .All(allowedFieldNames.Contains);

    private static HashSet<string> CreateAllowedFormFieldNames(IEnumerable<string> authoredFieldNames)
    {
        var allowedFieldNames = new HashSet<string>(authoredFieldNames, StringComparer.OrdinalIgnoreCase);
        foreach (var fieldName in KnownPlatformFormFieldNames)
        {
            allowedFieldNames.Add(fieldName);
        }

        return allowedFieldNames;
    }

    private static bool ShouldUseFieldShorthand(IntentFormSectionDefinition section) =>
        (section.Controls?.Count ?? 0) == 0
        || (section.Controls ?? []).All(control => string.Equals(control.Kind, "field", StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<IntentFormControlSpec> BuildFormControlSpecs(IntentFormSectionDefinition section)
    {
        if ((section.Controls?.Count ?? 0) == 0)
        {
            return (section.Fields ?? [])
                .Select(field => new IntentFormControlSpec
                {
                    Kind = "field",
                    Field = field
                })
                .ToArray();
        }

        return (section.Controls ?? [])
            .Select(control => new IntentFormControlSpec
            {
                Kind = control.Kind,
                Field = control.Field,
                Label = control.Label,
                QuickFormEntity = control.QuickFormEntity,
                QuickFormId = control.QuickFormId,
                ControlMode = control.ControlMode,
                RelationshipName = control.RelationshipName,
                TargetTable = control.TargetTable,
                DefaultViewId = control.DefaultViewId,
                IsUserView = control.IsUserView,
                AutoExpand = control.AutoExpand,
                EnableQuickFind = control.EnableQuickFind,
                EnableViewPicker = control.EnableViewPicker,
                EnableJumpBar = control.EnableJumpBar,
                EnableChartPicker = control.EnableChartPicker,
                RecordsPerPage = control.RecordsPerPage
            })
            .ToArray();
    }

    private static string NormalizeTrackedSourceRelativePath(string? value)
    {
        var normalized = (value ?? string.Empty).Replace('\\', '/').TrimStart('/');
        return normalized.StartsWith("tracked-source/", StringComparison.OrdinalIgnoreCase)
            ? normalized["tracked-source/".Length..]
            : normalized;
    }

    private static List<IntentAppSettingSpec> BuildAppSettingSpecs(IEnumerable<FamilyArtifact> artifacts, string appModuleLogicalName) =>
        artifacts
            .Where(artifact => artifact.Family == ComponentFamily.AppSetting
                && string.Equals(GetProperty(artifact, ArtifactPropertyKeys.ParentAppModuleUniqueName), appModuleLogicalName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(artifact => GetProperty(artifact, ArtifactPropertyKeys.SettingDefinitionUniqueName) ?? artifact.LogicalName, StringComparer.OrdinalIgnoreCase)
            .Select(artifact => new IntentAppSettingSpec
            {
                DefinitionUniqueName = GetProperty(artifact, ArtifactPropertyKeys.SettingDefinitionUniqueName),
                Value = GetProperty(artifact, ArtifactPropertyKeys.Value)
            })
            .ToList();

    private static List<IntentSourceBackedArtifactSpec> BuildSourceBackedArtifactSpecs(
        IEnumerable<FamilyArtifact> artifacts,
        string intentRoot,
        ICollection<IntentReportEntry> unsupportedEntries,
        ICollection<SourceBackedArtifactEntry> includedEntries,
        ISet<string> emittedFamilies,
        ICollection<EmittedArtifact> emittedFiles)
    {
        var specs = new List<IntentSourceBackedArtifactSpec>();
        foreach (var artifact in artifacts.OrderBy(artifact => artifact.Family).ThenBy(artifact => artifact.LogicalName, StringComparer.OrdinalIgnoreCase))
        {
            if (!ShouldEmitAsSourceBacked(artifact, artifacts))
            {
                continue;
            }

            if (!TryBuildSourceBackedArtifactSpec(artifact, intentRoot, emittedFiles, out var spec, out var reason))
            {
                unsupportedEntries.Add(new IntentReportEntry(
                    artifact.Family.ToString(),
                    artifact.LogicalName,
                    reason ?? "Source-backed reverse generation could not stage raw metadata evidence for this artifact.",
                    ReverseGenerationReportCategories.MissingSourceFidelity));
                continue;
            }

            specs.Add(spec!);
            includedEntries.Add(new SourceBackedArtifactEntry(artifact.Family.ToString(), artifact.LogicalName, spec!.PackageRelativePath!));
            emittedFamilies.Add(artifact.Family.ToString());
        }

        return specs;
    }

    private static bool ShouldEmitAsSourceBacked(FamilyArtifact artifact, IEnumerable<FamilyArtifact> allArtifacts)
    {
        if (artifact.Family == ComponentFamily.SolutionShell)
        {
            return !string.IsNullOrWhiteSpace(GetProperty(artifact, ArtifactPropertyKeys.MetadataSourcePath))
                   && allArtifacts.Any(candidate => candidate != artifact && IsPotentialSourceBackedSibling(candidate, allArtifacts));
        }

        if (SourceBackedFamilies.Contains(artifact.Family))
        {
            return true;
        }

        if (artifact.Family == ComponentFamily.Table)
        {
            return ShouldStageTableMetadataAsSourceBacked(artifact, allArtifacts);
        }

        if (artifact.Family == ComponentFamily.Form)
        {
            return HasUnsupportedStructuredFormShape(artifact, allArtifacts);
        }

        if (artifact.Family == ComponentFamily.Visualization)
        {
            return HasUnsupportedStructuredVisualizationShape(artifact);
        }

        if (artifact.Family == ComponentFamily.SiteMap)
        {
            return HasAdvancedSiteMapShape(artifact);
        }

        if (artifact.Family == ComponentFamily.AppModule)
        {
            return ParseInt(GetProperty(artifact, ArtifactPropertyKeys.RoleMapCount)) > 0;
        }

        return false;
    }

    private static bool IsPotentialSourceBackedSibling(FamilyArtifact artifact, IEnumerable<FamilyArtifact> allArtifacts) =>
        SourceBackedFamilies.Contains(artifact.Family)
        || artifact.Family == ComponentFamily.Table && ShouldStageTableMetadataAsSourceBacked(artifact, allArtifacts)
        || artifact.Family == ComponentFamily.Form && HasUnsupportedStructuredFormShape(artifact, allArtifacts)
        || artifact.Family == ComponentFamily.Visualization && HasUnsupportedStructuredVisualizationShape(artifact)
        || artifact.Family == ComponentFamily.SiteMap && HasAdvancedSiteMapShape(artifact)
        || artifact.Family == ComponentFamily.AppModule && ParseInt(GetProperty(artifact, ArtifactPropertyKeys.RoleMapCount)) > 0;

    private static bool ShouldStageTableMetadataAsSourceBacked(FamilyArtifact tableArtifact, IEnumerable<FamilyArtifact> allArtifacts)
    {
        if (string.IsNullOrWhiteSpace(GetProperty(tableArtifact, ArtifactPropertyKeys.MetadataSourcePath)))
        {
            return false;
        }

        var tableLogicalName = NormalizeLogicalName(GetProperty(tableArtifact, ArtifactPropertyKeys.EntityLogicalName))
            ?? NormalizeLogicalName(tableArtifact.LogicalName);
        if (string.IsNullOrWhiteSpace(tableLogicalName))
        {
            return false;
        }

        var hasStructuredColumns = allArtifacts.Any(artifact =>
            artifact.Family == ComponentFamily.Column
            && string.Equals(NormalizeLogicalName(GetProperty(artifact, ArtifactPropertyKeys.EntityLogicalName)), tableLogicalName, StringComparison.OrdinalIgnoreCase)
            && !GetBoolProperty(artifact, ArtifactPropertyKeys.IsPrimaryKey)
            && !GetBoolProperty(artifact, ArtifactPropertyKeys.IsPrimaryName));
        var hasStructuredKeys = allArtifacts.Any(artifact =>
            artifact.Family == ComponentFamily.Key
            && string.Equals(NormalizeLogicalName(GetProperty(artifact, ArtifactPropertyKeys.EntityLogicalName)), tableLogicalName, StringComparison.OrdinalIgnoreCase));
        var hasStructuredForms = allArtifacts.Any(artifact =>
            artifact.Family == ComponentFamily.Form
            && string.Equals(NormalizeLogicalName(GetProperty(artifact, ArtifactPropertyKeys.EntityLogicalName)), tableLogicalName, StringComparison.OrdinalIgnoreCase)
            && !ShouldEmitAsSourceBacked(artifact, allArtifacts));
        var hasStructuredViews = allArtifacts.Any(artifact =>
            artifact.Family == ComponentFamily.View
            && string.Equals(NormalizeLogicalName(GetProperty(artifact, ArtifactPropertyKeys.EntityLogicalName)), tableLogicalName, StringComparison.OrdinalIgnoreCase)
            && CanEmitStructuredView(artifact));

        return !hasStructuredColumns
               && !hasStructuredKeys
               && !hasStructuredForms
               && !hasStructuredViews;
    }

    private static bool CanEmitStructuredView(FamilyArtifact view)
    {
        var queryType = GetProperty(view, ArtifactPropertyKeys.QueryType);
        if (!string.IsNullOrWhiteSpace(queryType) && !string.Equals(queryType, "0", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (GetProperty(view, ArtifactPropertyKeys.CanBeDeleted) is { } canBeDeleted
            && !NormalizeBoolean(canBeDeleted))
        {
            return false;
        }

        var layoutColumns = ReadStringArray(GetProperty(view, ArtifactPropertyKeys.LayoutColumnsJson));
        var fetchAttributes = ReadStringArray(GetProperty(view, ArtifactPropertyKeys.FetchAttributesJson));
        return layoutColumns.Count > 0 && fetchAttributes.Count > 0;
    }

    private static bool HasUnsupportedStructuredFormShape(FamilyArtifact artifact, IEnumerable<FamilyArtifact> allArtifacts)
    {
        var formType = NormalizeFormType(GetProperty(artifact, ArtifactPropertyKeys.FormType));
        if (formType is null)
        {
            return true;
        }

        var definition = ReadFormDefinition(artifact);
        if (definition is null)
        {
            return true;
        }

        var allowedFieldNames = GetAllowedFormFieldNames(artifact, allArtifacts);
        if (!AllFieldsAreSupported(EnumerateSupportedFormFields(definition), allowedFieldNames)
            || !AllFieldsAreSupported(definition.HeaderFields, allowedFieldNames))
        {
            return true;
        }

        return definition.Tabs
            .SelectMany(tab => tab.Sections)
            .SelectMany(section => section.Controls ?? [])
            .Any(control =>
            {
                var kind = NormalizeFormControlKind(control.Kind);
                return kind switch
                {
                    "field" => string.IsNullOrWhiteSpace(control.Field),
                    "quickView" => string.IsNullOrWhiteSpace(control.Field)
                                   || string.IsNullOrWhiteSpace(control.QuickFormEntity)
                                   || string.IsNullOrWhiteSpace(control.QuickFormId),
                    "subgrid" => string.IsNullOrWhiteSpace(control.RelationshipName)
                                 || string.IsNullOrWhiteSpace(control.TargetTable),
                    _ => true
                };
            });
    }

    private static bool HasUnsupportedStructuredVisualizationShape(FamilyArtifact artifact) =>
        string.IsNullOrWhiteSpace(GetProperty(artifact, ArtifactPropertyKeys.DataDescriptionXml))
        || string.IsNullOrWhiteSpace(GetProperty(artifact, ArtifactPropertyKeys.PresentationDescriptionXml))
        || ReadStringArray(GetProperty(artifact, ArtifactPropertyKeys.ChartTypesJson)).Count == 0;

    private static HashSet<string> GetAllowedFormFieldNames(FamilyArtifact formArtifact, IEnumerable<FamilyArtifact> allArtifacts)
    {
        var entityLogicalName = NormalizeLogicalName(GetProperty(formArtifact, ArtifactPropertyKeys.EntityLogicalName));
        var allowedFieldNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var tableArtifact = allArtifacts.FirstOrDefault(artifact =>
            artifact.Family == ComponentFamily.Table
            && string.Equals(GetProperty(artifact, ArtifactPropertyKeys.EntityLogicalName), entityLogicalName, StringComparison.OrdinalIgnoreCase));
        if (tableArtifact is not null)
        {
            var primaryId = NormalizeLogicalName(GetProperty(tableArtifact, ArtifactPropertyKeys.PrimaryIdAttribute)) ?? $"{entityLogicalName}id";
            var primaryName = NormalizeLogicalName(GetProperty(tableArtifact, ArtifactPropertyKeys.PrimaryNameAttribute)) ?? $"{entityLogicalName}name";
            allowedFieldNames.Add(primaryId);
            allowedFieldNames.Add(primaryName);
        }

        foreach (var columnArtifact in allArtifacts.Where(artifact =>
                     artifact.Family == ComponentFamily.Column
                     && string.Equals(GetProperty(artifact, ArtifactPropertyKeys.EntityLogicalName), entityLogicalName, StringComparison.OrdinalIgnoreCase)))
        {
            var logicalName = NormalizeLogicalName(columnArtifact.LogicalName.Split('|').LastOrDefault());
            if (!string.IsNullOrWhiteSpace(logicalName))
            {
                allowedFieldNames.Add(logicalName);
            }
        }

        foreach (var fieldName in KnownPlatformFormFieldNames)
        {
            allowedFieldNames.Add(fieldName);
        }

        return allowedFieldNames;
    }

    private static string? NormalizeFormType(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "main" => "main",
            "quick" => "quick",
            "card" => "card",
            _ => null
        };

    private static string? NormalizeFormControlKind(string? value) =>
        value?.Trim() switch
        {
            { Length: 0 } => null,
            string text when text.Equals("field", StringComparison.OrdinalIgnoreCase) => "field",
            string text when text.Equals("quickView", StringComparison.OrdinalIgnoreCase) || text.Equals("quick-view", StringComparison.OrdinalIgnoreCase) => "quickView",
            string text when text.Equals("subgrid", StringComparison.OrdinalIgnoreCase) => "subgrid",
            _ => null
        };

    private static bool HasAdvancedSiteMapShape(FamilyArtifact siteMap)
    {
        var definition = Deserialize<SiteMapDefinition>(GetProperty(siteMap, ArtifactPropertyKeys.SiteMapDefinitionJson));
        return (definition?.Areas ?? [])
            .SelectMany(area => area.Groups ?? [])
            .SelectMany(group => group.SubAreas ?? [])
            .Any(subArea =>
            {
                var populatedTargets = new[]
                {
                    !string.IsNullOrWhiteSpace(subArea.Entity),
                    !string.IsNullOrWhiteSpace(subArea.Url),
                    !string.IsNullOrWhiteSpace(subArea.WebResource)
                }.Count(value => value);
                return populatedTargets != 1;
            });
    }

    private static bool TryBuildSourceBackedArtifactSpec(
        FamilyArtifact artifact,
        string intentRoot,
        ICollection<EmittedArtifact> emittedFiles,
        out IntentSourceBackedArtifactSpec? spec,
        out string? reason)
    {
        spec = null;
        reason = null;

        var metadataRelativePath = GetProperty(artifact, ArtifactPropertyKeys.MetadataSourcePath);
        var packageRelativePath = NormalizeTrackedSourceRelativePath(metadataRelativePath);
        if (string.IsNullOrWhiteSpace(metadataRelativePath) || string.IsNullOrWhiteSpace(packageRelativePath))
        {
            reason = "Metadata source path is missing.";
            return false;
        }

        var metadataFullPath = ResolveSourceBackedMaterializedPath(artifact, metadataRelativePath);
        if (string.IsNullOrWhiteSpace(metadataFullPath) || !File.Exists(metadataFullPath))
        {
            reason = $"Metadata source '{metadataRelativePath}' could not be materialized for reverse generation.";
            return false;
        }

        var stagedMetadataPath = StageSourceBackedFile(intentRoot, metadataFullPath, packageRelativePath, emittedFiles);
        var stagedAssetPaths = new List<string>();
        foreach (var assetRelativePath in ReadSourceBackedAssetRelativePaths(artifact))
        {
            var assetFullPath = ResolveSourceBackedMaterializedPath(artifact, assetRelativePath);
            if (string.IsNullOrWhiteSpace(assetFullPath) || !File.Exists(assetFullPath))
            {
                continue;
            }

            stagedAssetPaths.Add(StageSourceBackedFile(intentRoot, assetFullPath, NormalizeTrackedSourceRelativePath(assetRelativePath), emittedFiles));
        }

        spec = new IntentSourceBackedArtifactSpec
        {
            Family = artifact.Family.ToString(),
            LogicalName = artifact.LogicalName,
            DisplayName = artifact.DisplayName,
            MetadataSourcePath = stagedMetadataPath,
            AssetSourcePaths = stagedAssetPaths.Count == 0 ? null : stagedAssetPaths,
            PackageRelativePath = packageRelativePath,
            StableProperties = BuildStablePropertyMap(artifact)
        };

        return true;
    }

    private static Dictionary<string, JsonElement>? BuildStablePropertyMap(FamilyArtifact artifact)
    {
        if (artifact.Properties is null)
        {
            return null;
        }

        var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var property in artifact.Properties.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (property.Key.EndsWith("SourcePath", StringComparison.Ordinal)
                || string.Equals(property.Key, ArtifactPropertyKeys.MetadataSourcePath, StringComparison.Ordinal)
                || string.Equals(property.Key, ArtifactPropertyKeys.PackageRelativePath, StringComparison.Ordinal)
                || string.Equals(property.Key, ArtifactPropertyKeys.AssetSourceMapJson, StringComparison.Ordinal)
                || string.Equals(property.Key, ArtifactPropertyKeys.SummaryJson, StringComparison.Ordinal)
                || string.Equals(property.Key, ArtifactPropertyKeys.ComparisonSignature, StringComparison.Ordinal))
            {
                continue;
            }

            result[property.Key] = ToJsonElement(property.Key, property.Value);
        }

        return result.Count == 0 ? null : result;
    }

    private static JsonElement ToJsonElement(string key, string value) =>
        key.EndsWith("Json", StringComparison.Ordinal)
            ? JsonDocument.Parse(string.IsNullOrWhiteSpace(value) ? "null" : value).RootElement.Clone()
            : JsonDocument.Parse(JsonSerializer.Serialize(value)).RootElement.Clone();

    private static IReadOnlyList<string> ReadSourceBackedAssetRelativePaths(FamilyArtifact artifact)
    {
        var assetMapJson = GetProperty(artifact, ArtifactPropertyKeys.AssetSourceMapJson);
        if (!string.IsNullOrWhiteSpace(assetMapJson))
        {
            var stringArray = Deserialize<List<string>>(assetMapJson);
            if (stringArray is { Count: > 0 })
            {
                return stringArray
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .Select(path => NormalizeTrackedSourceRelativePath(path))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
        }

        if (artifact.Properties is null)
        {
            return [];
        }

        return artifact.Properties
            .Where(pair =>
                pair.Key.EndsWith("SourcePath", StringComparison.Ordinal)
                && !string.Equals(pair.Key, ArtifactPropertyKeys.MetadataSourcePath, StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(pair.Value))
            .Select(pair => NormalizeTrackedSourceRelativePath(pair.Value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string ResolveSourceBackedMaterializedPath(FamilyArtifact artifact, string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
        {
            return Path.GetFullPath(relativePath);
        }

        if (!string.IsNullOrWhiteSpace(artifact.SourcePath)
            && artifact.SourcePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            var trackedSourceRoot = ResolveTrackedSourceRoot(artifact.SourcePath);
            var trackedCandidate = Path.Combine(trackedSourceRoot, "source-backed", NormalizeTrackedSourceRelativePath(relativePath).Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(trackedCandidate))
            {
                return trackedCandidate;
            }
        }

        var metadataRelativePath = GetProperty(artifact, ArtifactPropertyKeys.MetadataSourcePath);
        if (!string.IsNullOrWhiteSpace(metadataRelativePath)
            && !string.IsNullOrWhiteSpace(artifact.SourcePath)
            && File.Exists(artifact.SourcePath))
        {
            var sourceRoot = artifact.SourcePath;
            foreach (var _ in NormalizeTrackedSourceRelativePath(metadataRelativePath).Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                sourceRoot = Path.GetDirectoryName(sourceRoot) ?? string.Empty;
            }

            var candidate = Path.GetFullPath(Path.Combine(sourceRoot, NormalizeTrackedSourceRelativePath(relativePath).Replace('/', Path.DirectorySeparatorChar)));
            if (File.Exists(candidate))
            {
                return candidate;
            }

            if (string.Equals(NormalizeTrackedSourceRelativePath(relativePath), NormalizeTrackedSourceRelativePath(metadataRelativePath), StringComparison.OrdinalIgnoreCase))
            {
                return artifact.SourcePath;
            }
        }

        return string.Empty;
    }

    private static string ResolveTrackedSourceRoot(string summaryPath)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(summaryPath)) ?? string.Empty;
        while (!string.IsNullOrWhiteSpace(directory))
        {
            if (File.Exists(Path.Combine(directory, "manifest.json")) && File.Exists(Path.Combine(directory, "solution", "manifest.json")))
            {
                return directory;
            }

            directory = Path.GetDirectoryName(directory) ?? string.Empty;
        }

        return Path.GetDirectoryName(Path.GetFullPath(summaryPath)) ?? string.Empty;
    }

    private static string StageSourceBackedFile(string intentRoot, string sourcePath, string packageRelativePath, ICollection<EmittedArtifact> emittedFiles)
    {
        var relativePath = $"source-backed/{NormalizeTrackedSourceRelativePath(packageRelativePath)}";
        var fullPath = GetContainedPath(intentRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        if (!File.Exists(fullPath))
        {
            File.Copy(sourcePath, fullPath, overwrite: true);
            emittedFiles.Add(new EmittedArtifact($"intent-spec/{relativePath.Replace('\\', '/')}", EmittedArtifactRole.IntentSpec, $"Staged source-backed artifact evidence for {packageRelativePath}."));
        }
        return relativePath.Replace('\\', '/');
    }

    private static string BuildArtifactKey(FamilyArtifact artifact) =>
        $"{artifact.Family}|{artifact.LogicalName}";

    private static string DetectInputKind(CanonicalSolution model)
    {
        if (model.Diagnostics.Any(diagnostic => string.Equals(diagnostic.Code, "tracked-source-reader-subset", StringComparison.Ordinal)))
        {
            return "tracked-source";
        }

        if (model.Diagnostics.Any(diagnostic => string.Equals(diagnostic.Code, "intent-spec-read", StringComparison.Ordinal)))
        {
            return "intent-spec-json";
        }

        if (model.Diagnostics.Any(diagnostic =>
                string.Equals(diagnostic.Code, "zip-reader-extracted", StringComparison.Ordinal)
                || string.Equals(diagnostic.Code, "zip-reader-normalized-classic-export", StringComparison.Ordinal)))
        {
            return "packed-zip";
        }

        return model.Diagnostics.Any(diagnostic => string.Equals(diagnostic.Code, "xml-reader-typed-families", StringComparison.Ordinal))
            ? "unpacked-xml-folder"
            : "canonical";
    }

    private static string? NormalizeAttributeType(string? value) =>
        (NormalizeLogicalName(value) ?? string.Empty) switch
        {
            "string" => "string",
            "nvarchar" => "string",
            "varchar" => "string",
            "memo" => "memo",
            "ntext" => "memo",
            "text" => "memo",
            "datetime" => "datetime",
            "datetime2" => "datetime",
            "dateandtime" => "datetime",
            "decimal" => "decimal",
            "money" => "decimal",
            "double" => "decimal",
            "float" => "decimal",
            "integer" => "integer",
            "int" => "integer",
            "bigint" => "decimal",
            "image" => "image",
            "picklist" => "choice",
            "boolean" or "bit" => "boolean",
            "lookup" => "lookup",
            _ => null
        };

    private static bool NormalizeBoolean(string? value) =>
        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "1", StringComparison.OrdinalIgnoreCase);

    private static string? NormalizeGuid(string? value) =>
        Guid.TryParse(value, out var guid) ? guid.ToString("D") : null;

    private static string? NormalizeLogicalName(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();

    private static string? GetProperty(FamilyArtifact artifact, string key) =>
        artifact.Properties is not null && artifact.Properties.TryGetValue(key, out var value) ? value : null;

    private static bool GetBoolProperty(FamilyArtifact artifact, string key) =>
        NormalizeBoolean(GetProperty(artifact, key));

    private static bool? GetNullableBoolProperty(FamilyArtifact artifact, string key) =>
        artifact.Properties is not null && artifact.Properties.TryGetValue(key, out var value)
            ? NormalizeBoolean(value)
            : null;

    private static int ParseInt(string? value) =>
        int.TryParse(value, out var parsed) ? parsed : 0;

    private static T? Deserialize<T>(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? default
            : JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
}
