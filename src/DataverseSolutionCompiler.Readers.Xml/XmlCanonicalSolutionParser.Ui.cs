using System.Globalization;
using DataverseSolutionCompiler.Domain.Model;

namespace DataverseSolutionCompiler.Readers.Xml;

internal sealed partial class XmlCanonicalSolutionParser
{
    private void ParseForms(string entityDirectory, string entityLogicalName)
    {
        var formsDirectory = Path.Combine(entityDirectory, "FormXml");
        if (!Directory.Exists(formsDirectory))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(formsDirectory, "*.xml", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var root = LoadRoot(file);
            var systemForm = root.ElementLocal("systemform");
            var form = systemForm?.ElementLocal("form");
            if (systemForm is null || form is null)
            {
                continue;
            }

            var formId = NormalizeGuid(Text(systemForm.ElementLocal("formid")));
            var formType = Path.GetFileName(Path.GetDirectoryName(file) ?? string.Empty).ToLowerInvariant();
            var displayName = LocalizedDescription(systemForm.ElementLocal("LocalizedNames")) ?? formId;
            var description = LocalizedDescription(systemForm.ElementLocal("Descriptions"));
            var controls = form.Descendants().Where(element => element.Name.LocalName.Equals("control", StringComparison.OrdinalIgnoreCase)).ToArray();
            var formDefinitionJson = SerializeJson(BuildFormDefinition(form, displayName, description, formType));
            var summary = new
            {
                formType,
                formId,
                tabCount = form.Descendants().Count(element => element.Name.LocalName.Equals("tab", StringComparison.OrdinalIgnoreCase)),
                sectionCount = form.Descendants().Count(element => element.Name.LocalName.Equals("section", StringComparison.OrdinalIgnoreCase)),
                controlCount = controls.Length,
                quickFormCount = controls.Count(IsQuickFormControl),
                subgridCount = controls.Count(IsSubgridControl),
                headerControlCount = form.ElementLocal("header")?.Descendants().Count(element => element.Name.LocalName.Equals("control", StringComparison.OrdinalIgnoreCase)) ?? 0,
                footerControlCount = form.ElementLocal("footer")?.Descendants().Count(element => element.Name.LocalName.Equals("control", StringComparison.OrdinalIgnoreCase)) ?? 0,
                controlDescriptions = controls
                    .Select(control => new
                    {
                        id = control.AttributeValue("id") ?? string.Empty,
                        dataFieldName = control.AttributeValue("datafieldname") ?? string.Empty,
                        role = DescribeControlRole(control)
                    })
                    .OrderBy(control => control.id, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(control => control.dataFieldName, StringComparer.OrdinalIgnoreCase)
                    .ToArray()
            };
            var summaryJson = SerializeJson(summary);

            AddArtifact(
                ComponentFamily.Form,
                $"{entityLogicalName}|{formType}|{formId}",
                displayName,
                file,
                CreateProperties(
                    (ArtifactPropertyKeys.EntityLogicalName, entityLogicalName),
                    (ArtifactPropertyKeys.FormType, formType),
                    (ArtifactPropertyKeys.FormTypeCode, Text(systemForm.ElementLocal("FormPresentation"))),
                    (ArtifactPropertyKeys.FormId, formId),
                    (ArtifactPropertyKeys.Description, description),
                    (ArtifactPropertyKeys.FormDefinitionJson, formDefinitionJson),
                    (ArtifactPropertyKeys.MetadataSourcePath, RelativePath(file)),
                    (ArtifactPropertyKeys.TabCount, summary.tabCount.ToString(CultureInfo.InvariantCulture)),
                    (ArtifactPropertyKeys.SectionCount, summary.sectionCount.ToString(CultureInfo.InvariantCulture)),
                    (ArtifactPropertyKeys.ControlCount, summary.controlCount.ToString(CultureInfo.InvariantCulture)),
                    (ArtifactPropertyKeys.QuickFormCount, summary.quickFormCount.ToString(CultureInfo.InvariantCulture)),
                    (ArtifactPropertyKeys.SubgridCount, summary.subgridCount.ToString(CultureInfo.InvariantCulture)),
                    (ArtifactPropertyKeys.HeaderControlCount, summary.headerControlCount.ToString(CultureInfo.InvariantCulture)),
                    (ArtifactPropertyKeys.FooterControlCount, summary.footerControlCount.ToString(CultureInfo.InvariantCulture)),
                    (ArtifactPropertyKeys.ControlDescriptionCount, summary.controlDescriptions.Length.ToString(CultureInfo.InvariantCulture)),
                    (ArtifactPropertyKeys.SummaryJson, summaryJson),
                    (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(summaryJson))));
        }
    }

    private void ParseViews(string entityDirectory, string entityLogicalName)
    {
        var viewsDirectory = Path.Combine(entityDirectory, "SavedQueries");
        if (!Directory.Exists(viewsDirectory))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(viewsDirectory, "*.xml", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var root = LoadRoot(file);
            var savedQuery = root.ElementLocal("savedquery");
            if (savedQuery is null)
            {
                continue;
            }

            var layoutGrid = savedQuery.ElementLocal("layoutxml")?.Elements().FirstOrDefault();
            var fetchRoot = savedQuery.ElementLocal("fetchxml")?.Elements().FirstOrDefault();
            var fetchEntity = fetchRoot?.Descendants().FirstOrDefault(element => element.Name.LocalName.Equals("entity", StringComparison.OrdinalIgnoreCase));
            var layoutColumns = layoutGrid?
                .Descendants()
                .Where(element => element.Name.LocalName.Equals("cell", StringComparison.OrdinalIgnoreCase))
                .Select(element => element.AttributeValue("name") ?? string.Empty)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToArray()
                ?? [];
            var fetchAttributes = fetchEntity?
                .Elements()
                .Where(element => element.Name.LocalName.Equals("attribute", StringComparison.OrdinalIgnoreCase))
                .Select(element => element.AttributeValue("name") ?? string.Empty)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToArray()
                ?? [];
            var filters = fetchEntity?
                .Descendants()
                .Where(element => element.Name.LocalName.Equals("condition", StringComparison.OrdinalIgnoreCase))
                .Select(condition => new
                {
                    attribute = condition.AttributeValue("attribute") ?? string.Empty,
                    @operator = condition.AttributeValue("operator") ?? string.Empty,
                    value = condition.AttributeValue("value") ?? string.Empty
                })
                .OrderBy(condition => condition.attribute, StringComparer.OrdinalIgnoreCase)
                .ThenBy(condition => condition.@operator, StringComparer.OrdinalIgnoreCase)
                .ThenBy(condition => condition.value, StringComparer.OrdinalIgnoreCase)
                .ToArray()
                ?? [];
            var orders = fetchEntity?
                .Elements()
                .Where(element => element.Name.LocalName.Equals("order", StringComparison.OrdinalIgnoreCase))
                .Select(order => new
                {
                    attribute = order.AttributeValue("attribute") ?? string.Empty,
                    descending = NormalizeBoolean(order.AttributeValue("descending"))
                })
                .ToArray()
                ?? [];
            var targetEntity = NormalizeLogicalName(fetchEntity?.AttributeValue("name") ?? entityLogicalName);
            var displayName = LocalizedDescription(savedQuery.ElementLocal("LocalizedNames")) ?? Text(savedQuery.ElementLocal("savedqueryid")) ?? Path.GetFileNameWithoutExtension(file);
            var viewId = NormalizeGuid(Text(savedQuery.ElementLocal("savedqueryid")));
            var summaryJson = SerializeJson(new
            {
                targetEntity,
                layoutColumns,
                fetchAttributes,
                filters,
                orders
            });

            AddArtifact(
                ComponentFamily.View,
                $"{entityLogicalName}|{displayName}",
                displayName,
                file,
                CreateProperties(
                    (ArtifactPropertyKeys.EntityLogicalName, entityLogicalName),
                    (ArtifactPropertyKeys.ViewId, viewId),
                    (ArtifactPropertyKeys.QueryType, Text(savedQuery.ElementLocal("querytype"))),
                    (ArtifactPropertyKeys.IsDefault, NormalizeBoolean(Text(savedQuery.ElementLocal("isdefault")))),
                    (ArtifactPropertyKeys.CanBeDeleted, NormalizeBoolean(Text(savedQuery.ElementLocal("CanBeDeleted")))),
                    (ArtifactPropertyKeys.IsQuickFindQuery, NormalizeBoolean(Text(savedQuery.ElementLocal("isquickfindquery")))),
                    (ArtifactPropertyKeys.IntroducedVersion, Text(savedQuery.ElementLocal("IntroducedVersion"))),
                    (ArtifactPropertyKeys.TargetEntity, targetEntity),
                    (ArtifactPropertyKeys.MetadataSourcePath, RelativePath(file)),
                    (ArtifactPropertyKeys.LayoutColumnsJson, SerializeJson(layoutColumns)),
                    (ArtifactPropertyKeys.FetchAttributesJson, SerializeJson(fetchAttributes)),
                    (ArtifactPropertyKeys.FiltersJson, SerializeJson(filters)),
                    (ArtifactPropertyKeys.OrdersJson, SerializeJson(orders)),
                    (ArtifactPropertyKeys.SummaryJson, summaryJson),
                    (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(summaryJson))));
        }
    }

    private void ParseVisualizations(string entityDirectory, string entityLogicalName)
    {
        var visualizationsDirectory = Path.Combine(entityDirectory, "Visualizations");
        if (!Directory.Exists(visualizationsDirectory))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(visualizationsDirectory, "*.xml", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var root = LoadRoot(file);
            var visualization = root.Name.LocalName.Equals("visualization", StringComparison.OrdinalIgnoreCase)
                ? root
                : root.ElementLocal("visualization");
            if (visualization is null)
            {
                continue;
            }

            var dataDefinition = visualization.ElementLocal("datadescription")?.Descendants().FirstOrDefault(element => element.Name.LocalName.Equals("datadefinition", StringComparison.OrdinalIgnoreCase));
            var fetchEntity = dataDefinition?.Descendants().FirstOrDefault(element => element.Name.LocalName.Equals("entity", StringComparison.OrdinalIgnoreCase));
            var targetEntity = NormalizeLogicalName(fetchEntity?.AttributeValue("name") ?? entityLogicalName);
            var displayName = LocalizedDescription(visualization.ElementLocal("LocalizedNames")) ?? Text(visualization.ElementLocal("savedqueryvisualizationid")) ?? Path.GetFileNameWithoutExtension(file);
            var visualizationId = NormalizeGuid(Text(visualization.ElementLocal("savedqueryvisualizationid")));
            var chartTypes = visualization
                .ElementLocal("presentationdescription")
                ?.Descendants()
                .Where(element => element.Name.LocalName.Equals("Series", StringComparison.OrdinalIgnoreCase))
                .Select(element => element.AttributeValue("ChartType") ?? string.Empty)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray()
                ?? [];
            var groupByColumns = fetchEntity?
                .Elements()
                .Where(element => element.Name.LocalName.Equals("attribute", StringComparison.OrdinalIgnoreCase)
                    && element.AttributeValue("groupby")?.Equals("true", StringComparison.OrdinalIgnoreCase) == true)
                .Select(element => element.AttributeValue("name") ?? string.Empty)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray()
                ?? [];
            var measureAliases = dataDefinition?
                .Descendants()
                .Where(element => element.Name.LocalName.Equals("measure", StringComparison.OrdinalIgnoreCase))
                .Select(element => element.AttributeValue("alias") ?? string.Empty)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray()
                ?? [];
            var titleNames = visualization
                .ElementLocal("presentationdescription")
                ?.Descendants()
                .Where(element => element.Name.LocalName.Equals("Title", StringComparison.OrdinalIgnoreCase))
                .Select(element => element.AttributeValue("Name") ?? string.Empty)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray()
                ?? [];
            var summaryJson = SerializeJson(new
            {
                targetEntity,
                chartTypes,
                groupByColumns,
                measureAliases,
                titleNames
            });

            AddArtifact(
                ComponentFamily.Visualization,
                $"{targetEntity}|{displayName}",
                displayName,
                file,
                CreateProperties(
                    (ArtifactPropertyKeys.EntityLogicalName, entityLogicalName),
                    (ArtifactPropertyKeys.TargetEntity, targetEntity),
                    (ArtifactPropertyKeys.VisualizationId, visualizationId),
                    (ArtifactPropertyKeys.Description, LocalizedDescription(visualization.ElementLocal("Descriptions"))),
                    (ArtifactPropertyKeys.IntroducedVersion, Text(visualization.ElementLocal("IntroducedVersion"))),
                    (ArtifactPropertyKeys.MetadataSourcePath, RelativePath(file)),
                    (ArtifactPropertyKeys.DataDescriptionXml, NormalizeXml(visualization.ElementLocal("datadescription"))),
                    (ArtifactPropertyKeys.PresentationDescriptionXml, NormalizeXml(visualization.ElementLocal("presentationdescription"))),
                    (ArtifactPropertyKeys.ChartTypesJson, SerializeJson(chartTypes)),
                    (ArtifactPropertyKeys.GroupByColumnsJson, SerializeJson(groupByColumns)),
                    (ArtifactPropertyKeys.MeasureAliasesJson, SerializeJson(measureAliases)),
                    (ArtifactPropertyKeys.TitleNamesJson, SerializeJson(titleNames)),
                    (ArtifactPropertyKeys.SummaryJson, summaryJson),
                    (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(summaryJson))));
        }
    }

    private void ParseRibbons(string entityDirectory, string entityLogicalName)
    {
        var ribbonPath = Path.Combine(entityDirectory, "RibbonDiff.xml");
        if (!File.Exists(ribbonPath))
        {
            return;
        }

        var root = LoadRoot(ribbonPath);
        var customActionIds = CollectRibbonIds(root, "CustomAction", "Id", "Location", "Command");
        var commandDefinitionIds = CollectRibbonIds(root, "CommandDefinition", "Id");
        var buttonIds = CollectRibbonIds(root, "Button", "Id");
        var displayRuleIds = CollectRibbonIds(root, "DisplayRule", "Id");
        var enableRuleIds = CollectRibbonIds(root, "EnableRule", "Id");
        var hideCustomActionIds = CollectRibbonIds(root, "HideCustomAction", "Id", "Location", "Command");
        var locLabelIds = CollectRibbonIds(root, "LocLabel", "Id");
        var byteLength = new FileInfo(ribbonPath).Length;
        var summaryJson = SerializeJson(new
        {
            entityLogicalName,
            counts = new
            {
                customActionCount = customActionIds.Length,
                commandDefinitionCount = commandDefinitionIds.Length,
                displayRuleCount = displayRuleIds.Length,
                enableRuleCount = enableRuleIds.Length,
                hideCustomActionCount = hideCustomActionIds.Length,
                buttonCount = buttonIds.Length,
                locLabelCount = locLabelIds.Length,
                byteLength
            },
            customActionIds,
            commandDefinitionIds,
            buttonIds,
            displayRuleIds,
            enableRuleIds,
            hideCustomActionIds,
            locLabelIds
        });

        AddArtifact(
            ComponentFamily.Ribbon,
            entityLogicalName,
            $"{entityLogicalName} Ribbon Diff",
            ribbonPath,
            CreateProperties(
                (ArtifactPropertyKeys.EntityLogicalName, entityLogicalName),
                (ArtifactPropertyKeys.MetadataSourcePath, RelativePath(ribbonPath)),
                (ArtifactPropertyKeys.ByteLength, byteLength.ToString(CultureInfo.InvariantCulture)),
                (ArtifactPropertyKeys.ContentHash, ComputeFileHash(ribbonPath)),
                (ArtifactPropertyKeys.SummaryJson, summaryJson),
                (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(summaryJson))));
    }

    private static string[] CollectRibbonIds(System.Xml.Linq.XElement root, string elementName, params string[] attributeNames) =>
        root.Descendants()
            .Where(element => element.Name.LocalName.Equals(elementName, StringComparison.OrdinalIgnoreCase))
            .Select(element => attributeNames
                .Select(attributeName => element.AttributeValue(attributeName))
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray()!;

    private static bool IsQuickFormControl(System.Xml.Linq.XElement control) =>
        !string.IsNullOrWhiteSpace(Text(control.ElementLocal("parameters")?.ElementLocal("QuickForms")));

    private static bool IsSubgridControl(System.Xml.Linq.XElement control) =>
        control.AttributeValue("indicationOfSubgrid")?.Equals("true", StringComparison.OrdinalIgnoreCase) == true
        || !string.IsNullOrWhiteSpace(Text(control.ElementLocal("parameters")?.ElementLocal("RelationshipName")));

    private static string DescribeControlRole(System.Xml.Linq.XElement control)
    {
        if (IsQuickFormControl(control))
        {
            return "quickView";
        }

        if (IsSubgridControl(control))
        {
            return "subgrid";
        }

        return !string.IsNullOrWhiteSpace(ExtractFormFieldLogicalName(control))
            ? "field"
            : "unsupported";
    }

    private static object BuildFormDefinition(System.Xml.Linq.XElement form, string? displayName, string? description, string formType)
    {
        var tabs = form
            .Descendants()
            .Where(element => element.Name.LocalName.Equals("tab", StringComparison.OrdinalIgnoreCase))
            .Select((tab, tabIndex) => new
            {
                name = tab.AttributeValue("name") ?? $"tab_{tabIndex + 1}",
                label = LocalizedDescription(tab.ElementLocal("labels")) ?? tab.AttributeValue("name") ?? $"Tab {tabIndex + 1}",
                sections = tab
                    .Descendants()
                    .Where(element => element.Name.LocalName.Equals("section", StringComparison.OrdinalIgnoreCase))
                    .Select((section, sectionIndex) => new
                    {
                        name = section.AttributeValue("name") ?? $"section_{sectionIndex + 1}",
                        label = LocalizedDescription(section.ElementLocal("labels")) ?? section.AttributeValue("name") ?? $"Section {sectionIndex + 1}",
                        controls = section
                            .Descendants()
                            .Where(element => element.Name.LocalName.Equals("cell", StringComparison.OrdinalIgnoreCase))
                            .Select(cell => BuildFormControlDefinition(cell))
                            .Where(control => control is not null)
                            .ToArray()
                    })
                    .ToArray()
            })
            .ToArray();

        var headerFields = form.ElementLocal("header")
            ?.Descendants()
            .Where(element => element.Name.LocalName.Equals("control", StringComparison.OrdinalIgnoreCase))
            .Select(ExtractFormFieldLogicalName)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray()
            ?? [];

        return new
        {
            name = displayName,
            description,
            type = formType,
            tabs,
            headerFields
        };
    }

    private static string? ExtractFormFieldLogicalName(System.Xml.Linq.XElement control) =>
        NormalizeLogicalName(control.AttributeValue("datafieldname"));

    private static object? BuildFormControlDefinition(System.Xml.Linq.XElement cell)
    {
        var control = cell.Elements().FirstOrDefault(element => element.Name.LocalName.Equals("control", StringComparison.OrdinalIgnoreCase));
        if (control is null)
        {
            return null;
        }

        var role = DescribeControlRole(control);
        var label = LocalizedDescription(cell.ElementLocal("labels"));
        var parameters = ReadControlParameters(control);
        var dataFieldName = ExtractFormFieldLogicalName(control);

        return role switch
        {
            "field" => new
            {
                kind = "field",
                field = dataFieldName,
                label
            },
            "quickView" => BuildQuickViewControlDefinition(dataFieldName, label, parameters),
            "subgrid" => new
            {
                kind = "subgrid",
                label,
                relationshipName = Text(parameters.ElementLocal("RelationshipName")),
                targetTable = NormalizeLogicalName(Text(parameters.ElementLocal("TargetEntityType"))),
                defaultViewId = NormalizeGuid(Text(parameters.ElementLocal("ViewId"))),
                isUserView = ParseOptionalBool(Text(parameters.ElementLocal("IsUserView"))),
                autoExpand = Text(parameters.ElementLocal("AutoExpand")),
                enableQuickFind = ParseOptionalBool(Text(parameters.ElementLocal("EnableQuickFind"))),
                enableViewPicker = ParseOptionalBool(Text(parameters.ElementLocal("EnableViewPicker"))),
                enableJumpBar = ParseOptionalBool(Text(parameters.ElementLocal("EnableJumpBar"))),
                enableChartPicker = ParseOptionalBool(Text(parameters.ElementLocal("EnableChartPicker"))),
                recordsPerPage = ParseOptionalInt(Text(parameters.ElementLocal("RecordsPerPage")))
            },
            _ => new
            {
                kind = "unsupported",
                label
            }
        };
    }

    private static object BuildQuickViewControlDefinition(string? dataFieldName, string? label, System.Xml.Linq.XElement parameters)
    {
        var (entityName, quickFormId) = ParseQuickFormReference(Text(parameters.ElementLocal("QuickForms")));
        return new
        {
            kind = "quickView",
            field = dataFieldName,
            label,
            quickFormEntity = entityName,
            quickFormId = quickFormId,
            controlMode = Text(parameters.ElementLocal("ControlMode"))
        };
    }

    private static System.Xml.Linq.XElement ReadControlParameters(System.Xml.Linq.XElement control) =>
        control.ElementLocal("parameters") ?? new System.Xml.Linq.XElement("parameters");

    private static (string? EntityName, string? QuickFormId) ParseQuickFormReference(string? quickFormsXml)
    {
        if (string.IsNullOrWhiteSpace(quickFormsXml))
        {
            return (null, null);
        }

        try
        {
            var root = System.Xml.Linq.XElement.Parse(quickFormsXml);
            var quickForm = root.Descendants().FirstOrDefault(element => element.Name.LocalName.Equals("QuickFormId", StringComparison.OrdinalIgnoreCase));
            return (
                NormalizeLogicalName(quickForm?.AttributeValue("entityname")),
                NormalizeGuid(quickForm?.Value));
        }
        catch
        {
            return (null, null);
        }
    }

    private static int? ParseOptionalInt(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;

    private static bool? ParseOptionalBool(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : NormalizeBoolean(value).Equals("true", StringComparison.OrdinalIgnoreCase);
}
