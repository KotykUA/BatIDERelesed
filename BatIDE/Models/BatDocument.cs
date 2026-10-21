using System.Collections.Generic;

namespace BatIDE.Models;

public class BatDocument
{
    public string Text { get; set; } = string.Empty;
    public List<BatLabel> Labels { get; } = new();
    public List<BatVariable> Variables { get; } = new();
    public List<BatCommand> Commands { get; } = new();
}

public class BatLabel
{
    public string Name { get; set; } = string.Empty;
    public int Line { get; set; }
    public int Offset { get; set; }
}

public class BatVariable
{
    public string Name { get; set; } = string.Empty;
    public int Line { get; set; }
    public bool IsDelayed { get; set; } // !var! замість %var%
    public bool IsDeclaration { get; set; } // true, якщо це set var=...
}

public class BatCommand
{
    public string Name { get; set; } = string.Empty;
    public int Line { get; set; }
    public string Arguments { get; set; } = string.Empty;
}