using DataverseSolutionCompiler.Apply;
using DataverseSolutionCompiler.Compiler;
using DataverseSolutionCompiler.Diff;
using DataverseSolutionCompiler.Domain.Abstractions;
using DataverseSolutionCompiler.Domain.Apply;
using DataverseSolutionCompiler.Domain.Build;
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
using DataverseSolutionCompiler.Readers.Live;
using System.Xml.Linq;

namespace DataverseSolutionCompiler.Agent;

public sealed class AgentOrchestrator : IDevApplyWorkflowRunner, IPackageBuildWorkflowRunner, IPublishWorkflowRunner
{
    private readonly ICompilerKernel _kernel;
    private readonly IExplanationService _explanationService;
    private readonly IApplyExecutor _applyExecutor;
    private readonly ICodeAssetBuilder _codeAssetBuilder;
    private readonly ILiveSnapshotProvider _liveSnapshotProvider;
    private readonly IDriftComparer _driftComparer;
    private readonly ISolutionEmitter? _packageEmitter;
    private readonly IPackageExecutor? _packageExecutor;
    private readonly IImportExecutor? _importExecutor;

    public AgentOrchestrator(
        ICompilerKernel? kernel = null,
        IExplanationService? explanationService = null,
        IApplyExecutor? applyExecutor = null,
        ILiveSnapshotProvider? liveSnapshotProvider = null,
        IDriftComparer? driftComparer = null,
        ISolutionEmitter? packageEmitter = null,
        IPackageExecutor? packageExecutor = null,
        IImportExecutor? importExecutor = null,
        ICodeAssetBuilder? codeAssetBuilder = null)
    {
        _kernel = kernel ?? new CompilerKernel();
        _explanationService = explanationService ?? new ExplanationService();
        _applyExecutor = applyExecutor ?? new WebApiApplyExecutor();
        _codeAssetBuilder = codeAssetBuilder ?? new DotNetCodeAssetBuilder();
        _liveSnapshotProvider = liveSnapshotProvider ?? new WebApiLiveSnapshotProvider();
        _driftComparer = driftComparer ?? new StableOverlapDriftComparer();
        _packageEmitter = packageEmitter;
        _packageExecutor = packageExecutor;
        _importExecutor = importExecutor;
    }

    public HumanReport Analyze(string inputPath, IReadOnlyList<string>? requestedCapabilities = null)
    {
        var result = _kernel.Compile(new CompilationRequest(
            inputPath,
            requestedCapabilities ?? Array.Empty<string>()));

        return _explanationService.Explain(result);
    }

