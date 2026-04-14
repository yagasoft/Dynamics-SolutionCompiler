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
    public void Registry_marks_environment_slice_as_seeded()
    {
        var registry = new CapabilityRegistry();

        registry.TryGet(CapabilityKind.EnvironmentAndConfiguration, out var descriptor).Should().BeTrue();
        descriptor.Readiness.Should().Be(CapabilityReadiness.Seeded);
        descriptor.RepresentativeFamilies.Should().Contain("canvas apps");
    }
}
