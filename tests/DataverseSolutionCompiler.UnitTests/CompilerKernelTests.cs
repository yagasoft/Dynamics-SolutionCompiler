using FluentAssertions;
using DataverseSolutionCompiler.Domain.Compilation;
using DataverseSolutionCompiler.Compiler;
using Xunit;

namespace DataverseSolutionCompiler.UnitTests;

public sealed class CompilerKernelTests
{
    [Fact]
    public void Compile_returns_all_capabilities_when_no_filter_is_supplied()
    {
        var kernel = new CompilerKernel();

        var result = kernel.Compile(new CompilationRequest(".", Array.Empty<string>()));

        result.Success.Should().BeTrue();
        result.Capabilities.Should().NotBeEmpty();
        result.Capabilities.Should().Contain(capability => capability.Name == "schema-core");
        result.Plan.Steps.Should().NotBeEmpty();
    }

    [Fact]
    public void Compile_can_filter_to_a_single_named_capability()
    {
        var kernel = new CompilerKernel();

        var result = kernel.Compile(new CompilationRequest(".", new[] { "schema-detail" }));

        result.Capabilities.Should().ContainSingle();
        result.Capabilities[0].Name.Should().Be("schema-detail");
    }

    [Fact]
    public void Compile_records_unknown_capabilities_as_diagnostics()
    {
        var kernel = new CompilerKernel();

        var result = kernel.Compile(new CompilationRequest(".", new[] { "not-a-real-capability" }));

        result.Diagnostics.Should().Contain(diagnostic => diagnostic.Code == "unknown-capability");
    }
}
