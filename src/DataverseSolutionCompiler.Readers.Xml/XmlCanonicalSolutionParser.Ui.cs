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
            var controls = form.Descendants().Where(element => element.Name.LocalName.Equals("control", StringComparison.OrdinalIgnoreCase)).ToArray();
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
                    (ArtifactPropertyKeys.Description, LocalizedDescription(systemForm.ElementLocal("Descriptions"))),
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

    private static bool IsQuickFormControl(System.Xml.Linq.XElement control) =>
        !string.IsNullOrWhiteSpace(Text(control.ElementLocal("parameters")?.ElementLocal("QuickForms")));

    private static bool IsSubgridControl(System.Xml.Linq.XElement control) =>
        control.AttributeValue("indicationOfSubgrid")?.Equals("true", StringComparison.OrdinalIgnoreCase) == true
        || !string.IsNullOrWhiteSpace(Text(control.ElementLocal("parameters")?.ElementLocal("RelationshipName")));

    private static string DescribeControlRole(System.Xml.Linq.XElement control)
    {
        if (IsQuickFormControl(control))
        {
            return "quick-form";
        }

        if (IsSubgridControl(control))
        {
            return "subgrid";
        }

        return "field";
    }
}
