using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;
using DataverseSolutionCompiler.Domain.Model;

namespace DataverseSolutionCompiler.Readers.Xml;

internal sealed partial class XmlCanonicalSolutionParser
{
    private void ParseProcessPolicyFamilies()
    {
        ParseWorkflows();
        ParseDuplicateRules();
        ParseRoutingRules();
        ParseMobileOfflineProfiles();
        ParseSimilarityRules();
        ParseSlas();
    }

    private void ParseDuplicateRules()
    {
        var rootDirectory = Path.Combine(_root, "duplicaterules");
        if (!Directory.Exists(rootDirectory))
        {
            return;
        }

        foreach (var directory in Directory.GetDirectories(rootDirectory).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var metadataPath = Path.Combine(directory, "duplicaterule.xml");
            if (!File.Exists(metadataPath))
            {
                continue;
            }

            var root = LoadRoot(metadataPath);
            var displayName = Text(root.ElementLocal("name")) ?? Path.GetFileName(directory);
            var logicalName = NormalizeLogicalName(root.AttributeValue("uniquename"))
                ?? NormalizeLogicalName(displayName)
                ?? NormalizeLogicalName(Path.GetFileName(directory));
            if (string.IsNullOrWhiteSpace(logicalName))
            {
                continue;
            }

            var baseEntityName = NormalizeLogicalName(Text(root.ElementLocal("baseentityname")));
            var matchingEntityName = NormalizeLogicalName(Text(root.ElementLocal("matchingentityname")));
            var isCaseSensitive = NormalizeBoolean(Text(root.ElementLocal("iscasesensitive")));
            var excludeInactiveRecords = NormalizeBoolean(Text(root.ElementLocal("excludeinactiverecords")));
            var conditionElements = root.ElementLocal("duplicateruleconditions")
                ?.Elements()
                .Where(element => element.Name.LocalName.Equals("duplicaterulecondition", StringComparison.OrdinalIgnoreCase))
                .ToArray()
                ?? [];
            var summaryJson = SerializeJson(new
            {
                logicalName,
                baseEntityName,
                matchingEntityName,
                isCaseSensitive,
                excludeInactiveRecords,
                conditionCount = conditionElements.Length
            });

            AddArtifact(
                ComponentFamily.DuplicateRule,
                logicalName!,
                displayName,
                metadataPath,
                CreateProperties(
                    (ArtifactPropertyKeys.MetadataSourcePath, RelativePath(metadataPath)),
                    (ArtifactPropertyKeys.Description, Text(root.ElementLocal("description"))),
                    (ArtifactPropertyKeys.BaseEntityName, baseEntityName),
                    (ArtifactPropertyKeys.MatchingEntityName, matchingEntityName),
                    (ArtifactPropertyKeys.IsCaseSensitive, isCaseSensitive),
                    (ArtifactPropertyKeys.ExcludeInactiveRecords, excludeInactiveRecords),
                    (ArtifactPropertyKeys.IsCustomizable, NormalizeBoolean(Text(root.ElementLocal("iscustomizable")))),
                    (ArtifactPropertyKeys.ItemCount, conditionElements.Length.ToString(CultureInfo.InvariantCulture)),
                    (ArtifactPropertyKeys.SummaryJson, summaryJson),
                    (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(summaryJson))));

            foreach (var conditionElement in conditionElements.OrderBy(element => BuildDuplicateRuleConditionLogicalName(logicalName, element), StringComparer.OrdinalIgnoreCase))
            {
                var conditionLogicalName = BuildDuplicateRuleConditionLogicalName(logicalName, conditionElement);
                var baseAttributeName = NormalizeLogicalName(conditionElement.AttributeValue("baseattributename") ?? Text(conditionElement.ElementLocal("baseattributename")));
                var matchingAttributeName = NormalizeLogicalName(conditionElement.AttributeValue("matchingattributename") ?? Text(conditionElement.ElementLocal("matchingattributename")));
                var operatorCode = conditionElement.AttributeValue("operatorcode") ?? Text(conditionElement.ElementLocal("operatorcode"));
                var ignoreBlankValues = NormalizeBoolean(Text(conditionElement.ElementLocal("ignoreblankvalues")));
                var conditionSummaryJson = SerializeJson(new
                {
                    logicalName = conditionLogicalName,
                    parentDuplicateRule = logicalName,
                    baseAttributeName,
                    matchingAttributeName,
                    operatorCode,
                    ignoreBlankValues
                });

                AddArtifact(
                    ComponentFamily.DuplicateRuleCondition,
                    conditionLogicalName,
                    $"{baseAttributeName ?? "source"} -> {matchingAttributeName ?? "target"}",
                    metadataPath,
                    CreateProperties(
                        (ArtifactPropertyKeys.ParentDuplicateRuleLogicalName, logicalName),
                        (ArtifactPropertyKeys.MetadataSourcePath, RelativePath(metadataPath)),
                        (ArtifactPropertyKeys.BaseAttributeName, baseAttributeName),
                        (ArtifactPropertyKeys.MatchingAttributeName, matchingAttributeName),
                        (ArtifactPropertyKeys.OperatorCode, operatorCode),
                        (ArtifactPropertyKeys.IgnoreBlankValues, ignoreBlankValues),
                        (ArtifactPropertyKeys.IsCustomizable, NormalizeBoolean(Text(conditionElement.ElementLocal("iscustomizable")))),
                        (ArtifactPropertyKeys.SummaryJson, conditionSummaryJson),
                        (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(conditionSummaryJson))));
            }
        }
    }

