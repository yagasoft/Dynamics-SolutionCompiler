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
                "Choice, key, and companion schema support.",
                CapabilityReadiness.PartiallyProven,
                new[] { "option sets", "global option sets", "alternate keys" },
                new[] { "Alternate keys are still best-effort in unattended live proof." }),
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
                "Plugin assemblies, steps, images, and source-first registration.",
                CapabilityReadiness.Seeded,
                new[] { "plugin assemblies", "steps", "images", "SDK message families" },
                new[] { "Neutral live plugin-step proof is not complete in bootstrap." }),
            [CapabilityKind.ProcessAndServicePolicy] = new(
                CapabilityKind.ProcessAndServicePolicy,
                "process-service-policy",
                "Workflow and service-policy artifact support.",
                CapabilityReadiness.Seeded,
                new[] { "workflows", "duplicate rules", "SLA" },
                new[] { "SLA and similarity-rule parity are still partial." }),
            [CapabilityKind.SecurityAndAccess] = new(
                CapabilityKind.SecurityAndAccess,
                "security-access",
                "Roles, privileges, field security, and connection roles.",
                CapabilityReadiness.Seeded,
                new[] { "roles", "field security profiles", "connection roles" },
                new[] { "Effective privilege parity remains source-first or best effort." }),
            [CapabilityKind.EnvironmentAndConfiguration] = new(
                CapabilityKind.EnvironmentAndConfiguration,
                "environment-configuration",
                "Canvas apps, import maps, and environment variables.",
                CapabilityReadiness.Seeded,
                new[] { "canvas apps", "import maps", "data source mappings" },
                new[] { "Import maps and analytics families are not yet fully seeded." }),
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
