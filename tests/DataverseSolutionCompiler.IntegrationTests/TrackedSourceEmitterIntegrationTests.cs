using FluentAssertions;
using DataverseSolutionCompiler.Emitters.TrackedSource;
using DataverseSolutionCompiler.Domain.Emission;
using DataverseSolutionCompiler.Domain.Model;
using Xunit;

namespace DataverseSolutionCompiler.IntegrationTests;

public sealed class TrackedSourceEmitterIntegrationTests
{
    [Fact]
    public void Emitter_produces_bootstrap_tracked_source_files()
    {
        var emitter = new TrackedSourceEmitter();
        var model = CanonicalSolution.CreatePlaceholder("C:\\Git\\Dataverse-Solution-KB", []);

        var emitted = emitter.Emit(model, new EmitRequest("C:\\Temp\\dsc", EmitLayout.TrackedSource));

        emitted.Success.Should().BeTrue();
        emitted.Files.Should().Contain(file => file.RelativePath == "tracked-source/manifest.json");
    }
}
