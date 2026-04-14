using DataverseSolutionCompiler.Compiler;
using DataverseSolutionCompiler.Domain.Abstractions;
using DataverseSolutionCompiler.Domain.Compilation;
using DataverseSolutionCompiler.Domain.Explanations;

namespace DataverseSolutionCompiler.Agent;

public sealed class AgentOrchestrator
{
    private readonly ICompilerKernel _kernel;
    private readonly IExplanationService _explanationService;

    public AgentOrchestrator(ICompilerKernel? kernel = null, IExplanationService? explanationService = null)
    {
        _kernel = kernel ?? new CompilerKernel();
        _explanationService = explanationService ?? new ExplanationService();
    }

    public HumanReport Analyze(string inputPath, IReadOnlyList<string>? requestedCapabilities = null)
    {
        var result = _kernel.Compile(new CompilationRequest(
            inputPath,
            requestedCapabilities ?? Array.Empty<string>()));

        return _explanationService.Explain(result);
    }
}
