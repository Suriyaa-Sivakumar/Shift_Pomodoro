# Flow for Windows — Comprehensive Project Document

**Version**: 1.0.0  
**Target Platform**: Windows 11 / Windows 10 (x64)  
**Framework**: .NET 8, WinUI 3 (Windows App SDK), SQLite  
**Architecture**: Local-First, Offline-Only, Unpackaged Desktop Application  

---

## 1. Executive Summary & Philosophy

**Flow for Windows** is a native Windows desktop productivity companion inspired by the minimalist aesthetic and behavioral psychology of *Flow for macOS*. Engineered in modern C# and WinUI 3, the application provides an authoritative Pomodoro and deep work timing system, seamlessly embedded into Windows with native taskbar progress, a floating mini timer, system tray background execution, and comprehensive analytics.

The application adheres strictly to the **Local-First & Offline Philosophy**:
- **Zero Cloud Dependencies**: No remote servers, user accounts, or external API requirements.
- **Zero Background Telemetry**: Session records, study plans, and preferences never leave the user's computer.
- **Data Integrity**: Local SQLite database in Write-Ahead Logging (WAL) mode with real-time heartbeat state and binary backup/restore.
- **Clean Focus**: Pure productivity assistance without invasive process termination or network proxy interception.

---

## 2. System Architecture & Topology

The application is structured into decoupled, modular projects following Clean Architecture principles:

```
FlowWindows.sln
├── src/
│   ├── FlowWindows.Core             # Domain entities, enums, events, and repository contracts
│   ├── FlowWindows.Data             # SQLite WAL repository with crash-safe heartbeat persistence
│   ├── FlowWindows.Timer            # Authoritative monotonic timing engine and cycle state machine
│   ├── FlowWindows.WindowsIntegration # Win32 ITaskbarList3, NotifyIcon tray, PCM sound synthesizer, hotkeys
│   ├── FlowWindows.Analytics        # Daily/weekly stats, multi-day streaks, insights, CSV & HTML reports
│   ├── FlowWindows.StudyPlanner     # Exam countdowns, subject targets, and topic mastery tracking
│   ├── FlowWindows.TaskManagement   # Task backlog, priority tiers, and Pomodoro session binding
│   └── FlowWindows.App              # WinUI 3 desktop presentation layer (MainWindow, MiniTimer, Views)
└── tests/
    └── FlowWindows.UnitTests        # Automated xUnit test suite (22 tests, 100% passing)
```

### Architecture Topology Diagram

```mermaid
graph TD
    App["FlowWindows.App (WinUI 3 Presentation)"]
    Timer["FlowWindows.Timer (State Machine & Engine)"]
    Data["FlowWindows.Data (SQLite WAL Repository)"]
    WinInt["FlowWindows.WindowsIntegration (Taskbar, Tray, Audio, Hotkeys)"]
    Analytics["FlowWindows.Analytics (Metrics, Insights & Reports)"]
    Study["FlowWindows.StudyPlanner (Exams & Milestones)"]
    Tasks["FlowWindows.TaskManagement (Backlog & Task Link)"]
    Core["FlowWindows.Core (Domain Entities & Contracts)"]

    App --> Timer
    App --> Data
    App --> WinInt
    App --> Analytics
    App --> Study
    App --> Tasks
    App --> Core

    Timer --> Core
    Data --> Core
    WinInt --> Core
    Analytics --> Core
    Study --> Core
    Tasks --> Core
```

---

## 3. Comprehensive Feature Inventory & Status

