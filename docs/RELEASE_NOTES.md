# Release Notes: Flow for Windows v1.0.0

**Release Date**: 2026-11-05  
**Version**: 1.0.0-release  
**Target Platform**: Windows 11 / Windows 10 x64  

---

## 🌟 Welcome to Flow for Windows v1.0.0!

We are proud to announce the initial release of **Flow for Windows**—a native Windows desktop productivity companion recreating the clean, minimalist focus experience of Flow for macOS.

---

## 🚀 Key Features

### Precision Focus Engine
- **Circular Vector Timer**: Smooth vector ring with Segoe UI typography and visual cycle dots.
- **Monotonic Precision Clock**: Sleep/wake aware timing engine that prevents clock drift during workstation sleep, window movements, or background suspension.
- **Configurable Pomodoro Cadence**: Focus, Short Break, Long Break, and customizable cycle limits.
- **Built-in Presets**: Standard (25/5), Deep Work (50/10), Study (45/10), and Quick Focus (15/3).

### Seamless Windows Integration
- **Taskbar Progress Indicator**: Real-time `ITaskbarList3` green/yellow progress fill in the taskbar icon.
- **Floating Mini Timer**: Lightweight, draggable, always-on-top window with compact and expanded views.
- **System Tray Management**: Background tray icon with live countdown tooltip, context menu, and minimize-to-tray window handling.
- **Single Instance & IPC**: Named pipe activation prevents multiple duplicate instances.
- **Global Hotkey**: Toggle focus from any window using `Ctrl+Alt+F`.

### Attention & Resilience Protection
- **Commitment Mode**: Enforces focus discipline by confirming or locking premature resets and skips during active sessions.
- **Crash Recovery & Heartbeat**: Real-time heartbeat persistence ensures interrupted sessions are recorded without data loss.
- **Sleep/Wake & Clock Synchronization**: Resumes timing seamlessly after Windows standby or system clock shifts.

### Planning, Tasks & Analytics
- **Task Backlog**: Create tasks, set priorities, and link them directly to Pomodoro intervals.
- **Study Planner**: Track upcoming exam countdowns, subject targets, and cumulative hours.
- **Visual Analytics**: KPI summary cards, 7-day focus bar chart, and tag distribution graphs.
- **Data Export**: RFC-4180 compliant CSV export and printable standalone HTML productivity reports.
- **Achievements & Insights**: Prime focus window detection and 4 unlockable milestones.

### Audio & Customization
- **Procedural Audio Synthesizer**: Pure sinusoidal PCM audio generation with exponential decay chimes.
- **Offline SQLite WAL Database**: Zero cloud dependencies, fast local storage, and one-click JSON backup/restore.

---

## 📦 Distribution Packages

| Package | Filename | Size | SHA256 Checksum |
|---|---|---|---|
| **Setup Installer** | `FlowWindows-Setup-x64.exe` | 60.6 MB | `06AB80C1F9689CBDBDBFA996D7A7C07C6C047CEA33853A609438B03EE85D5F68` |
| **Portable Zip** | `FlowWindows-v1.0.0-win-x64-portable.zip` | 89.6 MB | `A78F76B6A890A6C33526F260A3E0622848C089B86567D5C89C6F5F9F5DC51687` |

---

## 📋 System Requirements
- Windows 11 (all versions) or Windows 10 (Version 1809+, 64-bit).
- ~250 MB free disk space.
- No external .NET runtime required (self-contained binaries).