    private void ParseRoutingRules()
    {
        var rootDirectory = Path.Combine(_root, "RoutingRules");
        if (!Directory.Exists(rootDirectory))
        {
            return;
        }

        foreach (var metadataPath in Directory.GetFiles(rootDirectory, "*.xml", SearchOption.TopDirectoryOnly).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var root = LoadRoot(metadataPath);
            var displayName = Text(root.ElementLocal("Name")) ?? Path.GetFileNameWithoutExtension(metadataPath);
            var logicalName = NormalizeLogicalName(displayName);
            if (string.IsNullOrWhiteSpace(logicalName))
            {
                continue;
            }

            var description = Text(root.ElementLocal("Description"));
            var workflowId = NormalizeGuid(Text(root.ElementLocal("WorkflowId")));
            var itemElements = root.ElementLocal("RoutingRuleItems")
                ?.Elements()
                .Where(element => element.Name.LocalName.Equals("RoutingRuleItem", StringComparison.OrdinalIgnoreCase))
                .ToArray()
                ?? [];
            var summaryJson = SerializeJson(new
            {
                logicalName,
                description,
                itemCount = itemElements.Length
            });

            AddArtifact(
                ComponentFamily.RoutingRule,
                logicalName!,
                displayName,
                metadataPath,
                CreateProperties(
                    (ArtifactPropertyKeys.MetadataSourcePath, RelativePath(metadataPath)),
                    (ArtifactPropertyKeys.Description, description),
                    (ArtifactPropertyKeys.WorkflowId, workflowId),
                    (ArtifactPropertyKeys.ItemCount, itemElements.Length.ToString(CultureInfo.InvariantCulture)),
                    (ArtifactPropertyKeys.SummaryJson, summaryJson),
                    (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(summaryJson))));

            foreach (var itemElement in itemElements.OrderBy(element => BuildRoutingRuleItemLogicalName(logicalName, element), StringComparer.OrdinalIgnoreCase))
            {
                var itemLogicalName = BuildRoutingRuleItemLogicalName(logicalName, itemElement);
                var itemName = Text(itemElement.ElementLocal("Name")) ?? Text(itemElement.ElementLocal("Description")) ?? itemLogicalName;
                var conditionXml = NormalizeConditionXml(Text(itemElement.ElementLocal("ConditionXml")));
                var itemSummaryJson = SerializeJson(new
                {
                    logicalName = itemLogicalName,
                    parentRoutingRule = logicalName,
                    description = Text(itemElement.ElementLocal("Description")),
                    conditionXml
                });

                AddArtifact(
                    ComponentFamily.RoutingRuleItem,
                    itemLogicalName,
                    itemName,
                    metadataPath,
                    CreateProperties(
                        (ArtifactPropertyKeys.ParentRoutingRuleLogicalName, logicalName),
                        (ArtifactPropertyKeys.MetadataSourcePath, RelativePath(metadataPath)),
                        (ArtifactPropertyKeys.Description, Text(itemElement.ElementLocal("Description"))),
                        (ArtifactPropertyKeys.ConditionXml, conditionXml),
                        (ArtifactPropertyKeys.WorkflowId, workflowId),
                        (ArtifactPropertyKeys.SummaryJson, itemSummaryJson),
                        (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(itemSummaryJson))));
            }
        }
    }

