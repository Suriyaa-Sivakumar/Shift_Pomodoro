# Shift - Pomodoro ⏱️

> A modern, native Windows productivity companion designed for deep work, laser focus, and academic excellence. Engineered in C# and WinUI 3 (Windows App SDK) with monotonic precision timing, GitHub-style annual contribution heatmap, floating mini timer, dynamic taskbar countdown icon, study planner, task management, and offline SQLite storage.

---

## 🌟 Highlights

- **Native Windows 11 & 10 Experience**: Crafted with WinUI 3, Windows App SDK 1.6, and Mica backdrop material. High-contrast typography optimized for dark and light modes.
- **Monotonic Precision Timing Engine**: Wall-clock delta timing immune to UI thread lags and system sleep/wake states, with automatic crash recovery. Seamlessly handles custom session durations: 15m, 25m, 45m, 60m, 90m, 120m+.
- **GitHub-Style Annual Focus Heatmap**: Full 52-week activity calendar in the Statistics panel. Displays daily focus intensity squares, interactive hover tooltips (exact minutes & shift count), month labels, weekday indicators, streak counters, and annual hours summary.
- **Detachable Floating Mini-Timer**: Ultra-compact, draggable, always-on-top, position-memorized floating companion with real-time bidirectional synchronization with the main window.
- **Dynamic Taskbar Countdown & Progress**:
  - Dynamically transforms the Windows taskbar icon into an active countdown timer pill (`MM:SS`) while running.
  - Native `ITaskbarList3` green/amber taskbar progress bar.
- **Windows Startup Integration with Task Manager Sync**:
  - Automatically launches with Windows when enabled.
  - Full two-way synchronization with the **Windows Task Manager (`Startup apps` tab)** via native registry integration (`HKCU\Software\Microsoft\Windows\CurrentVersion\Run` and `StartupApproved\Run`).
- **Customizable Study Planner**: Track syllabus coverage, course units, exam countdown deadlines, and dedicated study hours per subject.
- **Integrated Tasks & Action Items**: Prioritize, plan, customize, and bind actionable tasks directly to active Pomodoro cycles.
- **Insights & Goal Targets**: Set custom daily/weekly minutes or session goals, discover productivity rhythms, and unlock milestone achievements.
- **Zero Cloud Footprint & 100% Privacy**: All data is stored locally on your machine in an offline SQLite WAL database (`%LOCALAPPDATA%\FlowWindows\Database\flow.db`), with built-in database backup/restore and CSV export.

---

## 📦 Releases & Downloads

| Package | Format | Architecture | Description |
|---|---|---|---|
| **[Setup Installer](dist/installer/Shift-Pomodoro-Setup-x64.exe)** | `.exe` (Inno Setup) | x64 | **Recommended**. Self-contained installer with Start Menu shortcuts, desktop icon, and uninstaller. Installs for current user (no admin rights needed) or machine-wide. |
| **[Portable Edition](dist/Shift-Pomodoro-v1.0.0-win-x64-portable.zip)** | `.zip` | x64 | Zero-install portable package. Extract anywhere and launch `FlowWindows.App.exe`. |

> [!NOTE]
> **No Prerequisites Required**: Both the installer and portable editions are 100% self-contained. You do not need to install the .NET Runtime, Windows App SDK, or Visual C++ redistributables.

---

## 🖥️ System Requirements

- **Operating System**: Windows 11 (all versions) or Windows 10 (Version 1809+, Build 17763+).
- **Architecture**: x64 (64-bit Intel/AMD).
- **Runtime**: Self-contained (all required runtimes are bundled).
- **Disk Space**: ~250 MB free space.

---

## 🚀 Quick Start