    public DevApplyWorkflowResult RunDevApply(DevApplyWorkflowRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var compilation = _kernel.Compile(request.Compilation);
        var stages = new List<WorkflowStageResult>
        {
            CreateStage(
                WorkflowStageKind.Compile,
                compilation.Success && !HasErrors(compilation.Diagnostics),
                compilation.Message,
                compilation.Diagnostics)
        };
        var aggregatedDiagnostics = new List<CompilerDiagnostic>(compilation.Diagnostics);

        if (!compilation.Success || HasErrors(compilation.Diagnostics))
        {
            AddSkippedStages(stages, WorkflowStageKind.Apply, WorkflowStageKind.Readback, WorkflowStageKind.Diff);
            return new DevApplyWorkflowResult(
                compilation,
                null,
                null,
                null,
                [],
                stages,
                aggregatedDiagnostics);
        }

        var environment = request.Compilation.Context?.Environment ?? CompilationContext.Default.Environment;
        var codeAssetBuild = _codeAssetBuilder.Build(new CodeAssetBuildRequest(
            compilation.Solution,
            Path.Combine(Path.GetTempPath(), $"dsc-code-assets-{Guid.NewGuid():N}")));
        aggregatedDiagnostics.AddRange(codeAssetBuild.Diagnostics);
        if (HasErrors(codeAssetBuild.Diagnostics))
        {
            var boundaryDiagnostic = codeAssetBuild.Diagnostics.FirstOrDefault(diagnostic =>
                diagnostic.Code == "code-asset-build-workflow-activity-package-unsupported");
            stages.Add(new WorkflowStageResult(
                WorkflowStageKind.Apply,
                WorkflowStageStatus.Failed,
                boundaryDiagnostic?.Message ?? "Code asset build failed before live metadata apply.",
                codeAssetBuild.Diagnostics));
            AddSkippedStages(stages, WorkflowStageKind.Readback, WorkflowStageKind.Diff);
            return new DevApplyWorkflowResult(
                compilation,
                new ApplyResult(false, ApplyMode.DevProof, [], codeAssetBuild.Diagnostics),
                null,
                null,
                [],
                stages,
                aggregatedDiagnostics);
        }

        if (!codeAssetBuild.Diagnostics.Any(diagnostic => diagnostic.Code == "code-asset-build-workflow-activity-package-unsupported")
            && ContainsUnsupportedWorkflowActivityPluginPackage(codeAssetBuild.Solution))
        {
            var boundaryDiagnostic = new CompilerDiagnostic(
                "code-asset-build-workflow-activity-package-unsupported",
                DiagnosticSeverity.Error,
                "Code-first plug-in packages that include custom workflow activity types are supported only through the classic assembly lane; apply-dev does not downgrade or silently skip that boundary.");
            aggregatedDiagnostics.Add(boundaryDiagnostic);
            stages.Add(new WorkflowStageResult(
                WorkflowStageKind.Apply,
                WorkflowStageStatus.Failed,
                "Custom workflow activity plug-in packages are supported only through the classic assembly lane.",
                [boundaryDiagnostic]));
            AddSkippedStages(stages, WorkflowStageKind.Readback, WorkflowStageKind.Diff);
            return new DevApplyWorkflowResult(
                compilation,
                new ApplyResult(false, ApplyMode.DevProof, [], [boundaryDiagnostic]),
                null,
                null,
                [],
                stages,
                aggregatedDiagnostics);
        }

        var applyResult = _applyExecutor.Apply(
            codeAssetBuild.Solution,
            new ApplyRequest(environment, ApplyMode.DevProof));
        stages.Add(CreateStage(
            WorkflowStageKind.Apply,
            applyResult.Success && !HasErrors(applyResult.Diagnostics) && !HasErrors(codeAssetBuild.Diagnostics),
            applyResult.AppliedFamilies.Count == 0
                ? "No live metadata apply steps were required for the supported scope."
                : $"Applied {applyResult.AppliedFamilies.Count} supported family update(s).",
            codeAssetBuild.Diagnostics.Concat(applyResult.Diagnostics).ToArray()));
        aggregatedDiagnostics.AddRange(applyResult.Diagnostics);

        if (!applyResult.Success || HasErrors(applyResult.Diagnostics) || HasErrors(codeAssetBuild.Diagnostics))
        {
            AddSkippedStages(stages, WorkflowStageKind.Readback, WorkflowStageKind.Diff);
            return new DevApplyWorkflowResult(
                compilation,
                applyResult,
                null,
                null,
                [],
                stages,
                aggregatedDiagnostics);
        }

        var verificationFamilies = codeAssetBuild.Solution.Artifacts
            .Select(artifact => artifact.Family)
            .Where(family => WebApiApplyExecutor.SupportedDevApplyFamilies.Contains(family))
            .Distinct()
            .ToArray();

        if (verificationFamilies.Length == 0)
        {
            var emptySnapshot = new LiveSnapshot(environment, ResolveSolutionUniqueName(request, compilation.Solution), [], []);
            var emptyDiff = new DriftReport(false, [], []);
            stages.Add(new WorkflowStageResult(
                WorkflowStageKind.Readback,
                WorkflowStageStatus.Succeeded,
                "No apply-supported families were selected for live readback verification.",
                []));
            stages.Add(new WorkflowStageResult(
                WorkflowStageKind.Diff,
                WorkflowStageStatus.Succeeded,
                "No apply-supported families were selected for stable-overlap verification.",
                []));

            return new DevApplyWorkflowResult(
                compilation,
                applyResult,
                emptySnapshot,
                emptyDiff,
                [],
                stages,
                aggregatedDiagnostics);
        }

        var verificationSolution = codeAssetBuild.Solution with
        {
            Artifacts = codeAssetBuild.Solution.Artifacts
                .Where(artifact => verificationFamilies.Contains(artifact.Family))
                .ToArray()
        };

        var snapshot = _liveSnapshotProvider.Readback(new ReadbackRequest(
            environment,
            ResolveSolutionUniqueName(request, compilation.Solution),
            verificationFamilies));
        stages.Add(CreateStage(
            WorkflowStageKind.Readback,
            !HasErrors(snapshot.Diagnostics),
            $"Read back {snapshot.Artifacts.Count} live artifact(s) for the supported apply families.",
            snapshot.Diagnostics));
        aggregatedDiagnostics.AddRange(snapshot.Diagnostics);

        if (HasErrors(snapshot.Diagnostics))
        {
            stages.Add(new WorkflowStageResult(
                WorkflowStageKind.Diff,
                WorkflowStageStatus.Skipped,
                "Skipped stable-overlap diff because live readback failed.",
                []));

            return new DevApplyWorkflowResult(
                compilation,
                applyResult,
                snapshot,
                null,
                verificationFamilies,
                stages,
                aggregatedDiagnostics);
        }

        var driftReport = _driftComparer.Compare(
            verificationSolution,
            snapshot,
            request.Compare ?? new CompareRequest(request.Compilation.Context?.IncludeBestEffortFamilies ?? false));
        stages.Add(new WorkflowStageResult(
            WorkflowStageKind.Diff,
            driftReport.HasBlockingDrift ? WorkflowStageStatus.Failed : WorkflowStageStatus.Succeeded,
            $"Compared {verificationFamilies.Length} supported family scope(s) and found {driftReport.Findings.Count} drift finding(s).",
            driftReport.Diagnostics));
        aggregatedDiagnostics.AddRange(driftReport.Diagnostics);

        return new DevApplyWorkflowResult(
            compilation,
            applyResult,
            snapshot,
            driftReport,
            verificationFamilies,
            stages,
            aggregatedDiagnostics);
    }

