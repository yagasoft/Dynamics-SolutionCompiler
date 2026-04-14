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
    IExplanationService ExplanationService)
{
    public static CompilerCliRuntime CreateDefault()
    {
        var pacCliExecutor = new PacCliExecutor();

        return new CompilerCliRuntime(
            new CompilerKernel(),
            new TrackedSourceEmitter(),
            new PackageEmitter(),
            new WebApiLiveSnapshotProvider(),
            new StableOverlapDriftComparer(),
            pacCliExecutor,
            pacCliExecutor,
            new WebApiApplyExecutor(),
            new ExplanationService());
    }
}
