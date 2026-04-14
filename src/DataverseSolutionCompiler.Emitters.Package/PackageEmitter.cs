using DataverseSolutionCompiler.Domain.Abstractions;
using DataverseSolutionCompiler.Domain.Diagnostics;
using DataverseSolutionCompiler.Domain.Emission;
using DataverseSolutionCompiler.Domain.Model;

namespace DataverseSolutionCompiler.Emitters.Package;

public sealed class PackageEmitter : ISolutionEmitter
{
    public EmittedArtifacts Emit(CanonicalSolution model, EmitRequest request)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(request);

        return new EmittedArtifacts(
            true,
            request.OutputRoot,
            [
                new EmittedArtifact("package-inputs/Other/Solution.xml", EmittedArtifactRole.PackageInput, "Solution shell placeholder."),
                new EmittedArtifact("package-inputs/Other/Customizations.xml", EmittedArtifactRole.PackageInput, "Customization payload placeholder."),
                new EmittedArtifact("package-inputs/settings/deployment-settings.json", EmittedArtifactRole.DeploymentSetting, "Deployment settings placeholder.")
            ],
            [
                new CompilerDiagnostic(
                    "package-emitter-bootstrap",
                    DiagnosticSeverity.Info,
                    "Package emitter bootstrap is active; PAC-ready materialization will deepen in later milestones.",
                    request.OutputRoot)
            ]);
    }
}
