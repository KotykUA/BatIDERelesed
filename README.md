<div align="center">

# 🦇 BatIDE

**A modern, cross-platform IDE for BAT (Windows batch) and Shell scripting.**

[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/)
[![Avalonia UI](https://img.shields.io/badge/Avalonia-11.2.3-8A2BE2?style=flat-square)](https://avaloniaui.net/)
[![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20macOS%20%7C%20Linux-lightgrey?style=flat-square)](#-supported-platforms)
[![License](https://img.shields.io/badge/License-WER-blue?style=flat-square)](#-license)

*Previously a Windows-only WinForms application, completely rewritten on Avalonia UI.*

</div>

---

## 📸 Screenshots

> *Coming soon — feel free to submit your own screenshots to the repository!*

<div align="center">
  <img src="docs/images/main-window.png" alt="Main Window" width="45%"/>
  <img src="docs/images/editor-window.png" alt="Editor Window" width="45%"/>
</div>

---

## 📖 Table of Contents

- [Features](#-features)
- [Supported Platforms](#-supported-platforms)
- [Installation](#-installation)
  - [Pre-built binaries](#pre-built-binaries)
  - [Build from source](#build-from-source)
- [System Requirements](#-system-requirements)
- [Running `.bat` files on non-Windows systems](#-running-bat-files-on-non-windows-systems)
- [Configuration](#-configuration)
- [Keyboard Shortcuts](#-keyboard-shortcuts)
- [Project Structure](#-project-structure)
- [Publishing](#-publishing)
- [Contributing](#-contributing)
- [License](#-license)

---

## ✨ Features

- **Cross-Platform:** Runs natively on Windows, macOS, and Linux.
- **Smart Editor:** Powered by `AvaloniaEdit` with syntax highlighting for BAT, CMD, and Shell scripts.
- **Autocompletion:** IntelliSense-like suggestions for common BAT commands (`Ctrl+Space` or auto-trigger).
- **Unsaved Changes Guard:** Prompt to Save / Discard / Cancel when switching files or closing the app.
- **Built-in Script Runner:** Run scripts directly from the editor.
  - `.sh` executes via the interpreter from the shebang or `/bin/sh`.
  - `.bat` runs via `cmd.exe` on Windows or auto-detects **Wine** on Linux/macOS.
- **Line Endings Normalization:** Automatically saves `.bat` as CRLF and `.sh` as LF.
- **Customizable UI:** Dark theme with support for custom editor background images.
- **Find & Replace:** Built-in search panel (`Ctrl+F`).

---

## 💻 Supported Platforms

| Platform | Status | Architecture |
| :--- | :---: | :--- |
| **Windows** | ✅ Full Support | x64, ARM64, x86 |
| **Linux** | ✅ Full Support | x64, ARM64, ARM |
| **macOS** | ✅ Full Support | Intel (x64), Apple Silicon (ARM64) |

---

## 🚀 Installation

### Pre-built binaries

You can download the latest single-file executables from the [Releases](https://github.com/KotykUA/BatIDERelesed/releases) page.

1. Download the archive for your OS.
2. Extract and run `BatIDE` (or `BatIDE.exe` on Windows). No installation required.

### Build from source

**Prerequisites:**
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

```bash
# 1. Clone the repository
git clone https://github.com/KotykUA/BatIDERelesed.git
cd BatIDERelesed

# 2. Restore dependencies
dotnet restore

# 3. Build and run
dotnet run --project BatIDE/BatIDE.csproj -c Release
```

---

## 📦 System Requirements

### Windows

- Windows 10 or later.
- No additional dependencies (self-contained builds available).

### macOS

- macOS 10.15 (Catalina) or later.

### Linux

`SkiaSharp` and `HarfBuzzSharp` require native system libraries. On minimal installations (like Debian slim or Alpine), install them first:

```
# Debian / Ubuntu
sudo apt install libfontconfig1 libfreetype6 libicu-dev

# Fedora
sudo dnf install fontconfig freetype icu

# Arch
sudo pacman -S fontconfig freetype2 icu
```

---

## 🍷 Running `.bat` files on non-Windows systems

Because `.bat` and `.cmd` are Windows-only formats, the **Run** button behaves differently on macOS/Linux:

1. **Wine is installed:** BatIDE automatically runs the script via `wine cmd /c "<file>"`.
2. **Wine is missing:** BatIDE shows a clear error message offering three options:

- Convert your script to `.sh` and run it natively.
- Install Wine, then retry.
- Run it on a Windows machine.

`.sh` files run natively using the interpreter from the shebang line (falling back to `/bin/sh`).

---

## ⚙️ Configuration

BatIDE stores your preferences (recent projects, last opened file, window state) in a JSON file in the platform-correct folder:

| OS ↕▾ | Settings File Location ↕▾ |
|---|---|
| −**Windows** | `%LOCALAPPDATA%\BatIDE\settings.json` |
| **Linux** | `~/.local/share/BatIDE/settings.json` |
| **macOS** | `~/Library/Application Support/BatIDE/settings.json` |
⚙

> **Note on encodings:** `.bat` and `.cmd` files must be UTF-8 **without BOM**. BatIDE automatically enforces this upon saving to prevent `cmd.exe` parsing errors.

---

## ⌨️ Keyboard Shortcuts

| Shortcut | Action |
|---|---|
| `Ctrl + S` | Save current file |
| `Ctrl + F` | Open Find / Replace panel |
| `Ctrl + Space` | Force autocompletion popup |
| `Ctrl + Z` | Undo |
| `Ctrl + Y` | Redo |
| `Ctrl + Shift + K` | Push to Git (Rider IDE default) |

---

## 📂 Project Structure

```
BatIDE/
├── App.axaml(.cs)              Application bootstrap & theme
├── Program.cs                  Application entry point
├── app.manifest                Windows DPI-awareness manifest
├── LOGO3.ico                   Application icon (cross-platform)
│
├── Models/
│   └── AppSettings.cs          Settings POCO (serialized to JSON)
│
├── Services/
│   ├── SettingsService.cs      Cross-platform JSON storage (debounced)
│   ├── ScriptRunner.cs         Script execution logic per OS
│   ├── BatCommands.cs          Autocomplete keyword list
│   ├── BatCompletionData.cs    AvaloniaEdit ICompletionData implementation
│   └── BatSyntaxHighlighting.cs XSHD syntax definition (cached)
│
└── Views/
    ├── MainWindow.axaml(.cs)   Start screen (Create / Open)
    └── EditorWindow.axaml(.cs) Code editor & project list
```

---

## 🏗️ Publishing

To build a self-contained single-file executable for distribution:

```
# Windows (x64)
dotnet publish BatIDE/BatIDE.csproj -c Release -r win-x64 --self-contained \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true -o ./publish/win-x64

# Linux (x64)
dotnet publish BatIDE/BatIDE.csproj -c Release -r linux-x64 --self-contained \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true -o ./publish/linux-x64

# macOS (Apple Silicon)
dotnet publish BatIDE/BatIDE.csproj -c Release -r osx-arm64 --self-contained \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true -o ./publish/osx-arm64
```

> ⚠️ `IncludeNativeLibrariesForSelfExtract=true` is **required** on Linux and macOS. Without it, the single-file binary cannot find `libSkiaSharp.so` / `libAvaloniaNative.dylib`.

---

## 🤝 Contributing

Contributions are always welcome!

1. Fork the repository.
2. Create your feature branch (`git checkout -b feature/AmazingFeature`).
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`).
4. Push to the branch (`git push origin feature/AmazingFeature`).
5. Open a Pull Request.

---

## 📄 License

This project is licensed under the **GPLv3** — see the LICENSE file for details.

**Copyright © 2023 – 2026 WER.**

