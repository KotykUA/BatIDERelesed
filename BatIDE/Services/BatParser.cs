using System;
using System.Text.RegularExpressions;
using BatIDE.Models;

namespace BatIDE.Services;

public static class BatParser
{
    // Регекси для швидкого виділення токенів
    private static readonly Regex LabelRegex = new(@"^\s*:([a-zA-Z0-9_\-]+)", RegexOptions.Compiled);
    private static readonly Regex VarUsageRegex = new(@"%([^%\s]+)%|!([^!\s]+)!", RegexOptions.Compiled);
    private static readonly Regex SetCommandRegex = new(@"^\s*set\s+(?:/a\s+|/p\s+)?(?:\""([^=]+)=|([^=\s]+)=)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static BatDocument Parse(string text)
    {
        var doc = new BatDocument { Text = text };
        if (string.IsNullOrEmpty(text)) return doc;

        var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var currentOffset = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.TrimStart();

            // 1. Ігноруємо коментарі
            if (trimmed.StartsWith("::", StringComparison.Ordinal) || 
                trimmed.StartsWith("REM ", StringComparison.OrdinalIgnoreCase))
            {
                currentOffset += line.Length + 1; // +1 для \n
                continue;
            }

            // 2. Шукаємо мітки (Labels)
            var labelMatch = LabelRegex.Match(line);
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
                // 3. Шукаємо команди (перше слово в рядку)
                var firstWord = trimmed.Split(' ', '\t')[0];
                if (!string.IsNullOrEmpty(firstWord) && char.IsLetter(firstWord[0]) || firstWord.StartsWith('@'))
                {
                    doc.Commands.Add(new BatCommand
                    {
                        Name = firstWord.TrimStart('@').ToUpperInvariant(),
                        Line = i,
                        Arguments = trimmed.Length > firstWord.Length ? trimmed[firstWord.Length..].Trim() : string.Empty
                    });

                    // 4. Шукаємо оголошення змінних (set)
                    if (firstWord.Equals("set", StringComparison.OrdinalIgnoreCase))
                    {
                        var setMatch = SetCommandRegex.Match(line);
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

                // 5. Шукаємо використання змінних (%var% та !var!)
                var varMatches = VarUsageRegex.Matches(line);
                foreach (Match m in varMatches)
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