using System.Text.Json;
using DataverseSolutionCompiler.Domain.Diagnostics;
using DataverseSolutionCompiler.Domain.Model;

namespace DataverseSolutionCompiler.Emitters.TrackedSource;

public sealed partial class IntentSpecEmitter
{
    private static IReadOnlyList<IntentReportEntry> BuildUnsupportedArtifactEntries(IEnumerable<FamilyArtifact> artifacts) =>
        artifacts
            .Where(artifact => !SupportedFamilies.Contains(artifact.Family))
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

                var columnSpec = BuildColumnSpec(column, localOptionSetsByColumn, relationshipsByTableAndAttribute, unsupportedEntries);
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

            var formSpecs = BuildFormSpecs(tableLogicalName, formsByTable.GetValueOrDefault(tableLogicalName) ?? [], allowedFieldNames, unsupportedEntries, preservedIds);
            if (formSpecs.Count > 0)
            {
                emittedFamilies.Add(ComponentFamily.Form.ToString());
            }

            var viewSpecs = BuildViewSpecs(tableLogicalName, viewsByTable.GetValueOrDefault(tableLogicalName) ?? [], allowedFieldNames, unsupportedEntries, preservedIds);
            if (viewSpecs.Count > 0)
            {
                emittedFamilies.Add(ComponentFamily.View.ToString());
            }

            tableSpecs.Add(new IntentTableSpec
            {
                LogicalName = tableLogicalName,
                SchemaName = GetProperty(table, ArtifactPropertyKeys.SchemaName) ?? table.DisplayName ?? tableLogicalName,
                DisplayName = table.DisplayName ?? tableLogicalName,
                Description = GetProperty(table, ArtifactPropertyKeys.Description),
                EntitySetName = GetProperty(table, ArtifactPropertyKeys.EntitySetName),
                OwnershipTypeMask = GetProperty(table, ArtifactPropertyKeys.OwnershipTypeMask),
                Columns = columnSpecs,
                Keys = keySpecs,
                Forms = formSpecs,
                Views = viewSpecs
            });
        }

        return tableSpecs;
    }

    private static IntentTableColumnSpec? BuildColumnSpec(
        FamilyArtifact column,
        IReadOnlyDictionary<string, FamilyArtifact> localOptionSetsByColumn,
        IReadOnlyDictionary<(string TableLogicalName, string ColumnLogicalName), FamilyArtifact> relationshipsByTableAndAttribute,
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

        var spec = new IntentTableColumnSpec
        {
            LogicalName = columnLogicalName,
            SchemaName = GetProperty(column, ArtifactPropertyKeys.SchemaName) ?? columnLogicalName,
            DisplayName = column.DisplayName ?? columnLogicalName,
            Description = GetProperty(column, ArtifactPropertyKeys.Description),
            Type = attributeType,
            IsSecured = GetBoolProperty(column, ArtifactPropertyKeys.IsSecured)
        };

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
        IReadOnlySet<string> allowedFieldNames,
        ICollection<IntentReportEntry> unsupportedEntries,
        ICollection<PreservedIdEntry> preservedIds)
    {
        var specs = new List<IntentFormSpec>();
        foreach (var form in forms)
        {
            if (!string.Equals(GetProperty(form, ArtifactPropertyKeys.FormType), "main", StringComparison.OrdinalIgnoreCase))
            {
                unsupportedEntries.Add(new IntentReportEntry(ComponentFamily.Form.ToString(), form.LogicalName, "Only main forms are supported in the reverse-generated intent subset."));
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

            if (!AllFieldsAreSupported(definition.Tabs.SelectMany(tab => tab.Sections).SelectMany(section => section.Fields), allowedFieldNames)
                || !AllFieldsAreSupported(definition.HeaderFields, allowedFieldNames))
            {
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
                Type = "main",
                Tabs = definition.Tabs.Select(tab => new IntentFormTabSpec
                {
                    Name = tab.Name,
                    Label = tab.Label,
                    Sections = tab.Sections.Select(section => new IntentFormSectionSpec
                    {
                        Name = section.Name,
                        Label = section.Label,
                        Fields = section.Fields
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
                unsupportedEntries.Add(new IntentReportEntry(ComponentFamily.AppModule.ToString(), appModule.LogicalName, "App modules require an entity-only site map to reverse-generate intent-spec JSON."));
                continue;
            }

            emittedFamilies.Add(ComponentFamily.AppModule.ToString());
            specs.Add(new IntentAppModuleSpec
            {
                UniqueName = appModule.LogicalName,
                DisplayName = appModule.DisplayName ?? appModule.LogicalName,
                Description = GetProperty(appModule, ArtifactPropertyKeys.Description),
                SiteMap = siteMap
            });
        }

        return specs;
    }

    private static IntentSiteMapSpec? BuildSiteMapSpec(FamilyArtifact siteMap, ICollection<IntentReportEntry> unsupportedEntries)
    {
        var definitionJson = GetProperty(siteMap, ArtifactPropertyKeys.SiteMapDefinitionJson);
        if (string.IsNullOrWhiteSpace(definitionJson))
        {
            unsupportedEntries.Add(new IntentReportEntry(
                ComponentFamily.SiteMap.ToString(),
                siteMap.LogicalName,
                "Site maps require siteMapDefinitionJson so entity-only area/group/subarea layout can be reconstructed.",
                ReverseGenerationReportCategories.MissingSourceFidelity));
            return null;
        }

        var definition = JsonSerializer.Deserialize<SiteMapDefinition>(definitionJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (definition?.Areas is null)
        {
            unsupportedEntries.Add(new IntentReportEntry(
                ComponentFamily.SiteMap.ToString(),
                siteMap.LogicalName,
                "Site map definition JSON could not be parsed.",
                ReverseGenerationReportCategories.MissingSourceFidelity));
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
                    if (string.IsNullOrWhiteSpace(NormalizeLogicalName(subArea.Entity)) || !string.IsNullOrWhiteSpace(subArea.Url))
                    {
                        unsupportedEntries.Add(new IntentReportEntry(ComponentFamily.SiteMap.ToString(), siteMap.LogicalName, "Only entity-only site map subareas can be reverse-generated into intent-spec JSON."));
                        return null;
                    }

                    subAreas.Add(new IntentSiteMapSubAreaSpec
                    {
                        Id = subArea.Id ?? $"subarea_{subAreas.Count + 1}",
                        Title = subArea.Title ?? subArea.Entity!,
                        Entity = NormalizeLogicalName(subArea.Entity)
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

    private static bool AllFieldsAreSupported(IEnumerable<string?> fieldNames, IReadOnlySet<string> allowedFieldNames) =>
        fieldNames
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => NormalizeLogicalName(value) ?? value!)
            .All(allowedFieldNames.Contains);

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
            "integer" => "decimal",
            "int" => "decimal",
            "bigint" => "decimal",
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

    private static T? Deserialize<T>(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? default
            : JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
}
