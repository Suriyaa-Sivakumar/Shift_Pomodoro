using System;
using System.IO;
using System.Runtime.InteropServices;
using Windows.Graphics;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using FlowWindows.Core.Common;
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

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hWnd);

    private readonly ITimerEngine _timerEngine;
    private readonly IRepository _repository;
    private readonly AppSettings _settings;
    private readonly Action _openMainWindowAction;
    private readonly OverlappedPresenter _presenter;
    private readonly IntPtr _hwnd;
    private readonly AppWindow _appWindow;
    public bool IsWindowVisible { get; private set; } = false;

    public void SetAlwaysOnTop(bool alwaysOnTop)
    {
        if (_presenter != null)
        {
            _presenter.IsAlwaysOnTop = alwaysOnTop;
        }
    }

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
        _appWindow.Title = "Flow Mini";

        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        if (File.Exists(iconPath))
        {
            _appWindow.SetIcon(iconPath);
        }

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
        // Frameless floating widget card
        _presenter.SetBorderAndTitleBar(hasBorder: false, hasTitleBar: false);
        _appWindow.SetPresenter(_presenter);

        // DPI-scaled floating card size (240x185 DIPs)
        uint dpi = GetDpiForWindow(_hwnd);
        float scale = dpi > 0 ? (float)dpi / 96.0f : 1.0f;
        int targetWidth = (int)Math.Round(240 * scale);
        int targetHeight = (int)Math.Round(185 * scale);
        _appWindow.Resize(new SizeInt32(targetWidth, targetHeight));

        // Restore position if previously saved
        if (settings.MiniTimerX >= 0 && settings.MiniTimerY >= 0)
        {
            _appWindow.Move(new PointInt32((int)settings.MiniTimerX, (int)settings.MiniTimerY));
        }

        _appWindow.Changed += OnAppWindowChanged;

        _timerEngine.Tick += OnTimerTick;
        _timerEngine.StateChanged += OnTimerStateChanged;
        _timerEngine.SessionTypeChanged += (s, t) => DispatcherQueue.TryEnqueue(UpdateUiFromEngine);
        _timerEngine.CycleChanged += (s, c) => DispatcherQueue.TryEnqueue(UpdateUiFromEngine);

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
            MiniTimeText.Text = TimeFormatHelper.FormatTimer(e.RemainingTime);
            if (e.State == SessionState.Running)
            {
                MiniPlayPauseIcon.Glyph = "\uE769";
                MiniPlayPauseIcon.Margin = new Thickness(0);
            }
            else
            {
                MiniPlayPauseIcon.Glyph = "\uE768";
                MiniPlayPauseIcon.Margin = new Thickness(1, 0, 0, 0);
            }
            RenderCycleDots(e.CurrentCycleSession, e.SessionsBeforeLongBreak);
        });
    }

    private void OnTimerStateChanged(object? sender, SessionState state)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (state == SessionState.Running)
            {
                MiniPlayPauseIcon.Glyph = "\uE769";
                MiniPlayPauseIcon.Margin = new Thickness(0);
            }
            else
            {
                MiniPlayPauseIcon.Glyph = "\uE768";
                MiniPlayPauseIcon.Margin = new Thickness(1, 0, 0, 0);
            }
            RenderCycleDots(_timerEngine.CurrentCycleSession, _timerEngine.SessionsBeforeLongBreak);
        });
    }

    private void UpdateUiFromEngine()
    {
        MiniTimeText.Text = TimeFormatHelper.FormatTimer(_timerEngine.RemainingTime);
        if (_timerEngine.State == SessionState.Running)
        {
            MiniPlayPauseIcon.Glyph = "\uE769";
            MiniPlayPauseIcon.Margin = new Thickness(0);
        }
        else
        {
            MiniPlayPauseIcon.Glyph = "\uE768";
            MiniPlayPauseIcon.Margin = new Thickness(1, 0, 0, 0);
        }
        RenderCycleDots(_timerEngine.CurrentCycleSession, _timerEngine.SessionsBeforeLongBreak);
    }

    private void RenderCycleDots(int currentSession, int totalSessions)
    {
        MiniCycleDotsPanel.Children.Clear();
        if (totalSessions <= 0) totalSessions = 4;
        if (currentSession <= 0) currentSession = 1;

        var activeBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 21, 128, 61)); // #15803D (vibrant emerald green)
        var inactiveBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 209, 235, 225)); // #D1EBE1 (soft mint)
        var completedBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 134, 200, 168)); // #86C8A8

        for (int i = 1; i <= totalSessions; i++)
        {
            if (i == currentSession)
            {
                // Active session: elongated rounded capsule
                var pill = new Border
                {
                    Width = 16,
                    Height = 5,
                    CornerRadius = new CornerRadius(2.5),
                    Background = activeBrush,
                    VerticalAlignment = VerticalAlignment.Center
                };
                MiniCycleDotsPanel.Children.Add(pill);
            }
            else if (i < currentSession)
            {
                // Completed session in cycle
                var dot = new Border
                {
                    Width = 5,
                    Height = 5,
                    CornerRadius = new CornerRadius(2.5),
                    Background = completedBrush,
                    VerticalAlignment = VerticalAlignment.Center
                };
                MiniCycleDotsPanel.Children.Add(dot);
            }
            else
            {
                // Upcoming session in cycle
                var dot = new Border
                {
                    Width = 5,
                    Height = 5,
                    CornerRadius = new CornerRadius(2.5),
                    Background = inactiveBrush,
                    VerticalAlignment = VerticalAlignment.Center
                };
                MiniCycleDotsPanel.Children.Add(dot);
            }
        }
    }

    private void OnRootPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        // Allow user to drag the frameless floating mini timer anywhere on screen
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

    private void OnResetClicked(object sender, RoutedEventArgs e)
    {
        _timerEngine.Reset();
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
