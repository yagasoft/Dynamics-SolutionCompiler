using DataverseSolutionCompiler.Domain.Abstractions;
using DataverseSolutionCompiler.Domain.Diff;
using DataverseSolutionCompiler.Domain.Diagnostics;
using DataverseSolutionCompiler.Domain.Live;
using DataverseSolutionCompiler.Domain.Model;

namespace DataverseSolutionCompiler.Diff;

public sealed class StableOverlapDriftComparer : IDriftComparer
{
    public DriftReport Compare(CanonicalSolution source, LiveSnapshot snapshot, CompareRequest request)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(request);

        var sourceArtifacts = FilterArtifacts(source.Artifacts, request);
        var liveArtifacts = FilterArtifacts(snapshot.Artifacts, request);

        var sourceKeys = sourceArtifacts.ToDictionary(KeyFor, artifact => artifact);
        var liveKeys = liveArtifacts.ToDictionary(KeyFor, artifact => artifact);
        var findings = new List<DriftFinding>();

        foreach (var (key, artifact) in sourceKeys)
        {
            if (!liveKeys.ContainsKey(key))
            {
                findings.Add(new DriftFinding(
                    "Missing in live snapshot",
                    DriftSeverity.Warning,
                    DriftCategory.MissingInLive,
                    artifact.Family,
                    $"{artifact.Family} '{artifact.LogicalName}' is present in source but not in live readback."));
            }
        }

        foreach (var (key, artifact) in liveKeys)
        {
            if (!sourceKeys.ContainsKey(key))
            {
                findings.Add(new DriftFinding(
                    "Missing in source",
                    DriftSeverity.Warning,
                    DriftCategory.MissingInSource,
                    artifact.Family,
                    $"{artifact.Family} '{artifact.LogicalName}' is present in live readback but not in source."));
            }
        }

        return new DriftReport(
            findings.Any(finding => finding.Severity == DriftSeverity.Error),
            findings,
            [
                new CompilerDiagnostic(
                    "stable-overlap-bootstrap",
                    DiagnosticSeverity.Info,
                    "Drift comparison is currently based on family/logical-name overlap while family-specific semantic comparison is still being implemented.")
            ]);
    }

    private static IReadOnlyList<FamilyArtifact> FilterArtifacts(IReadOnlyList<FamilyArtifact> artifacts, CompareRequest request) =>
        request.IncludeBestEffortFamilies
            ? artifacts
            : artifacts.Where(artifact => artifact.Evidence != EvidenceKind.BestEffort).ToArray();

    private static string KeyFor(FamilyArtifact artifact) =>
        $"{artifact.Family}:{artifact.LogicalName}".ToLowerInvariant();
}
