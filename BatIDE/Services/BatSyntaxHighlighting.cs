using System.IO;
using System.Xml;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;

namespace BatIDE.Services;

/// <summary>Minimal XSHD definition for BAT / SH syntax highlighting.</summary>
public static class BatSyntaxHighlighting
{
    private const string Xshd = """
<SyntaxDefinition name="BatIDE.BAT" extensions=".bat;.cmd;.sh"
    xmlns="http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008">
  <Color name="Comment" foreground="#5a9e5a" fontStyle="italic" />
  <Color name="Keyword" foreground="#4d9fff" />
  <Color name="String"  foreground="#d4b86a" />
  <Color name="Variable" foreground="#c586c0" />

  <RuleSet>
    <Span color="Comment" begin="::" />
    <Span color="Comment" begin="REM" />
    <Span color="Comment" begin="#" />
    <Rule color="String">&quot;[^&quot;]*&quot;</Rule>
    <Rule color="String">'[^']*'</Rule>
    <Rule color="Variable">%[^%\s]+%</Rule>
    <Rule color="Variable">![^!\s]+!</Rule>
    <Rule color="Variable">\$[A-Za-z_][A-Za-z0-9_]*</Rule>

    <Keywords color="Keyword">
      <Word>@echo</Word>   <Word>echo</Word>
      <Word>set</Word>     <Word>setlocal</Word>   <Word>endlocal</Word>
      <Word>if</Word>      <Word>else</Word>       <Word>exist</Word>
      <Word>goto</Word>    <Word>call</Word>       <Word>shift</Word>
      <Word>for</Word>     <Word>in</Word>         <Word>do</Word>
      <Word>pause</Word>   <Word>exit</Word>       <Word>choice</Word>
      <Word>copy</Word>    <Word>xcopy</Word>      <Word>move</Word>
      <Word>del</Word>     <Word>erase</Word>      <Word>mkdir</Word>
      <Word>rmdir</Word>   <Word>ren</Word>        <Word>rename</Word>
      <Word>cls</Word>     <Word>title</Word>      <Word>start</Word>
      <Word>break</Word>   <Word>rem</Word>
      <Word>then</Word>    <Word>fi</Word>         <Word>elif</Word>
      <Word>export</Word>  <Word>source</Word>     <Word>alias</Word>
    </Keywords>
  </RuleSet>
</SyntaxDefinition>
""";

    // Parsing XSHD is cheap but not free; cache so we only pay once per process.
    private static IHighlightingDefinition? _cached;
    private static readonly object Lock = new();

    public static IHighlightingDefinition Load()
    {
        var local = _cached;
        if (local is not null) return local;

        lock (Lock)
        {
            if (_cached is not null) return _cached;

            using var reader = XmlReader.Create(new StringReader(Xshd));
            _cached = HighlightingLoader.Load(reader, HighlightingManager.Instance);
            return _cached;
        }
    }
}
