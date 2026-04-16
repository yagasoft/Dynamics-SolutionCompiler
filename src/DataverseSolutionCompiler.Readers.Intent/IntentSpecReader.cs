using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using DataverseSolutionCompiler.Domain.Abstractions;
using DataverseSolutionCompiler.Domain.Diagnostics;
using DataverseSolutionCompiler.Domain.Model;
using DataverseSolutionCompiler.Domain.Read;

namespace DataverseSolutionCompiler.Readers.Intent;

public sealed class IntentSpecReader : ISolutionReader
{
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

        foreach (var sourceBackedArtifact in document.SourceBackedArtifacts ?? [])
        {
            artifacts.Add(CreateSourceBackedArtifact(sourceBackedArtifact!, request.SourcePath));
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

        var knownTables = new HashSet<string>(document.Tables?.Select(table => NormalizeLogicalName(table!.LogicalName) ?? string.Empty) ?? [], StringComparer.OrdinalIgnoreCase);
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
                    case "integer":
                    case "image":
                        if (column.Options is { Count: > 0 } || !string.IsNullOrWhiteSpace(column.GlobalOptionSet) || !string.IsNullOrWhiteSpace(column.TargetTable))
                        {
                            diagnostics.Add(CreateError(
                                sourcePath,
                                columnPath,
                                "Image columns cannot declare options, globalOptionSet, or targetTable."));
                        }
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
                            $"Unsupported column type '{column.Type}'. Supported JSON v1 types are string, memo, datetime, decimal, integer, image, choice, boolean, and lookup."));
                        break;
                }

                if ((column.CanStoreFullImage.HasValue || column.IsPrimaryImage.HasValue)
                    && !string.Equals(column.Type, "image", StringComparison.OrdinalIgnoreCase))
                {
                    diagnostics.Add(CreateError(
                        sourcePath,
                        columnPath,
                        "canStoreFullImage and isPrimaryImage are supported only on image columns."));
                }
            }

            if (!string.IsNullOrWhiteSpace(table.PrimaryImageAttribute))
            {
                var primaryImageAttribute = NormalizeLogicalName(table.PrimaryImageAttribute);
                var matchingImageColumn = (table.Columns ?? [])
                    .FirstOrDefault(column =>
                        string.Equals(NormalizeLogicalName(column?.LogicalName), primaryImageAttribute, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(column?.Type, "image", StringComparison.OrdinalIgnoreCase));
                if (matchingImageColumn is null)
                {
                    diagnostics.Add(CreateError(
                        sourcePath,
                        $"{tablePath}.primaryImageAttribute",
                        $"primaryImageAttribute '{table.PrimaryImageAttribute}' must reference an image column on table '{table.LogicalName}'."));
                }
            }

            var allowedFormFieldNames = CreateAllowedFormFieldNames(columnNames);
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

                    if (!columnNames.Contains(normalizedAttribute))
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

                var normalizedFormType = NormalizeFormType(form.Type);
                if (normalizedFormType is null)
                {
                    diagnostics.Add(CreateError(sourcePath, $"{formPath}.type", "JSON v1 supports main, quick, and card forms only."));
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
                            if (!IsSupportedFormFieldReference(field, allowedFormFieldNames))
                            {
                                diagnostics.Add(CreateError(
                                    sourcePath,
                                    $"{sectionPath}.fields[{fieldIndex}]",
                                    $"Form '{form.Name}' references unknown field '{field}' on table '{table.LogicalName}'."));
                            }
                        }

                        foreach (var (control, controlIndex) in Enumerate(section.Controls))
                        {
                            var controlPath = $"{sectionPath}.controls[{controlIndex}]";
                            ValidateUnexpectedProperties(control.ExtensionData, controlPath, sourcePath, diagnostics);
                            RequireValue(control.Kind, $"{controlPath}.kind", sourcePath, diagnostics);

                            switch (NormalizeFormControlKind(control.Kind))
                            {
                                case "field":
                                    ValidateFormFieldReference(control.Field, allowedFormFieldNames, sourcePath, $"{controlPath}.field", diagnostics, form.Name!, table.LogicalName!);
                                    break;
                                case "quickView":
                                    ValidateFormFieldReference(control.Field, allowedFormFieldNames, sourcePath, $"{controlPath}.field", diagnostics, form.Name!, table.LogicalName!);
                                    RequireValue(control.QuickFormEntity, $"{controlPath}.quickFormEntity", sourcePath, diagnostics);
                                    RequireValue(control.QuickFormId, $"{controlPath}.quickFormId", sourcePath, diagnostics);
                                    if (!string.IsNullOrWhiteSpace(control.QuickFormId) && !Guid.TryParse(control.QuickFormId, out _))
                                    {
                                        diagnostics.Add(CreateError(sourcePath, $"{controlPath}.quickFormId", "Quick-view controls require a valid quickFormId GUID."));
                                    }

                                    break;
                                case "subgrid":
                                    RequireValue(control.RelationshipName, $"{controlPath}.relationshipName", sourcePath, diagnostics);
                                    RequireValue(control.TargetTable, $"{controlPath}.targetTable", sourcePath, diagnostics);
                                    if (!string.IsNullOrWhiteSpace(control.TargetTable)
                                        && !knownTables.Contains(NormalizeLogicalName(control.TargetTable) ?? string.Empty))
                                    {
                                        diagnostics.Add(CreateError(
                                            sourcePath,
                                            $"{controlPath}.targetTable",
                                            $"Subgrid control on form '{form.Name}' references unknown target table '{control.TargetTable}'."));
                                    }

                                    if (!string.IsNullOrWhiteSpace(control.DefaultViewId) && !Guid.TryParse(control.DefaultViewId, out _))
                                    {
                                        diagnostics.Add(CreateError(sourcePath, $"{controlPath}.defaultViewId", "Subgrid defaultViewId values must be valid GUIDs when supplied."));
                                    }

                                    if (control.RecordsPerPage is <= 0)
                                    {
                                        diagnostics.Add(CreateError(sourcePath, $"{controlPath}.recordsPerPage", "Subgrid recordsPerPage must be a positive integer when supplied."));
                                    }

                                    break;
                                case null:
                                    break;
                                default:
                                    diagnostics.Add(CreateError(sourcePath, $"{controlPath}.kind", $"Unsupported form control kind '{control.Kind}'. Supported kinds are field, quickView, and subgrid."));
                                    break;
                            }
                        }
                    }
                }

                foreach (var (field, fieldIndex) in Enumerate(form.HeaderFields))
                {
                    if (!IsSupportedFormFieldReference(field, allowedFormFieldNames))
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

            foreach (var (visualization, visualizationIndex) in Enumerate(table.Visualizations))
            {
                var visualizationPath = $"{tablePath}.visualizations[{visualizationIndex}]";
                ValidateUnexpectedProperties(visualization.ExtensionData, visualizationPath, sourcePath, diagnostics);
                RequireValue(visualization.Name, $"{visualizationPath}.name", sourcePath, diagnostics);
                RequireValue(visualization.DataDescriptionXml, $"{visualizationPath}.dataDescriptionXml", sourcePath, diagnostics);
                RequireValue(visualization.PresentationDescriptionXml, $"{visualizationPath}.presentationDescriptionXml", sourcePath, diagnostics);
                if (!string.IsNullOrWhiteSpace(visualization.Id) && !Guid.TryParse(visualization.Id, out _))
                {
                    diagnostics.Add(CreateError(sourcePath, $"{visualizationPath}.id", "Visualization ids must be valid GUID values when supplied."));
                }

                if (visualization.ChartTypes is null || visualization.ChartTypes.Count == 0)
                {
                    diagnostics.Add(CreateError(sourcePath, $"{visualizationPath}.chartTypes", "Each visualization requires at least one chartTypes entry."));
                }

                ValidateXmlFragment(visualization.DataDescriptionXml, $"{visualizationPath}.dataDescriptionXml", sourcePath, diagnostics);
                ValidateXmlFragment(visualization.PresentationDescriptionXml, $"{visualizationPath}.presentationDescriptionXml", sourcePath, diagnostics);
            }
        }

        foreach (var (appModule, appModuleIndex) in Enumerate(document.AppModules))
        {
            var appModulePath = $"$.appModules[{appModuleIndex}]";
            ValidateUnexpectedProperties(appModule.ExtensionData, appModulePath, sourcePath, diagnostics);
            RequireValue(appModule.UniqueName, $"{appModulePath}.uniqueName", sourcePath, diagnostics);
            RequireValue(appModule.DisplayName, $"{appModulePath}.displayName", sourcePath, diagnostics);
            foreach (var (roleId, roleIdIndex) in Enumerate(appModule.RoleIds))
            {
                if (!Guid.TryParse(roleId, out _))
                {
                    diagnostics.Add(CreateError(sourcePath, $"{appModulePath}.roleIds[{roleIdIndex}]", "App module roleIds entries must be valid GUID values when supplied."));
                }
            }

            if (appModule.SiteMap is null)
            {
                diagnostics.Add(CreateError(sourcePath, $"{appModulePath}.siteMap", "JSON v1 app modules require a siteMap section."));
                continue;
            }

            foreach (var (appSetting, appSettingIndex) in Enumerate(appModule.AppSettings))
            {
                var appSettingPath = $"{appModulePath}.appSettings[{appSettingIndex}]";
                ValidateUnexpectedProperties(appSetting.ExtensionData, appSettingPath, sourcePath, diagnostics);
                RequireValue(appSetting.DefinitionUniqueName, $"{appSettingPath}.definitionUniqueName", sourcePath, diagnostics);
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
                        var hasEntity = !string.IsNullOrWhiteSpace(subArea.Entity);
                        var hasUrl = !string.IsNullOrWhiteSpace(subArea.Url);
                        var hasWebResource = !string.IsNullOrWhiteSpace(subArea.WebResource);
                        var hasDashboard = !string.IsNullOrWhiteSpace(subArea.Dashboard);
                        var hasCustomPage = !string.IsNullOrWhiteSpace(subArea.CustomPage);
                        var hasEntityList = hasEntity && !string.IsNullOrWhiteSpace(subArea.ViewId);
                        var hasEntityRecord = hasEntity && !string.IsNullOrWhiteSpace(subArea.RecordId);
                        var populatedTargets = new[]
                        {
                            hasEntity,
                            hasUrl,
                            hasWebResource,
                            hasDashboard,
                            hasCustomPage
                        }.Count(value => value);
                        if (populatedTargets != 1)
                        {
                            diagnostics.Add(CreateError(
                                sourcePath,
                                subAreaPath,
                                "Each site map sub-area must declare exactly one of entity, url, webResource, dashboard, or customPage."));
                            continue;
                        }

                        var normalizedEntity = NormalizeLogicalName(subArea.Entity);
                        if (hasEntity
                            && string.IsNullOrWhiteSpace(normalizedEntity))
                        {
                            diagnostics.Add(CreateError(
                                sourcePath,
                                $"{subAreaPath}.entity",
                                $"Site map sub-area '{subArea.Id}' references entity '{subArea.Entity}', but only logical-name entity targets are supported in the current structured subset."));
                        }
                        else if (!string.IsNullOrWhiteSpace(normalizedEntity)
                            && !knownTables.Contains(normalizedEntity)
                            && !hasEntityList
                            && !hasEntityRecord)
                        {
                            diagnostics.Add(CreateError(
                                sourcePath,
                                $"{subAreaPath}.entity",
                                $"Site map sub-area '{subArea.Id}' references unknown table '{subArea.Entity}'."));
                        }

                        if (hasDashboard
                            && NormalizeGuid(subArea.Dashboard) is null)
                        {
                            diagnostics.Add(CreateError(
                                sourcePath,
                                $"{subAreaPath}.dashboard",
                                $"Site map sub-area '{subArea.Id}' references dashboard '{subArea.Dashboard}', but only GUID-backed dashboard targets are supported in the current structured subset."));
                        }

                        if (hasCustomPage
                            && NormalizeLogicalName(subArea.CustomPage) is null)
                        {
                            diagnostics.Add(CreateError(
                                sourcePath,
                                $"{subAreaPath}.customPage",
                                $"Site map sub-area '{subArea.Id}' references custom page '{subArea.CustomPage}', but only logical-name custom-page targets are supported in the current structured subset."));
                        }

                        if (!string.IsNullOrWhiteSpace(subArea.AppId))
                        {
                            if (!hasCustomPage
                                && !hasDashboard
                                && string.IsNullOrWhiteSpace(subArea.ViewId)
                                && string.IsNullOrWhiteSpace(subArea.RecordId))
                            {
                                diagnostics.Add(CreateError(
                                    sourcePath,
                                    $"{subAreaPath}.appId",
                                    "appId is supported only for app-scoped dashboard, customPage, entity-list, or entity-record deep-link targets."));
                            }
                            else if (NormalizeGuid(subArea.AppId) is null)
                            {
                                diagnostics.Add(CreateError(
                                    sourcePath,
                                    $"{subAreaPath}.appId",
                                    $"Site map sub-area '{subArea.Id}' references appId '{subArea.AppId}', but only GUID-backed app-scoped dashboard, custom-page, entity-list, or entity-record targets are supported in the current structured subset."));
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(subArea.CustomPageEntityName))
                        {
                            if (!hasCustomPage)
                            {
                                diagnostics.Add(CreateError(
                                    sourcePath,
                                    $"{subAreaPath}.customPageEntityName",
                                    "customPageEntityName is supported only when customPage is also supplied."));
                            }
                            else if (NormalizeLogicalName(subArea.CustomPageEntityName) is null)
                            {
                                diagnostics.Add(CreateError(
                                    sourcePath,
                                    $"{subAreaPath}.customPageEntityName",
                                    $"Site map sub-area '{subArea.Id}' references customPageEntityName '{subArea.CustomPageEntityName}', but only logical-name custom-page context entities are supported in the current structured subset."));
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(subArea.CustomPageRecordId))
                        {
                            if (!hasCustomPage)
                            {
                                diagnostics.Add(CreateError(
                                    sourcePath,
                                    $"{subAreaPath}.customPageRecordId",
                                    "customPageRecordId is supported only when customPage is also supplied."));
                            }
                            else if (NormalizeGuid(subArea.CustomPageRecordId) is null)
                            {
                                diagnostics.Add(CreateError(
                                    sourcePath,
                                    $"{subAreaPath}.customPageRecordId",
                                    $"Site map sub-area '{subArea.Id}' references customPageRecordId '{subArea.CustomPageRecordId}', but only GUID-backed custom-page record context is supported in the current structured subset."));
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(subArea.ViewId))
                        {
                            if (!hasEntity)
                            {
                                diagnostics.Add(CreateError(
                                    sourcePath,
                                    $"{subAreaPath}.viewId",
                                    "viewId is supported only when entity is also supplied."));
                            }
                            else if (NormalizeGuid(subArea.ViewId) is null)
                            {
                                diagnostics.Add(CreateError(
                                    sourcePath,
                                    $"{subAreaPath}.viewId",
                                    $"Site map sub-area '{subArea.Id}' references viewId '{subArea.ViewId}', but only GUID-backed entity-list targets are supported in the current structured subset."));
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(subArea.ViewType))
                        {
                            if (string.IsNullOrWhiteSpace(subArea.ViewId))
                            {
                                diagnostics.Add(CreateError(
                                    sourcePath,
                                    $"{subAreaPath}.viewType",
                                    "viewType is supported only when viewId is also supplied."));
                            }
                            else if (NormalizeSiteMapViewType(subArea.ViewType) is null)
                            {
                                diagnostics.Add(CreateError(
                                    sourcePath,
                                    $"{subAreaPath}.viewType",
                                    $"Site map sub-area '{subArea.Id}' references viewType '{subArea.ViewType}', but only savedquery or userquery entity-list targets are supported in the current structured subset."));
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(subArea.RecordId))
                        {
                            if (!hasEntity)
                            {
                                diagnostics.Add(CreateError(
                                    sourcePath,
                                    $"{subAreaPath}.recordId",
                                    "recordId is supported only when entity is also supplied."));
                            }
                            else if (NormalizeGuid(subArea.RecordId) is null)
                            {
                                diagnostics.Add(CreateError(
                                    sourcePath,
                                    $"{subAreaPath}.recordId",
                                    $"Site map sub-area '{subArea.Id}' references recordId '{subArea.RecordId}', but only GUID-backed entity-record targets are supported in the current structured subset."));
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(subArea.FormId))
                        {
                            if (string.IsNullOrWhiteSpace(subArea.RecordId))
                            {
                                diagnostics.Add(CreateError(
                                    sourcePath,
                                    $"{subAreaPath}.formId",
                                    "formId is supported only when recordId is also supplied."));
                            }
                            else if (NormalizeGuid(subArea.FormId) is null)
                            {
                                diagnostics.Add(CreateError(
                                    sourcePath,
                                    $"{subAreaPath}.formId",
                                    $"Site map sub-area '{subArea.Id}' references formId '{subArea.FormId}', but only GUID-backed entity-record form targets are supported in the current structured subset."));
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(subArea.ViewId)
                            && !string.IsNullOrWhiteSpace(subArea.RecordId))
                        {
                            diagnostics.Add(CreateError(
                                sourcePath,
                                subAreaPath,
                                "Entity-targeted site map sub-areas can preserve either an entity-list view target or an entity-record target, but not both at the same time."));
                        }

                        if (!string.IsNullOrWhiteSpace(subArea.FormId)
                            && !string.IsNullOrWhiteSpace(subArea.ViewId))
                        {
                            diagnostics.Add(CreateError(
                                sourcePath,
                                subAreaPath,
                                "formId is not supported on entity-list targets."));
                        }

                        if (!string.IsNullOrWhiteSpace(subArea.ViewType)
                            && !string.IsNullOrWhiteSpace(subArea.RecordId))
                        {
                            diagnostics.Add(CreateError(
                                sourcePath,
                                subAreaPath,
                                "viewType is not supported on entity-record targets."));
                        }
                    }
                }
            }
        }

        foreach (var (sourceBackedArtifact, sourceBackedArtifactIndex) in Enumerate(document.SourceBackedArtifacts))
        {
            var artifactPath = $"$.sourceBackedArtifacts[{sourceBackedArtifactIndex}]";
            ValidateUnexpectedProperties(sourceBackedArtifact.ExtensionData, artifactPath, sourcePath, diagnostics);
            RequireValue(sourceBackedArtifact.Family, $"{artifactPath}.family", sourcePath, diagnostics);
            RequireValue(sourceBackedArtifact.LogicalName, $"{artifactPath}.logicalName", sourcePath, diagnostics);
            RequireValue(sourceBackedArtifact.MetadataSourcePath, $"{artifactPath}.metadataSourcePath", sourcePath, diagnostics);
            RequireValue(sourceBackedArtifact.PackageRelativePath, $"{artifactPath}.packageRelativePath", sourcePath, diagnostics);

            if (!string.IsNullOrWhiteSpace(sourceBackedArtifact.Family)
                && !Enum.TryParse<ComponentFamily>(sourceBackedArtifact.Family, ignoreCase: true, out _))
            {
                diagnostics.Add(CreateError(
                    sourcePath,
                    $"{artifactPath}.family",
                    $"Unknown component family '{sourceBackedArtifact.Family}'."));
            }

            foreach (var (assetSourcePath, assetIndex) in Enumerate(sourceBackedArtifact.AssetSourcePaths))
            {
                if (Path.IsPathRooted(assetSourcePath))
                {
                    diagnostics.Add(CreateError(
                        sourcePath,
                        $"{artifactPath}.assetSourcePaths[{assetIndex}]",
                        "assetSourcePaths entries must be relative intent-spec paths so package-relative destinations remain deterministic."));
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
            var visualizations = table.Visualizations ?? [];

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
                    (ArtifactPropertyKeys.IsCustomizable, (table.IsCustomizable ?? true) ? "true" : "false"),
                    (ArtifactPropertyKeys.PrimaryIdAttribute, primaryIdLogicalName),
                    (ArtifactPropertyKeys.PrimaryNameAttribute, primaryNameLogicalName),
                    (ArtifactPropertyKeys.PrimaryImageAttribute, NormalizeLogicalName(table.PrimaryImageAttribute)),
                    (ArtifactPropertyKeys.ShellOnly, (!authoredColumns.Any() && !forms.Any() && !views.Any() && !visualizations.Any()) ? "true" : "false")))
        };

        artifacts.Add(CreateColumnArtifact(
            tableLogicalName,
            primaryIdLogicalName,
            primaryIdSchemaName,
            $"{table.DisplayName} Id",
            table.Description,
            "primarykey",
            isSecured: false,
            isCustomizable: false,
            isCustomField: false,
            isPrimaryKey: true,
            isPrimaryName: false,
            optionSetName: null,
            optionSetType: null,
            isGlobalOptionSet: null,
            canStoreFullImage: null,
            isPrimaryImage: null,
            sourcePath));

        artifacts.Add(CreateColumnArtifact(
            tableLogicalName,
            primaryNameLogicalName,
            primaryNameSchemaName,
            table.DisplayName!,
            table.Description,
            "string",
            isSecured: false,
            isCustomizable: true,
            isCustomField: true,
            isPrimaryKey: false,
            isPrimaryName: true,
            optionSetName: null,
            optionSetType: null,
            isGlobalOptionSet: null,
            canStoreFullImage: null,
            isPrimaryImage: null,
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
            var localOptionSetName = localOptionArtifact?.Properties is not null
                && localOptionArtifact.Properties.TryGetValue(ArtifactPropertyKeys.OptionSetName, out var localOptionSetNameValue)
                    ? localOptionSetNameValue
                    : null;
            var localOptionSetType = localOptionArtifact?.Properties is not null
                && localOptionArtifact.Properties.TryGetValue(ArtifactPropertyKeys.OptionSetType, out var localOptionSetTypeValue)
                    ? localOptionSetTypeValue
                    : null;

            artifacts.Add(CreateColumnArtifact(
                tableLogicalName,
                normalizedColumnLogicalName,
                column.SchemaName!,
                column.DisplayName!,
                column.Description,
                normalizedColumnType,
                column.IsSecured,
                column.IsCustomizable ?? true,
                isCustomField: true,
                isPrimaryKey: false,
                isPrimaryName: false,
                optionSetName: normalizedGlobalOptionSet ?? localOptionSetName,
                optionSetType: localOptionArtifact is null ? (normalizedGlobalOptionSet is null ? null : "picklist") : localOptionSetType,
                isGlobalOptionSet: normalizedGlobalOptionSet is not null ? "true" : (localOptionArtifact is null ? null : "false"),
                canStoreFullImage: normalizedColumnType == "image" ? (column.CanStoreFullImage ?? false) : null,
                isPrimaryImage: normalizedColumnType == "image" ? (column.IsPrimaryImage ?? string.Equals(NormalizeLogicalName(table.PrimaryImageAttribute), normalizedColumnLogicalName, StringComparison.OrdinalIgnoreCase)) : null,
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

        foreach (var visualization in visualizations.Where(visualization => visualization is not null))
        {
            artifacts.Add(CreateVisualizationArtifact(tableLogicalName, visualization!, sourcePath));
        }

        foreach (var key in table.Keys ?? [])
        {
            artifacts.Add(CreateKeyArtifact(tableLogicalName, key!, sourcePath));
        }

        foreach (var imageConfiguration in CreateImageConfigurationArtifacts(table, tableLogicalName, sourcePath))
        {
            artifacts.Add(imageConfiguration);
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

        var optionSetName = BuildLocalOptionSetName(entityLogicalName, columnLogicalName);
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
                (ArtifactPropertyKeys.OptionSetName, optionSetName),
                (ArtifactPropertyKeys.OptionSetType, optionSetType),
                (ArtifactPropertyKeys.Description, column.Description),
                (ArtifactPropertyKeys.IsGlobal, "false"),
                (ArtifactPropertyKeys.OptionCount, options.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                (ArtifactPropertyKeys.OptionsJson, SerializeJson(options)),
                (ArtifactPropertyKeys.SummaryJson, summaryJson),
                (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(summaryJson))));
    }

    private static string BuildLocalOptionSetName(string entityLogicalName, string columnLogicalName) =>
        $"{NormalizeLogicalName(entityLogicalName)}_{NormalizeLogicalName(columnLogicalName)}";

    private static FamilyArtifact CreateColumnArtifact(
        string entityLogicalName,
        string logicalName,
        string schemaName,
        string displayName,
        string? description,
        string attributeType,
        bool isSecured,
        bool isCustomizable,
        bool isCustomField,
        bool isPrimaryKey,
        bool isPrimaryName,
        string? optionSetName,
        string? optionSetType,
        string? isGlobalOptionSet,
        bool? canStoreFullImage,
        bool? isPrimaryImage,
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
                (ArtifactPropertyKeys.IsCustomizable, isCustomizable ? "true" : "false"),
                (ArtifactPropertyKeys.IsPrimaryKey, isPrimaryKey ? "true" : "false"),
                (ArtifactPropertyKeys.IsPrimaryName, isPrimaryName ? "true" : "false"),
                (ArtifactPropertyKeys.IsLogical, "false"),
                (ArtifactPropertyKeys.OptionSetName, optionSetName),
                (ArtifactPropertyKeys.OptionSetType, optionSetType),
                (ArtifactPropertyKeys.IsGlobal, isGlobalOptionSet),
                (ArtifactPropertyKeys.CanStoreFullImage, canStoreFullImage.HasValue ? (canStoreFullImage.Value ? "true" : "false") : null),
                (ArtifactPropertyKeys.IsPrimaryImage, isPrimaryImage.HasValue ? (isPrimaryImage.Value ? "true" : "false") : null)));

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

    private static IEnumerable<FamilyArtifact> CreateImageConfigurationArtifacts(
        TableSpec table,
        string entityLogicalName,
        string sourcePath)
    {
        var primaryImageAttribute = NormalizeLogicalName(table.PrimaryImageAttribute);
        if (string.IsNullOrWhiteSpace(primaryImageAttribute))
        {
            yield break;
        }

        var imageColumns = (table.Columns ?? [])
            .Where(column => string.Equals(column?.Type, "image", StringComparison.OrdinalIgnoreCase))
            .Select(column => new
            {
                LogicalName = NormalizeLogicalName(column!.LogicalName),
                CanStoreFullImage = column!.CanStoreFullImage ?? false,
                IsPrimaryImage = column.IsPrimaryImage ?? string.Equals(NormalizeLogicalName(column.LogicalName), primaryImageAttribute, StringComparison.OrdinalIgnoreCase)
            })
            .Where(column => !string.IsNullOrWhiteSpace(column.LogicalName))
            .ToArray();
        if (imageColumns.Length == 0)
        {
            yield break;
        }

        var primaryColumn = imageColumns.FirstOrDefault(column => string.Equals(column.LogicalName, primaryImageAttribute, StringComparison.OrdinalIgnoreCase))
            ?? imageColumns[0];

        yield return new FamilyArtifact(
            ComponentFamily.ImageConfiguration,
            $"{entityLogicalName}|entity-image",
            primaryImageAttribute,
            sourcePath,
            EvidenceKind.Derived,
            CreateProperties(
                (ArtifactPropertyKeys.EntityLogicalName, entityLogicalName),
                (ArtifactPropertyKeys.ImageConfigurationScope, "entity"),
                (ArtifactPropertyKeys.PrimaryImageAttribute, primaryImageAttribute),
                (ArtifactPropertyKeys.ImageAttributeLogicalName, primaryImageAttribute),
                (ArtifactPropertyKeys.CanStoreFullImage, primaryColumn.CanStoreFullImage ? "true" : "false"),
                (ArtifactPropertyKeys.IsPrimaryImage, "true")));

        foreach (var imageColumn in imageColumns)
        {
            yield return new FamilyArtifact(
                ComponentFamily.ImageConfiguration,
                $"{entityLogicalName}|{imageColumn.LogicalName}|attribute-image",
                imageColumn.LogicalName,
                sourcePath,
                EvidenceKind.Derived,
                CreateProperties(
                    (ArtifactPropertyKeys.EntityLogicalName, entityLogicalName),
                    (ArtifactPropertyKeys.ImageConfigurationScope, "attribute"),
                    (ArtifactPropertyKeys.PrimaryImageAttribute, primaryImageAttribute),
                    (ArtifactPropertyKeys.ImageAttributeLogicalName, imageColumn.LogicalName),
                    (ArtifactPropertyKeys.CanStoreFullImage, imageColumn.CanStoreFullImage ? "true" : "false"),
                    (ArtifactPropertyKeys.IsPrimaryImage, imageColumn.IsPrimaryImage ? "true" : "false")));
        }
    }

    private static FamilyArtifact CreateFormArtifact(string entityLogicalName, FormSpec form, string sourcePath)
    {
        var formId = NormalizeGuid(form.Id) ?? CreateDeterministicGuid(entityLogicalName, "form", form.Name!);
        var controlDescriptions = BuildFormControlDescriptions(form);
        var formType = NormalizeFormType(form.Type) ?? "main";
        var summary = new
        {
            formType,
            formId,
            tabCount = form.Tabs?.Count ?? 0,
            sectionCount = form.Tabs?.Sum(tab => tab?.Sections?.Count ?? 0) ?? 0,
            controlCount = controlDescriptions.Length,
            quickFormCount = controlDescriptions.Count(control => string.Equals(control.Role, "quickView", StringComparison.OrdinalIgnoreCase)),
            subgridCount = controlDescriptions.Count(control => string.Equals(control.Role, "subgrid", StringComparison.OrdinalIgnoreCase)),
            headerControlCount = form.HeaderFields?.Count ?? 0,
            footerControlCount = 0,
            controlDescriptions = controlDescriptions.Select(control => new
            {
                id = control.Id,
                dataFieldName = control.DataFieldName,
                role = control.Role
            }).ToArray()
        };
        var summaryJson = SerializeJson(summary);

        return new FamilyArtifact(
            ComponentFamily.Form,
            $"{entityLogicalName}|{formType}|{formId}",
            form.Name,
            sourcePath,
            EvidenceKind.Derived,
            CreateProperties(
                (ArtifactPropertyKeys.EntityLogicalName, entityLogicalName),
                (ArtifactPropertyKeys.FormType, formType),
                (ArtifactPropertyKeys.FormTypeCode, "1"),
                (ArtifactPropertyKeys.FormId, formId),
                (ArtifactPropertyKeys.Description, form.Description),
                (ArtifactPropertyKeys.FormDefinitionJson, SerializeJson(form)),
                (ArtifactPropertyKeys.TabCount, summary.tabCount.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                (ArtifactPropertyKeys.SectionCount, summary.sectionCount.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                (ArtifactPropertyKeys.ControlCount, summary.controlCount.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                (ArtifactPropertyKeys.QuickFormCount, summary.quickFormCount.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                (ArtifactPropertyKeys.SubgridCount, summary.subgridCount.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                (ArtifactPropertyKeys.HeaderControlCount, summary.headerControlCount.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                (ArtifactPropertyKeys.FooterControlCount, "0"),
                (ArtifactPropertyKeys.ControlDescriptionCount, controlDescriptions.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                (ArtifactPropertyKeys.SummaryJson, summaryJson),
                (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(summaryJson))));
    }

    private static FormControlDescriptionEntry[] BuildFormControlDescriptions(FormSpec form)
    {
        var bodyControls = form.Tabs?
            .SelectMany(tab => tab?.Sections ?? [])
            .SelectMany(section => EnumerateFormControls(section))
            ?? [];
        var headerControls = form.HeaderFields?
            .Select(field => new FormControlDescriptionEntry(
                $"header_{NormalizeLogicalName(field)}",
                NormalizeLogicalName(field) ?? string.Empty,
                "field"))
            ?? [];

        return bodyControls
            .Concat(headerControls)
            .OrderBy(control => control.Id, StringComparer.OrdinalIgnoreCase)
            .ThenBy(control => control.DataFieldName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IEnumerable<FormControlDescriptionEntry> EnumerateFormControls(FormSectionSpec? section)
    {
        if (section is null)
        {
            yield break;
        }

        foreach (var field in section.Fields ?? [])
        {
            var normalizedField = NormalizeLogicalName(field);
            if (string.IsNullOrWhiteSpace(normalizedField))
            {
                continue;
            }

            yield return new FormControlDescriptionEntry(normalizedField, normalizedField, "field");
        }

        foreach (var control in section.Controls ?? [])
        {
            var kind = NormalizeFormControlKind(control?.Kind);
            if (kind is null)
            {
                continue;
            }

            var dataFieldName = NormalizeLogicalName(control?.Field) ?? string.Empty;
            var id = kind switch
            {
                "field" => dataFieldName,
                "quickView" => string.IsNullOrWhiteSpace(dataFieldName) ? "quickview" : $"quickview_{dataFieldName}",
                "subgrid" => NormalizeLogicalName(control?.RelationshipName) ?? NormalizeLogicalName(control?.TargetTable) ?? "subgrid",
                _ => string.Empty
            };

            yield return new FormControlDescriptionEntry(id, dataFieldName, kind);
        }
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

    private static FamilyArtifact CreateVisualizationArtifact(
        string entityLogicalName,
        VisualizationSpec visualization,
        string sourcePath)
    {
        var chartTypes = (visualization.ChartTypes ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var groupByColumns = (visualization.GroupByColumns ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => NormalizeLogicalName(value) ?? value!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var measureAliases = (visualization.MeasureAliases ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var titleNames = (visualization.TitleNames ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var visualizationId = NormalizeGuid(visualization.Id) ?? CreateDeterministicGuid(entityLogicalName, "visualization", visualization.Name!);
        var dataDescriptionXml = NormalizeXmlString(visualization.DataDescriptionXml);
        var presentationDescriptionXml = NormalizeXmlString(visualization.PresentationDescriptionXml);
        var summaryJson = SerializeJson(new
        {
            targetEntity = entityLogicalName,
            chartTypes,
            groupByColumns,
            measureAliases,
            titleNames
        });

        return new FamilyArtifact(
            ComponentFamily.Visualization,
            $"{entityLogicalName}|{visualization.Name}",
            visualization.Name,
            sourcePath,
            EvidenceKind.Derived,
            CreateProperties(
                (ArtifactPropertyKeys.EntityLogicalName, entityLogicalName),
                (ArtifactPropertyKeys.TargetEntity, entityLogicalName),
                (ArtifactPropertyKeys.VisualizationId, visualizationId),
                (ArtifactPropertyKeys.Description, visualization.Description),
                (ArtifactPropertyKeys.IntroducedVersion, "1.0.0.0"),
                (ArtifactPropertyKeys.DataDescriptionXml, dataDescriptionXml),
                (ArtifactPropertyKeys.PresentationDescriptionXml, presentationDescriptionXml),
                (ArtifactPropertyKeys.ChartTypesJson, SerializeJson(chartTypes)),
                (ArtifactPropertyKeys.GroupByColumnsJson, SerializeJson(groupByColumns)),
                (ArtifactPropertyKeys.MeasureAliasesJson, SerializeJson(measureAliases)),
                (ArtifactPropertyKeys.TitleNamesJson, SerializeJson(titleNames)),
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
        var roleIds = appModule.RoleIds
            ?.Select(NormalizeGuid)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .Cast<string>()
            .ToArray()
            ?? [];
        var appSettings = (appModule.AppSettings ?? [])
            .Where(setting => !string.IsNullOrWhiteSpace(setting?.DefinitionUniqueName))
            .Select(setting => setting!)
            .ToArray();
        var appModuleSummaryJson = SerializeJson(new
        {
            componentTypes = JsonNode.Parse(componentTypesJson),
            roleIds,
            roleMapCount = roleIds.Length,
            appSettingCount = appSettings.Length
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
                (ArtifactPropertyKeys.RoleIdsJson, SerializeJson(roleIds)),
                (ArtifactPropertyKeys.RoleMapCount, roleIds.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                (ArtifactPropertyKeys.AppSettingCount, appSettings.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                (ArtifactPropertyKeys.SummaryJson, appModuleSummaryJson),
                (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(appModuleSummaryJson))));

        foreach (var appSetting in appSettings)
        {
            yield return new FamilyArtifact(
                ComponentFamily.AppSetting,
                $"{uniqueName}|{appSetting.DefinitionUniqueName}",
                appSetting.DefinitionUniqueName,
                sourcePath,
                EvidenceKind.Derived,
                CreateProperties(
                    (ArtifactPropertyKeys.ParentAppModuleUniqueName, uniqueName),
                    (ArtifactPropertyKeys.SettingDefinitionUniqueName, appSetting.DefinitionUniqueName),
                    (ArtifactPropertyKeys.Value, appSetting.Value)));
        }

        var canonicalSiteMap = CanonicalizeSiteMap(appModule.SiteMap!);
        var areaCount = canonicalSiteMap.Areas?.Count ?? 0;
        var groupCount = canonicalSiteMap.Areas?.Sum(area => area?.Groups?.Count ?? 0) ?? 0;
        var subAreaCount = canonicalSiteMap.Areas?
            .SelectMany(area => area?.Groups ?? [])
            .Sum(group => group?.SubAreas?.Count ?? 0) ?? 0;
        var webResourceSubAreaCount = canonicalSiteMap.Areas?
            .SelectMany(area => area?.Groups ?? [])
            .SelectMany(group => group?.SubAreas ?? [])
            .Count(subArea => !string.IsNullOrWhiteSpace(subArea?.WebResource))
            ?? 0;
        var siteMapSummaryJson = SerializeJson(new
        {
            areaCount,
            groupCount,
            subAreaCount,
            webResourceSubAreaCount
        });
        var siteMapDefinitionJson = SerializeJson(canonicalSiteMap);

        yield return new FamilyArtifact(
            ComponentFamily.SiteMap,
            uniqueName,
            appModule.DisplayName,
            sourcePath,
            EvidenceKind.Derived,
            CreateProperties(
                (ArtifactPropertyKeys.SiteMapDefinitionJson, siteMapDefinitionJson),
                (ArtifactPropertyKeys.AreaCount, areaCount.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                (ArtifactPropertyKeys.GroupCount, groupCount.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                (ArtifactPropertyKeys.SubAreaCount, subAreaCount.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                (ArtifactPropertyKeys.WebResourceSubAreaCount, webResourceSubAreaCount.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                (ArtifactPropertyKeys.SummaryJson, siteMapSummaryJson),
                (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(siteMapDefinitionJson))));
    }

    private static SiteMapSpec CanonicalizeSiteMap(SiteMapSpec siteMap) =>
        new()
        {
            Areas = (siteMap.Areas ?? [])
                .Select(area => new SiteMapAreaSpec
                {
                    Id = area.Id,
                    Title = area.Title,
                    Groups = (area.Groups ?? [])
                        .Select(group => new SiteMapGroupSpec
                        {
                            Id = group.Id,
                            Title = group.Title,
                            SubAreas = (group.SubAreas ?? [])
                                .Select(subArea =>
                                {
                                    var normalizedDashboard = NormalizeGuid(subArea.Dashboard);
                                    var normalizedCustomPage = NormalizeLogicalName(subArea.CustomPage);
                                    var normalizedCustomPageEntityName = NormalizeLogicalName(subArea.CustomPageEntityName);
                                    var normalizedCustomPageRecordId = NormalizeGuid(subArea.CustomPageRecordId);
                                    var normalizedEntity = NormalizeLogicalName(subArea.Entity);
                                    var normalizedViewId = NormalizeGuid(subArea.ViewId);
                                    var normalizedViewType = NormalizeSiteMapViewType(subArea.ViewType);
                                    var normalizedRecordId = NormalizeGuid(subArea.RecordId);
                                    var normalizedFormId = NormalizeGuid(subArea.FormId);
                                    var normalizedAppId = normalizedCustomPage is not null
                                        || normalizedDashboard is not null
                                        || normalizedViewId is not null
                                        || normalizedRecordId is not null
                                        ? NormalizeGuid(subArea.AppId)
                                        : null;
                                    return new SiteMapSubAreaSpec
                                    {
                                        Id = subArea.Id,
                                        Title = subArea.Title,
                                        Entity = normalizedEntity,
                                        ViewId = normalizedViewId,
                                        ViewType = normalizedViewType,
                                        RecordId = normalizedRecordId,
                                        FormId = normalizedFormId,
                                        Url = normalizedDashboard is null
                                            && normalizedCustomPage is null
                                            && normalizedViewId is null
                                            && normalizedRecordId is null
                                            ? NormalizeSiteMapRawUrl(subArea.Url)
                                            : null,
                                        WebResource = subArea.WebResource,
                                        Dashboard = normalizedDashboard,
                                        CustomPage = normalizedCustomPage,
                                        CustomPageEntityName = normalizedCustomPage is not null ? normalizedCustomPageEntityName : null,
                                        CustomPageRecordId = normalizedCustomPage is not null ? normalizedCustomPageRecordId : null,
                                        AppId = normalizedAppId,
                                        Client = subArea.Client,
                                        PassParams = subArea.PassParams,
                                        AvailableOffline = subArea.AvailableOffline,
                                        Icon = subArea.Icon,
                                        VectorIcon = subArea.VectorIcon
                                    };
                                })
                                .ToArray()
                        })
                        .ToArray()
                })
                .ToArray()
        };

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

    private static FamilyArtifact CreateSourceBackedArtifact(SourceBackedArtifactSpec sourceBackedArtifact, string intentPath)
    {
        if (!Enum.TryParse<ComponentFamily>(sourceBackedArtifact.Family, ignoreCase: true, out var family))
        {
            throw new InvalidOperationException($"Unknown source-backed family '{sourceBackedArtifact.Family}'.");
        }

        var metadataFullPath = ResolveIntentSourcePath(intentPath, sourceBackedArtifact.MetadataSourcePath!);
        var properties = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ArtifactPropertyKeys.MetadataSourcePath] = sourceBackedArtifact.MetadataSourcePath!,
            [ArtifactPropertyKeys.PackageRelativePath] = sourceBackedArtifact.PackageRelativePath!
        };

        if (sourceBackedArtifact.AssetSourcePaths is { Count: > 0 })
        {
            var assetMap = sourceBackedArtifact.AssetSourcePaths
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => new
                {
                    sourcePath = ResolveIntentSourcePath(intentPath, path!),
                    packageRelativePath = DeriveSourceBackedPackageRelativePath(path!)
                })
                .ToArray();
            if (assetMap.Length > 0)
            {
                properties[ArtifactPropertyKeys.AssetSourceMapJson] = SerializeJson(assetMap);
            }
        }

        foreach (var property in ConvertStableProperties(sourceBackedArtifact.StableProperties))
        {
            properties[property.Key] = property.Value;
        }

        return new FamilyArtifact(
            family,
            sourceBackedArtifact.LogicalName!,
            sourceBackedArtifact.DisplayName ?? sourceBackedArtifact.LogicalName!,
            metadataFullPath,
            EvidenceKind.Source,
            properties);
    }

    private static IEnumerable<KeyValuePair<string, string>> ConvertStableProperties(IReadOnlyDictionary<string, JsonElement>? stableProperties)
    {
        if (stableProperties is null)
        {
            yield break;
        }

        foreach (var property in stableProperties.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            yield return new KeyValuePair<string, string>(
                property.Key,
                property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array
                    ? property.Value.GetRawText()
                    : property.Value.ToString());
        }
    }

    private static string ResolveIntentSourcePath(string intentPath, string relativeOrAbsolutePath) =>
        Path.IsPathRooted(relativeOrAbsolutePath)
            ? Path.GetFullPath(relativeOrAbsolutePath)
            : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(intentPath) ?? string.Empty, relativeOrAbsolutePath.Replace('/', Path.DirectorySeparatorChar)));

    private static string DeriveSourceBackedPackageRelativePath(string assetSourcePath)
    {
        var normalized = assetSourcePath.Replace('\\', '/').TrimStart('/');
        const string prefix = "source-backed/";
        return normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? normalized[prefix.Length..]
            : Path.GetFileName(normalized);
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
            "integer" => "integer",
            "image" => "image",
            "lookup" => "lookup",
            var other => other
        };

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

    private static string? TryNormalizeSiteMapRawGuid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim().Trim('{', '}');
        return Guid.TryParse(trimmed, out var guid) ? guid.ToString("D") : null;
    }

    private static string? NormalizeSiteMapRawUrl(string? rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl))
        {
            return null;
        }

        var trimmed = rawUrl.Trim();
        var separatorIndex = trimmed.IndexOf('?', StringComparison.Ordinal);
        if (separatorIndex < 0 || separatorIndex == trimmed.Length - 1)
        {
            return trimmed;
        }

        var path = trimmed[..separatorIndex];
        var parameters = ParseSiteMapRawQueryString(trimmed[(separatorIndex + 1)..]);
        if (parameters.Count == 0)
        {
            return path;
        }

        var normalizedQuery = string.Join("&", parameters
            .Select(pair => new KeyValuePair<string, string>(pair.Key.Trim(), NormalizeSiteMapRawQueryValue(pair.Key, pair.Value)))
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .ThenBy(pair => pair.Value, StringComparer.OrdinalIgnoreCase)
            .Select(FormatSiteMapRawQueryPair));

        return $"{path}?{normalizedQuery}";
    }

    private static string NormalizeSiteMapRawQueryValue(string key, string value)
    {
        var trimmed = value.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return string.Empty;
        }

        return key.Trim().ToLowerInvariant() switch
        {
            "appid" or "id" or "recordid" or "viewid" or "formid" => TryNormalizeSiteMapRawGuid(trimmed) ?? trimmed,
            "etn" or "entityname" or "name" or "pagetype" => trimmed.ToLowerInvariant(),
            "extraqs" => NormalizeSiteMapEmbeddedRawQuery(trimmed),
            _ => NormalizeSiteMapRawBoolean(trimmed) ?? trimmed
        };
    }

    private static string NormalizeSiteMapEmbeddedRawQuery(string value)
    {
        var parameters = ParseSiteMapRawQueryString(Uri.UnescapeDataString(value));
        if (parameters.Count == 0)
        {
            return value.Trim();
        }

        return string.Join("&", parameters
            .Select(pair => new KeyValuePair<string, string>(pair.Key.Trim(), NormalizeSiteMapRawQueryValue(pair.Key, pair.Value)))
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .ThenBy(pair => pair.Value, StringComparer.OrdinalIgnoreCase)
            .Select(pair => string.IsNullOrEmpty(pair.Value)
                ? Uri.EscapeDataString(pair.Key)
                : $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
    }

    private static string? NormalizeSiteMapRawBoolean(string value) =>
        value.Trim() switch
        {
            "1" => "true",
            "0" => "false",
            var text when text.Equals("true", StringComparison.OrdinalIgnoreCase) => "true",
            var text when text.Equals("false", StringComparison.OrdinalIgnoreCase) => "false",
            _ => null
        };

    private static string FormatSiteMapRawQueryPair(KeyValuePair<string, string> pair) =>
        string.IsNullOrEmpty(pair.Value)
            ? Uri.EscapeDataString(pair.Key)
            : $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}";

    private static Dictionary<string, string> ParseSiteMapRawQueryString(string query)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var segment in query.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separatorIndex = segment.IndexOf('=', StringComparison.Ordinal);
            var key = separatorIndex >= 0 ? segment[..separatorIndex] : segment;
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            var value = separatorIndex >= 0 ? segment[(separatorIndex + 1)..] : string.Empty;
            values[Uri.UnescapeDataString(key)] = Uri.UnescapeDataString(value);
        }

        return values;
    }

    private static string? NormalizeSiteMapViewType(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "1039" => "savedquery",
            "4230" => "userquery",
            "savedquery" => "savedquery",
            "userquery" => "userquery",
            _ => null
        };

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

    private static void ValidateXmlFragment(string? value, string path, string sourcePath, ICollection<CompilerDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        try
        {
            _ = XElement.Parse(value, LoadOptions.PreserveWhitespace);
        }
        catch (Exception exception)
        {
            diagnostics.Add(CreateError(sourcePath, path, $"Expected well-formed XML content, but parsing failed: {exception.Message}"));
        }
    }

    private static void ValidateFormFieldReference(
        string? field,
        IReadOnlySet<string> allowedFieldNames,
        string sourcePath,
        string path,
        ICollection<CompilerDiagnostic> diagnostics,
        string formName,
        string tableLogicalName)
    {
        if (string.IsNullOrWhiteSpace(field))
        {
            diagnostics.Add(CreateError(sourcePath, path, "A non-empty field logical name is required."));
            return;
        }

        if (!IsSupportedFormFieldReference(field, allowedFieldNames))
        {
            diagnostics.Add(CreateError(
                sourcePath,
                path,
                $"Form '{formName}' references unknown field '{field}' on table '{tableLogicalName}'."));
        }
    }

    private static HashSet<string> CreateAllowedFormFieldNames(IEnumerable<string> authoredFieldNames)
    {
        var allowedFieldNames = new HashSet<string>(authoredFieldNames, StringComparer.OrdinalIgnoreCase);
        foreach (var fieldName in KnownPlatformFormFieldNames)
        {
            allowedFieldNames.Add(fieldName);
        }

        return allowedFieldNames;
    }

    private static bool IsSupportedFormFieldReference(string? field, IReadOnlySet<string> allowedFieldNames) =>
        !string.IsNullOrWhiteSpace(field)
        && allowedFieldNames.Contains(field);

    private static string? NormalizeXmlString(string? xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            return null;
        }

        try
        {
            return XElement.Parse(xml, LoadOptions.PreserveWhitespace).ToString(SaveOptions.DisableFormatting);
        }
        catch
        {
            return xml;
        }
    }

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

    [JsonPropertyName("sourceBackedArtifacts")]
    public IReadOnlyList<SourceBackedArtifactSpec>? SourceBackedArtifacts { get; init; }

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

    [JsonPropertyName("roleIds")]
    public IReadOnlyList<string>? RoleIds { get; init; }

    [JsonPropertyName("siteMap")]
    public SiteMapSpec? SiteMap { get; init; }

    [JsonPropertyName("appSettings")]
    public IReadOnlyList<AppSettingSpec>? AppSettings { get; init; }

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

    [JsonPropertyName("primaryImageAttribute")]
    public string? PrimaryImageAttribute { get; init; }

    [JsonPropertyName("isCustomizable")]
    public bool? IsCustomizable { get; init; }

    [JsonPropertyName("columns")]
    public IReadOnlyList<TableColumnSpec>? Columns { get; init; }

    [JsonPropertyName("keys")]
    public IReadOnlyList<TableKeySpec>? Keys { get; init; }

    [JsonPropertyName("forms")]
    public IReadOnlyList<FormSpec>? Forms { get; init; }

    [JsonPropertyName("views")]
    public IReadOnlyList<ViewSpec>? Views { get; init; }

    [JsonPropertyName("visualizations")]
    public IReadOnlyList<VisualizationSpec>? Visualizations { get; init; }

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

    [JsonPropertyName("isCustomizable")]
    public bool? IsCustomizable { get; init; }

    [JsonPropertyName("targetTable")]
    public string? TargetTable { get; init; }

    [JsonPropertyName("relationshipSchemaName")]
    public string? RelationshipSchemaName { get; init; }

    [JsonPropertyName("globalOptionSet")]
    public string? GlobalOptionSet { get; init; }

    [JsonPropertyName("options")]
    public IReadOnlyList<OptionItemSpec>? Options { get; init; }

    [JsonPropertyName("canStoreFullImage")]
    public bool? CanStoreFullImage { get; init; }

    [JsonPropertyName("isPrimaryImage")]
    public bool? IsPrimaryImage { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

internal sealed record AppSettingSpec
{
    [JsonPropertyName("definitionUniqueName")]
    public string? DefinitionUniqueName { get; init; }

    [JsonPropertyName("value")]
    public string? Value { get; init; }

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

    [JsonPropertyName("controls")]
    public IReadOnlyList<FormControlSpec>? Controls { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

internal sealed record FormControlSpec
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

internal sealed record VisualizationSpec
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("chartTypes")]
    public IReadOnlyList<string>? ChartTypes { get; init; }

    [JsonPropertyName("groupByColumns")]
    public IReadOnlyList<string>? GroupByColumns { get; init; }

    [JsonPropertyName("measureAliases")]
    public IReadOnlyList<string>? MeasureAliases { get; init; }

    [JsonPropertyName("titleNames")]
    public IReadOnlyList<string>? TitleNames { get; init; }

    [JsonPropertyName("dataDescriptionXml")]
    public string? DataDescriptionXml { get; init; }

    [JsonPropertyName("presentationDescriptionXml")]
    public string? PresentationDescriptionXml { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

internal sealed record SourceBackedArtifactSpec
{
    [JsonPropertyName("family")]
    public string? Family { get; init; }

    [JsonPropertyName("logicalName")]
    public string? LogicalName { get; init; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; init; }

    [JsonPropertyName("metadataSourcePath")]
    public string? MetadataSourcePath { get; init; }

    [JsonPropertyName("assetSourcePaths")]
    public IReadOnlyList<string>? AssetSourcePaths { get; init; }

    [JsonPropertyName("packageRelativePath")]
    public string? PackageRelativePath { get; init; }

    [JsonPropertyName("stableProperties")]
    public Dictionary<string, JsonElement>? StableProperties { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

internal sealed record FormControlDescriptionEntry(string Id, string DataFieldName, string Role);
