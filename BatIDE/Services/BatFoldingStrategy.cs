using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using AvaloniaEdit.Document;
using AvaloniaEdit.Folding;

namespace BatIDE.Services;

/// <summary>
/// Стратегія згортання блоків для BAT. Шукає пари дужок ( ) та регіони :: #region.
/// </summary>
public class BatFoldingStrategy : AbstractFoldingStrategy
{
    private static readonly Regex RegionStartRegex = new(@"^\s*::\s*#region(.*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex RegionEndRegex = new(@"^\s*::\s*#endregion", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public override IEnumerable<NewFolding> CreateNewFoldings(TextDocument document, out int firstErrorOffset)
    {
        firstErrorOffset = -1;
        var foldings = new List<NewFolding>();
        
        var lines = document.Text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var regionStack = new Stack<(int StartLine, string Name)>();
        var parenStack = new Stack<int>();

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.Trim();

            // 1. Регіони
            var regionMatch = RegionStartRegex.Match(line);
            if (regionMatch.Success)
            {
                regionStack.Push((i, regionMatch.Groups[1].Value.Trim()));
                continue;
            }
            if (RegionEndRegex.IsMatch(line) && regionStack.Count > 0)
            {
                var (startLine, name) = regionStack.Pop();
                var startOffset = document.GetLineByNumber(startLine + 1).Offset;
                var endOffset = document.GetLineByNumber(i + 1).EndOffset;
                foldings.Add(new NewFolding(startOffset, endOffset) { Name = $"Region: {name}" });
                continue;
            }

            // 2. Дужки ( ) — ігноруємо закриті в тому ж рядку
            var openCount = 0;
            var closeCount = 0;
            foreach (var c in line)
            {
                if (c == '(') openCount++;
                else if (c == ')') closeCount++;
            }

            if (openCount > closeCount) // Блок починається
            {
                parenStack.Push(i);
            }
            else if (closeCount > openCount && parenStack.Count > 0) // Блок закривається
            {
                var startLine = parenStack.Pop();
                if (i > startLine) // Якщо не в одному рядку
                {
                    var startOffset = document.GetLineByNumber(startLine + 1).Offset;
                    var endOffset = document.GetLineByNumber(i + 1).EndOffset;
                    foldings.Add(new NewFolding(startOffset, endOffset) { Name = "..." });
                }
            }
        }

        foldings.Sort((a, b) => a.StartOffset.CompareTo(b.StartOffset));
        return foldings;
    }
}