    public PackageBuildWorkflowResult RunPackageBuild(PackageBuildWorkflowRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var compilation = _kernel.Compile(request.Compilation);
        var packageInputRoot = Path.Combine(request.OutputRoot, "package-inputs");
        var stages = new List<WorkflowStageResult>
        {
            CreateStage(
                WorkflowStageKind.Compile,
                compilation.Success && !HasErrors(compilation.Diagnostics),
                compilation.Message,
                compilation.Diagnostics)
        };
        var aggregatedDiagnostics = new List<CompilerDiagnostic>(compilation.Diagnostics);

        if (!compilation.Success || HasErrors(compilation.Diagnostics))
        {
            AddSkippedStages(stages, WorkflowStageKind.EmitPackageInputs, WorkflowStageKind.Pack);
            return new PackageBuildWorkflowResult(
                compilation,
                null,
                null,
                request.OutputRoot,
                packageInputRoot,
                stages,
                aggregatedDiagnostics);
        }

        var emitted = RequirePackageEmitter().Emit(compilation.Solution, new EmitRequest(request.OutputRoot, EmitLayout.PackageInputs));
        stages.Add(CreateStage(
            WorkflowStageKind.EmitPackageInputs,
            emitted.Success && !HasErrors(emitted.Diagnostics),
            $"Emitted {emitted.Files.Count} package-input artifact(s).",
            emitted.Diagnostics));
        aggregatedDiagnostics.AddRange(emitted.Diagnostics);

        if (!emitted.Success || HasErrors(emitted.Diagnostics))
        {
            AddSkippedStages(stages, WorkflowStageKind.Pack);
            return new PackageBuildWorkflowResult(
                compilation,
                emitted,
                null,
                request.OutputRoot,
                packageInputRoot,
                stages,
                aggregatedDiagnostics);
        }

        var packageResult = RequirePackageExecutor().Pack(new PackageRequest(
            packageInputRoot,
            request.OutputRoot,
            request.Flavor,
            request.RunSolutionCheck));
        stages.Add(CreateStage(
            WorkflowStageKind.Pack,
            packageResult.Success && !HasErrors(packageResult.Diagnostics) && !string.IsNullOrWhiteSpace(packageResult.PackagePath),
            $"Packed package-inputs to {packageResult.PackagePath ?? "(not created)"}.",
            packageResult.Diagnostics));
        aggregatedDiagnostics.AddRange(packageResult.Diagnostics);

        return new PackageBuildWorkflowResult(
            compilation,
            emitted,
            packageResult,
            request.OutputRoot,
            packageInputRoot,
            stages,
            aggregatedDiagnostics);
    }

