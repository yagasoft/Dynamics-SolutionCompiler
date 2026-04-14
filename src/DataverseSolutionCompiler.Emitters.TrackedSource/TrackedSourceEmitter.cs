using DataverseSolutionCompiler.Domain.Abstractions;
using DataverseSolutionCompiler.Domain.Diagnostics;
using DataverseSolutionCompiler.Domain.Emission;
using DataverseSolutionCompiler.Domain.Model;

namespace DataverseSolutionCompiler.Emitters.TrackedSource;

public sealed class TrackedSourceEmitter : ISolutionEmitter
{
    public EmittedArtifacts Emit(CanonicalSolution model, EmitRequest request)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(request);

        return new EmittedArtifacts(
            true,
            request.OutputRoot,
            [
                new EmittedArtifact("tracked-source/manifest.json", EmittedArtifactRole.TrackedSource, "Root manifest for tracked Dataverse compiler output."),
                new EmittedArtifact("tracked-source/solution/README.md", EmittedArtifactRole.TrackedSource, $"Bootstrap tracked source for {model.Identity.UniqueName}.")
            ],
            [
                new CompilerDiagnostic(
                    "tracked-source-emitter-bootstrap",
                    DiagnosticSeverity.Info,
                    "Tracked source emitter bootstrap is active; file materialization will deepen in later milestones.",
                    request.OutputRoot)
            ]);
    }
}
