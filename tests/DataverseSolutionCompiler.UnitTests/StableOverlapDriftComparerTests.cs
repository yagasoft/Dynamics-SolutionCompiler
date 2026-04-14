using FluentAssertions;
using DataverseSolutionCompiler.Diff;
using DataverseSolutionCompiler.Domain.Diff;
using DataverseSolutionCompiler.Domain.Diagnostics;
using DataverseSolutionCompiler.Domain.Live;
using DataverseSolutionCompiler.Domain.Model;
using DataverseSolutionCompiler.Domain.Planning;
using Xunit;

namespace DataverseSolutionCompiler.UnitTests;

public sealed class StableOverlapDriftComparerTests
{
    [Fact]
    public void Compare_reports_missing_live_artifacts()
    {
        var source = new CanonicalSolution(
            new SolutionIdentity("sample", "Sample", "1.0.0", LayeringIntent.Hybrid),
            new PublisherDefinition("dsc", "dsc", "dsc", "Dataverse Solution Compiler"),
            [new FamilyArtifact(ComponentFamily.Table, "account")],
            [],
            [],
            []);

        var live = new LiveSnapshot(
            new EnvironmentProfile("dev"),
            "sample",
            [],
            [new CompilerDiagnostic("live-bootstrap", DiagnosticSeverity.Info, "placeholder")]);

        var comparer = new StableOverlapDriftComparer();
        var report = comparer.Compare(source, live, new CompareRequest());

        report.Findings.Should().ContainSingle();
        report.Findings[0].Category.Should().Be(DriftCategory.MissingInLive);
    }
}
