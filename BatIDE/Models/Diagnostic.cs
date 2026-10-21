namespace BatIDE.Models;

public enum DiagnosticSeverity
{
    Error,
    Warning,
    Info
}

public class Diagnostic
{
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public int Line { get; init; }
    public int Column { get; init; }
    public int Length { get; init; }
    public DiagnosticSeverity Severity { get; init; } = DiagnosticSeverity.Warning;
}
