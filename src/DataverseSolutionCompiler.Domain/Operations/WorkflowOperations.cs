using DataverseSolutionCompiler.Domain.Apply;
using DataverseSolutionCompiler.Domain.Compilation;
using DataverseSolutionCompiler.Domain.Diagnostics;
using DataverseSolutionCompiler.Domain.Diff;
using DataverseSolutionCompiler.Domain.Emission;
using DataverseSolutionCompiler.Domain.Live;
using DataverseSolutionCompiler.Domain.Model;
using DataverseSolutionCompiler.Domain.Packaging;
using DataverseSolutionCompiler.Domain.Planning;

namespace DataverseSolutionCompiler.Domain.Workflows;

public enum WorkflowStageKind
{
    Compile,
    Apply,
    Readback,
    Diff,
    EmitPackageInputs,
    Pack,
    Import,
    FinalizeApply
}

public enum WorkflowStageStatus
{
    Succeeded,
    Failed,
    Skipped
}

public sealed record WorkflowStageResult(
    WorkflowStageKind Stage,
    WorkflowStageStatus Status,
    string Summary,
    IReadOnlyList<CompilerDiagnostic> Diagnostics);

public sealed record DevApplyWorkflowRequest(
    CompilationRequest Compilation,
    string? SolutionUniqueName = null,
    CompareRequest? Compare = null);

public sealed record PackageBuildWorkflowRequest(
    CompilationRequest Compilation,
    string OutputRoot,
    PackageFlavor Flavor = PackageFlavor.Unmanaged,
    bool RunSolutionCheck = false);

public sealed record PublishWorkflowRequest(
    CompilationRequest Compilation,
    string OutputRoot,
    EnvironmentProfile ImportEnvironment,
    EnvironmentProfile FinalizeApplyEnvironment,
    PackageFlavor Flavor = PackageFlavor.Unmanaged,
    bool RunSolutionCheck = false);

public sealed record DevApplyWorkflowResult(
    CompilationResult Compilation,
    ApplyResult? Apply,
    LiveSnapshot? Snapshot,
    DriftReport? Diff,
    IReadOnlyList<ComponentFamily> VerificationFamilies,
    IReadOnlyList<WorkflowStageResult> Stages,
    IReadOnlyList<CompilerDiagnostic> Diagnostics)
{
    public bool Success =>
        Compilation.Success
        && !HasErrors(Diagnostics)
        && !HasErrors(Compilation.Diagnostics)
        && Apply is not null
        && Apply.Success
        && !HasErrors(Apply.Diagnostics)
        && Snapshot is not null
        && !HasErrors(Snapshot.Diagnostics)
        && Diff is not null
        && !Diff.HasBlockingDrift;

    public bool IsNoOp =>
        Success
        && VerificationFamilies.Count == 0
        && Apply is not null
        && Apply.AppliedFamilies.Count == 0;

    private static bool HasErrors(IEnumerable<CompilerDiagnostic> diagnostics) =>
        diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
}

public sealed record PackageBuildWorkflowResult(
    CompilationResult Compilation,
    EmittedArtifacts? PackageInputs,
    PackageResult? Package,
    string OutputRoot,
    string PackageInputRoot,
    IReadOnlyList<WorkflowStageResult> Stages,
    IReadOnlyList<CompilerDiagnostic> Diagnostics)
{
    public bool Success =>
        Compilation.Success
        && !HasErrors(Diagnostics)
        && !HasErrors(Compilation.Diagnostics)
        && PackageInputs is not null
        && PackageInputs.Success
        && !HasErrors(PackageInputs.Diagnostics)
        && Package is not null
        && Package.Success
        && !HasErrors(Package.Diagnostics)
        && !string.IsNullOrWhiteSpace(Package.PackagePath);

    private static bool HasErrors(IEnumerable<CompilerDiagnostic> diagnostics) =>
        diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
}

public sealed record PublishWorkflowResult(
    CompilationResult Compilation,
    EmittedArtifacts? PackageInputs,
    PackageResult? Package,
    ImportResult? Import,
    ApplyResult? FinalizeApply,
    bool ImportSkippedBecauseApplyOnly,
    string OutputRoot,
    string PackageInputRoot,
    IReadOnlyList<WorkflowStageResult> Stages,
    IReadOnlyList<CompilerDiagnostic> Diagnostics)
{
    public bool Success =>
        Compilation.Success
        && !HasErrors(Diagnostics)
        && !HasErrors(Compilation.Diagnostics)
        && PackageInputs is not null
        && PackageInputs.Success
        && !HasErrors(PackageInputs.Diagnostics)
        && Package is not null
        && Package.Success
        && !HasErrors(Package.Diagnostics)
        && !string.IsNullOrWhiteSpace(Package.PackagePath)
        && (ImportSkippedBecauseApplyOnly || (Import is not null && Import.Success && !HasErrors(Import.Diagnostics)))
        && FinalizeApply is not null
        && FinalizeApply.Success
        && !HasErrors(FinalizeApply.Diagnostics);

    private static bool HasErrors(IEnumerable<CompilerDiagnostic> diagnostics) =>
        diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
}
