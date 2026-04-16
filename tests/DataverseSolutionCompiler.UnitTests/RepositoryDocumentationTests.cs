using FluentAssertions;
using Xunit;

namespace DataverseSolutionCompiler.UnitTests;

public sealed class RepositoryDocumentationTests
{
    private const string RepoRoot = @"C:\Git\Dataverse-Solution-KB";
    private static readonly string ReadmePath = Path.Combine(RepoRoot, "README.md");

    [Fact]
    public void Readme_is_portable_and_avoids_workspace_specific_absolute_paths()
    {
        var readme = File.ReadAllText(ReadmePath);

        readme.Should().NotContain(@"C:\Git\Dataverse-Solution-KB");
        readme.Should().NotContain("C:/Git/Dataverse-Solution-KB");
    }

    [Fact]
    public void Readme_links_core_docs_with_relative_paths()
    {
        var readme = File.ReadAllText(ReadmePath);

        readme.Should().Contain("[Architecture](docs/architecture.md)");
        readme.Should().Contain("[Roadmap](docs/roadmap.md)");
        readme.Should().Contain("[Backlog](docs/backlog/backlog.md)");
        readme.Should().Contain("[Acceptance Ledger](docs/acceptance/ledger.md)");
        readme.Should().Contain("[Coverage Matrix](fixtures/skill-corpus/references/component-coverage-matrix.md)");
    }
}
