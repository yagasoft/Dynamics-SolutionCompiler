using FluentAssertions;
using DataverseSolutionCompiler.Domain.Compilation;
using DataverseSolutionCompiler.Compiler;
using Xunit;

namespace DataverseSolutionCompiler.UnitTests;

public sealed class CompilerKernelTests
{
    private static readonly string SeedCorePath = Path.Combine(
        "C:\\Git\\Dataverse-Solution-KB",
        "fixtures",
        "skill-corpus",
        "examples",
        "seed-core",
        "unpacked");

    [Fact]
    public void Compile_reads_a_real_fixture_when_no_filter_is_supplied()
    {
        var kernel = new CompilerKernel();

        var result = kernel.Compile(new CompilationRequest(SeedCorePath, Array.Empty<string>()));

        result.Success.Should().BeTrue();
        result.Capabilities.Should().NotBeEmpty();
        result.Capabilities.Should().Contain(capability => capability.Name == "schema-core");
        result.Solution.Identity.UniqueName.Should().Be("CodexMetadataSeedCore");
        result.Solution.Artifacts.Should().NotBeEmpty();
        result.Diagnostics.Should().NotContain(diagnostic => diagnostic.Code == "bootstrap-placeholder");
        result.Plan.Steps.Should().NotBeEmpty();
    }

    [Fact]
    public void Compile_can_filter_to_a_single_named_capability()
    {
        var kernel = new CompilerKernel();

        var result = kernel.Compile(new CompilationRequest(SeedCorePath, new[] { "schema-detail" }));

        result.Capabilities.Should().ContainSingle();
        result.Capabilities[0].Name.Should().Be("schema-detail");
    }

    [Fact]
    public void Compile_records_unknown_capabilities_as_diagnostics()
    {
        var kernel = new CompilerKernel();

        var result = kernel.Compile(new CompilationRequest(SeedCorePath, new[] { "not-a-real-capability" }));

        result.Diagnostics.Should().Contain(diagnostic => diagnostic.Code == "unknown-capability");
    }

    [Fact]
    public void Compile_records_missing_path_diagnostics_and_returns_failure()
    {
        var kernel = new CompilerKernel();

        var result = kernel.Compile(new CompilationRequest("C:\\Git\\Dataverse-Solution-KB\\missing-solution", Array.Empty<string>()));

        result.Success.Should().BeFalse();
        result.Diagnostics.Should().Contain(diagnostic => diagnostic.Code == "path-not-found");
        result.Solution.Artifacts.Should().BeEmpty();
    }
}
