using DataverseSolutionCompiler.Domain.Compilation;

namespace DataverseSolutionCompiler.Domain.Abstractions;

public interface ICompilerKernel
{
    CompilationResult Compile(CompilationRequest request);
}
