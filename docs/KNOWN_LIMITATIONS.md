# Known Limitations & Platform Constraints

Flow for Windows is designed to operate seamlessly within standard Windows user-space security boundaries. This document outlines platform-specific limitations and the technical decisions made to address them.

---

## 1. Windows Taskbar Numerical Countdown Text

### Constraint:
The Windows 11 taskbar does not provide a supported public API for dynamically rendering arbitrary numerical countdown text (e.g., "24:59") directly into the application's taskbar icon button. Legacy mechanisms such as DeskBands were deprecated and removed by Microsoft in modern Windows releases.

### Architectural Solution:
- Flow for Windows integrates with the supported Win32 `ITaskbarList3` API, which renders a live circular or linear progress fill in the taskbar icon (`TBPF_NORMAL` green during focus, `TBPF_PAUSED` yellow during breaks).
- For users needing numerical second-by-second countdowns while working in full-screen applications, Flow provides the **Floating Mini Timer** (which stays on top of all windows) and live hover tooltips in the **System Tray**.

---

## 2. Windows SmartScreen First-Launch Prompt

### Constraint:
Because Flow for Windows is compiled as an independent open-source release without a commercial Extended Validation (EV) Code Signing Certificate, Windows Defender SmartScreen may display an *"Unrecognized app"* warning on initial download.

### Solution:
Click **More info** and select **Run anyway**. The complete source code is audited, 100% offline, and contains zero cloud or network dependencies.

---

## 3. Mica Backdrop on Windows 10

### Constraint:
The Mica material design backdrop is a native visual effect exclusive to Windows 11 (Build 22000 and later).

### Behavior:
When running on Windows 10 (Version 1809 to 21H2), the application automatically falls back to high-contrast acrylic or solid theme backgrounds, preserving full functionality and readability.

