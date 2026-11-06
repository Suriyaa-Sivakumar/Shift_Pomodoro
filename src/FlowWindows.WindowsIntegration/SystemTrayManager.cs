using System;
using System.Drawing;
using System.Windows.Forms;
using FlowWindows.Core.Common;
using FlowWindows.Core.Enums;
using FlowWindows.Core.Interfaces;

namespace FlowWindows.WindowsIntegration;

public class SystemTrayManager : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _contextMenu;
    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _startPauseItem;
    private readonly ToolStripMenuItem _skipItem;
    private readonly ToolStripMenuItem _miniTimerItem;

    public event Action? OpenMainWindowRequested;
    public event Action? ToggleMiniTimerRequested;
    public event Action? StartPauseRequested;
    public event Action? SkipRequested;
    public event Action? ExitRequested;

    public SystemTrayManager()
    {
        _contextMenu = new ContextMenuStrip();

        _statusItem = new ToolStripMenuItem("Shift - Idle") { Enabled = false };
        var openItem = new ToolStripMenuItem("Open Shift", null, (s, e) => OpenMainWindowRequested?.Invoke());
        openItem.Font = new Font(openItem.Font, FontStyle.Bold);

        _miniTimerItem = new ToolStripMenuItem("Toggle Mini Timer", null, (s, e) => ToggleMiniTimerRequested?.Invoke());
        _startPauseItem = new ToolStripMenuItem("Start Focus", null, (s, e) => StartPauseRequested?.Invoke());
        _skipItem = new ToolStripMenuItem("Skip Session", null, (s, e) => SkipRequested?.Invoke());
        var exitItem = new ToolStripMenuItem("Exit Shift", null, (s, e) => ExitRequested?.Invoke());

        _contextMenu.Items.Add(_statusItem);
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add(openItem);
        _contextMenu.Items.Add(_miniTimerItem);
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add(_startPauseItem);
        _contextMenu.Items.Add(_skipItem);
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add(exitItem);

        _notifyIcon = new NotifyIcon
        {
            Icon = CreateDefaultTrayIcon(Color.FromArgb(255, 82, 82)),
            Text = "Shift - Pomodoro",
            ContextMenuStrip = _contextMenu,
            Visible = true
        };

        _notifyIcon.DoubleClick += (s, e) => OpenMainWindowRequested?.Invoke();
        _notifyIcon.MouseClick += (s, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                OpenMainWindowRequested?.Invoke();
            }
        };
    }

    public void UpdateStatus(SessionState state, SessionType type, TimeSpan remainingTime)
    {
        var typeName = type switch
        {
            SessionType.Focus => "Focus",
            SessionType.ShortBreak => "Short Break",
            SessionType.LongBreak => "Long Break",
            _ => "Shift"
        };

        var timeStr = TimeFormatHelper.FormatTimer(remainingTime);
        var stateStr = state switch
        {
            SessionState.Running => $"Running ({timeStr})",
            SessionState.Paused => $"Paused ({timeStr})",
            SessionState.Completed => "Completed",
            _ => "Idle"
        };

        _statusItem.Text = $"{typeName}: {stateStr}";
        _startPauseItem.Text = state == SessionState.Running ? "Pause" : "Start";

        var tooltip = $"Shift - {typeName} ({timeStr})";
        if (tooltip.Length >= 64) tooltip = tooltip.Substring(0, 63);
        _notifyIcon.Text = tooltip;

        // Update icon color based on session
        var iconColor = type switch
        {
            SessionType.Focus => Color.FromArgb(255, 82, 82),
            SessionType.ShortBreak => Color.FromArgb(68, 138, 255),
            SessionType.LongBreak => Color.FromArgb(124, 77, 255),
            _ => Color.White
        };
        _notifyIcon.Icon = CreateDefaultTrayIcon(iconColor);
    }

    public void ShowNotification(string title, string message)
    {
        try
        {
            _notifyIcon.ShowBalloonTip(3000, title, message, ToolTipIcon.Info);
        }
        catch { }
    }

    private static Icon CreateDefaultTrayIcon(Color accentColor)
    {
        try
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
            if (File.Exists(iconPath))
            {
                return new Icon(iconPath, 16, 16);
            }
        }
        catch { }

        using var bitmap = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            using var pen = new Pen(accentColor, 2f);
            g.DrawEllipse(pen, 2, 2, 11, 11);

            using var brush = new SolidBrush(accentColor);
            g.FillEllipse(brush, 5, 5, 5, 5);
        }
        return Icon.FromHandle(bitmap.GetHicon());
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _contextMenu.Dispose();
    }
}

