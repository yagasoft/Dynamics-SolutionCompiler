using DataverseSolutionCompiler.Compiler;
using DataverseSolutionCompiler.Domain.Compilation;
using DataverseSolutionCompiler.Domain.Explanations;

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

    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        if (args.Length == 0 || IsHelp(args))
        {
            WriteHelp(output);
            return 0;
        }

        var command = args[0].ToLowerInvariant();
        var target = args.Length > 1 ? args[1] : ".";
        var requestedCapabilities = ParseRequestedCapabilities(args.Skip(2));

        var kernel = new CompilerKernel();
        var result = kernel.Compile(new CompilationRequest(target, requestedCapabilities));
        var explanationService = new ExplanationService();

        return command switch
        {
            "read" => WriteReadResult(output, result),
            "plan" => WritePlanResult(output, result),
            "emit" => WriteEmitResult(output, result),
            "apply-dev" => WriteApplyDevResult(output, result),
            "readback" => WriteReadbackResult(output, result),
            "diff" => WriteDiffResult(output, result),
            "pack" => WritePackResult(output, result),
            "import" => WriteImportResult(output, result),
            "publish" => WritePublishResult(output, result),
            "check" => WriteCheckResult(output, result),
            "doctor" => WriteDoctorResult(output, result),
            "explain" => WriteExplainResult(output, explanationService.Explain(result)),
            _ => WriteUnknownCommand(error, command)
        };
    }

    private static bool IsHelp(IReadOnlyList<string> args) =>
        args.Any(arg => string.Equals(arg, "--help", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(arg, "-h", StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<string> ParseRequestedCapabilities(IEnumerable<string> args) =>
        args.Where(arg => !string.IsNullOrWhiteSpace(arg))
            .Select(arg => arg.Trim())
            .ToArray();

    private static int WriteReadResult(TextWriter output, CompilationResult result)
    {
        output.WriteLine("Dataverse Solution Compiler");
        output.WriteLine(result.Message);
        output.WriteLine($"Solution: {result.Solution.Identity.UniqueName}");
        output.WriteLine($"Artifacts: {result.Solution.Artifacts.Count}");
        return 0;
    }

    private static int WritePlanResult(TextWriter output, CompilationResult result)
    {
        output.WriteLine(result.Plan.Summary);
        foreach (var step in result.Plan.Steps)
        {
            output.WriteLine($"- {step.Id} [{step.Kind}]: {step.Description}");
        }

        return 0;
    }

    private static int WriteEmitResult(TextWriter output, CompilationResult result)
    {
        output.WriteLine("Emit command registered.");
        output.WriteLine($"Tracked source and packaging will be emitted from {result.Solution.Identity.UniqueName} when emitters are wired.");
        return 0;
    }

    private static int WriteApplyDevResult(TextWriter output, CompilationResult result)
    {
        output.WriteLine("Apply-dev command registered.");
        output.WriteLine($"No live mutation executed. Planned artifacts: {result.Solution.Artifacts.Count}");
        return 0;
    }

    private static int WriteReadbackResult(TextWriter output, CompilationResult result)
    {
        output.WriteLine("Readback command registered.");
        output.WriteLine($"Expected capability slices: {result.Capabilities.Count}");
        return 0;
    }

    private static int WriteDiffResult(TextWriter output, CompilationResult result)
    {
        output.WriteLine("Diff command registered.");
        output.WriteLine("Stable-overlap drift comparison is available through the compiler contracts.");
        output.WriteLine($"Bootstrap plan steps: {result.Plan.Steps.Count}");
        return 0;
    }

    private static int WritePackResult(TextWriter output, CompilationResult result)
    {
        output.WriteLine("Pack command registered.");
        output.WriteLine($"PAC packaging adapter will package {result.Solution.Identity.UniqueName}.");
        return 0;
    }

    private static int WriteImportResult(TextWriter output, CompilationResult result)
    {
        output.WriteLine("Import command registered.");
        output.WriteLine($"No package import executed for {result.Solution.Identity.UniqueName} in bootstrap mode.");
        return 0;
    }

    private static int WritePublishResult(TextWriter output, CompilationResult result)
    {
        output.WriteLine("Publish command registered.");
        output.WriteLine($"Publish remains a release-pipeline step for {result.Solution.Identity.UniqueName}.");
        return 0;
    }

    private static int WriteCheckResult(TextWriter output, CompilationResult result)
    {
        output.WriteLine("Check command registered.");
        output.WriteLine($"Solution-check is part of the packaging path for {result.Solution.Identity.UniqueName}.");
        return 0;
    }

    private static int WriteDoctorResult(TextWriter output, CompilationResult result)
    {
        output.WriteLine("Compiler doctor");
        output.WriteLine($"Capabilities: {result.Capabilities.Count}");
        output.WriteLine($"Diagnostics: {result.Diagnostics.Count}");
        foreach (var diagnostic in result.Diagnostics)
        {
            output.WriteLine($"- {diagnostic.Severity}: {diagnostic.Message}");
        }

        return 0;
    }

    private static int WriteExplainResult(TextWriter output, HumanReport report)
    {
        output.WriteLine(report.Title);
        foreach (var section in report.Sections)
        {
            output.WriteLine(section);
        }

        return 0;
    }

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
            output.WriteLine($"  {command} <path> [capabilities...]");
        }
    }
}
