using DataverseSolutionCompiler.Domain.Apply;
using DataverseSolutionCompiler.Domain.Diff;
using DataverseSolutionCompiler.Domain.Emission;
using DataverseSolutionCompiler.Domain.Explanations;
using DataverseSolutionCompiler.Domain.Live;
using DataverseSolutionCompiler.Domain.Model;
using DataverseSolutionCompiler.Domain.Packaging;
using DataverseSolutionCompiler.Domain.Planning;
using DataverseSolutionCompiler.Domain.Read;

namespace DataverseSolutionCompiler.Domain.Abstractions;

public interface ISolutionReader
{
    CanonicalSolution Read(ReadRequest request);
}

public interface ICompilationPlanner
{
    CompilationPlan Plan(CanonicalSolution model, CompilationContext context);
}

public interface ISolutionEmitter
{
    EmittedArtifacts Emit(CanonicalSolution model, EmitRequest request);
}

public interface IApplyExecutor
{
    ApplyResult Apply(CanonicalSolution model, ApplyRequest request);
}

public interface ILiveSnapshotProvider
{
    LiveSnapshot Readback(ReadbackRequest request);
}

public interface IDriftComparer
{
    DriftReport Compare(CanonicalSolution source, LiveSnapshot snapshot, CompareRequest request);
}

public interface IPackageExecutor
{
    PackageResult Pack(PackageRequest request);
}

public interface IImportExecutor
{
    ImportResult Import(ImportRequest request);
}

public interface IExplanationService
{
    HumanReport Explain(object compilerResult);
}
