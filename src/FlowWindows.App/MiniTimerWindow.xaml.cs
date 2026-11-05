using System;
using System.Runtime.InteropServices;
using Windows.Graphics;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using FlowWindows.Core.Enums;
using FlowWindows.Core.Interfaces;
using FlowWindows.Core.Models;

namespace FlowWindows_App;

public sealed partial class MiniTimerWindow : Window
{
    private const int WM_NCLBUTTONDOWN = 0x00A1;
    private const int HTCAPTION = 0x2;
    private const int SW_HIDE = 0;
    private const int SW_SHOW = 5;
    private const int SW_RESTORE = 9;

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private readonly ITimerEngine _timerEngine;
    private readonly IRepository _repository;
    private readonly AppSettings _settings;
    private readonly Action _openMainWindowAction;
    private readonly OverlappedPresenter _presenter;
    private readonly IntPtr _hwnd;
    private readonly AppWindow _appWindow;
    public bool IsWindowVisible { get; private set; } = false;

    public MiniTimerWindow(ITimerEngine timerEngine, IRepository repository, AppSettings settings, Action openMainWindowAction)
    {
        InitializeComponent();

        _timerEngine = timerEngine;
        _repository = repository;
        _settings = settings;
        _openMainWindowAction = openMainWindowAction;

        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(_hwnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);

        _appWindow.Closing += (sender, args) =>
        {
            args.Cancel = true;
            HideWindow();
        };

        _presenter = OverlappedPresenter.Create();
        _presenter.IsAlwaysOnTop = settings.MiniTimerAlwaysOnTop;
        _presenter.IsResizable = false;
        _presenter.IsMaximizable = false;
        _presenter.IsMinimizable = false;
        // Flow signature: completely frameless floating widget without native OS titlebar!
        _presenter.SetBorderAndTitleBar(hasBorder: false, hasTitleBar: false);
        _appWindow.SetPresenter(_presenter);

        _appWindow.Resize(new SizeInt32(290, 56));

        // Restore position if previously saved
        if (settings.MiniTimerX >= 0 && settings.MiniTimerY >= 0)
        {
            _appWindow.Move(new PointInt32((int)settings.MiniTimerX, (int)settings.MiniTimerY));
        }

        _appWindow.Changed += OnAppWindowChanged;

        _timerEngine.Tick += OnTimerTick;
        _timerEngine.StateChanged += OnTimerStateChanged;

        UpdateUiFromEngine();
    }

    private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (args.DidPositionChange && IsWindowVisible)
        {
            _settings.MiniTimerX = _appWindow.Position.X;
            _settings.MiniTimerY = _appWindow.Position.Y;
            _ = _repository.SaveSettingsAsync(_settings);
        }
    }

    private void OnTimerTick(object? sender, TimerTickEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            MiniTimeText.Text = $"{e.RemainingTime:mm\\:ss}";
            MiniPlayPauseIcon.Glyph = e.State == SessionState.Running ? "\uE769" : "\uE768";
            UpdateLabel(e.SessionType);
            RenderCycleDots(e.CurrentCycleSession, e.SessionsBeforeLongBreak);
        });
    }

    private void OnTimerStateChanged(object? sender, SessionState state)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            MiniPlayPauseIcon.Glyph = state == SessionState.Running ? "\uE769" : "\uE768";
            UpdateLabel(_timerEngine.CurrentSessionType);
            RenderCycleDots(_timerEngine.CurrentCycleSession, _timerEngine.SessionsBeforeLongBreak);
        });
    }

    private void UpdateUiFromEngine()
    {
        MiniTimeText.Text = $"{_timerEngine.RemainingTime:mm\\:ss}";
        MiniPlayPauseIcon.Glyph = _timerEngine.State == SessionState.Running ? "\uE769" : "\uE768";
        UpdateLabel(_timerEngine.CurrentSessionType);
        RenderCycleDots(_timerEngine.CurrentCycleSession, _timerEngine.SessionsBeforeLongBreak);
    }

    private void UpdateLabel(SessionType sessionType)
    {
        MiniLabelText.Text = sessionType switch
        {
            SessionType.Focus => "Shift",
            SessionType.ShortBreak => "Break",
            SessionType.LongBreak => "Long Break",
            _ => "Shift"
        };
    }

    private void RenderCycleDots(int currentSession, int totalSessions)
    {
        MiniCycleDotsPanel.Children.Clear();
        if (totalSessions <= 0) totalSessions = 4;
        if (currentSession <= 0) currentSession = 1;

        var greenBrush = ThemeHelper.GetBrush("ShiftFlowGreenBrush", "#2DB88F");
        var primaryTextBrush = ThemeHelper.GetBrush("ShiftTextPrimaryBrush", "#F5F5F7");
        var mutedBrush = ThemeHelper.GetBrush("ShiftTrackBrush", "#2A2D32");

        for (int i = 1; i <= totalSessions; i++)
        {
            var dot = new Border
            {
                Width = 5,
                Height = 5,
                CornerRadius = new CornerRadius(2.5),
                VerticalAlignment = VerticalAlignment.Center
            };

            if (i == currentSession)
            {
                dot.Background = greenBrush;
            }
            else if (i < currentSession)
            {
                dot.Background = primaryTextBrush;
                dot.Opacity = 0.7;
            }
            else
            {
                dot.Background = mutedBrush;
            }

            MiniCycleDotsPanel.Children.Add(dot);
        }
    }

    private void OnRootPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        // Allow user to drag the frameless floating mini timer
        if (e.GetCurrentPoint(RootCard).Properties.IsLeftButtonPressed)
        {
            ReleaseCapture();
            SendMessage(_hwnd, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
        }
    }

    private void OnPlayPauseClicked(object sender, RoutedEventArgs e)
    {
        if (_timerEngine.State == SessionState.Running)
        {
            _timerEngine.Pause();
        }
        else
        {
            _timerEngine.Start();
        }
    }

    private void OnSkipClicked(object sender, RoutedEventArgs e)
    {
        _timerEngine.Skip();
    }

    private void OnOpenMainAppClicked(object sender, RoutedEventArgs e)
    {
        _openMainWindowAction?.Invoke();
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e)
    {
        HideWindow();
    }

    public void ToggleVisibility()
    {
        if (IsWindowVisible)
        {
            HideWindow();
        }
        else
        {
            ShowMiniTimer();
        }
    }

    public void ShowMiniTimer()
    {
        try
        {
            ShowWindow(_hwnd, SW_SHOW);
            ShowWindow(_hwnd, SW_RESTORE);
            _appWindow.Show();
            Activate();
            SetForegroundWindow(_hwnd);
            IsWindowVisible = true;
            UpdateUiFromEngine();
        }
        catch { }
    }

    public void HideWindow()
    {
        try
        {
            ShowWindow(_hwnd, SW_HIDE);
            IsWindowVisible = false;
        }
        catch { }
    }
}
