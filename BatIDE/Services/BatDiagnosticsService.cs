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

        foreach (var cmd in doc.Commands.Where(c => c.Name is "GOTO" or "CALL"))
        {
            var targetLabel = cmd.Arguments.Trim().TrimStart(':').Split(' ')[0];
            if (string.IsNullOrEmpty(targetLabel) || targetLabel.Equals("eof", StringComparison.OrdinalIgnoreCase))
                continue;

            usedLabels.Add(targetLabel);
            if (labels.Contains(targetLabel))
                continue;

            diagnostics.Add(new Diagnostic
            {
                Code = "BAT001",
                Message = $"Undefined label ':{targetLabel}'",
                Line = cmd.Line,
                Severity = DiagnosticSeverity.Error
            });
        }

        diagnostics.AddRange(doc.Labels
            .Where(label => !usedLabels.Contains(label.Name))
            .Select(label => new Diagnostic
            {
                Code = "BAT004",
                Message = $"Unused label ':{label.Name}'",
                Line = label.Line,
                Severity = DiagnosticSeverity.Warning
            }));

        diagnostics.AddRange(doc.Variables
            .Where(v => !v.IsDeclaration && !declaredVars.Contains(v.Name))
            .Select(v => new Diagnostic
            {
                Code = "BAT003",
                Message = $"Variable '{v.Name}' is used but never declared via 'set'",
                Line = v.Line,
                Severity = DiagnosticSeverity.Warning
            }));

        return diagnostics;
    }
}
