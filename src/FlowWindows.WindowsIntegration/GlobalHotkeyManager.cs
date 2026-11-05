using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FlowWindows.WindowsIntegration;

public class GlobalHotkeyManager : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_NOREPEAT = 0x4000;

    private const int HOTKEY_START_PAUSE = 1001;
    private const int HOTKEY_SKIP = 1002;
    private const int HOTKEY_RESET = 1003;
    private const int HOTKEY_TOGGLE_MINI = 1004;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private class HotkeyWindow : NativeWindow, IDisposable
    {
        private readonly GlobalHotkeyManager _parent;

        public HotkeyWindow(GlobalHotkeyManager parent)
        {
            _parent = parent;
            CreateHandle(new CreateParams());
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY)
            {
                int id = m.WParam.ToInt32();
                _parent.HandleHotkey(id);
            }
            base.WndProc(ref m);
        }

        public void Dispose()
        {
            DestroyHandle();
        }
    }

    private HotkeyWindow? _window;
    private bool _isRegistered = false;

    public event Action? StartPauseTriggered;
    public event Action? SkipTriggered;
    public event Action? ResetTriggered;
    public event Action? ToggleMiniTimerTriggered;

    public void Register()
    {
        if (_isRegistered) return;

        try
        {
            _window = new HotkeyWindow(this);

            // Ctrl + Alt + Space => Start/Pause
            RegisterHotKey(_window.Handle, HOTKEY_START_PAUSE, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, (uint)Keys.Space);

            // Ctrl + Alt + S => Skip
            RegisterHotKey(_window.Handle, HOTKEY_SKIP, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, (uint)Keys.S);

            // Ctrl + Alt + R => Reset
            RegisterHotKey(_window.Handle, HOTKEY_RESET, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, (uint)Keys.R);

            // Ctrl + Alt + M => Toggle Mini Timer
            RegisterHotKey(_window.Handle, HOTKEY_TOGGLE_MINI, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, (uint)Keys.M);

            _isRegistered = true;
        }
        catch
        {
            // Ignore if hotkeys conflict with another app
        }
    }

    public void Unregister()
    {
        if (!_isRegistered || _window == null) return;

        try
        {
            UnregisterHotKey(_window.Handle, HOTKEY_START_PAUSE);
            UnregisterHotKey(_window.Handle, HOTKEY_SKIP);
            UnregisterHotKey(_window.Handle, HOTKEY_RESET);
            UnregisterHotKey(_window.Handle, HOTKEY_TOGGLE_MINI);
        }
        catch { }

        _isRegistered = false;
    }

    private void HandleHotkey(int id)
    {
        switch (id)
        {
            case HOTKEY_START_PAUSE:
                StartPauseTriggered?.Invoke();
                break;
            case HOTKEY_SKIP:
                SkipTriggered?.Invoke();
                break;
            case HOTKEY_RESET:
                ResetTriggered?.Invoke();
                break;
            case HOTKEY_TOGGLE_MINI:
                ToggleMiniTimerTriggered?.Invoke();
                break;
        }
    }

    public void Dispose()
    {
        Unregister();
        _window?.Dispose();
        _window = null;
    }
}