    private void ParseMobileOfflineProfiles()
    {
        var rootDirectory = Path.Combine(_root, "MobileOfflineProfiles");
        if (!Directory.Exists(rootDirectory))
        {
            return;
        }

        foreach (var metadataPath in Directory.GetFiles(rootDirectory, "*.xml", SearchOption.TopDirectoryOnly).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var root = LoadRoot(metadataPath);
            var displayName = Text(root.ElementLocal("Name")) ?? Path.GetFileNameWithoutExtension(metadataPath);
            var logicalName = NormalizeLogicalName(displayName);
            if (string.IsNullOrWhiteSpace(logicalName))
            {
                continue;
            }

            var description = Text(root.ElementLocal("Description"));
            var isValidated = NormalizeBoolean(Text(root.ElementLocal("IsValidated")));
            var itemElements = root.ElementLocal("MobileOfflineProfileItems")
                ?.Elements()
                .Where(element => element.Name.LocalName.Equals("MobileOfflineProfileItem", StringComparison.OrdinalIgnoreCase))
                .ToArray()
                ?? [];
            var summaryJson = SerializeJson(new
            {
                logicalName,
                description,
                isValidated,
                itemCount = itemElements.Length
            });

            AddArtifact(
                ComponentFamily.MobileOfflineProfile,
                logicalName!,
                displayName,
                metadataPath,
                CreateProperties(
                    (ArtifactPropertyKeys.MetadataSourcePath, RelativePath(metadataPath)),
                    (ArtifactPropertyKeys.Description, description),
                    (ArtifactPropertyKeys.IsValidated, isValidated),
                    (ArtifactPropertyKeys.ItemCount, itemElements.Length.ToString(CultureInfo.InvariantCulture)),
                    (ArtifactPropertyKeys.SummaryJson, summaryJson),
                    (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(summaryJson))));

            foreach (var itemElement in itemElements.OrderBy(element => BuildMobileOfflineProfileItemLogicalName(logicalName, element), StringComparer.OrdinalIgnoreCase))
            {
                var itemLogicalName = BuildMobileOfflineProfileItemLogicalName(logicalName, itemElement);
                var entityLogicalName = NormalizeLogicalName(Text(itemElement.ElementLocal("EntitySchemaName")) ?? Text(itemElement.ElementLocal("SelectedEntityTypeCode")));
                var recordDistributionCriteria = Text(itemElement.ElementLocal("RecordDistributionCriteria"));
                var itemSummaryJson = SerializeJson(new
                {
                    logicalName = itemLogicalName,
                    parentMobileOfflineProfile = logicalName,
                    entityLogicalName,
                    recordDistributionCriteria,
                    recordsOwnedByMe = NormalizeBoolean(Text(itemElement.ElementLocal("RecordsOwnedByMe"))),
                    recordsOwnedByMyTeam = NormalizeBoolean(Text(itemElement.ElementLocal("RecordsOwnedByMyTeam"))),
                    recordsOwnedByMyBusinessUnit = NormalizeBoolean(Text(itemElement.ElementLocal("RecordsOwnedByMyBusinessUnit")))
                });

                AddArtifact(
                    ComponentFamily.MobileOfflineProfileItem,
                    itemLogicalName,
                    Text(itemElement.ElementLocal("Name")) ?? entityLogicalName ?? itemLogicalName,
                    metadataPath,
                    CreateProperties(
                        (ArtifactPropertyKeys.ParentMobileOfflineProfileLogicalName, logicalName),
                        (ArtifactPropertyKeys.MetadataSourcePath, RelativePath(metadataPath)),
                        (ArtifactPropertyKeys.EntityLogicalName, entityLogicalName),
                        (ArtifactPropertyKeys.RecordDistributionCriteria, recordDistributionCriteria),
                        (ArtifactPropertyKeys.RecordsOwnedByMe, NormalizeBoolean(Text(itemElement.ElementLocal("RecordsOwnedByMe")))),
                        (ArtifactPropertyKeys.RecordsOwnedByMyTeam, NormalizeBoolean(Text(itemElement.ElementLocal("RecordsOwnedByMyTeam")))),
                        (ArtifactPropertyKeys.RecordsOwnedByMyBusinessUnit, NormalizeBoolean(Text(itemElement.ElementLocal("RecordsOwnedByMyBusinessUnit")))),
                        (ArtifactPropertyKeys.ProfileItemEntityFilter, Text(itemElement.ElementLocal("ProfileItemEntityFilter"))),
                        (ArtifactPropertyKeys.SummaryJson, itemSummaryJson),
                        (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(itemSummaryJson))));
            }
        }
    }