    public PublishWorkflowResult RunPublish(PublishWorkflowRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var packageBuild = RunPackageBuild(new PackageBuildWorkflowRequest(
            request.Compilation,
            request.OutputRoot,
            request.Flavor,
            request.RunSolutionCheck));
        var stages = new List<WorkflowStageResult>(packageBuild.Stages);
        var aggregatedDiagnostics = new List<CompilerDiagnostic>(packageBuild.Diagnostics);

        if (!packageBuild.Success || packageBuild.Package is null || string.IsNullOrWhiteSpace(packageBuild.Package.PackagePath))
        {
            AddSkippedStages(stages, WorkflowStageKind.Import, WorkflowStageKind.FinalizeApply);
            return new PublishWorkflowResult(
                packageBuild.Compilation,
                packageBuild.PackageInputs,
                packageBuild.Package,
                null,
                null,
                false,
                request.OutputRoot,
                packageBuild.PackageInputRoot,
                stages,
                aggregatedDiagnostics);
        }

        if (ShouldSkipImportForApplyOnlyPackage(packageBuild.PackageInputRoot))
        {
            var applyOnlyDiagnostics = new[]
            {
                new CompilerDiagnostic(
                    "publish-skip-empty-package-import",
                    DiagnosticSeverity.Info,
                    "Skipped PAC import because the rebuilt package contains no packageable root components. Live apply will create or update the solution shell and finalize apply-only hybrid families directly.",
                    Path.Combine(packageBuild.PackageInputRoot, "Other", "Solution.xml"))
            };

            stages.Add(new WorkflowStageResult(
                WorkflowStageKind.Import,
                WorkflowStageStatus.Skipped,
                "Skipped PAC import because the rebuilt package contains no packageable root components.",
                applyOnlyDiagnostics));
            aggregatedDiagnostics.AddRange(applyOnlyDiagnostics);

            var builtFinalizeSolution = _codeAssetBuilder.Build(new CodeAssetBuildRequest(
                packageBuild.Compilation.Solution,
                Path.Combine(request.OutputRoot, "code-assets"),
                "Release"));
            aggregatedDiagnostics.AddRange(builtFinalizeSolution.Diagnostics);
            if (HasErrors(builtFinalizeSolution.Diagnostics))
            {
                var summary = builtFinalizeSolution.Diagnostics
                    .FirstOrDefault(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)?.Message
                    ?? "Skipped live finalize-apply because staged code-first build failed.";
                stages.Add(new WorkflowStageResult(
                    WorkflowStageKind.FinalizeApply,
                    WorkflowStageStatus.Failed,
                    summary,
                    builtFinalizeSolution.Diagnostics));

                return new PublishWorkflowResult(
                    packageBuild.Compilation,
                    packageBuild.PackageInputs,
                    packageBuild.Package,
                    null,
                    null,
                    true,
                    request.OutputRoot,
                    packageBuild.PackageInputRoot,
                    stages,
                    aggregatedDiagnostics);
            }

            var finalizeApply = _applyExecutor.Apply(
                builtFinalizeSolution.Solution,
                new ApplyRequest(request.FinalizeApplyEnvironment, ApplyMode.DevProof));
            stages.Add(CreateStage(
                WorkflowStageKind.FinalizeApply,
                finalizeApply.Success && !HasErrors(finalizeApply.Diagnostics) && !HasErrors(builtFinalizeSolution.Diagnostics),
                finalizeApply.AppliedFamilies.Count == 0
                    ? "No live finalize-apply steps were required after skipping import."
                    : $"Finalized {finalizeApply.AppliedFamilies.Count} supported family update(s) after skipping import.",
                builtFinalizeSolution.Diagnostics.Concat(finalizeApply.Diagnostics).ToArray()));
            aggregatedDiagnostics.AddRange(finalizeApply.Diagnostics);

            return new PublishWorkflowResult(
                packageBuild.Compilation,
                packageBuild.PackageInputs,
                packageBuild.Package,
                null,
                finalizeApply,
                true,
                request.OutputRoot,
                packageBuild.PackageInputRoot,
                stages,
                aggregatedDiagnostics);
        }

        var importResult = RequireImportExecutor().Import(new ImportRequest(
            request.ImportEnvironment,
            packageBuild.Package.PackagePath,
            PublishAfterImport: true));
        stages.Add(CreateStage(
            WorkflowStageKind.Import,
            importResult.Success && !HasErrors(importResult.Diagnostics),
            $"Imported package. Published after import: {importResult.Published}.",
            importResult.Diagnostics));
        aggregatedDiagnostics.AddRange(importResult.Diagnostics);

        if (!importResult.Success || HasErrors(importResult.Diagnostics))
        {
            AddSkippedStages(stages, WorkflowStageKind.FinalizeApply);
            return new PublishWorkflowResult(
                packageBuild.Compilation,
                packageBuild.PackageInputs,
                packageBuild.Package,
                importResult,
                null,
                false,
                request.OutputRoot,
                packageBuild.PackageInputRoot,
                stages,
                aggregatedDiagnostics);
        }

        var builtImportSolution = _codeAssetBuilder.Build(new CodeAssetBuildRequest(
            packageBuild.Compilation.Solution,
            Path.Combine(request.OutputRoot, "code-assets"),
            "Release"));
        aggregatedDiagnostics.AddRange(builtImportSolution.Diagnostics);
        if (HasErrors(builtImportSolution.Diagnostics))
        {
            var summary = builtImportSolution.Diagnostics
                .FirstOrDefault(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)?.Message
                ?? "Skipped live finalize-apply because staged code-first build failed.";
            stages.Add(new WorkflowStageResult(
                WorkflowStageKind.FinalizeApply,
                WorkflowStageStatus.Failed,
                summary,
                builtImportSolution.Diagnostics));

            return new PublishWorkflowResult(
                packageBuild.Compilation,
                packageBuild.PackageInputs,
                packageBuild.Package,
                importResult,
                null,
                false,
                request.OutputRoot,
                packageBuild.PackageInputRoot,
                stages,
                aggregatedDiagnostics);
        }

        var applyResult = _applyExecutor.Apply(
            builtImportSolution.Solution,
            new ApplyRequest(request.FinalizeApplyEnvironment, ApplyMode.DevProof));
        stages.Add(CreateStage(
            WorkflowStageKind.FinalizeApply,
            applyResult.Success && !HasErrors(applyResult.Diagnostics) && !HasErrors(builtImportSolution.Diagnostics),
            applyResult.AppliedFamilies.Count == 0
                ? "No live finalize-apply steps were required after import."
                : $"Finalized {applyResult.AppliedFamilies.Count} supported family update(s) after import.",
            builtImportSolution.Diagnostics.Concat(applyResult.Diagnostics).ToArray()));
        aggregatedDiagnostics.AddRange(applyResult.Diagnostics);

        return new PublishWorkflowResult(
            packageBuild.Compilation,
            packageBuild.PackageInputs,
            packageBuild.Package,
            importResult,
            applyResult,
            false,
            request.OutputRoot,
            packageBuild.PackageInputRoot,
            stages,
            aggregatedDiagnostics);
    }

