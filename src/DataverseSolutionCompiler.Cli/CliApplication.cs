using DataverseSolutionCompiler.Domain.Apply;
using DataverseSolutionCompiler.Domain.Compilation;
using DataverseSolutionCompiler.Domain.Diagnostics;
using DataverseSolutionCompiler.Domain.Diff;
using DataverseSolutionCompiler.Domain.Emission;
using DataverseSolutionCompiler.Domain.Explanations;
using DataverseSolutionCompiler.Domain.Live;
using DataverseSolutionCompiler.Domain.Model;
using DataverseSolutionCompiler.Domain.Packaging;
using DataverseSolutionCompiler.Domain.Planning;
using DataverseSolutionCompiler.Domain.Workflows;
using DataverseSolutionCompiler.Emitters.TrackedSource;
using System.Xml.Linq;

namespace DataverseSolutionCompiler.Cli;

public static class CliApplication
{
    private static readonly string[] RegisteredCommands =
    [
        "read",
        "plan",
        "emit",
        "apply-dev",
        "readback",
        "diff",
        "pack",
        "import",
        "publish",
        "check",
        "doctor",
        "explain"
    ];

    public static int Run(string[] args, TextWriter output, TextWriter error) =>
        Run(args, output, error, CompilerCliRuntime.CreateDefault());

