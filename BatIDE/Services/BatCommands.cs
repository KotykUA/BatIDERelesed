namespace BatIDE.Services;

/// <summary>BAT keyword list shown in autocompletion (ported from the old AutocompleteMenu setup).</summary>
public static class BatCommands
{
    public static readonly string[] All =
    [
        "@echo off", "echo text", "echo.", "echo !variable!", "echo off", "echo on",
        "set variable=value", "set \"variable=value\"", "set /p variable=[promptString]",
        "set /a variable=expression", "set /a \"variable+=value\"", "set /a \"variable-=value\"",
        "setlocal", "setlocal enabledelayedexpansion", "setlocal disabledelayedexpansion",
        "endlocal", "endlocal & set variable=value",
        "if condition command", "if exist file command", "if not condition command",
        "if not exist file command", "if errorlevel number command", "if defined variable command",
        "if not defined variable command",
        "goto label", "goto :eof", "goto :label",
        "call :label", "call :label arguments",
        "shift", "shift /n",
        "for %%variable in (set) do command [command-parameters]",
        "for /f \"options\" %%variable in (file-set) do command [command-parameters]",
        "for /l %%variable in (start,step,end) do command [command-parameters]",
        "for /d %%variable in (set) do command [command-parameters]",
        "for /r [[drive:]path] %%variable in (set) do command [command-parameters]",
        "for /f \"tokens=*\" %%variable in ('command') do command [command-parameters]",
        "choice /c choices /n /m promptString",
        "choice /t timeout /d choice /n /m promptString",
        "pause", "break", "rem comment",
        "copy source destination [/y]", "xcopy source destination /s /i /y",
        "xcopy source destination /s /i /d", "xcopy source destination /s /i /d /y",
        "move source destination [/y]", "rename old new", "ren old new",
        "del file", "erase file", "rmdir directory", "rd directory",
        "mkdir directory", "md directory",
        "attrib [±R] [±A] [±S] [±H] [drive:][path][filename] [/s] [/d]",
        "attrib -R -A -S -H [drive:][path][filename] [/s] [/d]",
        "cls", "title text",
        "start \"title\" [/d path] [options] \"command\" [parameters]",
        "start \"\" \"command\"",
        "timeout /t seconds /nobreak",
        "popd", "pushd",
        "assoc", "ftype",
        "robocopy source destination [file(s)] [options]",
        "takeown", "icacls", "schtasks", "bcdedit", "sc", "powercfg",
        "reg", "wevtutil", "wmic", "driverquery"
    ];
}
