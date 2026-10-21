using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using AvaloniaEdit.Document;
using AvaloniaEdit.Folding;

namespace BatIDE.Services;

public class BatFoldingStrategy
{
    private static readonly Regex RegionStartRegex = new(@"^\s*::\s*#region(.*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex RegionEndRegex = new(@"^\s*::\s*#endregion", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public void UpdateFoldings(FoldingManager manager, TextDocument document)
    {
        var foldings = CreateNewFoldings(document, out var firstErrorOffset);
        manager.UpdateFoldings(foldings, firstErrorOffset);
    }

    private static IEnumerable<NewFolding> CreateNewFoldings(TextDocument document, out int firstErrorOffset)
    {
        firstErrorOffset = -1;
        var foldings = new List<NewFolding>();

        var lines = document.Text.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);
        var regionStack = new Stack<(int StartLine, string Name)>();
        var parenStack = new Stack<int>();

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

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

            var (open, close) = CountParens(line);
            if (open > close)
            {
                parenStack.Push(i);
            }
            else if (close > open && parenStack.Count > 0)
            {
                var startLine = parenStack.Pop();
                if (i <= startLine) continue;

                var startOffset = document.GetLineByNumber(startLine + 1).Offset;
                var endOffset = document.GetLineByNumber(i + 1).EndOffset;
                foldings.Add(new NewFolding(startOffset, endOffset) { Name = "..." });
            }
        }

        foldings.Sort((a, b) => a.StartOffset.CompareTo(b.StartOffset));
        return foldings;
    }

    private static (int Open, int Close) CountParens(string line)
    {
        var open = 0;
        var close = 0;
        foreach (var c in line)
        {
            switch (c)
            {
                case '(': open++; break;
                case ')': close++; break;
            }
        }
        return (open, close);
    }
}