    private void ParseSimilarityRules()
    {
        var customizationsRoot = LoadCustomizationsRoot();
        if (customizationsRoot is null)
        {
            return;
        }

        var rules = customizationsRoot.ElementLocal("SimilarityRules")
            ?.Elements()
            .Where(element => element.Name.LocalName.Equals("SimilarityRule", StringComparison.OrdinalIgnoreCase))
            .ToArray()
            ?? [];
        foreach (var rule in rules.OrderBy(
                     element => element.AttributeValue("Name") ?? Text(element.ElementLocal("Name")),
                     StringComparer.OrdinalIgnoreCase))
        {
            var displayName = rule.AttributeValue("Name") ?? Text(rule.ElementLocal("Name"));
            var logicalName = NormalizeLogicalName(displayName);
            if (string.IsNullOrWhiteSpace(logicalName))
            {
                continue;
            }

            var summaryJson = SerializeJson(new
            {
                logicalName,
                baseEntityName = NormalizeLogicalName(Text(rule.ElementLocal("BaseEntityName"))),
                matchingEntityName = NormalizeLogicalName(Text(rule.ElementLocal("MatchingEntityName"))),
                excludeInactiveRecords = NormalizeBoolean(Text(rule.ElementLocal("ExcludeInactiveRecords"))),
                maxKeywords = Text(rule.ElementLocal("MaxKeywords")),
                ngramSize = Text(rule.ElementLocal("NgramSize"))
            });

            AddArtifact(
                ComponentFamily.SimilarityRule,
                logicalName!,
                displayName,
                Path.Combine(_root, "Other", "Customizations.xml"),
                CreateProperties(
                    (ArtifactPropertyKeys.MetadataSourcePath, "Other/Customizations.xml"),
                    (ArtifactPropertyKeys.Description, Text(rule.ElementLocal("Description"))),
                    (ArtifactPropertyKeys.BaseEntityName, NormalizeLogicalName(Text(rule.ElementLocal("BaseEntityName")))),
                    (ArtifactPropertyKeys.MatchingEntityName, NormalizeLogicalName(Text(rule.ElementLocal("MatchingEntityName")))),
                    (ArtifactPropertyKeys.ExcludeInactiveRecords, NormalizeBoolean(Text(rule.ElementLocal("ExcludeInactiveRecords")))),
                    (ArtifactPropertyKeys.MaxKeywords, Text(rule.ElementLocal("MaxKeywords"))),
                    (ArtifactPropertyKeys.NgramSize, Text(rule.ElementLocal("NgramSize"))),
                    (ArtifactPropertyKeys.SummaryJson, summaryJson),
                    (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(summaryJson))),
                EvidenceKind.BestEffort);
        }
    }

