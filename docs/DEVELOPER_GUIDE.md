# Developer & Contributor Guide: Flow for Windows

This guide contains technical instructions for setting up the development environment, compiling the solution from source, executing tests, debugging, and building release packages.

---

## 1. Prerequisites & Toolchain

To build and run Flow for Windows from source:

1. **Operating System**: Windows 11 x64 or Windows 10 (Version 1809+, Build 17763+).
2. **.NET 8 SDK**: .NET 8.0.400 or later (installed at `$env:LOCALAPPDATA\Microsoft\dotnet` or standard Program Files).
3. **Windows App SDK**: 1.6.241114003 or later (pulled automatically via NuGet).
4. **Inno Setup 6 (Optional)**: Required only for compiling the executable setup installer.

---

## 2. Directory Layout

```
FlowWindows/
├── FlowWindows.sln                      # Master solution file
├── src/
│   ├── FlowWindows.Core/                # Domain models, enums & interfaces
│   ├── FlowWindows.Data/                # SQLite WAL repository & migrations
│   ├── FlowWindows.Timer/               # Monotonic state machine & cycle tracking
│   ├── FlowWindows.WindowsIntegration/  # ITaskbarList3, NotifyIcon, Audio, Hotkeys
│   ├── FlowWindows.Analytics/           # Statistics, streaks & report generators
│   ├── FlowWindows.StudyPlanner/        # Exam & subject mastery management
│   ├── FlowWindows.TaskManagement/      # Tasks backlog & Pomodoro linking
│   └── FlowWindows.App/                 # WinUI 3 main executable & views
├── tests/
│   └── FlowWindows.UnitTests/           # xUnit test suite (22 tests)
└── installer/
    └── FlowWindows.iss                  # Inno Setup 6 compiler script
```

---

## 3. Building the Solution

### Restoring Dependencies
Open PowerShell in the repository root:
```powershell
dotnet restore FlowWindows.sln
```

### Compiling Debug
```powershell
dotnet build FlowWindows.sln -c Debug
```

### Compiling Release (x64)
```powershell
dotnet build FlowWindows.sln -c Release -p:Platform=x64
```

---

## 4. Running Automated Tests

The solution includes an xUnit automated test suite verifying timer state transitions, SQLite database operations, analytics aggregations, study planner schedules, crash recovery states, and report generation.

Execute all unit tests:
```powershell
dotnet test tests/FlowWindows.UnitTests/FlowWindows.UnitTests.csproj -c Release
```

Expected output:
```
Passed! - Failed: 0, Passed: 22, Skipped: 0, Total: 22
```

---

## 5. Publishing Self-Contained Executable

To generate a standalone, self-contained binary distribution that does not require any pre-installed .NET runtime on end-user machines:

```powershell
dotnet publish src/FlowWindows.App/FlowWindows.App.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=false `
  -o dist/FlowWindows-x64
```

This generates all binaries, runtime libraries, and assets into `dist/FlowWindows-x64/`.

---

## 6. Compiling the Inno Setup Release Installer

Ensure Inno Setup 6 is installed. Then compile the `.iss` script:

```powershell
$iscc = "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
if (-not (Test-Path $iscc)) {
    $iscc = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
}
& $iscc installer\FlowWindows-InnoSetup.iss
```

The resulting setup installer `FlowWindows-Setup-x64.exe` will be generated in `dist/installer/`.

---

## 7. Packaging the Portable Release ZIP

To create the zero-install portable zip archive:
```powershell
Compress-Archive -Path dist/FlowWindows-x64/* -DestinationPath dist/FlowWindows-v1.0.0-win-x64-portable.zip -Force
```

---

## 8. WinUI 3 Development Notes & Guidelines

1. **Unpackaged App Support**:
   - `FlowWindows.App.csproj` is configured with `<WindowsPackageType>None</WindowsPackageType>`.
   - Native bootstrap initialization is managed via `Microsoft.WindowsAppSDK`.
2. **Dispatcher Threading**:
   - Background services (`TimerEngine`, system events, heartbeat monitor) dispatch events on worker threads.
   - Any UI mutations must be dispatched via `Microsoft.UI.Dispatching.DispatcherQueue.TryEnqueue()`.
3. **Secondary Windows**:
   - WinUI 3 `Window` instances (such as `MiniTimerWindow`) should be instantiated and hidden/shown without destroying their native Win32 `HWND`.
   - Always use `AppWindow.Hide()` and `AppWindow.Show()` to prevent thread re-entrancy issues.
4. **Code Quality**:
   - Adhere to Microsoft C# coding conventions.
   - Use nullable reference types (`<Nullable>enable</Nullable>`).
   - Treat compiler warnings with high priority.

