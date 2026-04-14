using DataverseSolutionCompiler.Domain.Abstractions;
using DataverseSolutionCompiler.Domain.Capabilities;

namespace DataverseSolutionCompiler.Compiler;

public sealed class CapabilityRegistry : ICapabilityRegistry
{
    private readonly IReadOnlyDictionary<CapabilityKind, CapabilityDescriptor> _descriptors;
    private readonly IReadOnlyDictionary<string, CapabilityDescriptor> _descriptorsByName;

    public CapabilityRegistry()
    {
        _descriptors = new Dictionary<CapabilityKind, CapabilityDescriptor>
        {
            [CapabilityKind.SchemaCore] = new(
                CapabilityKind.SchemaCore,
                "schema-core",
                "Core table, column, relationship, and view support.",
                CapabilityReadiness.Proven,
                new[] { "tables", "columns", "relationships", "views" },
                []),
            [CapabilityKind.SchemaDetail] = new(
                CapabilityKind.SchemaDetail,
                "schema-detail",
                "Choice, key, image-configuration, and managed-flag schema support.",
                CapabilityReadiness.PartiallyProven,
                new[] { "option sets", "global option sets", "alternate keys", "image configurations", "table and column isCustomizable flags" },
                new[] { "Broader managed-property component-type 13 proof remains a separate follow-up once a neutral durable surface is captured." }),
            [CapabilityKind.ModelDrivenUi] = new(
                CapabilityKind.ModelDrivenUi,
                "model-driven-ui",
                "Forms, views, ribbons, and control surfaces.",
                CapabilityReadiness.PartiallyProven,
                new[] { "systemform", "savedquery", "FormXml", "custom controls" },
                new[] { "Standalone control families still need deeper neutral proof." }),
            [CapabilityKind.AppShell] = new(
                CapabilityKind.AppShell,
                "app-shell",
                "App modules, site maps, and app configuration.",
                CapabilityReadiness.PartiallyProven,
                new[] { "app modules", "site maps", "environment variables" },
                new[] { "Ribbon and app-setting readback remain family-aware best effort." }),
            [CapabilityKind.CodeAndExtensibility] = new(
                CapabilityKind.CodeAndExtensibility,
                "code-extensibility",
                "Plugin assemblies, plugin types, steps, images, service endpoints, connectors, and source-first registration.",
                CapabilityReadiness.PartiallyProven,
                new[] { "plugin assemblies", "plugin types", "steps", "step images", "service endpoints", "connectors" },
                new[] { "Code-source registration ingestion remains explicit follow-up work." }),
            [CapabilityKind.ProcessAndServicePolicy] = new(
                CapabilityKind.ProcessAndServicePolicy,
                "process-service-policy",
                "Workflow and service-policy artifact support.",
                CapabilityReadiness.PartiallyProven,
                new[] { "duplicate rules", "duplicate-rule conditions", "routing rules", "routing-rule items", "mobile offline profiles", "mobile-offline profile items", "similarity rules", "SLA", "SLA items" },
                new[] { "Duplicate-rule, routing-rule, and mobile-offline definition slices now reach source, emit, readback, and drift coverage; `SimilarityRule`, `SLA`, and `SLAItem` remain explicit source-first or best-effort families until honest neutral live parity exists." }),
            [CapabilityKind.SecurityAndAccess] = new(
                CapabilityKind.SecurityAndAccess,
                "security-access",
                "Roles, privileges, field security, and connection roles.",
                CapabilityReadiness.PartiallyProven,
                new[] { "roles", "role privileges", "field security profiles", "field permissions", "connection roles" },
                new[] { "Role shell, field-security profile and permission, and connection-role definition parity are now proven; effective access remains out of scope, and live role-privilege parity stays definition-adjacent best effort." }),
            [CapabilityKind.EnvironmentAndConfiguration] = new(
                CapabilityKind.EnvironmentAndConfiguration,
                "environment-configuration",
                "Canvas apps, import maps, AI families, entity analytics, and environment variables.",
                CapabilityReadiness.PartiallyProven,
                new[] { "canvas apps", "import maps", "data source mappings", "entity analytics", "AI project types", "AI projects", "AI configurations" },
                new[] { "Canvas apps, entity analytics, and the compact AI slice now have live-backed overlap proof; `ImportMap` and `DataSourceMapping` remain an explicit permanent source-first or best-effort boundary in the neutral corpus." }),
            [CapabilityKind.ReportingAndLegacy] = new(
                CapabilityKind.ReportingAndLegacy,
                "reporting-legacy",
                "Templates, reports, and legacy packaging assets.",
                CapabilityReadiness.Planned,
                new[] { "reports", "templates", "web wizards" },
                new[] { "Reporting and legacy assets remain roadmap-owned in bootstrap." })
        };

        _descriptorsByName = _descriptors.Values.ToDictionary(
            descriptor => descriptor.Name,
            descriptor => descriptor,
            StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<CapabilityDescriptor> GetAll() => _descriptors.Values.ToArray();

    public bool TryGet(CapabilityKind kind, out CapabilityDescriptor descriptor) =>
        _descriptors.TryGetValue(kind, out descriptor!);

    public bool TryGet(string capabilityName, out CapabilityDescriptor descriptor) =>
        _descriptorsByName.TryGetValue(capabilityName, out descriptor!);
}
