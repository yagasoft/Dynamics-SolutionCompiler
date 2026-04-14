using FluentAssertions;
using DataverseSolutionCompiler.Domain.Model;
using DataverseSolutionCompiler.Domain.Read;
using DataverseSolutionCompiler.Readers.Xml;
using Xunit;

namespace DataverseSolutionCompiler.UnitTests;

public sealed class XmlSolutionReaderTests
{
    [Fact]
    public void Reader_inventories_known_unpacked_families()
    {
        var fixtureRoot = Path.Combine(
            "C:\\Git\\Dataverse-Solution-KB",
            "fixtures",
            "skill-corpus",
            "examples",
            "seed-core",
            "unpacked");

        var reader = new XmlSolutionReader();
        var solution = reader.Read(new ReadRequest(fixtureRoot));

        solution.Artifacts.Should().Contain(artifact => artifact.Family == ComponentFamily.SolutionShell);
        solution.Artifacts.Should().Contain(artifact => artifact.Family == ComponentFamily.Table);
    }
}
