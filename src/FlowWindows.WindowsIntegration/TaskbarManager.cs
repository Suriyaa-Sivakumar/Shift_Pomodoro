using System;
using System.Runtime.InteropServices;
using FlowWindows.Core.Enums;

namespace FlowWindows.WindowsIntegration;

public enum TaskbarProgressBarStatus
{
    NoProgress = 0,
    Indeterminate = 0x1,
    Normal = 0x2,
    Error = 0x4,
    Paused = 0x8
}

[ComImport]
[Guid("ea1afb91-9e28-4b86-90e9-9e9f8a5eefaf")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ITaskbarList3
{
    // ITaskbarList
    [PreserveSig] void HrInit();
    [PreserveSig] void AddTab(IntPtr hwnd);
    [PreserveSig] void DeleteTab(IntPtr hwnd);
    [PreserveSig] void ActivateTab(IntPtr hwnd);
    [PreserveSig] void SetActiveAlt(IntPtr hwnd);

    // ITaskbarList2
    [PreserveSig] void MarkFullscreenWindow(IntPtr hwnd, [MarshalAs(UnmanagedType.Bool)] bool fFullscreen);

    // ITaskbarList3
    [PreserveSig] void SetProgressValue(IntPtr hwnd, ulong ullCompleted, ulong ullTotal);
    [PreserveSig] void SetProgressState(IntPtr hwnd, TaskbarProgressBarStatus tbpFlags);
    [PreserveSig] void RegisterTab(IntPtr hwndTab, IntPtr hwndMDI);
    [PreserveSig] void UnregisterTab(IntPtr hwndTab);
    [PreserveSig] void SetTabOrder(IntPtr hwndTab, IntPtr hwndInsertBefore);
    [PreserveSig] void SetTabActive(IntPtr hwndTab, IntPtr hwndMDI, uint dwReserved);
    [PreserveSig] void ThumbBarAddButtons(IntPtr hwnd, uint cButtons, IntPtr pButton);
    [PreserveSig] void ThumbBarUpdateButtons(IntPtr hwnd, uint cButtons, IntPtr pButton);
    [PreserveSig] void ThumbBarSetImageList(IntPtr hwnd, IntPtr himl);
    [PreserveSig] void SetOverlayIcon(IntPtr hwnd, IntPtr hIcon, [MarshalAs(UnmanagedType.LPWStr)] string pszDescription);
    [PreserveSig] void SetThumbnailTooltip(IntPtr hwnd, [MarshalAs(UnmanagedType.LPWStr)] string pszTip);
    [PreserveSig] void SetThumbnailClip(IntPtr hwnd, IntPtr prcClip);
}

[ComImport]
[Guid("56fdf344-fd6d-11d0-958a-006097c9a090")]
[ClassInterface(ClassInterfaceType.None)]
internal class TaskbarInstance
{
}

public class TaskbarManager
{
    private static ITaskbarList3? _taskbarList;
    private static bool _initialized = false;
    private static readonly object _lock = new();

    private static ITaskbarList3? Instance
    {
        get
        {
            lock (_lock)
            {
                if (!_initialized)
                {
                    try
                    {
                        _taskbarList = (ITaskbarList3)new TaskbarInstance();
                        _taskbarList.HrInit();
                        _initialized = true;
                    }
                    catch
                    {
                        _taskbarList = null;
                        _initialized = true;
                    }
                }
                return _taskbarList;
            }
        }
    }

    public static void SetProgress(IntPtr hwnd, double fraction, SessionType sessionType, SessionState state)
    {
        if (hwnd == IntPtr.Zero) return;

        try
        {
            var tb = Instance;
            if (tb == null) return;

            if (state == SessionState.Idle || state == SessionState.Completed)
            {
                tb.SetProgressState(hwnd, TaskbarProgressBarStatus.NoProgress);
                return;
            }

            var status = state switch
            {
                SessionState.Paused => TaskbarProgressBarStatus.Paused,
                SessionState.Running => TaskbarProgressBarStatus.Normal,
                _ => TaskbarProgressBarStatus.NoProgress
            };

            tb.SetProgressState(hwnd, status);
            var completed = (ulong)Math.Clamp(fraction * 1000.0, 0, 1000.0);
            tb.SetProgressValue(hwnd, completed, 1000);
        }
        catch
        {
            // Ignore Taskbar API errors if running without DWM
        }
    }

    public static void ClearProgress(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return;
        try
        {
            Instance?.SetProgressState(hwnd, TaskbarProgressBarStatus.NoProgress);
        }
        catch { }
    }
}

