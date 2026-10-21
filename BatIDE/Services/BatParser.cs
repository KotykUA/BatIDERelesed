using System;
using System.Text.RegularExpressions;
using BatIDE.Models;

namespace BatIDE.Services;

public static partial class BatParser
{
    [GeneratedRegex(@"^\s*:([a-zA-Z0-9_\-]+)", RegexOptions.Compiled)]
    private static partial Regex LabelRegex();

    [GeneratedRegex(@"%([^%\s]+)%|!([^!\s]+)!", RegexOptions.Compiled)]
    private static partial Regex VarUsageRegex();

    [GeneratedRegex(@"^\s*set\s+(?:/a\s+|/p\s+)?(?:\""([^=]+)=|([^=\s]+)=)", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex SetCommandRegex();

    public static BatDocument Parse(string text)
    {
        var doc = new BatDocument { Text = text };
        if (string.IsNullOrEmpty(text)) return doc;

        var lines = text.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);
        var currentOffset = 0;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.TrimStart();

            if (trimmed.StartsWith("::", StringComparison.Ordinal) ||
                trimmed.StartsWith("REM ", StringComparison.OrdinalIgnoreCase))
            {
                currentOffset += line.Length + 1;
                continue;
            }

            var labelMatch = LabelRegex().Match(line);
            if (labelMatch.Success)
            {
                doc.Labels.Add(new BatLabel
                {
                    Name = labelMatch.Groups[1].Value,
                    Line = i,
                    Offset = currentOffset + labelMatch.Groups[1].Index
                });
            }
            else
            {
                var firstWord = trimmed.Split(' ', '\t')[0];
                if (!string.IsNullOrEmpty(firstWord) && (char.IsLetter(firstWord[0]) || firstWord.StartsWith('@')))
                {
                    doc.Commands.Add(new BatCommand
                    {
                        Name = firstWord.TrimStart('@').ToUpperInvariant(),
                        Line = i,
                        Arguments = trimmed.Length > firstWord.Length ? trimmed[firstWord.Length..].Trim() : string.Empty
                    });

                    if (firstWord.Equals("set", StringComparison.OrdinalIgnoreCase))
                    {
                        var setMatch = SetCommandRegex().Match(line);
                        if (setMatch.Success)
                        {
                            var varName = setMatch.Groups[1].Success ? setMatch.Groups[1].Value : setMatch.Groups[2].Value;
                            doc.Variables.Add(new BatVariable
                            {
                                Name = varName,
                                Line = i,
                                IsDeclaration = true
                            });
                        }
                    }
                }

                foreach (Match m in VarUsageRegex().Matches(line))
                {
                    var isDelayed = m.Groups[2].Success;
                    var name = isDelayed ? m.Groups[2].Value : m.Groups[1].Value;

                    doc.Variables.Add(new BatVariable
                    {
                        Name = name,
                        Line = i,
                        IsDelayed = isDelayed,
                        IsDeclaration = false
                    });
                }
            }

            currentOffset += line.Length + 1;
        }

        return doc;
    }
}
