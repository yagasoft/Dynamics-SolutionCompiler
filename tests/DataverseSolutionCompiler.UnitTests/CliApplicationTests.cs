using FluentAssertions;
using DataverseSolutionCompiler.Cli;
using Xunit;

namespace DataverseSolutionCompiler.UnitTests;

public sealed class CliApplicationTests
{
    [Fact]
    public void Help_prints_registered_commands()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = CliApplication.Run(["--help"], output, error);

        exitCode.Should().Be(0);
        output.ToString().Should().Contain("Registered commands");
        output.ToString().Should().Contain("read <path>");
        output.ToString().Should().Contain("doctor <path>");
    }

    [Fact]
    public void Unknown_command_returns_non_zero_exit_code()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = CliApplication.Run(["unknown"], output, error);

        exitCode.Should().Be(1);
        error.ToString().Should().Contain("Unknown command");
    }
}
