using DataverseSolutionCompiler.Domain.Diagnostics;
using DataverseSolutionCompiler.Domain.Model;

namespace DataverseSolutionCompiler.Domain.Build;

public enum CodeAssetDeploymentFlavor
{
    ClassicAssembly,
    PluginPackage
}

public sealed record CodeAssetBuildRequest(
    CanonicalSolution Solution,
    string StagingRoot,
    string Configuration = "Release");

public sealed record CodeAssetBuildResult(
    CanonicalSolution Solution,
    IReadOnlyList<CompilerDiagnostic> Diagnostics)
{
    public bool Success =>
        Diagnostics.All(diagnostic => diagnostic.Severity != DiagnosticSeverity.Error);
}