    private void ParseSlas()
    {
        var customizationsRoot = LoadCustomizationsRoot();
        if (customizationsRoot is null)
        {
            return;
        }

        var slas = customizationsRoot.ElementLocal("SLAs")
            ?.Elements()
            .Where(element => element.Name.LocalName.Equals("SLA", StringComparison.OrdinalIgnoreCase))
            .ToArray()
            ?? [];

        foreach (var sla in slas.OrderBy(
                     element => element.AttributeValue("Name") ?? Text(element.ElementLocal("Name")),
                     StringComparer.OrdinalIgnoreCase))
        {
            var displayName = sla.AttributeValue("Name") ?? Text(sla.ElementLocal("Name"));
            var logicalName = NormalizeLogicalName(displayName);
            if (string.IsNullOrWhiteSpace(logicalName))
            {
                continue;
            }

            var itemElements = sla.ElementLocal("SLAItems")
                ?.Elements()
                .Where(element => element.Name.LocalName.Equals("SLAItem", StringComparison.OrdinalIgnoreCase))
                .ToArray()
                ?? [];
            var summaryJson = SerializeJson(new
            {
                logicalName,
                applicableFrom = NormalizeLogicalName(Text(sla.ElementLocal("ApplicableFrom"))),
                allowPauseResume = NormalizeBoolean(Text(sla.ElementLocal("AllowPauseResume"))),
                isDefault = NormalizeBoolean(Text(sla.ElementLocal("IsDefault"))),
                itemCount = itemElements.Length
            });

            AddArtifact(
                ComponentFamily.Sla,
                logicalName!,
                displayName,
                Path.Combine(_root, "Other", "Customizations.xml"),
                CreateProperties(
                    (ArtifactPropertyKeys.MetadataSourcePath, "Other/Customizations.xml"),
                    (ArtifactPropertyKeys.ApplicableFrom, NormalizeLogicalName(Text(sla.ElementLocal("ApplicableFrom")))),
                    (ArtifactPropertyKeys.AllowPauseResume, NormalizeBoolean(Text(sla.ElementLocal("AllowPauseResume")))),
                    (ArtifactPropertyKeys.IsDefault, NormalizeBoolean(Text(sla.ElementLocal("IsDefault")))),
                    (ArtifactPropertyKeys.WorkflowId, NormalizeGuid(Text(sla.ElementLocal("WorkflowId")))),
                    (ArtifactPropertyKeys.ItemCount, itemElements.Length.ToString(CultureInfo.InvariantCulture)),
                    (ArtifactPropertyKeys.SummaryJson, summaryJson),
                    (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(summaryJson))),
                EvidenceKind.BestEffort);

            foreach (var item in itemElements.OrderBy(element => BuildSlaItemLogicalName(logicalName, element), StringComparer.OrdinalIgnoreCase))
            {
                var itemLogicalName = BuildSlaItemLogicalName(logicalName, item);
                var applicableWhenXml = item.ElementLocal("ApplicableWhenXml")?.Elements().FirstOrDefault() is { } applicableWhenRoot
                    ? NormalizeXml(applicableWhenRoot)
                    : NormalizeConditionXml(Text(item.ElementLocal("ApplicableWhenXml")));
                var itemSummaryJson = SerializeJson(new
                {
                    logicalName = itemLogicalName,
                    parentSlaLogicalName = logicalName,
                    applicableEntity = NormalizeLogicalName(Text(item.ElementLocal("ApplicableEntity"))),
                    allowPauseResume = NormalizeBoolean(Text(item.ElementLocal("AllowPauseResume"))),
                    actionFlowUniqueName = NormalizeLogicalName(Text(item.ElementLocal("ActionFlowUniqueName"))),
                    applicableWhenXml
                });

                AddArtifact(
                    ComponentFamily.SlaItem,
                    itemLogicalName,
                    item.AttributeValue("Name") ?? Text(item.ElementLocal("Name")) ?? itemLogicalName,
                    Path.Combine(_root, "Other", "Customizations.xml"),
                    CreateProperties(
                        (ArtifactPropertyKeys.ParentSlaLogicalName, logicalName),
                        (ArtifactPropertyKeys.MetadataSourcePath, "Other/Customizations.xml"),
                        (ArtifactPropertyKeys.ApplicableEntity, NormalizeLogicalName(Text(item.ElementLocal("ApplicableEntity")))),
                        (ArtifactPropertyKeys.AllowPauseResume, NormalizeBoolean(Text(item.ElementLocal("AllowPauseResume")))),
                        (ArtifactPropertyKeys.ActionUrl, Text(item.ElementLocal("ActionUrl"))),
                        (ArtifactPropertyKeys.ActionFlowUniqueName, NormalizeLogicalName(Text(item.ElementLocal("ActionFlowUniqueName")))),
                        (ArtifactPropertyKeys.ApplicableWhenXml, applicableWhenXml),
                        (ArtifactPropertyKeys.SummaryJson, itemSummaryJson),
                        (ArtifactPropertyKeys.ComparisonSignature, ComputeSignature(itemSummaryJson))),
                    EvidenceKind.BestEffort);
            }
        }
    }

