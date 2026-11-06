using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Runtime.InteropServices;

namespace FlowWindows.WindowsIntegration;

public class DynamicTaskbarIconService : IDisposable
{
    private const int WM_SETICON = 0x0080;
    private const int ICON_SMALL = 0;
    private const int ICON_BIG = 1;
    private const int GCLP_HICON = -14;
    private const int GCLP_HICONSM = -34;

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll", EntryPoint = "SetClassLongPtr")]
    private static extern IntPtr SetClassLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetClassLong")]
    private static extern IntPtr SetClassLong32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    private static IntPtr SetClassLong(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
    {
        return IntPtr.Size == 8 ? SetClassLongPtr64(hWnd, nIndex, dwNewLong) : SetClassLong32(hWnd, nIndex, dwNewLong);
    }

    private readonly IntPtr _hwnd;
    private readonly string _defaultIconPath;
    private IntPtr _defaultIconHandle = IntPtr.Zero;
    private IntPtr _currentDynamicIcon = IntPtr.Zero;
    private string _lastRenderedTime = string.Empty;
    private bool _isShowingDynamicIcon = false;
    private readonly object _syncLock = new();

    public DynamicTaskbarIconService(IntPtr hwnd, string defaultIconPath)
    {
        _hwnd = hwnd;
        _defaultIconPath = defaultIconPath;

        if (File.Exists(_defaultIconPath))
        {
            try
            {
                using var ico = new Icon(_defaultIconPath);
                _defaultIconHandle = ico.Handle;
            }
            catch
            {
                _defaultIconHandle = IntPtr.Zero;
            }
        }
    }

    public IntPtr UpdateTimerIcon(string timeText)
    {
        if (_hwnd == IntPtr.Zero || string.IsNullOrWhiteSpace(timeText))
            return IntPtr.Zero;

        lock (_syncLock)
        {
            if (_isShowingDynamicIcon && _lastRenderedTime == timeText && _currentDynamicIcon != IntPtr.Zero)
            {
                return _currentDynamicIcon;
            }

            IntPtr newHIcon = IntPtr.Zero;
            try
            {
                newHIcon = CreateTimerPillIcon(timeText);
                if (newHIcon != IntPtr.Zero)
                {
                    SendMessage(_hwnd, WM_SETICON, (IntPtr)ICON_BIG, newHIcon);
                    SendMessage(_hwnd, WM_SETICON, (IntPtr)ICON_SMALL, newHIcon);
                    SetClassLong(_hwnd, GCLP_HICON, newHIcon);
                    SetClassLong(_hwnd, GCLP_HICONSM, newHIcon);

                    if (_currentDynamicIcon != IntPtr.Zero && _currentDynamicIcon != _defaultIconHandle)
                    {
                        DestroyIcon(_currentDynamicIcon);
                    }

                    _currentDynamicIcon = newHIcon;
                    _lastRenderedTime = timeText;
                    _isShowingDynamicIcon = true;
                }
            }
            catch
            {
                // Graceful fallback
            }

            return newHIcon;
        }
    }

    public void RestoreDefaultIcon()
    {
        if (_hwnd == IntPtr.Zero) return;

        lock (_syncLock)
        {
            if (!_isShowingDynamicIcon) return;

            try
            {
                if (_defaultIconHandle != IntPtr.Zero)
                {
                    SendMessage(_hwnd, WM_SETICON, (IntPtr)ICON_BIG, _defaultIconHandle);
                    SendMessage(_hwnd, WM_SETICON, (IntPtr)ICON_SMALL, _defaultIconHandle);
                    SetClassLong(_hwnd, GCLP_HICON, _defaultIconHandle);
                    SetClassLong(_hwnd, GCLP_HICONSM, _defaultIconHandle);
                }
                else if (File.Exists(_defaultIconPath))
                {
                    using var ico = new Icon(_defaultIconPath);
                    var h = ico.Handle;
                    SendMessage(_hwnd, WM_SETICON, (IntPtr)ICON_BIG, h);
                    SendMessage(_hwnd, WM_SETICON, (IntPtr)ICON_SMALL, h);
                    SetClassLong(_hwnd, GCLP_HICON, h);
                    SetClassLong(_hwnd, GCLP_HICONSM, h);
                }

                if (_currentDynamicIcon != IntPtr.Zero && _currentDynamicIcon != _defaultIconHandle)
                {
                    DestroyIcon(_currentDynamicIcon);
                    _currentDynamicIcon = IntPtr.Zero;
                }

                _lastRenderedTime = string.Empty;
                _isShowingDynamicIcon = false;
            }
            catch
            {
                // Graceful fallback
            }
        }
    }

    private static IntPtr CreateTimerPillIcon(string timeText)
    {
        const int size = 64;
        using var bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.Clear(Color.Transparent);

            // Rounded rectangle pill matching user specification:
            // High contrast, rounded pill pillW = 60, pillH = 36, centered at (2, 14)
            const float pillX = 2f;
            const float pillY = 14f;
            const float pillW = 60f;
            const float pillH = 36f;
            const float radius = 9f;
            const float diam = radius * 2f;

            using var path = new GraphicsPath();
            path.AddArc(pillX, pillY, diam, diam, 180, 90);
            path.AddArc(pillX + pillW - diam, pillY, diam, diam, 270, 90);
            path.AddArc(pillX + pillW - diam, pillY + pillH - diam, diam, diam, 0, 90);
            path.AddArc(pillX, pillY + pillH - diam, diam, diam, 90, 90);
            path.CloseFigure();

            // 1. Fill with crisp white
            using var fillBrush = new SolidBrush(Color.FromArgb(255, 255, 255, 255));
            g.FillPath(fillBrush, path);

            // 2. Crisp dark border
            using var pen = new Pen(Color.FromArgb(255, 30, 41, 59), 2.2f);
            g.DrawPath(pen, path);

            // 3. Crisp bold timer numerals (adaptive size for 5+ characters like 60:00, 90:00, 120:00)
            float fontSize = timeText.Length > 5 ? 11.5f : 13.5f;
            using var font = new Font("Segoe UI", fontSize, FontStyle.Bold);
            using var textBrush = new SolidBrush(Color.FromArgb(255, 15, 23, 42));
            using var sf = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };

            var textRect = new RectangleF(pillX, pillY + 1.5f, pillW, pillH - 1f);
            g.DrawString(timeText, font, textBrush, textRect, sf);
        }

        return bmp.GetHicon();
    }

    public void Dispose()
    {
        lock (_syncLock)
        {
            if (_currentDynamicIcon != IntPtr.Zero && _currentDynamicIcon != _defaultIconHandle)
            {
                DestroyIcon(_currentDynamicIcon);
                _currentDynamicIcon = IntPtr.Zero;
            }
        }
    }
}

