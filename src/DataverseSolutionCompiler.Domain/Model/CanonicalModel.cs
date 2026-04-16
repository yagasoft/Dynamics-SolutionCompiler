using DataverseSolutionCompiler.Domain.Capabilities;
using DataverseSolutionCompiler.Domain.Diagnostics;

namespace DataverseSolutionCompiler.Domain.Model;

public enum LayeringIntent
{
    UnmanagedDevelopment,
    ManagedRelease,
    Hybrid
}

public enum ComponentFamily
{
    SolutionShell,
    Publisher,
    Table,
    Column,
    Relationship,
    OptionSet,
    Key,
    EntityMap,
    ImageConfiguration,
    Form,
    View,
    Visualization,
    Ribbon,
    CustomControl,
    AppModule,
    SiteMap,
    WebResource,
    EnvironmentVariable,
    EnvironmentVariableDefinition,
    EnvironmentVariableValue,
    AppSetting,
    CanvasApp,
    ImportMap,
    DataSourceMapping,
    EntityAnalyticsConfiguration,
    AiProjectType,
    AiProject,
    AiConfiguration,
    PluginAssembly,
    PluginType,
    PluginStep,
    PluginStepImage,
    ServiceEndpoint,
    Connector,
    Workflow,
    DuplicateRule,
    DuplicateRuleCondition,
    RoutingRule,
    RoutingRuleItem,
    Sla,
    SlaItem,
    SimilarityRule,
    MobileOfflineProfile,
    MobileOfflineProfileItem,
    Role,
    RolePrivilege,
    FieldSecurityProfile,
    FieldPermission,
    ConnectionRole,
    Report,
    Template,
    DisplayString,
    Attachment,
    LegacyAsset
}

public enum EvidenceKind
{
    Source,
    Readback,
    Derived,
    BestEffort
}

public sealed record SolutionIdentity(
    string UniqueName,
    string DisplayName,
    string Version,
    LayeringIntent LayeringIntent);

public sealed record PublisherDefinition(
    string UniqueName,
    string Prefix,
    string CustomizationPrefix,
    string DisplayName);

public sealed record FamilyArtifact(
    ComponentFamily Family,
    string LogicalName,
    string? DisplayName = null,
    string? SourcePath = null,
    EvidenceKind Evidence = EvidenceKind.Source,
    IReadOnlyDictionary<string, string>? Properties = null);

public sealed record DependencyEdge(
    string FromArtifact,
    string ToArtifact,
    string Reason);

public sealed record EnvironmentBinding(
    string Name,
    string BindingType,
    bool IsEnvironmentLocal,
    string? DefaultValue = null);