1. **Download & Install**: Run `Shift-Pomodoro-Setup-x64.exe` from `dist/installer/` or unzip the portable edition.
2. **Launch Shift**: Open the app from your Start Menu, Desktop shortcut, or by launching `FlowWindows.App.exe`.
3. **Select Mode & Duration**: Choose **Shift** (Focus), **Short Break**, or **Long Break**. Customize duration from the menu (`15m` to `90m+`).
4. **Start Focusing**: Click the central circular play button or press `Space` to start your focus block.
5. **Mini Timer**: Click the Mini Timer icon in the menu or press `Ctrl+Alt+M` to pop out the floating companion widget.
6. **Check Statistics**: Click the Stats icon in the top bar to view your capsule charts and the 52-week annual GitHub-style contribution heatmap.

---

## 🛠️ Project Structure

```
Shift_Pomodoro/
├── Assets/                              # Circular logos, vectors, and brand icons
├── dist/                                # Packaged release distribution files
│   ├── FlowWindows-x64/                 # Self-contained published binaries
│   ├── installer/                       # Setup installer (Shift-Pomodoro-Setup-x64.exe)
│   └── Shift-Pomodoro-v1.0.0-...zip     # Standalone portable archive
├── docs/                                # Technical & user documentation
│   ├── ARCHITECTURE.md                  # Subsystem interactions & architectural overview
│   ├── DATABASE_SCHEMA.md               # SQLite table definitions & indexing
│   ├── DEVELOPER_GUIDE.md               # Build tools, SDKs, and contribution instructions
│   ├── FEATURES.md                      # Comprehensive feature breakdown
│   ├── INSTALLATION.md                  # Detailed installation walkthrough
│   ├── KNOWN_LIMITATIONS.md             # Windows platform constraints & behaviors
│   ├── RELEASE_NOTES.md                 # v1.0.0 release notes & changelog
│   ├── TEST_REPORT.md                   # Automated unit testing log
│   └── USER_GUIDE.md                    # Detailed end-user manual
├── installer/                           # Inno Setup compiler configuration (FlowWindows.iss)
├── src/
│   ├── FlowWindows.Core                 # Domain models, enums, and service contracts
│   ├── FlowWindows.Data                 # SQLite WAL repository & database backup manager
│   ├── FlowWindows.Timer                # Monotonic timing engine & Pomodoro state machine
│   ├── FlowWindows.WindowsIntegration   # TaskbarList3, system tray, hotkeys & startup manager
│   ├── FlowWindows.Analytics            # Heatmap data, KPI calculation, and report generation
│   ├── FlowWindows.StudyPlanner         # Exam deadlines, syllabus topics, and study logs
│   ├── FlowWindows.TaskManagement       # Task backlog, subtasks, and focus linking
│   └── FlowWindows.App                  # WinUI 3 desktop application & pages
├── tests/
│   └── FlowWindows.UnitTests            # Automated xUnit test suite (33/33 tests passing)
└── FlowWindows.sln                      # Visual Studio / .NET 8 solution
```

---

## ⌨️ Keyboard Shortcuts

| Shortcut | Action | Scope |
|---|---|---|
| `Space` | Start / Pause Timer | Main Window |
| `R` | Reset Active Session | Main Window |
| `S` | Skip to Next Session | Main Window |
| `Ctrl + Alt + F` | Global Start / Pause Focus | System-wide (Configurable) |
| `Ctrl + Alt + S` | Global Skip Session | System-wide (Configurable) |
| `Ctrl + Alt + R` | Global Reset Timer | System-wide (Configurable) |
| `Ctrl + Alt + M` | Global Toggle Mini Timer | System-wide (Configurable) |
| `Esc` | Minimize / Dismiss overlay | Main Window / Mini Timer |

---

## 🛡️ Privacy & Local Storage

Shift - Pomodoro is built with a **strict offline-first architecture**:
- **No telemetry, analytics, or tracking**: Nothing is transmitted over the network.
- **Local SQLite Database**: All session history, tasks, subjects, goals, and settings are saved on your workstation in `%LOCALAPPDATA%\FlowWindows\Database\flow.db`.
- **Easy Backup & Export**: Export your complete session records to CSV or generate a printable HTML report anytime in **Settings > Data Management**.

---

## 📄 License

Created under the MIT License. Built for deep focus and productivity on Windows.
