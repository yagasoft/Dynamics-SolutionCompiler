using DataverseSolutionCompiler.Agent;
using DataverseSolutionCompiler.Domain.Apply;
using DataverseSolutionCompiler.Domain.Compilation;
using DataverseSolutionCompiler.Domain.Diagnostics;
using DataverseSolutionCompiler.Domain.Diff;
using DataverseSolutionCompiler.Domain.Emission;
using DataverseSolutionCompiler.Domain.Live;
using DataverseSolutionCompiler.Domain.Model;
using DataverseSolutionCompiler.Domain.Packaging;
using DataverseSolutionCompiler.Domain.Planning;
using DataverseSolutionCompiler.Domain.Workflows;
using FluentAssertions;
using Xunit;

namespace DataverseSolutionCompiler.UnitTests;

public sealed class AgentOrchestratorTests
{
    [Fact]
    public void Dev_apply_workflow_runs_compile_apply_readback_and_diff_in_order()
    {
        var compilation = CreateCompilationResult(
            new FamilyArtifact(ComponentFamily.ImageConfiguration, "account-image"),
            new FamilyArtifact(ComponentFamily.Table, "account"));
        var kernel = new StubKernel(compilation);
        var applyExecutor = new RecordingApplyExecutor(new ApplyResult(true, ApplyMode.DevProof, [ComponentFamily.ImageConfiguration.ToString()], []));
        var liveProvider = new RecordingLiveSnapshotProvider(new LiveSnapshot(
            new EnvironmentProfile("dev", new Uri("https://example.crm.dynamics.com")),
            "sample",
            [new FamilyArtifact(ComponentFamily.ImageConfiguration, "account-image", Evidence: EvidenceKind.Readback)],
            []));
        var driftComparer = new RecordingDriftComparer(new DriftReport(false, [], []));
        var orchestrator = new AgentOrchestrator(kernel, new StubExplanationService(), applyExecutor, liveProvider, driftComparer);

        var result = orchestrator.RunDevApply(CreateDevApplyWorkflowRequest());

        result.Success.Should().BeTrue();
        result.Stages.Select(stage => stage.Stage)
            .Should()
            .ContainInOrder(
                WorkflowStageKind.Compile,
                WorkflowStageKind.Apply,
                WorkflowStageKind.Readback,
                WorkflowStageKind.Diff);
        liveProvider.Requests.Should().ContainSingle();
        liveProvider.Requests[0].Families.Should().Equal(ComponentFamily.ImageConfiguration);
        driftComparer.Requests.Should().ContainSingle();
        driftComparer.Requests[0].Source.Artifacts.Should().OnlyContain(artifact => artifact.Family == ComponentFamily.ImageConfiguration);
    }

    [Fact]
    public void Dev_apply_workflow_aggregates_diagnostics_from_every_stage()
    {
        var compilation = CreateCompilationResult(
            diagnostics:
            [
                new CompilerDiagnostic("compile-info", DiagnosticSeverity.Info, "compile")
            ]);
        var kernel = new StubKernel(compilation);
        var applyExecutor = new RecordingApplyExecutor(new ApplyResult(
            true,
            ApplyMode.DevProof,
            [],
            [
                new CompilerDiagnostic("apply-info", DiagnosticSeverity.Info, "apply")
            ]));
        var liveProvider = new RecordingLiveSnapshotProvider(new LiveSnapshot(
            new EnvironmentProfile("dev", new Uri("https://example.crm.dynamics.com")),
            "sample",
            [],
            [
                new CompilerDiagnostic("readback-warning", DiagnosticSeverity.Warning, "readback")
            ]));
        var driftComparer = new RecordingDriftComparer(new DriftReport(
            false,
            [],
            [
                new CompilerDiagnostic("diff-info", DiagnosticSeverity.Info, "diff")
            ]));
        var orchestrator = new AgentOrchestrator(kernel, new StubExplanationService(), applyExecutor, liveProvider, driftComparer);

        var result = orchestrator.RunDevApply(CreateDevApplyWorkflowRequest());

        result.Diagnostics.Select(diagnostic => diagnostic.Code)
            .Should()
            .Contain(["compile-info", "apply-info", "readback-warning", "diff-info"]);
    }

