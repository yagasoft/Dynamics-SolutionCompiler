using FluentAssertions;
using DataverseSolutionCompiler.Cli;
using DataverseSolutionCompiler.Compiler;
using DataverseSolutionCompiler.Domain.Compilation;
using Xunit;

namespace DataverseSolutionCompiler.E2ETests;

public sealed class CompilerBootstrapFlowTests
{
    [Fact]
    public void Kernel_and_cli_support_the_real_plan_flow()
    {
        var fixturePath = Path.Combine(
            "C:\\Git\\Dataverse-Solution-KB",
            "fixtures",
            "skill-corpus",
            "examples",
            "seed-core",
            "unpacked");
        var kernel = new CompilerKernel();
        var result = kernel.Compile(new CompilationRequest(fixturePath, []));

        result.Success.Should().BeTrue();
        result.Solution.Artifacts.Should().NotBeEmpty();
        result.Plan.Steps.Should().Contain(step => step.Id == "emit-tracked-source");

        var output = new StringWriter();
        var error = new StringWriter();
        var exitCode = CliApplication.Run(["plan", fixturePath], output, error);

        exitCode.Should().Be(0);
        output.ToString().Should().Contain("Prepared");
    }
}