    internal static int Run(string[] args, TextWriter output, TextWriter error, CompilerCliRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(runtime);

        if (args.Length == 0 || IsHelp(args))
        {
            WriteHelp(output);
            return 0;
        }

        CliCommandOptions options;
        try
        {
            options = CliCommandOptions.Parse(args);
        }
        catch (ArgumentException exception)
        {
            error.WriteLine(exception.Message);
            error.WriteLine("Use --help to see the registered compiler commands and flags.");
            return 1;
        }

        try
        {
            return options.Command switch
            {
                "read" => RunRead(options, output, error, runtime),
                "plan" => RunPlan(options, output, error, runtime),
                "emit" => RunEmit(options, output, error, runtime),
                "apply-dev" => RunApplyDev(options, output, error, runtime),
                "readback" => RunReadback(options, output, error, runtime),
                "diff" => RunDiff(options, output, error, runtime),
                "pack" => RunPack(options, output, error, runtime, forceSolutionCheck: false),
                "import" => RunImport(options, output, error, runtime),
                "publish" => RunPublish(options, output, error, runtime),
                "check" => RunPack(options, output, error, runtime, forceSolutionCheck: true),
                "doctor" => RunDoctor(options, output, error, runtime),
                "explain" => RunExplain(options, output, error, runtime),
                _ => WriteUnknownCommand(error, options.Command)
            };
        }
        catch (ArgumentException exception)
        {
            error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static int RunRead(CliCommandOptions options, TextWriter output, TextWriter error, CompilerCliRuntime runtime)
    {
        var result = CompileSource(options, runtime);
        output.WriteLine("Dataverse Solution Compiler");
        output.WriteLine(result.Message);
        output.WriteLine($"Solution: {result.Solution.Identity.UniqueName}");
        output.WriteLine($"Artifacts: {result.Solution.Artifacts.Count}");
        output.WriteLine($"Capabilities: {result.Capabilities.Count}");
        WriteDiagnostics(output, error, result.Diagnostics);
        return result.Success ? 0 : 1;
    }

    private static int RunPlan(CliCommandOptions options, TextWriter output, TextWriter error, CompilerCliRuntime runtime)
    {
        var result = CompileSource(options, runtime);
        output.WriteLine(result.Plan.Summary);
        foreach (var step in result.Plan.Steps)
        {
            output.WriteLine($"- {step.Id} [{step.Kind}]: {step.Description}");
        }

        WriteDiagnostics(output, error, result.Diagnostics);
        return result.Success ? 0 : 1;
    }

    private static int RunEmit(CliCommandOptions options, TextWriter output, TextWriter error, CompilerCliRuntime runtime)
    {
        var result = CompileSource(options, runtime);
        if (!result.Success)
        {
            WriteDiagnostics(output, error, result.Diagnostics);
            return 1;
        }

        var outputRoot = ResolveOutputRoot(options);
        var emitter = options.Layout switch
        {
            EmitLayout.PackageInputs => runtime.PackageEmitter,
            EmitLayout.IntentSpec => new IntentSpecEmitter(),
            _ => runtime.TrackedSourceEmitter
        };
        var emitted = emitter.Emit(result.Solution, new EmitRequest(outputRoot, options.Layout));

        output.WriteLine($"Emit layout: {options.Layout}");
        output.WriteLine($"Output root: {Path.GetFullPath(outputRoot)}");
        output.WriteLine($"Files written: {emitted.Files.Count}");
        WriteDiagnostics(output, error, result.Diagnostics.Concat(emitted.Diagnostics));
        return emitted.Success && !HasErrors(emitted.Diagnostics) ? 0 : 1;
    }

    private static int RunApplyDev(CliCommandOptions options, TextWriter output, TextWriter error, CompilerCliRuntime runtime)
    {
        output.WriteLine("Apply-dev");
        var workflow = runtime.ResolveDevApplyWorkflowRunner().RunDevApply(CreateDevApplyWorkflowRequest(options));
        WriteWorkflowStages(output, workflow.Stages);
        output.WriteLine($"Verification families: {workflow.VerificationFamilies.Count}");
        output.WriteLine($"Applied families: {workflow.Apply?.AppliedFamilies.Count ?? 0}");
        if (workflow.Apply is not null && workflow.Apply.AppliedFamilies.Count > 0)
        {
            output.WriteLine($"Applied family names: {string.Join(", ", workflow.Apply.AppliedFamilies)}");
        }

        output.WriteLine($"Live artifacts: {workflow.Snapshot?.Artifacts.Count ?? 0}");
        output.WriteLine($"Findings: {workflow.Diff?.Findings.Count ?? 0}");
        WriteDriftFindings(output, workflow.Diff);
        WriteDiagnostics(output, error, workflow.Diagnostics);
        return workflow.Success ? 0 : 1;
    }

    private static int RunReadback(CliCommandOptions options, TextWriter output, TextWriter error, CompilerCliRuntime runtime)
    {
        var result = CompileSource(options, runtime, enableDevApply: true);
        if (!result.Success)
        {
            WriteDiagnostics(output, error, result.Diagnostics);
            return 1;
        }

        var snapshot = runtime.LiveSnapshotProvider.Readback(CreateReadbackRequest(options, result.Solution));
        output.WriteLine("Readback");
        output.WriteLine($"Live artifacts: {snapshot.Artifacts.Count}");
        output.WriteLine($"Diagnostics: {snapshot.Diagnostics.Count}");
        WriteDiagnostics(output, error, result.Diagnostics.Concat(snapshot.Diagnostics));
        return HasErrors(snapshot.Diagnostics) ? 1 : 0;
    }

    private static int RunDiff(CliCommandOptions options, TextWriter output, TextWriter error, CompilerCliRuntime runtime)
    {
        var result = CompileSource(options, runtime, enableDevApply: true);
        if (!result.Success)
        {
            WriteDiagnostics(output, error, result.Diagnostics);
            return 1;
        }

        var snapshot = runtime.LiveSnapshotProvider.Readback(CreateReadbackRequest(options, result.Solution));
        var report = runtime.DriftComparer.Compare(result.Solution, snapshot, new CompareRequest(options.IncludeBestEffortFamilies));

        output.WriteLine("Diff");
        output.WriteLine($"Findings: {report.Findings.Count}");
        WriteDriftFindings(output, report);

        WriteDiagnostics(output, error, result.Diagnostics.Concat(snapshot.Diagnostics).Concat(report.Diagnostics));
        return HasErrors(snapshot.Diagnostics) || report.HasBlockingDrift ? 1 : 0;
    }

    private static int RunPack(CliCommandOptions options, TextWriter output, TextWriter error, CompilerCliRuntime runtime, bool forceSolutionCheck)
    {
        var workflow = runtime.ResolvePackageBuildWorkflowRunner().RunPackageBuild(CreatePackageBuildWorkflowRequest(options, forceSolutionCheck));
        if (!workflow.Compilation.Success
            || HasErrors(workflow.Compilation.Diagnostics)
            || workflow.PackageInputs is null
            || !workflow.PackageInputs.Success
            || HasErrors(workflow.PackageInputs.Diagnostics))
        {
            WriteDiagnostics(output, error, workflow.Diagnostics);
            return 1;
        }

        output.WriteLine(forceSolutionCheck ? "Check" : "Pack");
        output.WriteLine($"Package root: {Path.GetFullPath(workflow.PackageInputRoot)}");
        output.WriteLine($"Package path: {workflow.Package?.PackagePath ?? "(not created)"}");
        WriteDiagnostics(output, error, workflow.Diagnostics);
        return workflow.Success ? 0 : 1;
    }

    private static int RunImport(CliCommandOptions options, TextWriter output, TextWriter error, CompilerCliRuntime runtime)
    {
        var environment = CreateEnvironmentProfile(options, requireUrl: true, "import", isDevelopment: false);
        var importResult = runtime.ImportExecutor.Import(new ImportRequest(
            environment,
            Path.GetFullPath(options.TargetPath),
            options.PublishAfterImport));

        output.WriteLine("Import");
        output.WriteLine($"Package path: {Path.GetFullPath(options.TargetPath)}");
        output.WriteLine($"Published: {importResult.Published}");
        WriteDiagnostics(output, error, importResult.Diagnostics);
        return importResult.Success && !HasErrors(importResult.Diagnostics) ? 0 : 1;
    }

    private static int RunPublish(CliCommandOptions options, TextWriter output, TextWriter error, CompilerCliRuntime runtime)
    {
        var workflow = runtime.ResolvePublishWorkflowRunner().RunPublish(CreatePublishWorkflowRequest(options));
        output.WriteLine("Publish");
        WriteWorkflowStages(output, workflow.Stages);
        output.WriteLine($"Package path: {workflow.Package?.PackagePath ?? "(not created)"}");
        output.WriteLine($"Import skipped: {workflow.ImportSkippedBecauseApplyOnly}");
        output.WriteLine($"Published: {workflow.Import?.Published ?? false}");
        output.WriteLine($"Applied families: {workflow.FinalizeApply?.AppliedFamilies.Count ?? 0}");
        if (workflow.FinalizeApply is not null && workflow.FinalizeApply.AppliedFamilies.Count > 0)
        {
            output.WriteLine($"Applied family names: {string.Join(", ", workflow.FinalizeApply.AppliedFamilies)}");
        }

        WriteDiagnostics(output, error, workflow.Diagnostics);
        return workflow.Success ? 0 : 1;
    }

    private static int RunDoctor(CliCommandOptions options, TextWriter output, TextWriter error, CompilerCliRuntime runtime)
    {
        var result = CompileSource(options, runtime, enableDevApply: true);
        output.WriteLine("Compiler doctor");
        output.WriteLine($"Capabilities: {result.Capabilities.Count}");
        output.WriteLine($"Diagnostics: {result.Diagnostics.Count}");
        WriteDiagnostics(output, error, result.Diagnostics);
        return result.Success ? 0 : 1;
    }

    private static int RunExplain(CliCommandOptions options, TextWriter output, TextWriter error, CompilerCliRuntime runtime)
    {
        var result = CompileSource(options, runtime, enableDevApply: true);
        var report = runtime.ExplanationService.Explain(result);

        output.WriteLine(report.Title);
        foreach (var section in report.Sections)
        {
            output.WriteLine(section);
        }

        WriteDiagnostics(output, error, result.Diagnostics.Concat(report.Diagnostics));
        return result.Success ? 0 : 1;
    }

    private static CompilationResult CompileSource(CliCommandOptions options, CompilerCliRuntime runtime, bool enableDevApply = false) =>
        runtime.Kernel.Compile(CreateCompilationRequest(options, enableDevApply));

    private static CompilationRequest CreateCompilationRequest(
        CliCommandOptions options,
        bool enableDevApply = false,
        bool requireEnvironmentUrl = false) =>
        new(
            options.TargetPath,
            options.RequestedCapabilities,
            new CompilationContext(
                CreateEnvironmentProfile(options, requireUrl: requireEnvironmentUrl, options.Command, enableDevApply),
                EnablePackaging: true,
                EnableDevApply: enableDevApply,
                IncludeBestEffortFamilies: options.IncludeBestEffortFamilies));

    private static DevApplyWorkflowRequest CreateDevApplyWorkflowRequest(CliCommandOptions options) =>
        new(
            CreateCompilationRequest(options, enableDevApply: true, requireEnvironmentUrl: true),
            options.SolutionUniqueName,
            new CompareRequest(options.IncludeBestEffortFamilies));

    private static PackageBuildWorkflowRequest CreatePackageBuildWorkflowRequest(
        CliCommandOptions options,
        bool forceSolutionCheck) =>
        new(
            CreateCompilationRequest(options),
            ResolveOutputRoot(options),
            options.Flavor,
            forceSolutionCheck || options.RunSolutionCheck);

    private static PublishWorkflowRequest CreatePublishWorkflowRequest(CliCommandOptions options) =>
        new(
            CreateCompilationRequest(options, enableDevApply: true, requireEnvironmentUrl: true),
            ResolveOutputRoot(options),
            CreateEnvironmentProfile(options, requireUrl: true, "publish", isDevelopment: false),
            CreateEnvironmentProfile(options, requireUrl: true, "publish", isDevelopment: true),
            options.Flavor,
            options.RunSolutionCheck);

    private static ReadbackRequest CreateReadbackRequest(CliCommandOptions options, CanonicalSolution solution)
    {
        var families = solution.Artifacts
            .Select(artifact => artifact.Family)
            .Distinct()
            .ToArray();

        return new ReadbackRequest(
            CreateEnvironmentProfile(options, requireUrl: true, options.Command, isDevelopment: false),
            options.SolutionUniqueName ?? solution.Identity.UniqueName,
            families.Length == 0 ? null : families);
    }

    private static EnvironmentProfile CreateEnvironmentProfile(CliCommandOptions options, bool requireUrl, string name, bool isDevelopment)
    {
        Uri? dataverseUrl = null;
        if (!string.IsNullOrWhiteSpace(options.EnvironmentUrl))
        {
            if (!Uri.TryCreate(options.EnvironmentUrl, UriKind.Absolute, out dataverseUrl))
            {
                throw new ArgumentException($"The value for --environment must be an absolute Dataverse URL. Received: {options.EnvironmentUrl}");
            }
        }
        else if (requireUrl)
        {
            throw new ArgumentException($"The '{options.Command}' command requires --environment <absolute Dataverse URL>.");
        }

        return new EnvironmentProfile(
            name,
            dataverseUrl,
            string.IsNullOrWhiteSpace(options.TenantId) ? null : options.TenantId,
            isDevelopment);
    }

    private static bool HasErrors(IEnumerable<CompilerDiagnostic> diagnostics) =>
        diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);

    private static void WriteWorkflowStages(TextWriter output, IEnumerable<WorkflowStageResult> stages)
    {
        output.WriteLine("Stages:");
        foreach (var stage in stages)
        {
            output.WriteLine($"- {stage.Stage}: {stage.Status} - {stage.Summary}");
        }
    }

    private static void WriteDriftFindings(TextWriter output, DriftReport? report)
    {
        if (report is null)
        {
            return;
        }

        foreach (var finding in report.Findings)
        {
            output.WriteLine($"- {finding.Severity} [{finding.Category}] {finding.Family}: {finding.Description}");
        }
    }

    private static string ResolveOutputRoot(CliCommandOptions options) =>
        Path.GetFullPath(string.IsNullOrWhiteSpace(options.OutputRoot)
            ? Path.Combine(Environment.CurrentDirectory, "artifacts")
            : options.OutputRoot);

    private static void WriteDiagnostics(TextWriter output, TextWriter error, IEnumerable<CompilerDiagnostic> diagnostics)
    {
        foreach (var diagnostic in diagnostics)
        {
            var writer = diagnostic.Severity == DiagnosticSeverity.Error ? error : output;
            writer.WriteLine($"- {diagnostic.Severity}: {diagnostic.Message}");
        }
    }

    private static bool IsHelp(IReadOnlyList<string> args) =>
        args.Any(arg => string.Equals(arg, "--help", StringComparison.OrdinalIgnoreCase)
            || string.Equals(arg, "-h", StringComparison.OrdinalIgnoreCase));

    private static int WriteUnknownCommand(TextWriter error, string command)
    {
        error.WriteLine($"Unknown command: {command}");
        error.WriteLine("Use --help to see the registered compiler commands.");
        return 1;
    }

    private static void WriteHelp(TextWriter output)
    {
        output.WriteLine("Dataverse Solution Compiler");
        output.WriteLine("Registered commands:");
        foreach (var command in RegisteredCommands)
        {
            output.WriteLine($"  {command} <path> [capabilities...] [options]");
        }

        output.WriteLine("Common options:");
        output.WriteLine("  --output <path>");
        output.WriteLine("  --layout tracked-source|intent-spec|package-inputs");
        output.WriteLine("  --environment <absolute Dataverse URL>");
        output.WriteLine("  --tenant <tenant id>");
        output.WriteLine("  --solution <solution unique name>");
        output.WriteLine("  --managed | --unmanaged");
        output.WriteLine("  --run-solution-check");
        output.WriteLine("  --publish-after-import");
        output.WriteLine("  --include-best-effort");
    }

    internal sealed record CliCommandOptions(
        string Command,
        string TargetPath,
        IReadOnlyList<string> RequestedCapabilities,
        string? OutputRoot,
        EmitLayout Layout,
        string? EnvironmentUrl,
        string? TenantId,
        string? SolutionUniqueName,
        PackageFlavor Flavor,
        bool RunSolutionCheck,
        bool PublishAfterImport,
        bool IncludeBestEffortFamilies)
    {
        public static CliCommandOptions Parse(string[] args)
        {
            var command = args[0].ToLowerInvariant();
            string? targetPath = null;
            string? outputRoot = null;
            string? environmentUrl = null;
            string? tenantId = null;
            string? solutionUniqueName = null;
            var layout = EmitLayout.TrackedSource;
            var flavor = PackageFlavor.Unmanaged;
            var runSolutionCheck = false;
            var publishAfterImport = false;
            var includeBestEffortFamilies = false;
            var capabilities = new List<string>();

            for (var index = 1; index < args.Length; index++)
            {
                var argument = args[index];
                if (!argument.StartsWith("--", StringComparison.Ordinal))
                {
                    if (targetPath is null)
                    {
                        targetPath = argument;
                    }
                    else
                    {
                        capabilities.Add(argument);
                    }

                    continue;
                }

                switch (argument.ToLowerInvariant())
                {
                    case "--output":
                        outputRoot = ConsumeValue(args, ref index, argument);
                        break;
                    case "--layout":
                        layout = ParseLayout(ConsumeValue(args, ref index, argument));
                        break;
                    case "--environment":
                        environmentUrl = ConsumeValue(args, ref index, argument);
                        break;
                    case "--tenant":
                        tenantId = ConsumeValue(args, ref index, argument);
                        break;
                    case "--solution":
                        solutionUniqueName = ConsumeValue(args, ref index, argument);
                        break;
                    case "--managed":
                        flavor = PackageFlavor.Managed;
                        break;
                    case "--unmanaged":
                        flavor = PackageFlavor.Unmanaged;
                        break;
                    case "--run-solution-check":
                        runSolutionCheck = true;
                        break;
                    case "--publish-after-import":
                        publishAfterImport = true;
                        break;
                    case "--include-best-effort":
                        includeBestEffortFamilies = true;
                        break;
                    default:
                        throw new ArgumentException($"Unknown option: {argument}");
                }
            }

            return new CliCommandOptions(
                command,
                targetPath ?? ".",
                capabilities,
                outputRoot,
                layout,
                environmentUrl,
                tenantId,
                solutionUniqueName,
                flavor,
                runSolutionCheck,
                publishAfterImport,
                includeBestEffortFamilies);
        }

        private static string ConsumeValue(IReadOnlyList<string> args, ref int index, string option)
        {
            if (index + 1 >= args.Count || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Option {option} requires a value.");
            }

            index++;
            return args[index];
        }

        private static EmitLayout ParseLayout(string value) =>
            value.ToLowerInvariant() switch
            {
                "tracked-source" => EmitLayout.TrackedSource,
                "intent-spec" => EmitLayout.IntentSpec,
                "package-inputs" => EmitLayout.PackageInputs,
                _ => throw new ArgumentException($"Unknown emit layout: {value}")
            };
    }
}
