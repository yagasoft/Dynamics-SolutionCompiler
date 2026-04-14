using DataverseSolutionCompiler.Domain.Abstractions;
using DataverseSolutionCompiler.Domain.Capabilities;
using DataverseSolutionCompiler.Domain.Diagnostics;
using DataverseSolutionCompiler.Domain.Model;
using DataverseSolutionCompiler.Domain.Planning;

namespace DataverseSolutionCompiler.Compiler;

public sealed class CompilationPlanner : ICompilationPlanner
{
    public CompilationPlan Plan(CanonicalSolution model, CompilationContext context)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(context);

        var steps = new List<PlanStep>
        {
            new("read-source", PlanStepKind.Read, "Read source artifacts into the canonical intermediate representation.", null, true),
            new("validate-ir", PlanStepKind.Validate, "Validate family ownership, dependencies, and environment bindings.", null, true),
            new("emit-tracked-source", PlanStepKind.Emit, "Emit deterministic tracked source from the canonical model.", CapabilityKind.SchemaCore, true)
        };

        if (context.EnablePackaging)
        {
            steps.Add(new("package", PlanStepKind.Package, "Prepare PAC packaging inputs and release artifacts.", CapabilityKind.AppShell, true));
            steps.Add(new("check", PlanStepKind.Check, "Run solution packaging validation and solution-check workflow.", CapabilityKind.AppShell, false));
        }

        if (context.EnableDevApply)
        {
            steps.Add(new("apply-dev", PlanStepKind.Apply, "Apply supported families to Dev for proof.", CapabilityKind.SchemaCore, false));
            steps.Add(new("publish", PlanStepKind.Publish, "Publish changes needed for live proof.", CapabilityKind.ModelDrivenUi, false));
            steps.Add(new("readback", PlanStepKind.Readback, "Read back live Dataverse state.", CapabilityKind.AppShell, false));
            steps.Add(new("compare", PlanStepKind.Compare, "Compare tracked intent to live readback using stable-overlap rules.", CapabilityKind.SchemaDetail, false));
        }

        steps.Add(new("explain", PlanStepKind.Explain, "Produce a human-readable explanation of the plan and boundaries.", null, true));

        var diagnostics = new List<CompilerDiagnostic>();
        if (model.Artifacts.Count == 0)
        {
            diagnostics.Add(new CompilerDiagnostic(
                "planner-no-artifacts",
                DiagnosticSeverity.Warning,
                "The canonical model does not yet contain concrete artifacts; the plan is a bootstrap skeleton."));
        }

        return new CompilationPlan(
            $"Prepared {steps.Count} compiler step(s) for {model.Identity.UniqueName}.",
            steps,
            diagnostics);
    }
}
