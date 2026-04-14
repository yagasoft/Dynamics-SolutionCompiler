using DataverseSolutionCompiler.Domain.Abstractions;
using DataverseSolutionCompiler.Domain.Apply;
using DataverseSolutionCompiler.Domain.Diagnostics;
using DataverseSolutionCompiler.Domain.Model;

namespace DataverseSolutionCompiler.Apply;

public sealed class WebApiApplyExecutor : IApplyExecutor
{
    public ApplyResult Apply(CanonicalSolution model, ApplyRequest request)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(request);

        return new ApplyResult(
            Success: true,
            request.Mode,
            model.Artifacts.Select(artifact => artifact.Family.ToString()).Distinct().OrderBy(value => value).ToArray(),
            [
                new CompilerDiagnostic(
                    "apply-bootstrap-noop",
                    DiagnosticSeverity.Info,
                    "Bootstrap apply registered the intended family set but intentionally performed no live Dataverse mutation yet.",
                    request.Environment.DataverseUrl?.ToString())
            ]);
    }
}
