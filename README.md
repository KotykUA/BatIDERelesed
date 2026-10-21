# BatIDE

Cross-platform IDE for **BAT** (Windows batch) and shell scripting,
now rewritten on **Avalonia UI** — runs on **Windows, macOS and Linux**.

## What's new in 2.0

| | Old (WinForms) | New (Avalonia) |
|---|---|---|
| Platform | Windows only | **Windows / macOS / Linux** |
| Editor | FastColoredTextBox + AutocompleteMenu | **AvaloniaEdit** (port of AvalonEdit) |
| Settings | .NET Framework `Settings1` | JSON in platform-correct folder |
| UI theme | Custom dark WinForms | Fluent dark theme |
| Autocompletion | Separate DLL | Built-in `CompletionWindow` |

## Features

- Code editor with syntax highlighting (`AvaloniaEdit`)
- Autocompletion for BAT commands (`Ctrl+Space` or auto as you type)
- Toggle line comments (`::` prefix)
- Create / open scripts (`.bat`, `.cmd`, `.sh`)
- Save with correct line endings (CRLF for BAT, LF for SH)
- Run scripts directly from the editor
- Find & replace panel (`Ctrl+F`)
- Custom editor background image
- Dark theme with a custom bottom toolbar
- Settings stored in a cross-platform JSON file

## Requirements

- **.NET 8 SDK** — <https://dotnet.microsoft.com/download/dotnet/8.0>

## Build & run

```bash
cd BatIDE
dotnet restore
dotnet run -c Release
```

## Publish as a single-file executable

```
# Windows
dotnet publish -c Release -r win-x64   --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true

# macOS (Intel)
dotnet publish -c Release -r osx-x64   --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true

# macOS (Apple Silicon)
dotnet publish -c Release -r osx-arm64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true

# Linux (x64)
dotnet publish -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

## Running `.bat` files on non-Windows systems

`.bat` and `.cmd` are Windows-only. On macOS/Linux the **Run** button will
show a clear error and offer three options:

1. Convert your script to `.sh` and run it natively.
2. Install **Wine** and run: `wine cmd /c "your-script.bat"`
3. Run it on a Windows machine.

`.sh` files run natively on macOS/Linux (`/bin/sh <file>`).

## Settings file location

| OS ↕▾ | Path ↕▾ |
|---|---|
| −Windows | `%LOCALAPPDATA%\BatIDE\settings.json` |
| Linux | `~/.local/share/BatIDE/settings.json` |
| macOS | `~/Library/Application Support/BatIDE/settings.json` |
⚙

## Keyboard shortcuts

| Shortcut ↕▾ | Action ↕▾ |
|---|---|
| −`Ctrl+S` | Save (planned) |
| `Ctrl+F` | Find / replace panel |
| `Ctrl+Space` | Force autocompletion popup |
| `Ctrl+Z` / `Ctrl+Y` | Undo / redo |
⚙

## Project structure

```
BatIDE/
├── App.axaml(.cs)              Application bootstrap
├── Program.cs                  Entry point
├── app.manifest                Windows DPI manifest
├── Models/
│   └── AppSettings.cs          Settings POCO
├── Services/
│   ├── SettingsService.cs      Cross-platform JSON storage
│   ├── ScriptRunner.cs         Run .bat / .sh per platform
│   ├── BatCommands.cs          Autocomplete keyword list
│   ├── BatCompletionData.cs    AvaloniaEdit ICompletionData impl
│   └── BatSyntaxHighlighting.cs XSHD syntax definition
└── Views/
    ├── MainWindow.axaml(.cs)   Start screen: Create / Open
    └── EditorWindow.axaml(.cs) Code editor + project list
```

## License

WER, 2023 — 2026

</BDS:create_file>

<BDS:create_file fileName=".gitignore">

```gitignore
# ---- .NET 8 / Avalonia ----
[Bb]in/
[Oo]bj/
[Dd]ebug/
[Rr]elease/
x64/
x86/
[Aa][Rr][Mm]/
[Aa][Rr][Mm]64/
publish/
artifacts/
.vs/
.idea/
*.user
*.suo
*.userosscache
*.sln.docstates
*.userprefs

# ---- Avalonia cache ----
.avalonia/
*.axaml.g.cs

# ---- Rider / VS / ReSharper ----
_ReSharper*/
*.[Rr]e[Ss]harper
*.DotSettings.user
*.sln.iml

# ---- NuGet ----
*.nupkg
*.snupkg
**/[Pp]ackages/*
!**/[Pp]ackages/build/
project.lock.json
project.fragment.lock.json
project.assets.json

# ---- Build artifacts ----
*.dll
*.exe
*.pdb
*.obj
*.tmp
*.cache
*.binlog
*.tlog
*.ilk
*.meta
*.pch
*.pgc
*.pgd
*.rsp
*.sbr
*.tlb
*.tli
*.tlh
*.svclog
*.scc

# ---- Publish output ----
publish/
*.publish.xml
*.pubxml
*.pubxml.user

# ---- OS junk ----
Thumbs.db
ehthumbs.db
Desktop.ini
$RECYCLE.BIN/
.DS_Store
._*

# ---- Logs ----
*.log
*.dump
*.dmp

# ---- Misc ----
*.GhostDoc.xml
*.pfx
*.publishsettings