    [Fact]
    public void Dev_apply_workflow_returns_successful_noop_when_no_supported_families_exist()
    {
        var compilation = CreateCompilationResult(new FamilyArtifact(ComponentFamily.Table, "account"));
        var kernel = new StubKernel(compilation);
        var applyExecutor = new RecordingApplyExecutor(new ApplyResult(
            true,
            ApplyMode.DevProof,
            [],
            [
                new CompilerDiagnostic("apply-noop", DiagnosticSeverity.Info, "noop")
            ]));
        var liveProvider = new RecordingLiveSnapshotProvider(new LiveSnapshot(
            new EnvironmentProfile("dev", new Uri("https://example.crm.dynamics.com")),
            "sample",
            [],
            []));
        var driftComparer = new RecordingDriftComparer(new DriftReport(false, [], []));
        var orchestrator = new AgentOrchestrator(kernel, new StubExplanationService(), applyExecutor, liveProvider, driftComparer);

        var result = orchestrator.RunDevApply(CreateDevApplyWorkflowRequest());

        result.Success.Should().BeTrue();
        result.IsNoOp.Should().BeTrue();
        liveProvider.Requests.Should().BeEmpty();
        driftComparer.Requests.Should().BeEmpty();
        result.Stages.Should().OnlyContain(stage => stage.Status == WorkflowStageStatus.Succeeded);
    }

    [Fact]
    public void Dev_apply_workflow_fails_on_readback_errors_and_skips_diff()
    {
        var kernel = new StubKernel(CreateCompilationResult());
        var applyExecutor = new RecordingApplyExecutor(new ApplyResult(true, ApplyMode.DevProof, [], []));
        var liveProvider = new RecordingLiveSnapshotProvider(new LiveSnapshot(
            new EnvironmentProfile("dev", new Uri("https://example.crm.dynamics.com")),
            "sample",
            [],
            [
                new CompilerDiagnostic("readback-error", DiagnosticSeverity.Error, "readback failed")
            ]));
        var driftComparer = new RecordingDriftComparer(new DriftReport(false, [], []));
        var orchestrator = new AgentOrchestrator(kernel, new StubExplanationService(), applyExecutor, liveProvider, driftComparer);

        var result = orchestrator.RunDevApply(CreateDevApplyWorkflowRequest());

        result.Success.Should().BeFalse();
        driftComparer.Requests.Should().BeEmpty();
        result.Stages.Should().ContainSingle(stage => stage.Stage == WorkflowStageKind.Diff && stage.Status == WorkflowStageStatus.Skipped);
    }

    [Fact]
    public void Dev_apply_workflow_fails_on_blocking_drift()
    {
        var kernel = new StubKernel(CreateCompilationResult());
        var applyExecutor = new RecordingApplyExecutor(new ApplyResult(true, ApplyMode.DevProof, [], []));
        var liveProvider = new RecordingLiveSnapshotProvider(new LiveSnapshot(
            new EnvironmentProfile("dev", new Uri("https://example.crm.dynamics.com")),
            "sample",
            [],
            []));
        var driftComparer = new RecordingDriftComparer(new DriftReport(
            true,
            [new DriftFinding("Mismatch", DriftSeverity.Error, DriftCategory.Mismatch, ComponentFamily.ImageConfiguration, "blocking drift")],
            []));
        var orchestrator = new AgentOrchestrator(kernel, new StubExplanationService(), applyExecutor, liveProvider, driftComparer);

        var result = orchestrator.RunDevApply(CreateDevApplyWorkflowRequest());

        result.Success.Should().BeFalse();
        result.Stages.Should().ContainSingle(stage => stage.Stage == WorkflowStageKind.Diff && stage.Status == WorkflowStageStatus.Failed);
    }

