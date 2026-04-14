using DataverseSolutionCompiler.Domain.Abstractions;
using DataverseSolutionCompiler.Domain.Diagnostics;
using DataverseSolutionCompiler.Domain.Packaging;

namespace DataverseSolutionCompiler.Packaging.Pac;

public sealed class PacCliExecutor : IPackageExecutor, IImportExecutor
{
    public PackageResult Pack(PackageRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var packageName = $"{Path.GetFileName(request.InputRoot)}-{request.Flavor.ToString().ToLowerInvariant()}.zip";
        var packagePath = Path.Combine(request.OutputRoot, packageName);

        return new PackageResult(
            true,
            packagePath,
            [
                new CompilerDiagnostic(
                    "pac-bootstrap-pack",
                    DiagnosticSeverity.Info,
                    "PAC CLI packaging adapter is registered; command invocation is deferred to later milestones.",
                    packagePath)
            ]);
    }

    public ImportResult Import(ImportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new ImportResult(
            true,
            request.PackagePath,
            request.PublishAfterImport,
            [
                new CompilerDiagnostic(
                    "pac-bootstrap-import",
                    DiagnosticSeverity.Info,
                    "PAC CLI import adapter is registered; no environment mutation was executed during bootstrap.",
                    request.PackagePath)
            ]);
    }
}
