using DataverseSolutionCompiler.Apply;
using FluentAssertions;
using Xunit;

namespace DataverseSolutionCompiler.UnitTests;

public sealed class WebApiApplyExecutorTests
{
    [Theory]
    [InlineData(null, "Assembly")]
    [InlineData("", "Assembly")]
    [InlineData("ClassicAssembly", "Assembly")]
    [InlineData("classicassembly", "Assembly")]
    [InlineData("PluginPackage", "Nuget")]
    [InlineData("pluginpackage", "Nuget")]
    [InlineData("unsupported", "Assembly")]
    public void ResolvePluginPushType_maps_supported_code_first_deployment_flavors(string? deploymentFlavor, string expectedPushType)
    {
        WebApiApplyExecutor.ResolvePluginPushType(deploymentFlavor).Should().Be(expectedPushType);
    }
}
