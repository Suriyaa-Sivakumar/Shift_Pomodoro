# Installation & Setup Guide: Flow for Windows

This guide covers installing the desktop application, configuring the portable release, and verifying your installation.

---

## 1. Option A: Windows Setup Installer (Recommended)

The setup installer provides an automated setup with desktop shortcuts, Start Menu integration, and a clean uninstaller.

1. Locate the installer executable:
   ```
   dist/installer/FlowWindows-Setup-x64.exe
   ```
2. Double-click `FlowWindows-Setup-x64.exe`.
3. If prompted by Windows SmartScreen:
   - Click **More info**.
   - Click **Run anyway**. *(This prompt appears because the binary is compiled locally without a commercial EV Code Signing certificate).*
4. Select the destination directory (defaults to `C:\Program Files\Flow for Windows` or user local apps).
5. Select whether to create a Desktop icon and Start Menu shortcut.
6. Click **Install**.
7. Once completed, select **Launch Flow for Windows** and click **Finish**.

### Uninstalling
To remove the application:
- Open **Windows Settings** $\to$ **Apps** $\to$ **Installed Apps**.
- Search for **Flow for Windows** and click **Uninstall**.
- Alternatively, run `unins000.exe` in the application install directory.

---

## 2. Option B: Portable Edition

The portable edition requires zero installation and can be run from any folder or USB flash drive.

1. Locate the portable ZIP archive:
   ```
   dist/FlowWindows-v1.0.0-win-x64-portable.zip
   ```
2. Extract the archive into a folder of your choice (e.g., `D:\Tools\FlowWindows`).
3. Open the extracted folder and double-click:
   ```
   FlowWindows.App.exe
   ```
4. All user configuration and session logs will automatically be saved into your local AppData folder (`%LocalAppData%\FlowWindows\Database\flow.db`).

---

## 3. Verifying Installation & Features

1. **Launch the Application**: Ensure the main window appears with the circular timer.
2. **Start a Focus Session**: Click the Play button or press Space.
3. **Verify Taskbar Progress**: Check the Windows taskbar; the Flow icon will display a green progress fill that advances as time elapses.
4. **Test Floating Mini Timer**:
   - In the top bar, click the **Mini Timer** button or use the tray icon context menu.
   - A floating window will appear on top of other applications.
5. **Test Minimize-to-Tray**:
   - Click the $[X]$ button on the main window.
   - The application will minimize directly to the system tray near the clock.
   - Double-click the tray icon to restore the main window.