    [Fact]
    public void Package_build_workflow_runs_compile_emit_and_pack_in_order()
    {
        var outputRoot = Path.Combine(Path.GetTempPath(), $"dsc-agent-package-build-{Guid.NewGuid():N}");
        var kernel = new StubKernel(CreateCompilationResult(new FamilyArtifact(ComponentFamily.Table, "account")));
        var packageEmitter = new RecordingEmitter((model, request) =>
            new EmittedArtifacts(
                true,
                request.OutputRoot,
                [new EmittedArtifact("package-inputs/manifest.json", EmittedArtifactRole.PackageInput, "fixture")],
                []));
        var packageExecutor = new RecordingPackageExecutor(new PackageResult(true, Path.Combine(outputRoot, "sample.zip"), []));
        var orchestrator = new AgentOrchestrator(
            kernel,
            new StubExplanationService(),
            new StubApplyExecutor(),
            new RecordingLiveSnapshotProvider(new LiveSnapshot(new EnvironmentProfile("dev"), "sample", [], [])),
            new RecordingDriftComparer(new DriftReport(false, [], [])),
            packageEmitter,
            packageExecutor,
            new RecordingImportExecutor(new ImportResult(true, Path.Combine(outputRoot, "sample.zip"), true, [])));

        try
        {
            var result = orchestrator.RunPackageBuild(CreatePackageBuildWorkflowRequest(outputRoot));

            result.Success.Should().BeTrue();
            result.Stages.Select(stage => stage.Stage)
                .Should()
                .ContainInOrder(
                    WorkflowStageKind.Compile,
                    WorkflowStageKind.EmitPackageInputs,
                    WorkflowStageKind.Pack);
            packageEmitter.Requests.Should().ContainSingle();
            packageExecutor.Requests.Should().ContainSingle();
            result.PackageInputRoot.Should().Be(Path.Combine(outputRoot, "package-inputs"));
        }
        finally
        {
            if (Directory.Exists(outputRoot))
            {
                Directory.Delete(outputRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void Publish_workflow_runs_compile_emit_pack_import_and_finalize_apply_in_order()
    {
        var outputRoot = Path.Combine(Path.GetTempPath(), $"dsc-agent-publish-{Guid.NewGuid():N}");
        var kernel = new StubKernel(CreateCompilationResult(new FamilyArtifact(ComponentFamily.ImageConfiguration, "account-image")));
        var packageEmitter = new RecordingEmitter((model, request) =>
        {
            var otherRoot = Path.Combine(request.OutputRoot, "package-inputs", "Other");
            Directory.CreateDirectory(otherRoot);
            File.WriteAllText(
                Path.Combine(otherRoot, "Solution.xml"),
                """
                <ImportExportXml>
                  <SolutionManifest>
                    <RootComponents>
                      <RootComponent type="1" id="{00000000-0000-0000-0000-000000000001}" />
                    </RootComponents>
                  </SolutionManifest>
                </ImportExportXml>
                """);

            return new EmittedArtifacts(
                true,
                request.OutputRoot,
                [new EmittedArtifact("package-inputs/Other/Solution.xml", EmittedArtifactRole.PackageInput, "fixture")],
                []);
        });
        var packageExecutor = new RecordingPackageExecutor(new PackageResult(true, Path.Combine(outputRoot, "sample.zip"), []));
        var importExecutor = new RecordingImportExecutor(new ImportResult(true, Path.Combine(outputRoot, "sample.zip"), true, []));
        var applyExecutor = new RecordingApplyExecutor(new ApplyResult(true, ApplyMode.DevProof, [ComponentFamily.ImageConfiguration.ToString()], []));
        var orchestrator = new AgentOrchestrator(
            kernel,
            new StubExplanationService(),
            applyExecutor,
            new RecordingLiveSnapshotProvider(new LiveSnapshot(new EnvironmentProfile("dev"), "sample", [], [])),
            new RecordingDriftComparer(new DriftReport(false, [], [])),
            packageEmitter,
            packageExecutor,
            importExecutor);

        try
        {
            var result = orchestrator.RunPublish(CreatePublishWorkflowRequest(outputRoot));

            result.Success.Should().BeTrue();
            result.ImportSkippedBecauseApplyOnly.Should().BeFalse();
            result.Stages.Select(stage => stage.Stage)
                .Should()
                .ContainInOrder(
                    WorkflowStageKind.Compile,
                    WorkflowStageKind.EmitPackageInputs,
                    WorkflowStageKind.Pack,
                    WorkflowStageKind.Import,
                    WorkflowStageKind.FinalizeApply);
            importExecutor.Requests.Should().ContainSingle();
            applyExecutor.Requests.Should().ContainSingle();
        }
        finally
        {
            if (Directory.Exists(outputRoot))
            {
                Directory.Delete(outputRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void Publish_workflow_skips_import_for_apply_only_package_and_still_runs_finalize_apply()
    {
        var outputRoot = Path.Combine(Path.GetTempPath(), $"dsc-agent-publish-apply-only-{Guid.NewGuid():N}");
        var kernel = new StubKernel(CreateCompilationResult(new FamilyArtifact(ComponentFamily.EntityAnalyticsConfiguration, "contact")));
        var packageEmitter = new RecordingEmitter((model, request) =>
        {
            var otherRoot = Path.Combine(request.OutputRoot, "package-inputs", "Other");
            Directory.CreateDirectory(otherRoot);
            File.WriteAllText(
                Path.Combine(otherRoot, "Solution.xml"),
                """
                <ImportExportXml>
                  <SolutionManifest>
                    <RootComponents>
                    </RootComponents>
                  </SolutionManifest>
                </ImportExportXml>
                """);

            return new EmittedArtifacts(
                true,
                request.OutputRoot,
                [new EmittedArtifact("package-inputs/Other/Solution.xml", EmittedArtifactRole.PackageInput, "fixture")],
                []);
        });
        var packageExecutor = new RecordingPackageExecutor(new PackageResult(true, Path.Combine(outputRoot, "sample.zip"), []));
        var importExecutor = new RecordingImportExecutor(new ImportResult(true, Path.Combine(outputRoot, "sample.zip"), true, []));
        var applyExecutor = new RecordingApplyExecutor(new ApplyResult(true, ApplyMode.DevProof, [ComponentFamily.EntityAnalyticsConfiguration.ToString()], []));
        var orchestrator = new AgentOrchestrator(
            kernel,
            new StubExplanationService(),
            applyExecutor,
            new RecordingLiveSnapshotProvider(new LiveSnapshot(new EnvironmentProfile("dev"), "sample", [], [])),
            new RecordingDriftComparer(new DriftReport(false, [], [])),
            packageEmitter,
            packageExecutor,
            importExecutor);

        try
        {
            var result = orchestrator.RunPublish(CreatePublishWorkflowRequest(outputRoot));

            result.Success.Should().BeTrue();
            result.ImportSkippedBecauseApplyOnly.Should().BeTrue();
            importExecutor.Requests.Should().BeEmpty();
            applyExecutor.Requests.Should().ContainSingle();
            result.Stages.Should().ContainSingle(stage => stage.Stage == WorkflowStageKind.Import && stage.Status == WorkflowStageStatus.Skipped);
        }
        finally
        {
            if (Directory.Exists(outputRoot))
            {
                Directory.Delete(outputRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void Publish_workflow_skips_finalize_apply_when_import_fails()
    {
        var outputRoot = Path.Combine(Path.GetTempPath(), $"dsc-agent-publish-import-failure-{Guid.NewGuid():N}");
        var kernel = new StubKernel(CreateCompilationResult(new FamilyArtifact(ComponentFamily.ImageConfiguration, "account-image")));
        var packageEmitter = new RecordingEmitter((model, request) =>
        {
            var otherRoot = Path.Combine(request.OutputRoot, "package-inputs", "Other");
            Directory.CreateDirectory(otherRoot);
            File.WriteAllText(
                Path.Combine(otherRoot, "Solution.xml"),
                """
                <ImportExportXml>
                  <SolutionManifest>
                    <RootComponents>
                      <RootComponent type="1" id="{00000000-0000-0000-0000-000000000001}" />
                    </RootComponents>
                  </SolutionManifest>
                </ImportExportXml>
                """);

            return new EmittedArtifacts(
                true,
                request.OutputRoot,
                [new EmittedArtifact("package-inputs/Other/Solution.xml", EmittedArtifactRole.PackageInput, "fixture")],
                []);
        });
        var packageExecutor = new RecordingPackageExecutor(new PackageResult(true, Path.Combine(outputRoot, "sample.zip"), []));
        var importExecutor = new RecordingImportExecutor(new ImportResult(
            false,
            Path.Combine(outputRoot, "sample.zip"),
            false,
            [new CompilerDiagnostic("import-error", DiagnosticSeverity.Error, "import failed")]));
        var applyExecutor = new RecordingApplyExecutor(new ApplyResult(true, ApplyMode.DevProof, [], []));
        var orchestrator = new AgentOrchestrator(
            kernel,
            new StubExplanationService(),
            applyExecutor,
            new RecordingLiveSnapshotProvider(new LiveSnapshot(new EnvironmentProfile("dev"), "sample", [], [])),
            new RecordingDriftComparer(new DriftReport(false, [], [])),
            packageEmitter,
            packageExecutor,
            importExecutor);

        try
        {
            var result = orchestrator.RunPublish(CreatePublishWorkflowRequest(outputRoot));

            result.Success.Should().BeFalse();
            applyExecutor.Requests.Should().BeEmpty();
            result.Stages.Should().ContainSingle(stage => stage.Stage == WorkflowStageKind.FinalizeApply && stage.Status == WorkflowStageStatus.Skipped);
        }
        finally
        {
            if (Directory.Exists(outputRoot))
            {
                Directory.Delete(outputRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void Publish_workflow_aggregates_diagnostics_from_every_executed_stage()
    {
        var outputRoot = Path.Combine(Path.GetTempPath(), $"dsc-agent-publish-diags-{Guid.NewGuid():N}");
        var compilation = CreateCompilationResult(
            diagnostics:
            [
                new CompilerDiagnostic("compile-info", DiagnosticSeverity.Info, "compile")
            ]);
        var kernel = new StubKernel(compilation);
        var packageEmitter = new RecordingEmitter((model, request) =>
        {
            var otherRoot = Path.Combine(request.OutputRoot, "package-inputs", "Other");
            Directory.CreateDirectory(otherRoot);
            File.WriteAllText(
                Path.Combine(otherRoot, "Solution.xml"),
                """
                <ImportExportXml>
                  <SolutionManifest>
                    <RootComponents>
                      <RootComponent type="1" id="{00000000-0000-0000-0000-000000000001}" />
                    </RootComponents>
                  </SolutionManifest>
                </ImportExportXml>
                """);

            return new EmittedArtifacts(
                true,
                request.OutputRoot,
                [new EmittedArtifact("package-inputs/Other/Solution.xml", EmittedArtifactRole.PackageInput, "fixture")],
                [new CompilerDiagnostic("emit-warning", DiagnosticSeverity.Warning, "emit")]);
        });
        var packageExecutor = new RecordingPackageExecutor(new PackageResult(
            true,
            Path.Combine(outputRoot, "sample.zip"),
            [new CompilerDiagnostic("pack-info", DiagnosticSeverity.Info, "pack")]));
        var importExecutor = new RecordingImportExecutor(new ImportResult(
            true,
            Path.Combine(outputRoot, "sample.zip"),
            true,
            [new CompilerDiagnostic("import-info", DiagnosticSeverity.Info, "import")]));
        var applyExecutor = new RecordingApplyExecutor(new ApplyResult(
            true,
            ApplyMode.DevProof,
            [],
            [new CompilerDiagnostic("apply-info", DiagnosticSeverity.Info, "apply")]));
        var orchestrator = new AgentOrchestrator(
            kernel,
            new StubExplanationService(),
            applyExecutor,
            new RecordingLiveSnapshotProvider(new LiveSnapshot(new EnvironmentProfile("dev"), "sample", [], [])),
            new RecordingDriftComparer(new DriftReport(false, [], [])),
            packageEmitter,
            packageExecutor,
            importExecutor);

        try
        {
            var result = orchestrator.RunPublish(CreatePublishWorkflowRequest(outputRoot));

            result.Diagnostics.Select(diagnostic => diagnostic.Code)
                .Should()
                .Contain(["compile-info", "emit-warning", "pack-info", "import-info", "apply-info"]);
        }
        finally
        {
            if (Directory.Exists(outputRoot))
            {
                Directory.Delete(outputRoot, recursive: true);
            }
        }
    }

    private static DevApplyWorkflowRequest CreateDevApplyWorkflowRequest() =>
        new(new CompilationRequest(
            "C:\\source",
            [],
            new CompilationContext(
                new EnvironmentProfile("dev", new Uri("https://example.crm.dynamics.com")),
                EnablePackaging: true,
                EnableDevApply: true)));

    private static PackageBuildWorkflowRequest CreatePackageBuildWorkflowRequest(string outputRoot) =>
        new(
            new CompilationRequest("C:\\source", [], new CompilationContext(new EnvironmentProfile("dev"))),
            outputRoot);

    private static PublishWorkflowRequest CreatePublishWorkflowRequest(string outputRoot) =>
        new(
            new CompilationRequest(
                "C:\\source",
                [],
                new CompilationContext(
                    new EnvironmentProfile("dev", new Uri("https://example.crm.dynamics.com"), IsDevelopment: true),
                    EnablePackaging: true,
                    EnableDevApply: true)),
            outputRoot,
            new EnvironmentProfile("publish", new Uri("https://example.crm.dynamics.com")),
            new EnvironmentProfile("dev", new Uri("https://example.crm.dynamics.com"), IsDevelopment: true));

    private static CompilationResult CreateCompilationResult(
        params FamilyArtifact[] artifacts) =>
        CreateCompilationResult([], artifacts);

    private static CompilationResult CreateCompilationResult(
        IReadOnlyList<CompilerDiagnostic> diagnostics,
        params FamilyArtifact[] artifacts) =>
        new(
            true,
            "compiled",
            new CanonicalSolution(
                new SolutionIdentity("sample", "Sample", "1.0.0.0", LayeringIntent.UnmanagedDevelopment),
                new PublisherDefinition("dsc", "dsc", "dsc", "Dataverse Solution Compiler"),
                artifacts.Length == 0
                    ? [new FamilyArtifact(ComponentFamily.ImageConfiguration, "account-image")]
                    : artifacts,
                [],
                [],
                []),
            new("plan", [], []),
            [],
            diagnostics);
}