| Feature Component | Implementation Details | Status |
|---|---|---|
| **Main Focus Timer** | Circular progress arc, Start/Pause/Resume/Reset/Skip, segmented mode switcher (`[ Focus | Short Break | Long Break ]`), tag & task headers. | **VERIFIED** |
| **Monotonic Timing Engine** | Wall-clock delta timing via `DateTimeOffset.UtcNow`, resistant to UI delays, clock drift, and thread sleep. | **VERIFIED** |
| **Active Session Heartbeat** | Active timer state recorded every 5 seconds to `ActiveSessionState` SQLite table. | **VERIFIED** |
| **Crash Recovery** | On app restart, detects any unclosed sessions ($\ge 10\text{s}$) and archives them into history as interrupted sessions. | **VERIFIED** |
| **Sleep/Wake Resynchronization** | Listens to `Microsoft.Win32.SystemEvents.PowerModeChanged` and `TimeChanged` for instant resync. | **VERIFIED** |
| **Floating Mini Timer** | Secondary `AppWindow` with `IsAlwaysOnTop = true`, draggable, compact/expanded modes, position memorization. | **VERIFIED** |
| **Windows Taskbar Integration** | Native `ITaskbarList3` progress fill in taskbar icon (`TBPF_NORMAL` green during focus, `TBPF_PAUSED` yellow on pause/break). | **VERIFIED** |
| **System Tray & Lifecycle** | WinForms `NotifyIcon` with countdown tooltip, context menu, minimize-to-tray on close, and clean exit. | **VERIFIED** |
| **Single-Instance Mutex** | Named mutex `Local\FlowWindows_SingleInstance_Mutex_2026` + named pipe activation prevents duplicate instances. | **VERIFIED** |
| **Commitment Mode** | Tiers: `Off`, `Gentle`, `Strict`, `Lockdown`. Prompts confirmation dialogs before resetting or skipping active sessions. | **VERIFIED** |
| **Task Management** | Backlog with priority badges (High, Medium, Low), task completion toggles, and one-click "Focus" linking to timer. | **VERIFIED** |
| **Study Planner** | Subject creation, target hours tracking, exam countdown dates, and one-click "Study" timer activation. | **VERIFIED** |
| **Productivity Analytics** | Local timezone calculations, daily/weekly stats, multi-day streaks, prime focus time analysis, and category charts. | **VERIFIED** |
| **Goals & Milestones** | Daily minutes and session targets with progress percentage indicators and automatic progress advancement. | **VERIFIED** |
| **Reports & Exports** | RFC-4180 CSV session logs export and formatted standalone HTML productivity reports with CSS styling. | **VERIFIED** |
| **Audio Notification Synthesizer** | Pure math-synthesized harmonic PCM WAV chimes (Bell, Chime, Digital) with zero external audio assets. | **VERIFIED** |
| **Global Hotkeys** | `Ctrl+Alt+F` global hotkey toggle via Win32 `RegisterHotKey`. | **VERIFIED** |
| **Database Management** | SQLite WAL mode, binary database backup (`ExportDatabaseBackupAsync`) and live restoration. | **VERIFIED** |

---

## 4. Authoritative Timing Engine Specification

The timer is the core component of Flow for Windows. To eliminate clock drift and freezing during UI thread stalls or OS sleep states, the timing engine uses an **authoritative wall-clock delta algorithm**:

### Timing Formula

$$\text{Remaining Time} = \max\left(0, \text{TotalDuration} - \left(\text{AccumulatedElapsed} + (\text{UtcNow} - \text{SegmentStartTime})\right)\right)$$

$$\text{Progress Fraction} = \min\left(1.0, \frac{\text{ElapsedTime}}{\text{TotalDuration}}\right)$$

### Engine Capabilities

1. **Drift Immunity**: Elapsed time is computed directly against the system UTC clock. If the UI thread is delayed or frozen by heavy disk/GPU work, the timer automatically reports the correct remaining time upon the next tick.
2. **Sleep & Wake Recovery**: When Windows enters sleep or modern connected standby, interval tick loops suspend. Upon resume, `SystemEvents.PowerModeChanged` triggers `SyncExternalTick()`, instantly advancing the elapsed time to match the wall-clock delta.
3. **Heartbeat Persistence**: Every 5 seconds during an active session, `TimerEngine` saves an `ActiveSessionState` record to the SQLite database. If power is cut or the application is forcefully terminated, no work is lost.
4. **Crash Recovery**: When `MainWindow` initializes, it checks for an existing `ActiveSessionState`. If an unclosed session with at least 10 seconds of elapsed focus is found, it is automatically saved to the `Sessions` table with `Interrupted = true` and annotated as recovered, then cleared from active state.
5. **Zero Progress Ring Visual Artifact**: When progress is 0% (e.g., at the start of a session), the progress arc path is collapsed (`Visibility.Collapsed`), preventing WinUI 3 from rendering overlapping round caps at the top vertex.

---

## 5. Local Database Schema & Persistence

Database files are stored in:
`%LocalAppData%\FlowWindows\Database\flow.db`

### SQLite Configuration
- **Journal Mode**: `WAL` (Write-Ahead Logging) for non-blocking concurrent reads and safe writes.
- **Synchronous**: `NORMAL`
- **Foreign Keys**: `ON`

### Table Definitions

| Table Name | Primary Key | Description |
|---|---|---|
| `Sessions` | `Id` (TEXT GUID) | Recorded focus intervals, durations, timestamps, completion flags, and linked tags/tasks/subjects. |
| `TimerPresets` | `Id` (TEXT GUID) | Timing configurations (Standard 25/5/15, Deep Work 50/10/20, Study 45/10/20, Quick Focus 15/3/10). |
| `Tags` | `Id` (TEXT GUID) | Custom color-coded categorization tags (Deep Work, Study, Reading, Coding, Writing). |
| `Tasks` | `Id` (TEXT GUID) | Task items, priority levels, estimated minutes, time spent, and completion status. |
| `Subtasks` | `Id` (TEXT GUID) | Checkable sub-items linked to a parent task. |
| `StudySubjects` | `Id` (TEXT GUID) | Academic subjects, target hours, logged hours, and scheduled exam dates. |
| `StudyTopics` | `Id` (TEXT GUID) | Modules and topics associated with a study subject. |
| `Goals` | `Id` (TEXT GUID) | Daily and weekly focus time and session goals with target and current values. |
| `Achievements` | `Id` (TEXT GUID) | Productivity milestones and unlocked badges. |
| `AppSettings` | `Key` (TEXT) | Key-value store containing serialized JSON preferences (`MainSettings`). |
| `ActiveSessionState` | `Id` (TEXT GUID) | Singleton heartbeat state table for crash recovery and session resumption. |