    private ISolutionEmitter RequirePackageEmitter() =>
        _packageEmitter ?? throw new InvalidOperationException("Package workflow execution requires a package emitter.");

    private IPackageExecutor RequirePackageExecutor() =>
        _packageExecutor ?? throw new InvalidOperationException("Package workflow execution requires a package executor.");

    private IImportExecutor RequireImportExecutor() =>
        _importExecutor ?? throw new InvalidOperationException("Publish workflow execution requires an import executor.");

    private static string ResolveSolutionUniqueName(DevApplyWorkflowRequest request, CanonicalSolution solution) =>
        string.IsNullOrWhiteSpace(request.SolutionUniqueName)
            ? solution.Identity.UniqueName
            : request.SolutionUniqueName;

    private static WorkflowStageResult CreateStage(
        WorkflowStageKind stage,
        bool succeeded,
        string summary,
        IReadOnlyList<CompilerDiagnostic> diagnostics) =>
        new(
            stage,
            succeeded ? WorkflowStageStatus.Succeeded : WorkflowStageStatus.Failed,
            summary,
            diagnostics);

    private static void AddSkippedStages(ICollection<WorkflowStageResult> stages, params WorkflowStageKind[] kinds)
    {
        foreach (var kind in kinds)
        {
            stages.Add(new WorkflowStageResult(
                kind,
                WorkflowStageStatus.Skipped,
                $"Skipped {kind.ToString().ToLowerInvariant()} because an earlier stage failed.",
                []));
        }
    }

