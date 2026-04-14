using FluentAssertions;
using DataverseSolutionCompiler.Cli;
using DataverseSolutionCompiler.Compiler;
using DataverseSolutionCompiler.Domain.Compilation;
using Xunit;

namespace DataverseSolutionCompiler.E2ETests;

public sealed class CompilerBootstrapFlowTests
{
    [Fact]
    public void Kernel_and_cli_support_the_bootstrap_plan_flow()
    {
        var kernel = new CompilerKernel();
        var result = kernel.Compile(new CompilationRequest("C:\\Git\\Dataverse-Solution-KB", []));

        result.Success.Should().BeTrue();
        result.Plan.Steps.Should().Contain(step => step.Id == "emit-tracked-source");

        var output = new StringWriter();
        var error = new StringWriter();
        var exitCode = CliApplication.Run(["plan", "C:\\Git\\Dataverse-Solution-KB"], output, error);

        exitCode.Should().Be(0);
        output.ToString().Should().Contain("Prepared");
    }
}