public sealed record CanonicalSolution(
    SolutionIdentity Identity,
    PublisherDefinition Publisher,
    IReadOnlyList<FamilyArtifact> Artifacts,
    IReadOnlyList<DependencyEdge> Dependencies,
    IReadOnlyList<EnvironmentBinding> EnvironmentBindings,
    IReadOnlyList<CompilerDiagnostic> Diagnostics)
{
    public static CanonicalSolution CreatePlaceholder(string inputPath, IReadOnlyCollection<CapabilityKind> capabilities)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);

        var solutionName = BuildSolutionName(inputPath);
        var artifacts = capabilities.SelectMany(MapArtifacts).ToArray();

        return new CanonicalSolution(
            new SolutionIdentity(solutionName, solutionName, "0.1.0", LayeringIntent.Hybrid),
            new PublisherDefinition("dsc", "dsc", "dsc", "Dataverse Solution Compiler"),
            artifacts,
            [],
            [],
            [
                new CompilerDiagnostic(
                    "bootstrap-placeholder",
                    DiagnosticSeverity.Info,
                    "The canonical solution was synthesized from bootstrap metadata and requested capability slices.",
                    inputPath)
            ]);
    }

    private static string BuildSolutionName(string inputPath)
    {
        var rawName = Directory.Exists(inputPath)
            ? new DirectoryInfo(inputPath).Name
            : Path.GetFileNameWithoutExtension(inputPath);

        var normalized = new string(rawName.Where(ch => char.IsLetterOrDigit(ch) || ch == '_').ToArray());
        return string.IsNullOrWhiteSpace(normalized) ? "dataversesolution" : normalized.ToLowerInvariant();
    }

    private static IEnumerable<FamilyArtifact> MapArtifacts(CapabilityKind capability) =>
        capability switch
        {
            CapabilityKind.SchemaCore =>
            [
                new FamilyArtifact(ComponentFamily.Table, "table"),
                new FamilyArtifact(ComponentFamily.Column, "column"),
                new FamilyArtifact(ComponentFamily.Relationship, "relationship"),
                new FamilyArtifact(ComponentFamily.View, "view")
            ],
            CapabilityKind.SchemaDetail =>
            [
                new FamilyArtifact(ComponentFamily.OptionSet, "option-set"),
                new FamilyArtifact(ComponentFamily.Key, "alternate-key"),
                new FamilyArtifact(ComponentFamily.ImageConfiguration, "image-configuration")
            ],
            CapabilityKind.ModelDrivenUi =>
            [
                new FamilyArtifact(ComponentFamily.Form, "main-form"),
                new FamilyArtifact(ComponentFamily.Visualization, "chart"),
                new FamilyArtifact(ComponentFamily.Ribbon, "ribbon"),
                new FamilyArtifact(ComponentFamily.CustomControl, "standalone-custom-control")
            ],
            CapabilityKind.AppShell =>
            [
                new FamilyArtifact(ComponentFamily.AppModule, "model-driven-app"),
                new FamilyArtifact(ComponentFamily.SiteMap, "site-map"),
                new FamilyArtifact(ComponentFamily.WebResource, "web-resource"),
                new FamilyArtifact(ComponentFamily.EnvironmentVariableDefinition, "environment-variable-definition"),
                new FamilyArtifact(ComponentFamily.EnvironmentVariableValue, "environment-variable-value"),
                new FamilyArtifact(ComponentFamily.AppSetting, "app-setting")
            ],
            CapabilityKind.CodeAndExtensibility =>
            [
                new FamilyArtifact(ComponentFamily.PluginAssembly, "plugin-assembly"),
                new FamilyArtifact(ComponentFamily.PluginType, "plugin-type"),
                new FamilyArtifact(ComponentFamily.PluginStep, "plugin-step"),
                new FamilyArtifact(ComponentFamily.PluginStepImage, "plugin-step-image"),
                new FamilyArtifact(ComponentFamily.ServiceEndpoint, "service-endpoint"),
                new FamilyArtifact(ComponentFamily.Connector, "connector")
            ],
            CapabilityKind.ProcessAndServicePolicy =>
            [
                new FamilyArtifact(ComponentFamily.Workflow, "workflow"),
                new FamilyArtifact(ComponentFamily.DuplicateRule, "duplicate-rule"),
                new FamilyArtifact(ComponentFamily.DuplicateRuleCondition, "duplicate-rule-condition"),
                new FamilyArtifact(ComponentFamily.RoutingRule, "routing-rule"),
                new FamilyArtifact(ComponentFamily.RoutingRuleItem, "routing-rule-item"),
                new FamilyArtifact(ComponentFamily.Sla, "sla"),
                new FamilyArtifact(ComponentFamily.SlaItem, "sla-item"),
                new FamilyArtifact(ComponentFamily.SimilarityRule, "similarity-rule"),
                new FamilyArtifact(ComponentFamily.MobileOfflineProfile, "mobile-offline-profile"),
                new FamilyArtifact(ComponentFamily.MobileOfflineProfileItem, "mobile-offline-profile-item")
            ],
            CapabilityKind.SecurityAndAccess =>
            [
                new FamilyArtifact(ComponentFamily.Role, "role"),
                new FamilyArtifact(ComponentFamily.RolePrivilege, "role-privilege"),
                new FamilyArtifact(ComponentFamily.FieldSecurityProfile, "field-security-profile"),
                new FamilyArtifact(ComponentFamily.FieldPermission, "field-permission"),
                new FamilyArtifact(ComponentFamily.ConnectionRole, "connection-role")
            ],
            CapabilityKind.EnvironmentAndConfiguration =>
            [
                new FamilyArtifact(ComponentFamily.CanvasApp, "canvas-app"),
                new FamilyArtifact(ComponentFamily.ImportMap, "import-map"),
                new FamilyArtifact(ComponentFamily.DataSourceMapping, "data-source-mapping"),
                new FamilyArtifact(ComponentFamily.EntityAnalyticsConfiguration, "entity-analytics-configuration"),
                new FamilyArtifact(ComponentFamily.AiProjectType, "ai-project-type"),
                new FamilyArtifact(ComponentFamily.AiProject, "ai-project"),
                new FamilyArtifact(ComponentFamily.AiConfiguration, "ai-configuration")
            ],
            CapabilityKind.ReportingAndLegacy =>
            [
                new FamilyArtifact(ComponentFamily.Report, "report"),
                new FamilyArtifact(ComponentFamily.Template, "template"),
                new FamilyArtifact(ComponentFamily.DisplayString, "display-string"),
                new FamilyArtifact(ComponentFamily.Attachment, "attachment"),
                new FamilyArtifact(ComponentFamily.LegacyAsset, "legacy-asset")
            ],
            _ => []
        };
}
