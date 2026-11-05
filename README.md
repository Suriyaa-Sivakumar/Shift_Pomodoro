# Flow for Windows ⏱️

> A native Windows 11 productivity and focus companion inspired by the minimalist philosophy of Flow for macOS. Engineered in C# and WinUI 3 with monotonic precision timing, Windows taskbar integration, floating mini-timer, productivity analytics, study planning, task management, and offline SQLite storage.

---

## 🌟 Highlights

- **Native Windows 11 Experience**: Crafted with WinUI 3, Windows App SDK 1.6, and Mica backdrop material.
- **Monotonic Precision Timing Engine**: Wall-clock delta timing resistant to UI delays, sleep/wake aware, application crash and unexpected termination recovery. Single authoritative timer engine driving all windows and controls.
- **Windows Taskbar Progress Integration**: Live `ITaskbarList3` green/yellow taskbar progress states reflecting active session completion directly in the Windows taskbar.
- **Detachable Floating Mini-Timer**: Lightweight, compact/expanded views, draggable, always-on-top, position-memorized floating companion that runs even when the main window is closed.
- **System Tray Management**: Seamless minimize-to-tray lifecycle with single-instance mutex and named-pipe activation.
- **Commitment Mode**: Enforces focus sessions with gentle or strict guards against accidental resets and skips.
- **Productivity Analytics & Insights**: Streak tracking, daily/weekly focus charts, prime focus window detection, CSV export and formatted HTML report generation.
- **Integrated Study Planner & Tasks**: Exam countdown timers, subject mastery trackers, task lists linked directly into Pomodoro cycles.
- **Built-in Audio Synthesizer**: Pure math-synthesized harmonic bell and chime alerts (PCM WAV generation) without external audio dependencies.
- **Zero Cloud Footprint**: 100% offline, privacy-first SQLite database in WAL mode with full database backup and restore.

---

## 📦 Releases & Downloads

| Package | Format | Architecture | Description |
|---|---|---|---|
| **[Setup Installer](dist/installer/FlowWindows-Setup-x64.exe)** | `.exe` (Inno Setup) | x64 | Full installer with Start Menu shortcuts, uninstaller, and desktop icon. Self-contained (no .NET runtime required). |
| **[Portable Edition](dist/FlowWindows-v1.0.0-win-x64-portable.zip)** | `.zip` | x64 | Zero-install portable folder. Unzip anywhere and launch `FlowWindows.App.exe`. |

---

## 🖥️ System Requirements

- **Operating System**: Windows 11 (Build 22000+) or Windows 10 (Version 1809+, Build 17763+).
- **Architecture**: x64 (64-bit Intel/AMD).
- **Runtime**: None required for release binaries (self-contained deployment includes .NET 8 and Windows App SDK).
- **Disk Space**: ~250 MB free space.

---

## 🚀 Quick Start

1. Download `FlowWindows-Setup-x64.exe` from `dist/installer/` or unzip the portable release.
2. Launch **Flow for Windows**.
3. Select your mode (`Focus`, `Short Break`, or `Long Break`) or pick a preset.
4. Click the circular timer or press **Start** to begin your session!

---

## 🛠️ Project Structure

```
FlowWindows/
├── assets/                          # App icons, vectors, and UI graphics
├── dist/                            # Packaged release binaries
│   ├── FlowWindows-x64/             # Self-contained published binaries
│   ├── installer/                   # Setup installer (Inno Setup: FlowWindows-Setup-x64.exe)
│   └── FlowWindows-v1.0.0-...zip    # Portable release zip
├── docs/                            # In-depth architectural & user documentation
│   ├── ARCHITECTURE.md              # Technical design and subsystem interaction
│   ├── DATABASE_SCHEMA.md           # SQLite tables, indexes, and queries
│   ├── DEVELOPER_GUIDE.md           # Build tools, SDKs, and contribution guide
│   ├── FEATURES.md                  # Comprehensive breakdown of features
│   ├── INSTALLATION.md              # Installation walkthrough & setup
│   ├── KNOWN_LIMITATIONS.md         # Windows platform constraints & behaviors
│   ├── RELEASE_NOTES.md             # v1.0.0 changelog & feature breakdown
│   ├── TEST_REPORT.md               # Unit test outcomes & verification log
│   └── USER_GUIDE.md                # End-user manual & workflow guides
├── installer/                       # Inno Setup compiler scripts (.iss)
├── src/
│   ├── FlowWindows.Core             # Domain entities, models, and contracts
│   ├── FlowWindows.Data             # SQLite WAL repository & backup services
│   ├── FlowWindows.Timer            # Monotonic state machine & cycle tracking
│   ├── FlowWindows.WindowsIntegration # TaskbarList3, NotifyIcon, Audio, Hotkeys
│   ├── FlowWindows.Analytics        # KPIs, streaks, prime focus, and reports
│   ├── FlowWindows.StudyPlanner     # Exam schedules, subjects, and milestones
│   ├── FlowWindows.TaskManagement   # Task backlog and Pomodoro binding
│   └── FlowWindows.App              # WinUI 3 desktop application & pages
├── tests/
│   └── FlowWindows.UnitTests        # xUnit automated test suite (22/22 passing)
└── FlowWindows.sln                  # Visual Studio / .NET 8 solution
```

---

## 🧭 Navigation & Shortcuts

- **Space**: Start / Pause Timer
- **R**: Reset Active Session
- **S**: Skip to Next Session
- **Ctrl+Alt+F**: Global Toggle Focus (Configurable in Settings)
- **Esc**: Hide Mini Timer / Dismiss overlay

---

## 🛡️ Privacy & Security

Flow for Windows does not transmit any telemetry, metrics, or personal data over the internet. All session logs, tasks, study plans, and preferences are stored exclusively on your local workstation in `%LocalAppData%\FlowWindows\Database\flow.db`.

---

## 📄 License

Created under the MIT License. Built with ❤️ for deep focus on Windows.
