using System;

namespace BatIDE.Models;

public enum DiagnosticSeverity
{
    Error,
    Warning,
    Info
}

public class Diagnostic
{
    public string Code { get; set; } = string.Empty; // Наприклад, "BAT001"
    public string Message { get; set; } = string.Empty;
    public int Line { get; set; } // 0-based
    public int Column { get; set; } // 0-based
    public int Length { get; set; }
    public DiagnosticSeverity Severity { get; set; } = DiagnosticSeverity.Warning;
}
