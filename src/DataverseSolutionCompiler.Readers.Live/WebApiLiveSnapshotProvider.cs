using DataverseSolutionCompiler.Domain.Abstractions;
using DataverseSolutionCompiler.Domain.Diagnostics;
using DataverseSolutionCompiler.Domain.Live;

namespace DataverseSolutionCompiler.Readers.Live;

public sealed class WebApiLiveSnapshotProvider : ILiveSnapshotProvider
{
    public LiveSnapshot Readback(ReadbackRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new LiveSnapshot(
            request.Environment,
            request.SolutionUniqueName,
            [],
            [
                new CompilerDiagnostic(
                    "live-webapi-bootstrap",
                    DiagnosticSeverity.Warning,
                    "Live Dataverse Web API readback is registered but not yet connected in the bootstrap milestone.",
                    request.Environment.DataverseUrl?.ToString())
            ]);
    }
}