    private XElement? LoadCustomizationsRoot()
    {
        var customizationsPath = Path.Combine(_root, "Other", "Customizations.xml");
        return File.Exists(customizationsPath) ? LoadRoot(customizationsPath) : null;
    }

    private static string BuildDuplicateRuleConditionLogicalName(string ruleLogicalName, XElement conditionElement)
    {
        var baseAttributeName = NormalizeLogicalName(conditionElement.AttributeValue("baseattributename") ?? Text(conditionElement.ElementLocal("baseattributename"))) ?? "base";
        var matchingAttributeName = NormalizeLogicalName(conditionElement.AttributeValue("matchingattributename") ?? Text(conditionElement.ElementLocal("matchingattributename"))) ?? "matching";
        var operatorCode = conditionElement.AttributeValue("operatorcode") ?? Text(conditionElement.ElementLocal("operatorcode")) ?? "0";
        return $"{ruleLogicalName}|{baseAttributeName}|{matchingAttributeName}|{operatorCode}";
    }

    private static string BuildRoutingRuleItemLogicalName(string parentLogicalName, XElement itemElement)
    {
        var stableSegment = NormalizeLogicalName(Text(itemElement.ElementLocal("Description")) ?? Text(itemElement.ElementLocal("Name"))) ?? "item";
        return $"{parentLogicalName}|{stableSegment}";
    }

    private static string BuildMobileOfflineProfileItemLogicalName(string parentLogicalName, XElement itemElement)
    {
        var entityLogicalName = NormalizeLogicalName(Text(itemElement.ElementLocal("EntitySchemaName")) ?? Text(itemElement.ElementLocal("SelectedEntityTypeCode"))) ?? "entity";
        return $"{parentLogicalName}|{entityLogicalName}";
    }

    private static string BuildSlaItemLogicalName(string parentLogicalName, XElement itemElement)
    {
        var name = NormalizeLogicalName(itemElement.AttributeValue("Name") ?? Text(itemElement.ElementLocal("Name"))) ?? "sla-item";
        var applicableEntity = NormalizeLogicalName(Text(itemElement.ElementLocal("ApplicableEntity"))) ?? "entity";
        return $"{parentLogicalName}|{name}|{applicableEntity}";
    }

    private static string? NormalizeConditionXml(string? xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            return null;
        }

        try
        {
            return XElement.Parse(xml).ToString(SaveOptions.DisableFormatting);
        }
        catch
        {
            return xml.Trim();
        }
    }
}
