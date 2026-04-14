using DataverseSolutionCompiler.Domain.Capabilities;
using DataverseSolutionCompiler.Domain.Diagnostics;
using DataverseSolutionCompiler.Domain.Model;

namespace DataverseSolutionCompiler.Domain.Read
{
    public enum ReadSourceKind
    {
        Auto,
        IntentSpecJson,
        UnpackedXmlFolder,
        PackedZip,
        TrackedSource,
        LiveSnapshot
    }

    public sealed record ReadRequest(
        string SourcePath,
        ReadSourceKind SourceKind = ReadSourceKind.Auto,
        IReadOnlyCollection<CapabilityKind>? RequestedCapabilities = null);
}

namespace DataverseSolutionCompiler.Domain.Planning
{
    public sealed record EnvironmentProfile(
        string Name,
        Uri? DataverseUrl = null,
        string? TenantId = null,
        bool IsDevelopment = false);

    public sealed record CompilationContext(
        EnvironmentProfile Environment,
        bool EnablePackaging = true,
        bool EnableDevApply = false,
        bool IncludeBestEffortFamilies = false)
    {
        public static CompilationContext Default { get; } =
            new(new EnvironmentProfile("local-bootstrap"));
    }

    public enum PlanStepKind
    {
        Read,
        Validate,
        Generate,
        Emit,
        Apply,
        Publish,
        Readback,
        Compare,
        Package,
        Import,
        Check,
        Explain
    }

    public sealed record PlanStep(
        string Id,
        PlanStepKind Kind,
        string Description,
        CapabilityKind? Capability,
        bool Required);

    public sealed record CompilationPlan(
        string Summary,
        IReadOnlyList<PlanStep> Steps,
        IReadOnlyList<CompilerDiagnostic> Diagnostics);
}

namespace DataverseSolutionCompiler.Domain.Emission
{
    public enum EmitLayout
    {
        TrackedSource,
        PackageInputs,
        AcceptanceEvidence
    }

    public enum EmittedArtifactRole
    {
        TrackedSource,
        PackageInput,
        DeploymentSetting,
        AcceptanceEvidence
    }

    public sealed record EmitRequest(
        string OutputRoot,
        EmitLayout Layout,
        bool IncludeEvidenceArtifacts = true);

    public sealed record EmittedArtifact(
        string RelativePath,
        EmittedArtifactRole Role,
        string Description);

    public sealed record EmittedArtifacts(
        bool Success,
        string OutputRoot,
        IReadOnlyList<EmittedArtifact> Files,
        IReadOnlyList<CompilerDiagnostic> Diagnostics);
}

namespace DataverseSolutionCompiler.Domain.Apply
{
    using DataverseSolutionCompiler.Domain.Planning;

    public enum ApplyMode
    {
        PlanOnly,
        DevProof
    }

    public sealed record ApplyRequest(
        EnvironmentProfile Environment,
        ApplyMode Mode = ApplyMode.DevProof);

    public sealed record ApplyResult(
        bool Success,
        ApplyMode Mode,
        IReadOnlyList<string> AppliedFamilies,
        IReadOnlyList<CompilerDiagnostic> Diagnostics);
}

namespace DataverseSolutionCompiler.Domain.Live
{
    using DataverseSolutionCompiler.Domain.Planning;

    public sealed record ReadbackRequest(
        EnvironmentProfile Environment,
        string? SolutionUniqueName = null,
        IReadOnlyList<ComponentFamily>? Families = null);

    public sealed record LiveSnapshot(
        EnvironmentProfile Environment,
        string? SolutionUniqueName,
        IReadOnlyList<FamilyArtifact> Artifacts,
        IReadOnlyList<CompilerDiagnostic> Diagnostics);
}

namespace DataverseSolutionCompiler.Domain.Diff
{
    public enum DriftSeverity
    {
        Info,
        Warning,
        Error
    }

    public enum DriftCategory
    {
        MissingInSource,
        MissingInLive,
        Mismatch,
        EnvironmentLocal,
        BestEffort
    }

    public sealed record DriftFinding(
        string Title,
        DriftSeverity Severity,
        DriftCategory Category,
        ComponentFamily Family,
        string Description);

    public sealed record CompareRequest(
        bool IncludeBestEffortFamilies = false);

    public sealed record DriftReport(
        bool HasBlockingDrift,
        IReadOnlyList<DriftFinding> Findings,
        IReadOnlyList<CompilerDiagnostic> Diagnostics);
}

namespace DataverseSolutionCompiler.Domain.Packaging
{
    using DataverseSolutionCompiler.Domain.Planning;

    public enum PackageFlavor
    {
        Unmanaged,
        Managed
    }

    public sealed record PackageRequest(
        string InputRoot,
        string OutputRoot,
        PackageFlavor Flavor,
        bool RunSolutionCheck = false);

    public sealed record PackageResult(
        bool Success,
        string? PackagePath,
        IReadOnlyList<CompilerDiagnostic> Diagnostics);

    public sealed record ImportRequest(
        EnvironmentProfile Environment,
        string PackagePath,
        bool PublishAfterImport = true);

    public sealed record ImportResult(
        bool Success,
        string PackagePath,
        bool Published,
        IReadOnlyList<CompilerDiagnostic> Diagnostics);
}

namespace DataverseSolutionCompiler.Domain.Explanations
{
    public sealed record HumanReport(
        string Title,
        IReadOnlyList<string> Sections,
        IReadOnlyList<CompilerDiagnostic> Diagnostics);
}

namespace DataverseSolutionCompiler.Domain.Compilation
{
    using DataverseSolutionCompiler.Domain.Planning;

    public sealed record CompilationRequest(
        string InputPath,
        IReadOnlyList<string> RequestedCapabilities,
        CompilationContext? Context = null);

    public sealed record CompilationResult(
        bool Success,
        string Message,
        CanonicalSolution Solution,
        CompilationPlan Plan,
        IReadOnlyList<CapabilityDescriptor> Capabilities,
        IReadOnlyList<CompilerDiagnostic> Diagnostics);
}
