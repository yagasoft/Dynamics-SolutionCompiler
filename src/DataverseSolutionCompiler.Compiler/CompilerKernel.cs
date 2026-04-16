using DataverseSolutionCompiler.Domain.Abstractions;
using DataverseSolutionCompiler.Domain.Capabilities;
using DataverseSolutionCompiler.Domain.Compilation;
using DataverseSolutionCompiler.Domain.Diagnostics;
using DataverseSolutionCompiler.Domain.Model;
using DataverseSolutionCompiler.Readers.Intent;
using DataverseSolutionCompiler.Domain.Planning;
using DataverseSolutionCompiler.Domain.Read;
using DataverseSolutionCompiler.Readers.TrackedSource;
using DataverseSolutionCompiler.Readers.Code;
using DataverseSolutionCompiler.Readers.Xml;

namespace DataverseSolutionCompiler.Compiler;

public sealed class CompilerKernel : ICompilerKernel
{
    private readonly ICapabilityRegistry _capabilityRegistry;
    private readonly ICompilationPlanner _planner;
    private readonly ISolutionReader _intentReader;
    private readonly ISolutionReader _trackedSourceReader;
    private readonly ISolutionReader _codeReader;
    private readonly ISolutionReader _xmlReader;

    public CompilerKernel(
        ICapabilityRegistry? capabilityRegistry = null,
        ICompilationPlanner? planner = null,
        ISolutionReader? intentReader = null,
        ISolutionReader? xmlReader = null,
        ISolutionReader? trackedSourceReader = null,
        ISolutionReader? codeReader = null)
    {
        _capabilityRegistry = capabilityRegistry ?? new CapabilityRegistry();
        _planner = planner ?? new CompilationPlanner();
        _intentReader = intentReader ?? new IntentSpecReader();
        _xmlReader = xmlReader ?? new XmlSolutionReader();
        _trackedSourceReader = trackedSourceReader ?? new TrackedSourceReader();
        _codeReader = codeReader ?? new CodeFirstSdkRegistrationReader();
    }

    public CompilationResult Compile(CompilationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.InputPath);

        var diagnostics = new List<CompilerDiagnostic>();
        var capabilities = ResolveCapabilities(request.RequestedCapabilities, diagnostics);
        var context = request.Context ?? CompilationContext.Default;
        var success = true;
        var sourceKind = DetectSourceKind(request.InputPath);
        var solution = ReadSolution(request.InputPath, sourceKind, diagnostics, ref success);
        var plan = _planner.Plan(solution, context);

        diagnostics.AddRange(solution.Diagnostics);
        diagnostics.AddRange(plan.Diagnostics);
        success &= diagnostics.All(diagnostic => diagnostic.Severity is not DiagnosticSeverity.Error);

        return new CompilationResult(
            Success: success,
            Message: success
                ? $"Compiler kernel read {solution.Artifacts.Count} artifact(s) for {solution.Identity.UniqueName}."
                : $"Compiler kernel could not fully read a canonical solution from {request.InputPath}.",
            Solution: solution,
            Plan: plan,
            Capabilities: capabilities,
            Diagnostics: diagnostics);
    }

    private CanonicalSolution ReadSolution(
        string inputPath,
        ReadSourceKind sourceKind,
        ICollection<CompilerDiagnostic> diagnostics,
        ref bool success)
    {
        if (!File.Exists(inputPath) && !Directory.Exists(inputPath))
        {
            diagnostics.Add(new CompilerDiagnostic(
                "path-not-found",
                DiagnosticSeverity.Error,
                $"Path not found: {inputPath}",
                inputPath));
            success = false;
            return CreateEmptySolution(inputPath);
        }

        try
        {
            diagnostics.Add(new CompilerDiagnostic(
                "source-kind-detected",
                DiagnosticSeverity.Info,
                $"Compiler kernel detected source kind: {sourceKind}.",
                inputPath));

            return SelectReader(sourceKind).Read(new ReadRequest(inputPath, sourceKind));
        }
        catch (Exception exception) when (exception is DirectoryNotFoundException or FileNotFoundException or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            diagnostics.Add(new CompilerDiagnostic(
                "source-read-failure",
                DiagnosticSeverity.Error,
                $"Compiler kernel could not read source input '{inputPath}': {exception.Message}",
                inputPath));
            success = false;
            return CreateEmptySolution(inputPath);
        }
    }

    private ISolutionReader SelectReader(ReadSourceKind sourceKind) =>
        sourceKind switch
        {
            ReadSourceKind.IntentSpecJson => _intentReader,
            ReadSourceKind.TrackedSource => _trackedSourceReader,
            ReadSourceKind.CodeFirstSdkRegistration => _codeReader,
            ReadSourceKind.UnpackedXmlFolder or ReadSourceKind.PackedZip or ReadSourceKind.Auto => _xmlReader,
            _ => _xmlReader
        };

    private static ReadSourceKind DetectSourceKind(string inputPath)
    {
        if (File.Exists(inputPath) && string.Equals(Path.GetExtension(inputPath), ".json", StringComparison.OrdinalIgnoreCase))
        {
            return ReadSourceKind.IntentSpecJson;
        }

        if (File.Exists(inputPath) && string.Equals(Path.GetExtension(inputPath), ".zip", StringComparison.OrdinalIgnoreCase))
        {
            return ReadSourceKind.PackedZip;
        }

        if (File.Exists(inputPath)
            && (string.Equals(Path.GetExtension(inputPath), ".csproj", StringComparison.OrdinalIgnoreCase)
                || string.Equals(Path.GetExtension(inputPath), ".cs", StringComparison.OrdinalIgnoreCase))
            && CodeFirstSdkRegistrationReader.IsProbableCodeFirstRegistrationRoot(inputPath))
        {
            return ReadSourceKind.CodeFirstSdkRegistration;
        }

        if (!Directory.Exists(inputPath))
        {
            return ReadSourceKind.Auto;
        }

        if (File.Exists(Path.Combine(inputPath, "manifest.json"))
            && File.Exists(Path.Combine(inputPath, "solution", "manifest.json")))
        {
            return ReadSourceKind.TrackedSource;
        }

        if (CodeFirstSdkRegistrationReader.IsProbableCodeFirstRegistrationRoot(inputPath))
        {
            return ReadSourceKind.CodeFirstSdkRegistration;
        }

        return ReadSourceKind.UnpackedXmlFolder;
    }

    private static CanonicalSolution CreateEmptySolution(string inputPath)
    {
        var solutionName = BuildSolutionName(inputPath);
        return new CanonicalSolution(
            new SolutionIdentity(solutionName, solutionName, "0.0.0", LayeringIntent.Hybrid),
            new PublisherDefinition("dsc", "dsc", "dsc", "Dataverse Solution Compiler"),
            [],
            [],
            [],
            []);
    }

    private static string BuildSolutionName(string inputPath)
    {
        var rawName = Directory.Exists(inputPath)
            ? new DirectoryInfo(inputPath).Name
            : Path.GetFileNameWithoutExtension(inputPath);

        var normalized = new string(rawName.Where(ch => char.IsLetterOrDigit(ch) || ch == '_').ToArray());
        return string.IsNullOrWhiteSpace(normalized) ? "dataversesolution" : normalized.ToLowerInvariant();
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
