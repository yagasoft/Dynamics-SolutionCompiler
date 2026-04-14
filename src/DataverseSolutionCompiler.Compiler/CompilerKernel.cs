using DataverseSolutionCompiler.Domain.Abstractions;
using DataverseSolutionCompiler.Domain.Capabilities;
using DataverseSolutionCompiler.Domain.Compilation;
using DataverseSolutionCompiler.Domain.Diagnostics;
using DataverseSolutionCompiler.Domain.Model;
using DataverseSolutionCompiler.Domain.Planning;

namespace DataverseSolutionCompiler.Compiler;

public sealed class CompilerKernel : ICompilerKernel
{
    private readonly ICapabilityRegistry _capabilityRegistry;
    private readonly ICompilationPlanner _planner;

    public CompilerKernel(
        ICapabilityRegistry? capabilityRegistry = null,
        ICompilationPlanner? planner = null)
    {
        _capabilityRegistry = capabilityRegistry ?? new CapabilityRegistry();
        _planner = planner ?? new CompilationPlanner();
    }

    public CompilationResult Compile(CompilationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.InputPath);

        var diagnostics = new List<CompilerDiagnostic>();
        var capabilities = ResolveCapabilities(request.RequestedCapabilities, diagnostics);

        if (!File.Exists(request.InputPath) && !Directory.Exists(request.InputPath))
        {
            diagnostics.Add(new CompilerDiagnostic(
                "path-not-found",
                DiagnosticSeverity.Warning,
                $"Path not found: {request.InputPath}",
                request.InputPath));
        }

        var context = request.Context ?? CompilationContext.Default;
        var solution = CanonicalSolution.CreatePlaceholder(request.InputPath, capabilities.Select(capability => capability.Kind).ToArray());
        var plan = _planner.Plan(solution, context);
        diagnostics.AddRange(solution.Diagnostics);
        diagnostics.AddRange(plan.Diagnostics);

        return new CompilationResult(
            Success: true,
            Message: $"Compiler kernel analyzed {capabilities.Count} capability slice(s) for {solution.Identity.UniqueName}.",
            Solution: solution,
            Plan: plan,
            Capabilities: capabilities,
            Diagnostics: diagnostics);
    }

    private IReadOnlyList<CapabilityDescriptor> ResolveCapabilities(
        IReadOnlyList<string> requestedCapabilities,
        ICollection<CompilerDiagnostic> diagnostics)
    {
        if (requestedCapabilities.Count == 0)
        {
            return _capabilityRegistry.GetAll().OrderBy(capability => capability.Name).ToArray();
        }

        var resolved = new List<CapabilityDescriptor>();
        foreach (var capabilityName in requestedCapabilities)
        {
            if (_capabilityRegistry.TryGet(capabilityName, out var descriptor))
            {
                resolved.Add(descriptor);
            }
            else
            {
                diagnostics.Add(new CompilerDiagnostic(
                    "unknown-capability",
                    DiagnosticSeverity.Warning,
                    $"Unknown capability requested: {capabilityName}",
                    capabilityName));
            }
        }

        return resolved;
    }
}
