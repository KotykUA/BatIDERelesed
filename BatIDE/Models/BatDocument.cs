using System.Collections.Generic;

namespace BatIDE.Models;

public class BatDocument
{
    public string Text { get; init; } = string.Empty;
    public List<BatLabel> Labels { get; } = [];
    public List<BatVariable> Variables { get; } = [];
    public List<BatCommand> Commands { get; } = [];
}

public class BatLabel
{
    public string Name { get; init; } = string.Empty;
    public int Line { get; init; }
    public int Offset { get; init; }
}

public class BatVariable
{
    public string Name { get; init; } = string.Empty;
    public int Line { get; init; }
    public bool IsDelayed { get; init; }
    public bool IsDeclaration { get; init; }
}

public class BatCommand
{
    public string Name { get; init; } = string.Empty;
    public int Line { get; init; }
    public string Arguments { get; init; } = string.Empty;
}
