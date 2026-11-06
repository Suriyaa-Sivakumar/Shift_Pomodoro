using System;
using System.IO;
using Microsoft.Win32;

namespace FlowWindows.WindowsIntegration;

public static class StartupManager
{
    private const string RunRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string StartupApprovedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    public const string AppRegistryName = "ShiftPomodoro";

    /// <summary>
    /// Checks whether Shift Pomodoro is configured to launch on Windows startup,
    /// taking into account user enable/disable preferences in Windows Task Manager.
    /// </summary>
    public static bool IsStartupEnabled()
    {
        try
        {
            using var runKey = Registry.CurrentUser.OpenSubKey(RunRegistryKey, false);
            if (runKey == null) return false;

            var val = runKey.GetValue(AppRegistryName);
            if (val == null) return false;

            // Check if Task Manager has disabled this startup app
            using var approvedKey = Registry.CurrentUser.OpenSubKey(StartupApprovedKey, false);
            if (approvedKey != null)
            {
                var approvedData = approvedKey.GetValue(AppRegistryName) as byte[];
                if (approvedData != null && approvedData.Length > 0)
                {
                    // In Windows Task Manager:
                    // First byte 0x02 (or even) = Enabled
                    // First byte 0x03 (or odd) = Disabled by user in Task Manager
                    if ((approvedData[0] & 1) == 1)
                    {
                        return false; // Disabled in Task Manager
                    }
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Enables or disables automatic launch on Windows startup.
    /// The entry is registered in HKCU\Run so it appears natively in Windows Task Manager's "Startup apps" tab,
    /// allowing the user to view, enable, or disable it directly in Task Manager whenever they wish.
    /// </summary>
    public static bool SetStartupEnabled(bool enable, string? customExecutablePath = null)
    {
        try
        {
            using var runKey = Registry.CurrentUser.OpenSubKey(RunRegistryKey, true);
            if (runKey == null) return false;

            if (enable)
            {
                string exePath = customExecutablePath ?? Environment.ProcessPath ?? string.Empty;
                if (string.IsNullOrEmpty(exePath))
                {
                    exePath = Path.Combine(AppContext.BaseDirectory, "FlowWindows.App.exe");
                }

                // Format with quotes to handle spaces in folder paths
                string command = $"\"{exePath}\"";
                runKey.SetValue(AppRegistryName, command, RegistryValueKind.String);

                // If disabled previously in Task Manager, re-enable it in StartupApproved
                try
                {
                    using var approvedKey = Registry.CurrentUser.OpenSubKey(StartupApprovedKey, true);
                    if (approvedKey != null)
                    {
                        var data = approvedKey.GetValue(AppRegistryName) as byte[];
                        if (data != null && data.Length > 0 && (data[0] & 1) == 1)
                        {
                            data[0] = 0x02; // Mark enabled
                            approvedKey.SetValue(AppRegistryName, data, RegistryValueKind.Binary);
                        }
                    }
                }
                catch { }

                return true;
            }
            else
            {
                if (runKey.GetValue(AppRegistryName) != null)
                {
                    runKey.DeleteValue(AppRegistryName, false);
                }
                return true;
            }
        }
        catch
        {
            return false;
        }
    }
}

