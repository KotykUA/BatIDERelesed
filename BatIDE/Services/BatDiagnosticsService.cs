using System;
using System.Collections.Generic;
using System.Linq;
using BatIDE.Models;

namespace BatIDE.Services;

public static class BatDiagnosticsService
{
    public static List<Diagnostic> Analyze(BatDocument doc)
    {
        var diagnostics = new List<Diagnostic>();
        if (string.IsNullOrEmpty(doc.Text)) return diagnostics;

        var labels = doc.Labels.Select(l => l.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var declaredVars = doc.Variables.Where(v => v.IsDeclaration).Select(v => v.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var usedLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var cmd in doc.Commands)
        {
            // 1. Перевірка goto / call
            if (cmd.Name == "GOTO" || cmd.Name == "CALL")
            {
                var targetLabel = cmd.Arguments.Trim().TrimStart(':').Split(' ')[0];
                if (!string.IsNullOrEmpty(targetLabel) && !targetLabel.Equals("eof", StringComparison.OrdinalIgnoreCase))
                {
                    usedLabels.Add(targetLabel);
                    if (!labels.Contains(targetLabel))
                    {
                        diagnostics.Add(new Diagnostic
                        {
                            Code = "BAT001",
                            Message = $"Undefined label ':{targetLabel}'",
                            Line = cmd.Line,
                            Severity = DiagnosticSeverity.Error
                        });
                    }
                }
            }
        }

        // 2. Перевірка невикористаних лейблів
        foreach (var label in doc.Labels)
        {
            if (!usedLabels.Contains(label.Name))
            {
                diagnostics.Add(new Diagnostic
                {
                    Code = "BAT004",
                    Message = $"Unused label ':{label.Name}'",
                    Line = label.Line,
                    Severity = DiagnosticSeverity.Warning
                });
            }
        }

        // 3. Перевірка змінних (використання до оголошення)
        foreach (var variable in doc.Variables.Where(v => !v.IsDeclaration))
        {
            if (!declaredVars.Contains(variable.Name))
            {
                diagnostics.Add(new Diagnostic
                {
                    Code = "BAT003",
                    Message = $"Variable '{variable.Name}' is used but never declared via 'set'",
                    Line = variable.Line,
                    Severity = DiagnosticSeverity.Warning
                });
            }
        }

        return diagnostics;
    }
}
