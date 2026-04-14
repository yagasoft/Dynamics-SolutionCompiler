namespace DataverseSolutionCompiler.Domain.Diagnostics;

public sealed record CompilerDiagnostic(
    string Code,
    DiagnosticSeverity Severity,
    string Message,
    string? Location = null);
