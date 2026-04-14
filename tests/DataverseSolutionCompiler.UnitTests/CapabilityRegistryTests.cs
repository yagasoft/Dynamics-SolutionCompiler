using FluentAssertions;
using DataverseSolutionCompiler.Domain.Capabilities;
using DataverseSolutionCompiler.Compiler;
using Xunit;

namespace DataverseSolutionCompiler.UnitTests;

public sealed class CapabilityRegistryTests
{
    [Fact]
    public void Registry_exposes_expected_capabilities()
    {
        var registry = new CapabilityRegistry();

        var capabilities = registry.GetAll();

        capabilities.Should().Contain(capability => capability.Name == "schema-core");
        capabilities.Should().Contain(capability => capability.Name == "schema-detail");
        capabilities.Should().Contain(capability => capability.Name == "environment-configuration");
    }

    [Fact]
    public void Registry_marks_environment_process_and_security_slices_as_partially_proven()
    {
        var registry = new CapabilityRegistry();

        registry.TryGet(CapabilityKind.EnvironmentAndConfiguration, out var environmentDescriptor).Should().BeTrue();
        environmentDescriptor.Readiness.Should().Be(CapabilityReadiness.PartiallyProven);
        environmentDescriptor.RepresentativeFamilies.Should().Contain("canvas apps");
        environmentDescriptor.KnownBoundaries.Should().Contain(note => note.Contains("ImportMap", StringComparison.OrdinalIgnoreCase));

        registry.TryGet(CapabilityKind.ProcessAndServicePolicy, out var processDescriptor).Should().BeTrue();
        processDescriptor.Readiness.Should().Be(CapabilityReadiness.PartiallyProven);
        processDescriptor.RepresentativeFamilies.Should().Contain("duplicate rules");

        registry.TryGet(CapabilityKind.SecurityAndAccess, out var securityDescriptor).Should().BeTrue();
        securityDescriptor.Readiness.Should().Be(CapabilityReadiness.PartiallyProven);
        securityDescriptor.RepresentativeFamilies.Should().Contain("field permissions");
    }
}
