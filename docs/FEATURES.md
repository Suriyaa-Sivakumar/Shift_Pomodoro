# Comprehensive Feature Guide: Flow for Windows

Flow for Windows implements an expansive suite of focus and productivity capabilities faithful to the minimalist desktop philosophy of the original macOS application.

---

## Module Index

1. [Main Focus Timer](#module-1-main-focus-timer)
2. [Advanced Pomodoro Engine](#module-2-advanced-pomodoro-engine)
3. [Windows Taskbar Integration](#module-3-windows-taskbar-integration)
4. [Floating Mini Timer](#module-4-floating-mini-timer)
5. [System Tray & Lifecycle Management](#module-5-system-tray--lifecycle-management)
6. [Commitment Mode](#module-6-commitment-mode)
7. [Statistics & Visual Analytics](#module-7-statistics--visual-analytics)
8. [Tags, Categories & Presets](#module-8-tags-categories--presets)
9. [Study Planner & Exam Milestones](#module-9-study-planner--exam-milestones)
10. [Task Management & Pomodoro Binding](#module-10-task-management--pomodoro-binding)
11. [Productivity Insights & Recommendations](#module-11-productivity-insights--recommendations)
12. [Goals & Achievement System](#module-12-goals--achievement-system)
13. [Data Export, Reports & Backups](#module-13-data-export-reports--backups)
14. [Audio Synthesizer & Notifications](#module-14-audio-synthesizer--notifications)
15. [Global Hotkeys & Accessibility](#module-15-global-hotkeys--accessibility)
16. [Settings & Personalization](#module-16-settings--personalization)

---

### Module 1: Main Focus Timer
- **Circular Progress Vector**: Procedural smooth vector ring rendering the exact session completion fraction.
- **Session Types**: Supports *Focus*, *Short Break*, and *Long Break* cycles with segmented quick switcher.
- **Monotonic Timing**: Built with UTC time comparisons resilient to clock adjustments, sleep/hibernate transitions, and system lags.
- **Crash Recovery & Heartbeat**: Real-time heartbeat state persisted in SQLite so interrupted sessions are safely recorded and never lost.
- **Direct Controls**: Start, Pause, Resume, Reset, and Skip actions.
- **Session Header**: Live display of current task, tag, or study topic.

### Module 2: Advanced Pomodoro Engine
- **Customizable Interval Durations**: Configurable focus, short break, and long break durations from 1 to 180 minutes.
- **Cycle Cadence**: Configurable number of focus sessions before triggering a long break (default: 4 sessions).
- **Auto-Transitions**: Independent toggle switches for automatically starting breaks and automatically starting subsequent focus intervals.
- **Presets**: Built-in default configurations (Standard 25/5/15, Deep Work 50/10/20, Study 45/10/20, Quick Focus 15/3/10) with custom preset creation.

### Module 3: Windows Taskbar Integration
- **`ITaskbarList3` Progress**: Live progress indicator rendered directly in the Windows taskbar icon.
- **State Coloring**:
  - Green progress indicator (`TBPF_NORMAL`) during active Focus sessions.
  - Yellow paused indicator (`TBPF_PAUSED`) during Breaks and Pauses.
  - Cleared indicator (`TBPF_NOPROGRESS`) upon session reset.
- **Thumbnail Support**: Windows thumbnail preview maintains live view of the circular timer.

### Module 4: Floating Mini Timer
- **Detachable Window**: Dedicated compact window designed to stay visible while writing, coding, or studying.
- **Always on Top**: Configurable `AppWindow.IsAlwaysOnTop` capability.
- **Dual Layouts**:
  - *Compact Mode*: Minimalist pill displaying remaining time and micro progress ring.
  - *Expanded Mode*: Displays session label, tag, time remaining, and quick action buttons (Pause, Skip, Hide).
- **Position Persistence**: Automatically saves and restores desktop $(X, Y)$ screen coordinates across app restarts.
- **Coordinated State**: Shares the authoritative `ITimerEngine` instance with the main window.

### Module 5: System Tray & Lifecycle Management
- **Persistent Tray Icon**: Accessible notification tray icon with live tooltip showing remaining session time.
- **Context Menu**: Right-click actions for quick start, pause, skip, open mini timer, open main window, and clean exit.
- **Minimize-to-Tray**: Configurable close behavior where clicking the window $[X]$ minimizes the application into the background tray without stopping the timer.
- **Single Instance Mutex**: Running `FlowWindows.App.exe` while an instance is active triggers IPC communication over a named pipe to restore the existing window.

### Module 6: Commitment Mode
- **Focus Shield**: Configurable commitment levels (`Off`, `Gentle`, `Strict`, `Lockdown`).
- **Confirmation Prompts**: In Gentle and Strict modes, prompts confirmation before resetting or skipping an active focus session.
- **Interruption Auditing**: Tracks any emergency overrides or premature resets into the local analytics log.

### Module 7: Statistics & Visual Analytics
- **KPI Metrics Cards**: Total focus time, total sessions completed, daily streak counter, and completion rate percentage.
- **7-Day Focus Distribution Chart**: Custom visual bar chart displaying focus hours logged for each day of the current week.
- **Category Breakdown**: Progress bars visualizing time spent across different focus tags (e.g., Deep Work, Study, Coding, Writing).
- **Session History Log**: Chronological table displaying all recorded focus intervals with timestamps and durations.

### Module 8: Tags, Categories & Presets
- **Custom Color-Coded Tags**: Organize work sessions into distinct subjects or projects.
- **Preset Manager**: Save, switch, and delete timing profiles for specific workflows.

### Module 9: Study Planner & Exam Milestones
- **Target Countdown**: Visual countdown timer showing days and hours remaining until scheduled exams or deadlines.
- **Subject Mastery Tracking**: Target study hours vs. actual completed hours with progress indicators.
- **Study Plan Overview**: Centralized dashboard linking study topics directly to Pomodoro sessions.

### Module 10: Task Management & Pomodoro Binding
- **Integrated Task List**: Add, edit, prioritize (Low, Medium, High), and complete tasks.
- **Pomodoro Linking**: Click "Focus" on any task to immediately load its title as the active timer session name and associate completed intervals with that task.
- **Estimated vs. Actual Pomodoros**: Track productivity estimation accuracy over time.

### Module 11: Productivity Insights & Recommendations
- **Prime Focus Window**: Automatically detects the time of day (Morning, Afternoon, Evening, Night) with the highest concentration of completed sessions.
- **AI-Style Rule Recommendations**: Actionable suggestions based on user patterns (e.g., optimal break intervals, fatigue detection).

### Module 12: Goals & Achievement System
- **Weekly Focus Targets**: Set daily and weekly focus hour goals with visual progress rings.
- **Unlockable Badges**:
  - *First Step*: Complete your first focus session.
  - *Deep Diver*: Complete 5 focus sessions in a single day.
  - *Consistency Master*: Maintain a 7-day focus streak.
  - *Centurion*: Accumulate 100 total hours of logged focus time.

### Module 13: Data Export, Reports & Backups
- **CSV Export**: Export all session logs to RFC-4180 compliant CSV files for spreadsheet analysis in Excel or Google Sheets.
- **HTML Performance Reports**: Formatted, standalone, printable HTML summary reports complete with styling, metrics, and session summaries.
- **Full Database Backup & Restore**: One-click complete database export and live restore for safe local migrations.

### Module 14: Audio Synthesizer & Notifications
- **Pure Math Audio Synthesis**: Generates crisp, ambient audio chimes directly in memory using PCM sinusoidal tone generation with exponential decay envelopes.
- **Sound Presets**: Bell, Chime, Digital, Subtle Gong, and Mute options.
- **Sound Previewing**: Test notification volume and sounds directly from Settings.
- **Native Windows Notifications**: Non-intrusive toast alerts upon session completion.

### Module 15: Global Hotkeys & Accessibility
- **System-Wide Keybinding**: `Ctrl+Alt+F` to start/pause focus from anywhere in Windows without switching windows.
- **Keyboard Navigation**: Full Tab-stop and high-contrast accessibility support across all WinUI 3 views.

### Module 16: Settings & Personalization
- **Theme Selection**: Seamless support for Light, Dark, and Windows System theme matching.
- **Backdrop Styling**: Native Windows 11 Mica / Acrylic material design.
- **Storage Management**: View database storage size, execute database backups, and manage configurations.

