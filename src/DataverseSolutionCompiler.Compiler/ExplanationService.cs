using DataverseSolutionCompiler.Domain.Abstractions;
using DataverseSolutionCompiler.Domain.Compilation;
using DataverseSolutionCompiler.Domain.Diff;
using DataverseSolutionCompiler.Domain.Explanations;
using DataverseSolutionCompiler.Domain.Model;
using DataverseSolutionCompiler.Domain.Planning;

namespace DataverseSolutionCompiler.Compiler;

public sealed class ExplanationService : IExplanationService
{
    public HumanReport Explain(object compilerResult) =>
        compilerResult switch
        {
            CompilationResult result => ExplainCompilation(result),
            CompilationPlan plan => ExplainPlan(plan),
            DriftReport drift => ExplainDrift(drift),
            CanonicalSolution solution => ExplainSolution(solution),
            _ => new HumanReport(
                "Compiler Explanation",
                new[] { $"No specialized explanation is registered for {compilerResult.GetType().Name}." },
                [])
        };

    private static HumanReport ExplainCompilation(CompilationResult result) =>
        new(
            "Compilation Summary",
            new[]
            {
                result.Message,
                $"Capabilities: {string.Join(", ", result.Capabilities.Select(capability => capability.Name))}",
                $"Planned steps: {result.Plan.Steps.Count}"
            },
            result.Diagnostics);

    private static HumanReport ExplainPlan(CompilationPlan plan) =>
        new(
            "Compilation Plan",
            new[]
            {
                plan.Summary,
                string.Join(Environment.NewLine, plan.Steps.Select(step => $"- {step.Id}: {step.Description}"))
            },
            plan.Diagnostics);

    private static HumanReport ExplainDrift(DriftReport drift) =>
        new(
            "Drift Report",
            new[]
            {
                drift.HasBlockingDrift ? "Blocking drift detected." : "No blocking drift detected.",
                string.Join(Environment.NewLine, drift.Findings.Select(finding => $"- {finding.Family}: {finding.Description}"))
            },
            drift.Diagnostics);

    private static HumanReport ExplainSolution(CanonicalSolution solution) =>
        new(
            "Canonical Solution",
            new[]
            {
                $"Solution: {solution.Identity.UniqueName}",
                $"Artifacts: {solution.Artifacts.Count}",
                $"Environment bindings: {solution.EnvironmentBindings.Count}"
            },
            solution.Diagnostics);
}
