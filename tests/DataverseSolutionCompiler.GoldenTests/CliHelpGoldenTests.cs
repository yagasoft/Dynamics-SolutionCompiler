using DataverseSolutionCompiler.Cli;
using VerifyXunit;
using Xunit;

namespace DataverseSolutionCompiler.GoldenTests;

public sealed class CliHelpGoldenTests
{
    [Fact]
    public Task Help_text_stays_stable()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        CliApplication.Run(["--help"], output, error);

        return Verifier.Verify(output.ToString());
    }
}