    private static bool ContainsUnsupportedWorkflowActivityPluginPackage(CanonicalSolution solution)
    {
        var packageAssemblies = solution.Artifacts
            .Where(artifact => artifact.Family == ComponentFamily.PluginAssembly)
            .Where(artifact =>
            {
                var deploymentFlavor = artifact.Properties is not null
                    && artifact.Properties.TryGetValue(ArtifactPropertyKeys.DeploymentFlavor, out var flavor)
                    ? flavor
                    : null;
                return string.Equals(deploymentFlavor, nameof(CodeAssetDeploymentFlavor.PluginPackage), StringComparison.OrdinalIgnoreCase);
            })
            .ToArray();
        if (packageAssemblies.Length == 0)
        {
            return false;
        }

        var customWorkflowActivityTypes = solution.Artifacts
            .Where(artifact => artifact.Family == ComponentFamily.PluginType)
            .Where(artifact => string.Equals(
                artifact.Properties is not null && artifact.Properties.TryGetValue(ArtifactPropertyKeys.PluginTypeKind, out var pluginTypeKind)
                    ? pluginTypeKind
                    : null,
                "customWorkflowActivity",
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (customWorkflowActivityTypes.Length == 0)
        {
            return false;
        }

        if (packageAssemblies.Length == 1)
        {
            return true;
        }

        foreach (var assemblyArtifact in packageAssemblies)
        {
            var assemblyFullName = assemblyArtifact.Properties is not null
                && assemblyArtifact.Properties.TryGetValue(ArtifactPropertyKeys.AssemblyFullName, out var fullName)
                    ? fullName
                    : assemblyArtifact.LogicalName;
            if (string.IsNullOrWhiteSpace(assemblyFullName))
            {
                return true;
            }

            if (customWorkflowActivityTypes.Any(artifact =>
                    string.Equals(
                        artifact.Properties is not null && artifact.Properties.TryGetValue(ArtifactPropertyKeys.AssemblyFullName, out var pluginAssemblyFullName)
                            ? pluginAssemblyFullName
                            : null,
                        assemblyFullName,
                        StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ShouldSkipImportForApplyOnlyPackage(string packageInputRoot)
    {
        var solutionManifestPath = Path.Combine(packageInputRoot, "Other", "Solution.xml");
        if (!File.Exists(solutionManifestPath))
        {
            return false;
        }

        var document = XDocument.Load(solutionManifestPath, LoadOptions.PreserveWhitespace);
        var rootComponents = document
            .Descendants()
            .FirstOrDefault(element => string.Equals(element.Name.LocalName, "RootComponents", StringComparison.OrdinalIgnoreCase));

        return rootComponents is not null
            && !rootComponents.Elements().Any();
    }

    private static bool HasErrors(IEnumerable<CompilerDiagnostic> diagnostics) =>
        diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
}
