using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using DataverseSolutionCompiler.Domain.Abstractions;
using DataverseSolutionCompiler.Domain.Diagnostics;
using DataverseSolutionCompiler.Domain.Model;
using DataverseSolutionCompiler.Domain.Read;

namespace DataverseSolutionCompiler.Readers.Intent;

public sealed class IntentSpecReader : ISolutionReader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonSerializerOptions CanonicalJsonOptions = new()
    {
        WriteIndented = false
    };

    public CanonicalSolution Read(ReadRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SourcePath);

        if (!File.Exists(request.SourcePath))
        {
            throw new FileNotFoundException($"Intent spec file not found: {request.SourcePath}", request.SourcePath);
        }

        IntentSpecDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<IntentSpecDocument>(File.ReadAllText(request.SourcePath), JsonOptions);
        }
        catch (JsonException exception)
        {
            return CreateFailureSolution(
                request.SourcePath,
                [
                    new CompilerDiagnostic(
                        "intent-spec-invalid-json",
                        DiagnosticSeverity.Error,
                        $"Intent spec JSON could not be parsed: {exception.Message}",
                        request.SourcePath)
                ]);
        }

        if (document is null)
        {
            return CreateFailureSolution(
                request.SourcePath,
                [
                    new CompilerDiagnostic(
                        "intent-spec-empty",
                        DiagnosticSeverity.Error,
                        "Intent spec JSON did not contain a document.",
                        request.SourcePath)
                ]);
        }

        var diagnostics = ValidateDocument(document, request.SourcePath).ToList();
        if (diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
        {
            return CreateFailureSolution(request.SourcePath, diagnostics);
        }

        var solution = document.Solution!;
        var publisher = document.Publisher!;
        var solutionIdentity = new SolutionIdentity(
            solution.UniqueName!,
            solution.DisplayName!,
            solution.Version!,
            ParseLayeringIntent(solution.LayeringIntent!));
        var publisherDefinition = new PublisherDefinition(
            publisher.UniqueName!,
            publisher.Prefix!,
            publisher.Prefix!,
            publisher.DisplayName!);

        var artifacts = new List<FamilyArtifact>
        {
            CreateSolutionShellArtifact(solutionIdentity, solution.Description, publisherDefinition, request.SourcePath),
            CreatePublisherArtifact(publisherDefinition, publisher.Description, request.SourcePath)
        };

        var globalOptionSets = document.GlobalOptionSets?
            .Select(optionSet => CreateGlobalOptionSetArtifact(optionSet!, request.SourcePath))
            .ToDictionary(artifact => artifact.LogicalName, artifact => artifact, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, FamilyArtifact>(StringComparer.OrdinalIgnoreCase);
        artifacts.AddRange(globalOptionSets.Values);

        var tablesByLogicalName = document.Tables!
            .ToDictionary(table => NormalizeLogicalName(table!.LogicalName)!, table => table!, StringComparer.OrdinalIgnoreCase);

        foreach (var table in document.Tables!)
        {
            artifacts.AddRange(CreateTableArtifacts(table!, globalOptionSets, request.SourcePath));
        }

        foreach (var environmentVariable in document.EnvironmentVariables ?? [])
        {
            artifacts.AddRange(CreateEnvironmentVariableArtifacts(environmentVariable!, request.SourcePath));
        }

        foreach (var appModule in document.AppModules ?? [])
        {
            artifacts.AddRange(CreateAppModuleArtifacts(appModule!, tablesByLogicalName, request.SourcePath));
        }

        diagnostics.Add(new CompilerDiagnostic(
            "intent-spec-read",
            DiagnosticSeverity.Info,
            "Intent-spec reader projected JSON v1 intent into the canonical Dataverse model for the supported greenfield families.",
            request.SourcePath));

        return new CanonicalSolution(
            solutionIdentity,
            publisherDefinition,
            artifacts
                .OrderBy(artifact => artifact.Family)
                .ThenBy(artifact => artifact.LogicalName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(artifact => artifact.SourcePath, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            [],
            [],
            diagnostics);
    }

    private static IReadOnlyList<CompilerDiagnostic> ValidateDocument(IntentSpecDocument document, string sourcePath)
    {
        var diagnostics = new List<CompilerDiagnostic>();
        ValidateUnexpectedProperties(document.ExtensionData, "$", sourcePath, diagnostics);

        if (!string.Equals(document.SpecVersion, "1.0", StringComparison.Ordinal))
        {
            diagnostics.Add(CreateError(sourcePath, "$.specVersion", "Intent spec v1 currently requires \"specVersion\": \"1.0\"."));
        }

        if (document.Solution is null)
        {
            diagnostics.Add(CreateError(sourcePath, "$.solution", "Intent spec v1 requires a solution section."));
        }
        else
        {
            ValidateUnexpectedProperties(document.Solution.ExtensionData, "$.solution", sourcePath, diagnostics);
            RequireValue(document.Solution.UniqueName, "$.solution.uniqueName", sourcePath, diagnostics);
            RequireValue(document.Solution.DisplayName, "$.solution.displayName", sourcePath, diagnostics);
            RequireValue(document.Solution.Version, "$.solution.version", sourcePath, diagnostics);
            RequireValue(document.Solution.LayeringIntent, "$.solution.layeringIntent", sourcePath, diagnostics);

            if (!string.IsNullOrWhiteSpace(document.Solution.LayeringIntent)
                && !Enum.TryParse<LayeringIntent>(document.Solution.LayeringIntent, ignoreCase: true, out _))
            {
                diagnostics.Add(CreateError(
                    sourcePath,
                    "$.solution.layeringIntent",
                    $"Unsupported layeringIntent '{document.Solution.LayeringIntent}'. Use UnmanagedDevelopment, ManagedRelease, or Hybrid."));
            }
        }

        if (document.Publisher is null)
        {
            diagnostics.Add(CreateError(sourcePath, "$.publisher", "Intent spec v1 requires a publisher section."));
        }
        else
        {
            ValidateUnexpectedProperties(document.Publisher.ExtensionData, "$.publisher", sourcePath, diagnostics);
            RequireValue(document.Publisher.UniqueName, "$.publisher.uniqueName", sourcePath, diagnostics);
            RequireValue(document.Publisher.Prefix, "$.publisher.prefix", sourcePath, diagnostics);
            RequireValue(document.Publisher.DisplayName, "$.publisher.displayName", sourcePath, diagnostics);
        }

        if (document.Tables is null)
        {
            diagnostics.Add(CreateError(sourcePath, "$.tables", "Intent spec v1 requires a tables array, even when it is empty."));
        }

        var globalOptionSetNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (optionSet, index) in Enumerate(document.GlobalOptionSets))
        {
            var path = $"$.globalOptionSets[{index}]";
            ValidateUnexpectedProperties(optionSet.ExtensionData, path, sourcePath, diagnostics);
            RequireValue(optionSet.LogicalName, $"{path}.logicalName", sourcePath, diagnostics);
            RequireValue(optionSet.DisplayName, $"{path}.displayName", sourcePath, diagnostics);
            if (!string.Equals(optionSet.OptionSetType, "picklist", StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(CreateError(sourcePath, $"{path}.optionSetType", "Only picklist global option sets are supported in JSON v1."));
            }

            if (!globalOptionSetNames.Add(optionSet.LogicalName ?? string.Empty))
            {
                diagnostics.Add(CreateError(sourcePath, $"{path}.logicalName", $"Duplicate global option set logical name '{optionSet.LogicalName}'."));
            }

            ValidateOptionItems(optionSet.Options, $"{path}.options", sourcePath, diagnostics, requireTwoBooleanOptions: false);
        }

        var tableNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (table, tableIndex) in Enumerate(document.Tables))
        {
            var tablePath = $"$.tables[{tableIndex}]";
            ValidateUnexpectedProperties(table.ExtensionData, tablePath, sourcePath, diagnostics);
            RequireValue(table.LogicalName, $"{tablePath}.logicalName", sourcePath, diagnostics);
            RequireValue(table.SchemaName, $"{tablePath}.schemaName", sourcePath, diagnostics);
            RequireValue(table.DisplayName, $"{tablePath}.displayName", sourcePath, diagnostics);

            if (!tableNames.Add(table.LogicalName ?? string.Empty))
            {
                diagnostics.Add(CreateError(sourcePath, $"{tablePath}.logicalName", $"Duplicate table logical name '{table.LogicalName}'."));
            }

            var columnNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                BuildPrimaryIdLogicalName(table.LogicalName),
                BuildPrimaryNameLogicalName(table.LogicalName)
            };

            foreach (var (column, columnIndex) in Enumerate(table.Columns))
            {
                var columnPath = $"{tablePath}.columns[{columnIndex}]";
                ValidateUnexpectedProperties(column.ExtensionData, columnPath, sourcePath, diagnostics);
                RequireValue(column.LogicalName, $"{columnPath}.logicalName", sourcePath, diagnostics);
                RequireValue(column.SchemaName, $"{columnPath}.schemaName", sourcePath, diagnostics);
                RequireValue(column.DisplayName, $"{columnPath}.displayName", sourcePath, diagnostics);
                RequireValue(column.Type, $"{columnPath}.type", sourcePath, diagnostics);

                if (!columnNames.Add(column.LogicalName ?? string.Empty))
                {
                    diagnostics.Add(CreateError(sourcePath, $"{columnPath}.logicalName", $"Duplicate column logical name '{column.LogicalName}' in table '{table.LogicalName}'."));
                }

                switch ((column.Type ?? string.Empty).Trim().ToLowerInvariant())
                {
                    case "string":
                    case "memo":
                    case "datetime":
                    case "decimal":
                        break;
                    case "choice":
                        if (string.IsNullOrWhiteSpace(column.GlobalOptionSet) == (column.Options is not { Count: > 0 }))
                        {
                            diagnostics.Add(CreateError(
                                sourcePath,
                                columnPath,
                                "Choice columns must provide either globalOptionSet or a local options array, but not both."));
                        }

                        if (!string.IsNullOrWhiteSpace(column.GlobalOptionSet)
                            && !globalOptionSetNames.Contains(column.GlobalOptionSet))
                        {
                            diagnostics.Add(CreateError(
                                sourcePath,
                                $"{columnPath}.globalOptionSet",
                                $"Choice column '{column.LogicalName}' references unknown global option set '{column.GlobalOptionSet}'."));
                        }

                        if (column.Options is { Count: > 0 })
                        {
                            ValidateOptionItems(column.Options, $"{columnPath}.options", sourcePath, diagnostics, requireTwoBooleanOptions: false);
                        }
                        break;
                    case "boolean":
                        ValidateOptionItems(column.Options, $"{columnPath}.options", sourcePath, diagnostics, requireTwoBooleanOptions: true);
                        break;
                    case "lookup":
                        RequireValue(column.TargetTable, $"{columnPath}.targetTable", sourcePath, diagnostics);
                        break;
                    default:
                        diagnostics.Add(CreateError(
                            sourcePath,
                            $"{columnPath}.type",
                            $"Unsupported column type '{column.Type}'. Supported JSON v1 types are string, memo, datetime, decimal, choice, boolean, and lookup."));
                        break;
                }
            }

            var allowedFieldNames = columnNames;
            var keyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, keyIndex) in Enumerate(table.Keys))
            {
                var keyPath = $"{tablePath}.keys[{keyIndex}]";
                ValidateUnexpectedProperties(key.ExtensionData, keyPath, sourcePath, diagnostics);
                RequireValue(key.LogicalName, $"{keyPath}.logicalName", sourcePath, diagnostics);
                RequireValue(key.SchemaName, $"{keyPath}.schemaName", sourcePath, diagnostics);

                if (!keyNames.Add(key.LogicalName ?? string.Empty))
                {
                    diagnostics.Add(CreateError(
                        sourcePath,
                        $"{keyPath}.logicalName",
                        $"Duplicate key logical name '{key.LogicalName}' in table '{table.LogicalName}'."));
                }

                if (key.KeyAttributes is null || key.KeyAttributes.Count == 0)
                {
                    diagnostics.Add(CreateError(sourcePath, $"{keyPath}.keyAttributes", "Each key requires at least one keyAttributes entry."));
                    continue;
                }

                var seenKeyAttributes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var (attributeName, attributeIndex) in Enumerate(key.KeyAttributes))
                {
                    var normalizedAttribute = NormalizeLogicalName(attributeName);
                    if (string.IsNullOrWhiteSpace(normalizedAttribute))
                    {
                        diagnostics.Add(CreateError(sourcePath, $"{keyPath}.keyAttributes[{attributeIndex}]", "Key attributes must be non-empty logical names."));
                        continue;
                    }

                    if (!seenKeyAttributes.Add(normalizedAttribute))
                    {
                        diagnostics.Add(CreateError(
                            sourcePath,
                            $"{keyPath}.keyAttributes[{attributeIndex}]",
                            $"Duplicate key attribute '{attributeName}' in key '{key.LogicalName}'."));
                    }

                    if (!allowedFieldNames.Contains(normalizedAttribute))
                    {
                        diagnostics.Add(CreateError(
                            sourcePath,
                            $"{keyPath}.keyAttributes[{attributeIndex}]",
                            $"Key '{key.LogicalName}' references unknown field '{attributeName}' on table '{table.LogicalName}'."));
                    }
                    else if (normalizedAttribute.Equals(BuildPrimaryIdLogicalName(table.LogicalName), StringComparison.OrdinalIgnoreCase)
                             || normalizedAttribute.Equals(BuildPrimaryNameLogicalName(table.LogicalName), StringComparison.OrdinalIgnoreCase))
                    {
                        diagnostics.Add(CreateError(
                            sourcePath,
                            $"{keyPath}.keyAttributes[{attributeIndex}]",
                            $"Key '{key.LogicalName}' cannot reference autogenerated primary id or primary name columns on table '{table.LogicalName}'."));
                    }
                }
            }

            foreach (var (form, formIndex) in Enumerate(table.Forms))
            {
                var formPath = $"{tablePath}.forms[{formIndex}]";
                ValidateUnexpectedProperties(form.ExtensionData, formPath, sourcePath, diagnostics);
                RequireValue(form.Name, $"{formPath}.name", sourcePath, diagnostics);
                if (!string.IsNullOrWhiteSpace(form.Id) && !Guid.TryParse(form.Id, out _))
                {
                    diagnostics.Add(CreateError(sourcePath, $"{formPath}.id", "Form ids must be valid GUID values when supplied."));
                }

                if (!string.Equals(form.Type, "main", StringComparison.OrdinalIgnoreCase))
                {
                    diagnostics.Add(CreateError(sourcePath, $"{formPath}.type", "JSON v1 supports main forms only."));
                }

                foreach (var (tab, tabIndex) in Enumerate(form.Tabs))
                {
                    var tabPath = $"{formPath}.tabs[{tabIndex}]";
                    ValidateUnexpectedProperties(tab.ExtensionData, tabPath, sourcePath, diagnostics);
                    RequireValue(tab.Name, $"{tabPath}.name", sourcePath, diagnostics);
                    RequireValue(tab.Label, $"{tabPath}.label", sourcePath, diagnostics);

                    foreach (var (section, sectionIndex) in Enumerate(tab.Sections))
                    {
                        var sectionPath = $"{tabPath}.sections[{sectionIndex}]";
                        ValidateUnexpectedProperties(section.ExtensionData, sectionPath, sourcePath, diagnostics);
                        RequireValue(section.Name, $"{sectionPath}.name", sourcePath, diagnostics);
                        RequireValue(section.Label, $"{sectionPath}.label", sourcePath, diagnostics);
                        foreach (var (field, fieldIndex) in Enumerate(section.Fields))
                        {
                            if (!allowedFieldNames.Contains(field))
                            {
                                diagnostics.Add(CreateError(
                                    sourcePath,
                                    $"{sectionPath}.fields[{fieldIndex}]",
                                    $"Form '{form.Name}' references unknown field '{field}' on table '{table.LogicalName}'."));
                            }
                        }
                    }
                }

                foreach (var (field, fieldIndex) in Enumerate(form.HeaderFields))
                {
                    if (!allowedFieldNames.Contains(field))
                    {
                        diagnostics.Add(CreateError(
                            sourcePath,
                            $"{formPath}.headerFields[{fieldIndex}]",
                            $"Form '{form.Name}' references unknown header field '{field}' on table '{table.LogicalName}'."));
                    }
                }
            }

            foreach (var (view, viewIndex) in Enumerate(table.Views))
            {
                var viewPath = $"{tablePath}.views[{viewIndex}]";
                ValidateUnexpectedProperties(view.ExtensionData, viewPath, sourcePath, diagnostics);
                RequireValue(view.Name, $"{viewPath}.name", sourcePath, diagnostics);
                if (!string.IsNullOrWhiteSpace(view.Id) && !Guid.TryParse(view.Id, out _))
                {
                    diagnostics.Add(CreateError(sourcePath, $"{viewPath}.id", "View ids must be valid GUID values when supplied."));
                }

                if (!string.IsNullOrWhiteSpace(view.Type)
                    && !string.Equals(view.Type, "savedquery", StringComparison.OrdinalIgnoreCase))
                {
                    diagnostics.Add(CreateError(sourcePath, $"{viewPath}.type", "JSON v1 supports savedquery views only."));
                }

                if (view.LayoutColumns is null || view.LayoutColumns.Count == 0)
                {
                    diagnostics.Add(CreateError(sourcePath, $"{viewPath}.layoutColumns", "Each view requires at least one layout column."));
                }

                foreach (var (columnName, layoutIndex) in Enumerate(view.LayoutColumns))
                {
                    if (!allowedFieldNames.Contains(columnName))
                    {
                        diagnostics.Add(CreateError(
                            sourcePath,
                            $"{viewPath}.layoutColumns[{layoutIndex}]",
                            $"View '{view.Name}' references unknown layout column '{columnName}' on table '{table.LogicalName}'."));
                    }
                }

                foreach (var (columnName, fetchIndex) in Enumerate(view.FetchAttributes))
                {
                    if (!allowedFieldNames.Contains(columnName))
                    {
                        diagnostics.Add(CreateError(
                            sourcePath,
                            $"{viewPath}.fetchAttributes[{fetchIndex}]",
                            $"View '{view.Name}' references unknown fetch attribute '{columnName}' on table '{table.LogicalName}'."));
                    }
                }

                foreach (var (filter, filterIndex) in Enumerate(view.Filters))
                {
                    var filterPath = $"{viewPath}.filters[{filterIndex}]";
                    ValidateUnexpectedProperties(filter.ExtensionData, filterPath, sourcePath, diagnostics);
                    RequireValue(filter.Attribute, $"{filterPath}.attribute", sourcePath, diagnostics);
                    RequireValue(filter.Operator, $"{filterPath}.operator", sourcePath, diagnostics);
                }

                foreach (var (order, orderIndex) in Enumerate(view.Orders))
                {
                    var orderPath = $"{viewPath}.orders[{orderIndex}]";
                    ValidateUnexpectedProperties(order.ExtensionData, orderPath, sourcePath, diagnostics);
                    RequireValue(order.Attribute, $"{orderPath}.attribute", sourcePath, diagnostics);
                }
            }
        }

        var knownTables = new HashSet<string>(document.Tables?.Select(table => NormalizeLogicalName(table!.LogicalName) ?? string.Empty) ?? [], StringComparer.OrdinalIgnoreCase);
        foreach (var (appModule, appModuleIndex) in Enumerate(document.AppModules))
        {
            var appModulePath = $"$.appModules[{appModuleIndex}]";
            ValidateUnexpectedProperties(appModule.ExtensionData, appModulePath, sourcePath, diagnostics);
            RequireValue(appModule.UniqueName, $"{appModulePath}.uniqueName", sourcePath, diagnostics);
            RequireValue(appModule.DisplayName, $"{appModulePath}.displayName", sourcePath, diagnostics);
            if (appModule.SiteMap is null)
            {
                diagnostics.Add(CreateError(sourcePath, $"{appModulePath}.siteMap", "JSON v1 app modules require a siteMap section."));
                continue;
            }

            ValidateUnexpectedProperties(appModule.SiteMap.ExtensionData, $"{appModulePath}.siteMap", sourcePath, diagnostics);
            foreach (var (area, areaIndex) in Enumerate(appModule.SiteMap.Areas))
            {
                var areaPath = $"{appModulePath}.siteMap.areas[{areaIndex}]";
                ValidateUnexpectedProperties(area.ExtensionData, areaPath, sourcePath, diagnostics);
                RequireValue(area.Id, $"{areaPath}.id", sourcePath, diagnostics);
                RequireValue(area.Title, $"{areaPath}.title", sourcePath, diagnostics);

                foreach (var (group, groupIndex) in Enumerate(area.Groups))
                {
                    var groupPath = $"{areaPath}.groups[{groupIndex}]";
                    ValidateUnexpectedProperties(group.ExtensionData, groupPath, sourcePath, diagnostics);
                    RequireValue(group.Id, $"{groupPath}.id", sourcePath, diagnostics);
                    RequireValue(group.Title, $"{groupPath}.title", sourcePath, diagnostics);

                    foreach (var (subArea, subAreaIndex) in Enumerate(group.SubAreas))
                    {
                        var subAreaPath = $"{groupPath}.subAreas[{subAreaIndex}]";
                        ValidateUnexpectedProperties(subArea.ExtensionData, subAreaPath, sourcePath, diagnostics);
                        RequireValue(subArea.Id, $"{subAreaPath}.id", sourcePath, diagnostics);
                        RequireValue(subArea.Title, $"{subAreaPath}.title", sourcePath, diagnostics);
                        RequireValue(subArea.Entity, $"{subAreaPath}.entity", sourcePath, diagnostics);
                        var normalizedEntity = NormalizeLogicalName(subArea.Entity);
                        if (!string.IsNullOrWhiteSpace(normalizedEntity)
                            && !knownTables.Contains(normalizedEntity))
                        {
                            diagnostics.Add(CreateError(
                                sourcePath,
                                $"{subAreaPath}.entity",
                                $"Site map sub-area '{subArea.Id}' references unknown table '{subArea.Entity}'."));
                        }
                    }
                }
            }
        }

        foreach (var (environmentVariable, index) in Enumerate(document.EnvironmentVariables))
        {
            var path = $"$.environmentVariables[{index}]";
            ValidateUnexpectedProperties(environmentVariable.ExtensionData, path, sourcePath, diagnostics);
            RequireValue(environmentVariable.SchemaName, $"{path}.schemaName", sourcePath, diagnostics);
            RequireValue(environmentVariable.DisplayName, $"{path}.displayName", sourcePath, diagnostics);
            RequireValue(environmentVariable.Type, $"{path}.type", sourcePath, diagnostics);
            if (!string.IsNullOrWhiteSpace(environmentVariable.Type)
                && NormalizeEnvironmentVariableType(environmentVariable.Type) is null)
            {
                diagnostics.Add(CreateError(sourcePath, $"{path}.type", $"Unsupported environment variable type '{environmentVariable.Type}'."));
            }
        }

        return diagnostics;
    }

    private static IEnumerable<FamilyArtifact> CreateTableArtifacts(
        TableSpec table,
        IReadOnlyDictionary<string, FamilyArtifact> globalOptionSets,
        string sourcePath)
    {
        var tableLogicalName = NormalizeLogicalName(table.LogicalName)!;
        var primaryIdLogicalName = BuildPrimaryIdLogicalName(tableLogicalName);
        var primaryNameLogicalName = BuildPrimaryNameLogicalName(tableLogicalName);
        var primaryIdSchemaName = BuildPrimaryIdSchemaName(table.SchemaName!);
        var primaryNameSchemaName = BuildPrimaryNameSchemaName(table.SchemaName!);
        var entitySetName = string.IsNullOrWhiteSpace(table.EntitySetName)
            ? $"{tableLogicalName}s"
            : table.EntitySetName;
        var authoredColumns = table.Columns ?? [];
        var forms = table.Forms ?? [];
        var views = table.Views ?? [];

        var artifacts = new List<FamilyArtifact>
        {
            new(
                ComponentFamily.Table,
                tableLogicalName,
                table.DisplayName,
                sourcePath,
                EvidenceKind.Derived,
                CreateProperties(
                    (ArtifactPropertyKeys.EntityLogicalName, tableLogicalName),
                    (ArtifactPropertyKeys.SchemaName, table.SchemaName),
                    (ArtifactPropertyKeys.Description, table.Description),
                    (ArtifactPropertyKeys.EntitySetName, entitySetName),
                    (ArtifactPropertyKeys.OwnershipTypeMask, table.OwnershipTypeMask ?? "UserOwned"),
                    (ArtifactPropertyKeys.IsCustomizable, "true"),
                    (ArtifactPropertyKeys.PrimaryIdAttribute, primaryIdLogicalName),
                    (ArtifactPropertyKeys.PrimaryNameAttribute, primaryNameLogicalName),
                    (ArtifactPropertyKeys.ShellOnly, (!authoredColumns.Any() && !forms.Any() && !views.Any()) ? "true" : "false")))
        };

        artifacts.Add(CreateColumnArtifact(
            tableLogicalName,
            primaryIdLogicalName,
            primaryIdSchemaName,
            $"{table.DisplayName} Id",
            table.Description,
            "primarykey",
            isSecured: false,
            isCustomField: false,
            isPrimaryKey: true,
            isPrimaryName: false,
            optionSetName: null,
            optionSetType: null,
            isGlobalOptionSet: null,
            sourcePath));

        artifacts.Add(CreateColumnArtifact(
            tableLogicalName,
            primaryNameLogicalName,
            primaryNameSchemaName,
            table.DisplayName!,
            table.Description,
            "string",
            isSecured: false,
            isCustomField: true,
            isPrimaryKey: false,
            isPrimaryName: true,
            optionSetName: null,
            optionSetType: null,
            isGlobalOptionSet: null,
            sourcePath));

        foreach (var column in authoredColumns)
        {
            var normalizedColumnType = NormalizeColumnType(column.Type!);
            var normalizedColumnLogicalName = NormalizeLogicalName(column.LogicalName)!;
            var isGlobalChoice = normalizedColumnType == "picklist" && !string.IsNullOrWhiteSpace(column.GlobalOptionSet);
            var normalizedGlobalOptionSet = isGlobalChoice ? NormalizeLogicalName(column.GlobalOptionSet) : null;

            if (normalizedGlobalOptionSet is not null && !globalOptionSets.ContainsKey(normalizedGlobalOptionSet))
            {
                continue;
            }

            var localOptionArtifact = CreateLocalOptionArtifactIfNeeded(
                tableLogicalName,
                normalizedColumnLogicalName,
                column,
                sourcePath);

            artifacts.Add(CreateColumnArtifact(
                tableLogicalName,
                normalizedColumnLogicalName,
                column.SchemaName!,
                column.DisplayName!,
                column.Description,
                normalizedColumnType,
                column.IsSecured,
                isCustomField: true,
                isPrimaryKey: false,
                isPrimaryName: false,
                optionSetName: normalizedGlobalOptionSet ?? localOptionArtifact?.LogicalName,
                optionSetType: localOptionArtifact is null ? (normalizedGlobalOptionSet is null ? null : "picklist") : GetProperty(localOptionArtifact, ArtifactPropertyKeys.OptionSetType),
                isGlobalOptionSet: normalizedGlobalOptionSet is not null ? "true" : (localOptionArtifact is null ? null : "false"),
                sourcePath));

            if (localOptionArtifact is not null)
            {
                artifacts.Add(localOptionArtifact);
            }

            if (normalizedColumnType == "lookup")
            {
                var referencedEntity = NormalizeLogicalName(column.TargetTable)!;
                var relationshipSchemaName = NormalizeLogicalName(column.RelationshipSchemaName)
                    ?? $"{referencedEntity}_{tableLogicalName}";
                artifacts.Add(new FamilyArtifact(
                    ComponentFamily.Relationship,
                    relationshipSchemaName,
                    relationshipSchemaName,
                    sourcePath,
                    EvidenceKind.Derived,
                    CreateProperties(
                        (ArtifactPropertyKeys.RelationshipType, "OneToMany"),
                        (ArtifactPropertyKeys.ReferencedEntity, referencedEntity),
                        (ArtifactPropertyKeys.ReferencingEntity, tableLogicalName),
                        (ArtifactPropertyKeys.ReferencingAttribute, normalizedColumnLogicalName),
                        (ArtifactPropertyKeys.OwningEntityLogicalName, referencedEntity),
                        (ArtifactPropertyKeys.Description, column.Description))));
            }
        }

        foreach (var form in forms.Where(form => form is not null))
        {
            artifacts.Add(CreateFormArtifact(tableLogicalName, form!, sourcePath));
        }

        foreach (var view in views.Where(view => view is not null))
        {
            artifacts.Add(CreateViewArtifact(tableLogicalName, primaryIdLogicalName, view!, sourcePath));
        }

        foreach (var key in table.Keys ?? [])
        {
            artifacts.Add(CreateKeyArtifact(tableLogicalName, key!, sourcePath));
        }

        return artifacts;
    }

    private static FamilyArtifact CreateSolutionShellArtifact(
        SolutionIdentity identity,
        string? description,
        PublisherDefinition publisher,
        string sourcePath) =>
        new(
            ComponentFamily.SolutionShell,
            identity.UniqueName,
            identity.DisplayName,
            sourcePath,
            EvidenceKind.Derived,
            CreateProperties(
                (ArtifactPropertyKeys.Version, identity.Version),
                (ArtifactPropertyKeys.Managed, identity.LayeringIntent == LayeringIntent.ManagedRelease ? "true" : "false"),
                (ArtifactPropertyKeys.PublisherUniqueName, publisher.UniqueName),
                (ArtifactPropertyKeys.PublisherPrefix, publisher.Prefix),
                (ArtifactPropertyKeys.PublisherDisplayName, publisher.DisplayName),
                (ArtifactPropertyKeys.Description, description)));

    private static FamilyArtifact CreatePublisherArtifact(
        PublisherDefinition publisher,
        string? description,
        string sourcePath) =>
        new(
            ComponentFamily.Publisher,
            publisher.UniqueName,
            publisher.DisplayName,
            sourcePath,
            EvidenceKind.Derived,
            CreateProperties(
                (ArtifactPropertyKeys.PublisherPrefix, publisher.Prefix),
                (ArtifactPropertyKeys.PublisherDisplayName, publisher.DisplayName),
                (ArtifactPropertyKeys.Description, description)));

    private static FamilyArtifact CreateGlobalOptionSetArtifact(GlobalOptionSetSpec optionSet, string sourcePath)
    {
        var logicalName = NormalizeLogicalName(optionSet.LogicalName)!;
        var optionEntries = optionSet.Options!
            .Select(option => new
            {
                value = option!.Value!,
                label = option.Label!,
                isHidden = "false"
            })
            .ToArray();
        var summaryJson = SerializeJson(new
        {
            optionSetName = logicalName,
            optionSetType = "picklist",
            isGlobal = true,
            optionCount = optionEntries.Length,
            options = optionEntries
        });

        return new FamilyArtifact(
            ComponentFamily.OptionSet,
            logicalName,
            optionSet.DisplayName,
            sourcePath,
            EvidenceKind.Derived,
            CreateProperties(
                (ArtifactPropertyKeys.OptionSetName, logicalName),
                (ArtifactPropertyKeys.OptionSetType, "picklist"),
                (ArtifactPropertyKeys.Description, optionSet.Description),
                (ArtifactPropertyKeys.IsGlobal, "true"),
                (ArtifactPropertyKeys.OptionCount, optionEntries.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                (ArtifactPropertyKeys.OptionsJson, SerializeJson(optionEntries)),
                (ArtifactPropertyKeys.SummaryJson, summaryJson),
                (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(summaryJson))));
    }

    private static FamilyArtifact? CreateLocalOptionArtifactIfNeeded(
        string entityLogicalName,
        string columnLogicalName,
        TableColumnSpec column,
        string sourcePath)
    {
        var normalizedColumnType = NormalizeColumnType(column.Type!);
        if (normalizedColumnType == "picklist" && !string.IsNullOrWhiteSpace(column.GlobalOptionSet))
        {
            return null;
        }

        if (normalizedColumnType is not ("picklist" or "boolean"))
        {
            return null;
        }

        var optionSetType = normalizedColumnType == "boolean" ? "bit" : "picklist";
        var options = normalizedColumnType == "boolean"
            ? BuildBooleanOptions(column)
            : column.Options!
                .Select(option => new
                {
                    value = option!.Value!,
                    label = option.Label!,
                    isHidden = "false"
                })
                .ToArray();

        var summaryJson = SerializeJson(new
        {
            entityLogicalName,
            attributeLogicalName = columnLogicalName,
            optionSetType,
            isGlobal = false,
            optionCount = options.Length,
            options
        });

        return new FamilyArtifact(
            ComponentFamily.OptionSet,
            $"{entityLogicalName}|{columnLogicalName}",
            column.DisplayName,
            sourcePath,
            EvidenceKind.Derived,
            CreateProperties(
                (ArtifactPropertyKeys.EntityLogicalName, entityLogicalName),
                (ArtifactPropertyKeys.OptionSetName, columnLogicalName),
                (ArtifactPropertyKeys.OptionSetType, optionSetType),
                (ArtifactPropertyKeys.Description, column.Description),
                (ArtifactPropertyKeys.IsGlobal, "false"),
                (ArtifactPropertyKeys.OptionCount, options.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                (ArtifactPropertyKeys.OptionsJson, SerializeJson(options)),
                (ArtifactPropertyKeys.SummaryJson, summaryJson),
                (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(summaryJson))));
    }

    private static FamilyArtifact CreateColumnArtifact(
        string entityLogicalName,
        string logicalName,
        string schemaName,
        string displayName,
        string? description,
        string attributeType,
        bool isSecured,
        bool isCustomField,
        bool isPrimaryKey,
        bool isPrimaryName,
        string? optionSetName,
        string? optionSetType,
        string? isGlobalOptionSet,
        string sourcePath) =>
        new(
            ComponentFamily.Column,
            $"{entityLogicalName}|{logicalName}",
            displayName,
            sourcePath,
            EvidenceKind.Derived,
            CreateProperties(
                (ArtifactPropertyKeys.EntityLogicalName, entityLogicalName),
                (ArtifactPropertyKeys.SchemaName, schemaName),
                (ArtifactPropertyKeys.Description, description),
                (ArtifactPropertyKeys.AttributeType, attributeType),
                (ArtifactPropertyKeys.IsSecured, isSecured ? "true" : "false"),
                (ArtifactPropertyKeys.IsCustomField, isCustomField ? "true" : "false"),
                (ArtifactPropertyKeys.IsCustomizable, "true"),
                (ArtifactPropertyKeys.IsPrimaryKey, isPrimaryKey ? "true" : "false"),
                (ArtifactPropertyKeys.IsPrimaryName, isPrimaryName ? "true" : "false"),
                (ArtifactPropertyKeys.IsLogical, "false"),
                (ArtifactPropertyKeys.OptionSetName, optionSetName),
                (ArtifactPropertyKeys.OptionSetType, optionSetType),
                (ArtifactPropertyKeys.IsGlobal, isGlobalOptionSet)));

    private static FamilyArtifact CreateKeyArtifact(
        string entityLogicalName,
        TableKeySpec key,
        string sourcePath)
    {
        var keyLogicalName = NormalizeLogicalName(key.LogicalName)
            ?? throw new InvalidOperationException("Validated key logical names must be present.");
        var normalizedKeyAttributes = (key.KeyAttributes ?? [])
            .Select(NormalizeLogicalName)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new FamilyArtifact(
            ComponentFamily.Key,
            $"{entityLogicalName}|{keyLogicalName}",
            key.SchemaName,
            sourcePath,
            EvidenceKind.Derived,
            CreateProperties(
                (ArtifactPropertyKeys.EntityLogicalName, entityLogicalName),
                (ArtifactPropertyKeys.SchemaName, key.SchemaName),
                (ArtifactPropertyKeys.Description, key.Description),
                (ArtifactPropertyKeys.KeyAttributesJson, SerializeJson(normalizedKeyAttributes))));
    }

    private static FamilyArtifact CreateFormArtifact(string entityLogicalName, FormSpec form, string sourcePath)
    {
        var formId = NormalizeGuid(form.Id) ?? CreateDeterministicGuid(entityLogicalName, "form", form.Name!);
        var controlDescriptions = BuildFormControlDescriptions(form);
        var summary = new
        {
            formType = "main",
            formId,
            tabCount = form.Tabs?.Count ?? 0,
            sectionCount = form.Tabs?.Sum(tab => tab?.Sections?.Count ?? 0) ?? 0,
            controlCount = controlDescriptions.Length,
            quickFormCount = 0,
            subgridCount = 0,
            headerControlCount = form.HeaderFields?.Count ?? 0,
            footerControlCount = 0,
            controlDescriptions
        };
        var summaryJson = SerializeJson(summary);

        return new FamilyArtifact(
            ComponentFamily.Form,
            $"{entityLogicalName}|main|{formId}",
            form.Name,
            sourcePath,
            EvidenceKind.Derived,
            CreateProperties(
                (ArtifactPropertyKeys.EntityLogicalName, entityLogicalName),
                (ArtifactPropertyKeys.FormType, "main"),
                (ArtifactPropertyKeys.FormTypeCode, "1"),
                (ArtifactPropertyKeys.FormId, formId),
                (ArtifactPropertyKeys.Description, form.Description),
                (ArtifactPropertyKeys.FormDefinitionJson, SerializeJson(form)),
                (ArtifactPropertyKeys.TabCount, summary.tabCount.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                (ArtifactPropertyKeys.SectionCount, summary.sectionCount.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                (ArtifactPropertyKeys.ControlCount, summary.controlCount.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                (ArtifactPropertyKeys.QuickFormCount, "0"),
                (ArtifactPropertyKeys.SubgridCount, "0"),
                (ArtifactPropertyKeys.HeaderControlCount, summary.headerControlCount.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                (ArtifactPropertyKeys.FooterControlCount, "0"),
                (ArtifactPropertyKeys.ControlDescriptionCount, controlDescriptions.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                (ArtifactPropertyKeys.SummaryJson, summaryJson),
                (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(summaryJson))));
    }

    private static object[] BuildFormControlDescriptions(FormSpec form)
    {
        var bodyControls = form.Tabs?
            .SelectMany(tab => tab?.Sections ?? [])
            .SelectMany(section => section?.Fields ?? [])
            .Select(field => new
            {
                id = NormalizeLogicalName(field) ?? string.Empty,
                dataFieldName = NormalizeLogicalName(field) ?? string.Empty,
                role = "field"
            })
            ?? [];
        var headerControls = form.HeaderFields?
            .Select(field => new
            {
                id = $"header_{NormalizeLogicalName(field)}",
                dataFieldName = NormalizeLogicalName(field) ?? string.Empty,
                role = "field"
            })
            ?? [];

        return bodyControls
            .Concat(headerControls)
            .OrderBy(control => control.id, StringComparer.OrdinalIgnoreCase)
            .ThenBy(control => control.dataFieldName, StringComparer.OrdinalIgnoreCase)
            .Cast<object>()
            .ToArray();
    }

    private static FamilyArtifact CreateViewArtifact(
        string entityLogicalName,
        string primaryIdLogicalName,
        ViewSpec view,
        string sourcePath)
    {
        var layoutColumns = view.LayoutColumns?.Select(column => NormalizeLogicalName(column) ?? string.Empty).ToArray() ?? [];
        var fetchAttributes = (view.FetchAttributes is { Count: > 0 } ? view.FetchAttributes : [primaryIdLogicalName])
            .Select(column => NormalizeLogicalName(column) ?? string.Empty)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var filters = (view.Filters ?? [])
            .Select(filter => new
            {
                attribute = NormalizeLogicalName(filter!.Attribute) ?? string.Empty,
                @operator = filter.Operator ?? string.Empty,
                value = filter.Value ?? string.Empty
            })
            .OrderBy(filter => filter.attribute, StringComparer.OrdinalIgnoreCase)
            .ThenBy(filter => filter.@operator, StringComparer.OrdinalIgnoreCase)
            .ThenBy(filter => filter.value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var orders = (view.Orders ?? [])
            .Select(order => new
            {
                attribute = NormalizeLogicalName(order!.Attribute) ?? string.Empty,
                descending = order.Descending ? "true" : "false"
            })
            .ToArray();
        var viewId = CreateDeterministicGuid(entityLogicalName, "view", view.Name!);
        viewId = NormalizeGuid(view.Id) ?? viewId;
        var summaryJson = SerializeJson(new
        {
            targetEntity = entityLogicalName,
            layoutColumns,
            fetchAttributes,
            filters,
            orders
        });

        return new FamilyArtifact(
            ComponentFamily.View,
            $"{entityLogicalName}|{view.Name}",
            view.Name,
            sourcePath,
            EvidenceKind.Derived,
            CreateProperties(
                (ArtifactPropertyKeys.EntityLogicalName, entityLogicalName),
                (ArtifactPropertyKeys.ViewId, viewId),
                (ArtifactPropertyKeys.QueryType, "0"),
                (ArtifactPropertyKeys.TargetEntity, entityLogicalName),
                (ArtifactPropertyKeys.LayoutColumnsJson, SerializeJson(layoutColumns)),
                (ArtifactPropertyKeys.FetchAttributesJson, SerializeJson(fetchAttributes)),
                (ArtifactPropertyKeys.FiltersJson, SerializeJson(filters)),
                (ArtifactPropertyKeys.OrdersJson, SerializeJson(orders)),
                (ArtifactPropertyKeys.SummaryJson, summaryJson),
                (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(summaryJson))));
    }

    private static IEnumerable<FamilyArtifact> CreateAppModuleArtifacts(
        AppModuleSpec appModule,
        IReadOnlyDictionary<string, TableSpec> tablesByLogicalName,
        string sourcePath)
    {
        _ = tablesByLogicalName;

        var uniqueName = NormalizeLogicalName(appModule.UniqueName)!;
        var componentTypesJson = SerializeJson(new[] { "62" });
        var appModuleSummaryJson = SerializeJson(new
        {
            componentTypes = JsonNode.Parse(componentTypesJson),
            roleIds = JsonNode.Parse("[]"),
            roleMapCount = 0,
            appSettingCount = 0
        });

        yield return new FamilyArtifact(
            ComponentFamily.AppModule,
            uniqueName,
            appModule.DisplayName,
            sourcePath,
            EvidenceKind.Derived,
            CreateProperties(
                (ArtifactPropertyKeys.Description, appModule.Description),
                (ArtifactPropertyKeys.ComponentTypesJson, componentTypesJson),
                (ArtifactPropertyKeys.RoleIdsJson, "[]"),
                (ArtifactPropertyKeys.RoleMapCount, "0"),
                (ArtifactPropertyKeys.AppSettingCount, "0"),
                (ArtifactPropertyKeys.SummaryJson, appModuleSummaryJson),
                (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(appModuleSummaryJson))));

        var areaCount = appModule.SiteMap!.Areas?.Count ?? 0;
        var groupCount = appModule.SiteMap.Areas?.Sum(area => area?.Groups?.Count ?? 0) ?? 0;
        var subAreaCount = appModule.SiteMap.Areas?
            .SelectMany(area => area?.Groups ?? [])
            .Sum(group => group?.SubAreas?.Count ?? 0) ?? 0;
        var siteMapSummaryJson = SerializeJson(new
        {
            areaCount,
            groupCount,
            subAreaCount,
            webResourceSubAreaCount = 0
        });

        yield return new FamilyArtifact(
            ComponentFamily.SiteMap,
            uniqueName,
            appModule.DisplayName,
            sourcePath,
            EvidenceKind.Derived,
            CreateProperties(
                (ArtifactPropertyKeys.SiteMapDefinitionJson, SerializeJson(appModule.SiteMap)),
                (ArtifactPropertyKeys.AreaCount, areaCount.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                (ArtifactPropertyKeys.GroupCount, groupCount.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                (ArtifactPropertyKeys.SubAreaCount, subAreaCount.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                (ArtifactPropertyKeys.WebResourceSubAreaCount, "0"),
                (ArtifactPropertyKeys.SummaryJson, siteMapSummaryJson),
                (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(siteMapSummaryJson))));
    }

    private static IEnumerable<FamilyArtifact> CreateEnvironmentVariableArtifacts(EnvironmentVariableSpec environmentVariable, string sourcePath)
    {
        var schemaName = environmentVariable.SchemaName!;
        var type = NormalizeEnvironmentVariableType(environmentVariable.Type!)!;
        yield return new FamilyArtifact(
            ComponentFamily.EnvironmentVariableDefinition,
            schemaName,
            environmentVariable.DisplayName,
            sourcePath,
            EvidenceKind.Derived,
            CreateProperties(
                (ArtifactPropertyKeys.DefaultValue, environmentVariable.DefaultValue),
                (ArtifactPropertyKeys.SecretStore, NormalizeSecretStore(environmentVariable.SecretStore)),
                (ArtifactPropertyKeys.ValueSchema, environmentVariable.ValueSchema),
                (ArtifactPropertyKeys.AttributeType, type)));

        if (!string.IsNullOrWhiteSpace(environmentVariable.CurrentValue))
        {
            yield return new FamilyArtifact(
                ComponentFamily.EnvironmentVariableValue,
                schemaName,
                schemaName,
                sourcePath,
                EvidenceKind.Derived,
                CreateProperties(
                    (ArtifactPropertyKeys.DefinitionSchemaName, schemaName),
                    (ArtifactPropertyKeys.Value, environmentVariable.CurrentValue)));
        }
    }

    private static IReadOnlyDictionary<string, string>? CreateProperties(params (string Key, string? Value)[] properties)
    {
        var dictionary = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in properties)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            dictionary[key] = value;
        }

        return dictionary.Count == 0 ? null : dictionary;
    }

    private static string? GetProperty(FamilyArtifact artifact, string key) =>
        artifact.Properties is not null && artifact.Properties.TryGetValue(key, out var value) ? value : null;

    private static string NormalizeColumnType(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "choice" => "picklist",
            "boolean" => "boolean",
            "string" => "string",
            "memo" => "memo",
            "datetime" => "datetime",
            "decimal" => "decimal",
            "lookup" => "lookup",
            var other => other
        };

    private static string? NormalizeEnvironmentVariableType(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "100000000" or "text" or "string" => "100000000",
            "100000001" or "number" or "decimal" => "100000001",
            "100000002" or "boolean" => "100000002",
            "100000003" or "json" => "100000003",
            "100000004" or "datasource" => "100000004",
            "100000005" or "secret" => "100000005",
            _ => null
        };

    private static string NormalizeSecretStore(string? value) =>
        (value ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "1" or "azurekeyvault" or "keyvault" => "1",
            _ => "0"
        };

    private static string? NormalizeLogicalName(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();

    private static string? NormalizeGuid(string? value) =>
        Guid.TryParse(value, out var guid) ? guid.ToString("D") : null;

    private static LayeringIntent ParseLayeringIntent(string value) =>
        Enum.Parse<LayeringIntent>(value, ignoreCase: true);

    private static string BuildPrimaryIdLogicalName(string? tableLogicalName) =>
        $"{NormalizeLogicalName(tableLogicalName)}id";

    private static string BuildPrimaryNameLogicalName(string? tableLogicalName) =>
        $"{NormalizeLogicalName(tableLogicalName)}name";

    private static string BuildPrimaryIdSchemaName(string tableSchemaName) =>
        tableSchemaName.EndsWith("Id", StringComparison.OrdinalIgnoreCase) ? tableSchemaName : $"{tableSchemaName}Id";

    private static string BuildPrimaryNameSchemaName(string tableSchemaName) =>
        tableSchemaName.EndsWith("Name", StringComparison.OrdinalIgnoreCase) ? tableSchemaName : $"{tableSchemaName}Name";

    private static object[] BuildBooleanOptions(TableColumnSpec column) =>
        (column.Options is { Count: 2 } ? column.Options : null)?
            .Select(option => new
            {
                value = option!.Value!,
                label = option.Label!,
                isHidden = "false"
            })
            .ToArray()
        ?? [
            new { value = "1", label = "Yes", isHidden = "false" },
            new { value = "0", label = "No", isHidden = "false" }
        ];

    private static string CreateDeterministicGuid(params string[] segments)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("|", segments.Select(segment => segment.Trim().ToLowerInvariant()))));
        var guidBytes = bytes[..16].ToArray();
        return new Guid(guidBytes).ToString("D");
    }

    private static string SerializeJson<T>(T value) =>
        JsonSerializer.Serialize(value, CanonicalJsonOptions);

    private static string ComputeSignature(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static void RequireValue(string? value, string path, string sourcePath, ICollection<CompilerDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            diagnostics.Add(CreateError(sourcePath, path, "A non-empty value is required."));
        }
    }

    private static void ValidateOptionItems(
        IReadOnlyList<OptionItemSpec>? options,
        string path,
        string sourcePath,
        ICollection<CompilerDiagnostic> diagnostics,
        bool requireTwoBooleanOptions)
    {
        if (options is null || options.Count == 0)
        {
            diagnostics.Add(CreateError(sourcePath, path, "At least one option is required."));
            return;
        }

        if (requireTwoBooleanOptions && options.Count != 2)
        {
            diagnostics.Add(CreateError(sourcePath, path, "Boolean columns require exactly two options."));
        }

        var seenValues = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (option, index) in Enumerate(options))
        {
            var optionPath = $"{path}[{index}]";
            ValidateUnexpectedProperties(option.ExtensionData, optionPath, sourcePath, diagnostics);
            RequireValue(option.Value, $"{optionPath}.value", sourcePath, diagnostics);
            RequireValue(option.Label, $"{optionPath}.label", sourcePath, diagnostics);
            if (!seenValues.Add(option.Value ?? string.Empty))
            {
                diagnostics.Add(CreateError(sourcePath, $"{optionPath}.value", $"Duplicate option value '{option.Value}'."));
            }
        }
    }

    private static void ValidateUnexpectedProperties(
        IReadOnlyDictionary<string, JsonElement>? extensionData,
        string path,
        string sourcePath,
        ICollection<CompilerDiagnostic> diagnostics)
    {
        if (extensionData is null || extensionData.Count == 0)
        {
            return;
        }

        diagnostics.Add(CreateError(
            sourcePath,
            path,
            $"Unsupported JSON v1 properties were supplied: {string.Join(", ", extensionData.Keys.OrderBy(key => key, StringComparer.Ordinal))}."));
    }

    private static CompilerDiagnostic CreateError(string sourcePath, string path, string message) =>
        new(
            "intent-spec-validation",
            DiagnosticSeverity.Error,
            $"{path}: {message}",
            sourcePath);

    private static CanonicalSolution CreateFailureSolution(
        string inputPath,
        IReadOnlyList<CompilerDiagnostic> diagnostics)
    {
        var fallbackName = Path.GetFileNameWithoutExtension(inputPath);
        return new CanonicalSolution(
            new SolutionIdentity(fallbackName, fallbackName, "0.0.0", LayeringIntent.Hybrid),
            new PublisherDefinition("dsc", "dsc", "dsc", "Dataverse Solution Compiler"),
            [],
            [],
            [],
            diagnostics);
    }

    private static IEnumerable<(T Item, int Index)> Enumerate<T>(IReadOnlyList<T?>? items)
        where T : class
    {
        if (items is null)
        {
            yield break;
        }

        for (var index = 0; index < items.Count; index++)
        {
            if (items[index] is T item)
            {
                yield return (item, index);
            }
        }
    }

    private static IEnumerable<(string Item, int Index)> Enumerate(IReadOnlyList<string>? items)
    {
        if (items is null)
        {
            yield break;
        }

        for (var index = 0; index < items.Count; index++)
        {
            if (!string.IsNullOrWhiteSpace(items[index]))
            {
                yield return (items[index], index);
            }
        }
    }
}

internal sealed record IntentSpecDocument
{
    [JsonPropertyName("specVersion")]
    public string? SpecVersion { get; init; }

    [JsonPropertyName("solution")]
    public SolutionSpec? Solution { get; init; }

    [JsonPropertyName("publisher")]
    public PublisherSpec? Publisher { get; init; }

    [JsonPropertyName("globalOptionSets")]
    public IReadOnlyList<GlobalOptionSetSpec>? GlobalOptionSets { get; init; }

    [JsonPropertyName("environmentVariables")]
    public IReadOnlyList<EnvironmentVariableSpec>? EnvironmentVariables { get; init; }

    [JsonPropertyName("appModules")]
    public IReadOnlyList<AppModuleSpec>? AppModules { get; init; }

    [JsonPropertyName("tables")]
    public IReadOnlyList<TableSpec>? Tables { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

internal sealed record SolutionSpec
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

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

internal sealed record PublisherSpec
{
    [JsonPropertyName("uniqueName")]
    public string? UniqueName { get; init; }

    [JsonPropertyName("prefix")]
    public string? Prefix { get; init; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

internal sealed record GlobalOptionSetSpec
{
    [JsonPropertyName("logicalName")]
    public string? LogicalName { get; init; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("optionSetType")]
    public string? OptionSetType { get; init; } = "picklist";

    [JsonPropertyName("options")]
    public IReadOnlyList<OptionItemSpec>? Options { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

internal sealed record EnvironmentVariableSpec
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

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

internal sealed record AppModuleSpec
{
    [JsonPropertyName("uniqueName")]
    public string? UniqueName { get; init; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("siteMap")]
    public SiteMapSpec? SiteMap { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

internal sealed record SiteMapSpec
{
    [JsonPropertyName("areas")]
    public IReadOnlyList<SiteMapAreaSpec>? Areas { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

internal sealed record SiteMapAreaSpec
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("groups")]
    public IReadOnlyList<SiteMapGroupSpec>? Groups { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

internal sealed record SiteMapGroupSpec
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("subAreas")]
    public IReadOnlyList<SiteMapSubAreaSpec>? SubAreas { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

internal sealed record SiteMapSubAreaSpec
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("entity")]
    public string? Entity { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

internal sealed record TableSpec
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
    public IReadOnlyList<TableColumnSpec>? Columns { get; init; }

    [JsonPropertyName("keys")]
    public IReadOnlyList<TableKeySpec>? Keys { get; init; }

    [JsonPropertyName("forms")]
    public IReadOnlyList<FormSpec>? Forms { get; init; }

    [JsonPropertyName("views")]
    public IReadOnlyList<ViewSpec>? Views { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

internal sealed record TableColumnSpec
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
    public IReadOnlyList<OptionItemSpec>? Options { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

internal sealed record TableKeySpec
{
    [JsonPropertyName("logicalName")]
    public string? LogicalName { get; init; }

    [JsonPropertyName("schemaName")]
    public string? SchemaName { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("keyAttributes")]
    public IReadOnlyList<string>? KeyAttributes { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

internal sealed record OptionItemSpec
{
    [JsonPropertyName("value")]
    public string? Value { get; init; }

    [JsonPropertyName("label")]
    public string? Label { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

internal sealed record FormSpec
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("type")]
    public string? Type { get; init; } = "main";

    [JsonPropertyName("tabs")]
    public IReadOnlyList<FormTabSpec>? Tabs { get; init; }

    [JsonPropertyName("headerFields")]
    public IReadOnlyList<string>? HeaderFields { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

internal sealed record FormTabSpec
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("label")]
    public string? Label { get; init; }

    [JsonPropertyName("sections")]
    public IReadOnlyList<FormSectionSpec>? Sections { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

internal sealed record FormSectionSpec
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("label")]
    public string? Label { get; init; }

    [JsonPropertyName("fields")]
    public IReadOnlyList<string>? Fields { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

internal sealed record ViewSpec
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("type")]
    public string? Type { get; init; } = "savedquery";

    [JsonPropertyName("layoutColumns")]
    public IReadOnlyList<string>? LayoutColumns { get; init; }

    [JsonPropertyName("fetchAttributes")]
    public IReadOnlyList<string>? FetchAttributes { get; init; }

    [JsonPropertyName("filters")]
    public IReadOnlyList<ViewFilterSpec>? Filters { get; init; }

    [JsonPropertyName("orders")]
    public IReadOnlyList<ViewOrderSpec>? Orders { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

internal sealed record ViewFilterSpec
{
    [JsonPropertyName("attribute")]
    public string? Attribute { get; init; }

    [JsonPropertyName("operator")]
    public string? Operator { get; init; }

    [JsonPropertyName("value")]
    public string? Value { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

internal sealed record ViewOrderSpec
{
    [JsonPropertyName("attribute")]
    public string? Attribute { get; init; }

    [JsonPropertyName("descending")]
    public bool Descending { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}
