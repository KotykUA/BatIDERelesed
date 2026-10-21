using System.Collections.Generic;

namespace BatIDE.Services;

/// <summary>
/// Шаблони коду (Snippets), які розгортаються клавішею Tab.
/// </summary>
public static class SnippetManager
{
    public static readonly Dictionary<string, string> Snippets = new()
    {
        { "forf", "for /f \"tokens=*\" %%i in ('command') do (\n    %%i\n)" },
        { "forl", "for /l %%i in (1,1,10) do (\n    echo %%i\n)" },
        { "ford", "for /d %%d in (*) do (\n    echo %%d\n)" },
        { "ifexist", "if exist \"file.txt\" (\n    \n) else (\n    \n)" },
        { "errcheck", "if %errorlevel% neq 0 (\n    echo [ERROR] Failed with code %errorlevel%\n    exit /b %errorlevel%\n)" },
        { "region", ":: #region Name\n\n:: #endregion" },
        { "try", "if %errorlevel% neq 0 (\n    echo Error\n    goto :eof\n)" },
        { "pause", "pause" }
    };
}
