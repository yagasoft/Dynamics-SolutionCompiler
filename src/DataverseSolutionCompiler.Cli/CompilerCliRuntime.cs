using DataverseSolutionCompiler.Agent;
using DataverseSolutionCompiler.Apply;
using DataverseSolutionCompiler.Compiler;
using DataverseSolutionCompiler.Diff;
using DataverseSolutionCompiler.Domain.Abstractions;
using DataverseSolutionCompiler.Emitters.Package;
using DataverseSolutionCompiler.Emitters.TrackedSource;
using DataverseSolutionCompiler.Packaging.Pac;
using DataverseSolutionCompiler.Readers.Live;

namespace DataverseSolutionCompiler.Cli;

internal sealed record CompilerCliRuntime(
    ICompilerKernel Kernel,
    ISolutionEmitter TrackedSourceEmitter,
    ISolutionEmitter PackageEmitter,
    ILiveSnapshotProvider LiveSnapshotProvider,
    IDriftComparer DriftComparer,
    IPackageExecutor PackageExecutor,
    IImportExecutor ImportExecutor,
    IApplyExecutor ApplyExecutor,
    IExplanationService ExplanationService,
    IDevApplyWorkflowRunner? DevApplyWorkflowRunner = null,
    IPackageBuildWorkflowRunner? PackageBuildWorkflowRunner = null,
    IPublishWorkflowRunner? PublishWorkflowRunner = null,
    ICodeAssetBuilder? CodeAssetBuilder = null)
{
    public IDevApplyWorkflowRunner ResolveDevApplyWorkflowRunner() =>
        DevApplyWorkflowRunner ?? CreateWorkflowOrchestrator();

    public IPackageBuildWorkflowRunner ResolvePackageBuildWorkflowRunner() =>
        PackageBuildWorkflowRunner ?? CreateWorkflowOrchestrator();

    public IPublishWorkflowRunner ResolvePublishWorkflowRunner() =>
        PublishWorkflowRunner ?? CreateWorkflowOrchestrator();

    private AgentOrchestrator CreateWorkflowOrchestrator() =>
        new(
            Kernel,
            ExplanationService,
            ApplyExecutor,
            LiveSnapshotProvider,
            DriftComparer,
            PackageEmitter,
            PackageExecutor,
            ImportExecutor,
            CodeAssetBuilder ?? new DotNetCodeAssetBuilder());

    public static CompilerCliRuntime CreateDefault()
    {
        var pacCliExecutor = new PacCliExecutor();
        var kernel = new CompilerKernel();
        var trackedSourceEmitter = new TrackedSourceEmitter();
        var packageEmitter = new PackageEmitter();
        var liveSnapshotProvider = new WebApiLiveSnapshotProvider();
        var driftComparer = new StableOverlapDriftComparer();
        var applyExecutor = new WebApiApplyExecutor();
        var codeAssetBuilder = new DotNetCodeAssetBuilder();
        var explanationService = new ExplanationService();
        var workflowOrchestrator = new AgentOrchestrator(
            kernel,
            explanationService,
            applyExecutor,
            liveSnapshotProvider,
            driftComparer,
            packageEmitter,
            pacCliExecutor,
            pacCliExecutor,
            codeAssetBuilder);

        return new CompilerCliRuntime(
            kernel,
            trackedSourceEmitter,
            packageEmitter,
            liveSnapshotProvider,
            driftComparer,
            pacCliExecutor,
            pacCliExecutor,
            applyExecutor,
            explanationService,
            workflowOrchestrator,
            workflowOrchestrator,
            workflowOrchestrator,
            codeAssetBuilder);
    }
}