---

## 6. Testing & Quality Assurance

### Automated Unit Test Suite
The test suite in `tests/FlowWindows.UnitTests` contains **22 automated tests** targeting all core engine mechanics and domain layers.

| Test Class | Test Count | Key Invariants Verified | Result |
|---|---|---|---|
| `TimerEngineTests` | 8 | Monotonic accuracy, pause/resume elapsed time preservation, cycle advancing, auto-transitions, completion events, heartbeat persistence, reset state clearing. | **PASS** |
| `RepositoryAndDataTests` | 6 | SQLite schema initialization, default seeding, session CRUD, settings JSON updates, tag additions, active state save/retrieve/clear, database backup & restore. | **PASS** |
| `AnalyticsAndGoalsTests` | 3 | Daily summary aggregation, local timezone day boundaries, multi-day consecutive streak calculations, goal progression upon session completion. | **PASS** |
| `TaskAndStudyTests` | 2 | Task status toggling, subject and topic creation, logged hours progression. | **PASS** |
| `ReportServiceTests` | 2 | RFC-4180 CSV export generation, standalone HTML productivity report rendering with empty and populated datasets. | **PASS** |

### Live Windows Verification
- **App Startup**: Launch time $< 1.2\text{s}$, initial working set $\approx 29.5\text{MB}$.
- **UI Responsiveness**: Smooth navigation between Timer, Statistics, Tasks, Study Planner, Insights, and Settings.
- **Taskbar Progress**: Verified `ITaskbarList3` green progress updates in Windows 11 taskbar.
- **Mini Timer**: Verified always-on-top positioning, drag movement, compact/expanded modes, and synchronization with main window.
- **Clean Exit**: Verified closing to tray keeps timer running, and "Exit Flow" from tray menu terminates all threads with zero orphaned processes.

---

## 7. Build, Packaging & Distribution

### Distribution Packages

| Deliverable | Location | Description |
|---|---|---|
| **Release Binaries** | `dist\FlowWindows-x64\` | Self-contained, optimized release build containing all .NET 8 and Windows App SDK assemblies. |
| **Portable Zip Archive** | `dist\FlowWindows-v1.0.0-win-x64-portable.zip` | Zero-install portable zip archive (89.5 MB). Extract and run anywhere. |
| **Setup Installer Script** | `installer\FlowWindows.iss` | Inno Setup compiler configuration with Start Menu and desktop icon support. |
| **Compiled Setup Installer** | `dist\installer\FlowWindows-Setup-x64.exe` | Standalone setup installer executable (60.5 MB). |

### Compilation Commands

```powershell
# 1. Run unit test suite
dotnet test tests\FlowWindows.UnitTests\FlowWindows.UnitTests.csproj -c Release

# 2. Publish self-contained release binaries
dotnet publish src\FlowWindows.App\FlowWindows.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o dist\FlowWindows-x64

# 3. Create portable zip package
Compress-Archive -Path dist\FlowWindows-x64\* -DestinationPath dist\FlowWindows-v1.0.0-win-x64-portable.zip -CompressionLevel Optimal

# 4. Compile Inno Setup installer
& "ISCC.exe" installer\FlowWindows.iss
```

---

## 8. Keyboard Shortcuts & User Reference

| Shortcut | Context | Action |
|---|---|---|
| `Space` | Main Timer / Mini Timer | Start / Pause active session |
| `R` | Main Timer | Reset active session (respects Commitment Mode) |
| `S` | Main Timer | Skip to next session (respects Commitment Mode) |
| `Ctrl+Alt+F` | System-Wide | Global hotkey to toggle Focus / Pause from any app |
| `Esc` | Mini Timer | Close / Hide floating mini timer |

---

## 9. Platform Constraints & Behavior

- **Windows SmartScreen**: As an independently compiled local desktop application, Windows Defender SmartScreen may display an *"Unrecognized app"* advisory on first execution. Click **More info** $\to$ **Run anyway** to proceed.
- **Mica Backdrop**: Native Windows 11 Mica material is rendered automatically on Windows 11 (Build 22000+). On Windows 10, the UI renders cleanly with solid surface colors.
- **Audio Playback**: Chimes are generated using internal PCM math synthesis via `System.Media.SoundPlayer`. Volume control adjusts sound amplitude in the synthesized WAV data.

