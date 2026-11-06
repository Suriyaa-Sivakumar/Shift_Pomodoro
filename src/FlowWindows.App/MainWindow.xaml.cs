using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Graphics;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using FlowWindows.Analytics;
using FlowWindows.Core.Common;
using FlowWindows.Core.Enums;
using FlowWindows.Core.Interfaces;
using FlowWindows.Core.Models;
using FlowWindows.Data;
using FlowWindows.StudyPlanner;
using FlowWindows.TaskManagement;
using FlowWindows.Timer;
using FlowWindows.WindowsIntegration;

namespace FlowWindows_App;

public sealed partial class MainWindow : Window
{
    private const int SW_HIDE = 0;
    private const int SW_SHOW = 5;

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern void SwitchToThisWindow(IntPtr hWnd, bool fUnknown);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hWnd);

    private readonly IntPtr _hwnd;
    private readonly AppWindow _appWindow;

    private FlowSqliteRepository _repository = null!;
    private TimerEngine _timerEngine = null!;
    private AnalyticsService _analyticsService = null!;
    private StudyPlannerService _studyPlannerService = null!;
    private TaskService _taskService = null!;
    private GoalsService _goalsService = null!;
    private AudioNotificationService _audioService = null!;
    private ReportService _reportService = null!;
    private SystemTrayManager _trayManager = null!;
    private GlobalHotkeyManager _hotkeyManager = null!;
    private MiniTimerWindow? _miniTimerWindow;
    private DynamicTaskbarIconService? _taskbarIconService;
    private AppSettings _settings = new();

    public MainWindow()
    {
        Log("MainWindow constructor: starting...");
        try
        {
            InitializeComponent();
            Log("MainWindow constructor: InitializeComponent succeeded.");

            this.Title = "Shift - Pomodoro";

            _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            Log($"MainWindow constructor: _hwnd = {_hwnd}");

            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(_hwnd);
            _appWindow = AppWindow.GetFromWindowId(windowId);
            _appWindow.Title = "Shift - Pomodoro";
            Log("MainWindow constructor: _appWindow acquired.");

            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
            if (File.Exists(iconPath))
            {
                _appWindow.SetIcon(iconPath);
                Log("MainWindow constructor: _appWindow icon set.");
            }
            _taskbarIconService = new DynamicTaskbarIconService(_hwnd, iconPath);

            // Window size and position (DPI-scaled Flow-style compact window: 520x440 DIPs)
            uint dpi = GetDpiForWindow(_hwnd);
            float scale = dpi > 0 ? (float)dpi / 96.0f : 1.0f;
            _appWindow.Resize(new SizeInt32((int)Math.Round(520 * scale), (int)Math.Round(440 * scale)));
            Log("MainWindow constructor: _appWindow resized with DPI scaling.");

            try
            {
                CenterWindow();
                Log("MainWindow constructor: CenterWindow succeeded.");
            }
            catch (Exception ex)
            {
                Log($"MainWindow constructor: CenterWindow error: {ex.Message}");
            }

            _appWindow.Closing += OnAppWindowClosing;
            Log("MainWindow constructor: OnAppWindowClosing attached.");

            _ = InitializeApplicationAsync();
            Log("MainWindow constructor: InitializeApplicationAsync dispatched.");
        }
        catch (Exception ex)
        {
            Log($"FATAL in MainWindow constructor: {ex}");
            throw;
        }
    }

    private void CenterWindow()
    {
        var displayArea = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Primary);
        if (displayArea != null)
        {
            var centeredPosition = new PointInt32(
                displayArea.WorkArea.X + (displayArea.WorkArea.Width - _appWindow.Size.Width) / 2,
                displayArea.WorkArea.Y + (displayArea.WorkArea.Height - _appWindow.Size.Height) / 2
            );
            _appWindow.Move(centeredPosition);
        }
    }

    private static void Log(string message)
    {
        try
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var logDir = Path.Combine(appData, "FlowWindows", "Logs");
            Directory.CreateDirectory(logDir);
            File.AppendAllText(Path.Combine(logDir, "app.log"), $"[{DateTime.UtcNow:O}] {message}{Environment.NewLine}");
        }
        catch { }
    }

    private async Task InitializeApplicationAsync()
    {
        try
        {
            Log("Starting application initialization...");
            // 1. Data & Repository
            _repository = new FlowSqliteRepository();
            await _repository.InitializeAsync();
            _settings = await _repository.GetSettingsAsync();
            Log("Database initialized successfully.");


            // Check for crash recovery from previous unexpected shutdown
            try
            {
                var activeState = await _repository.GetActiveSessionStateAsync();
                if (activeState != null)
                {
                    Log($"Crash Recovery: Found unclosed session from {activeState.StartTime:O}");
                    double elapsed = activeState.AccumulatedElapsedSeconds;
                    if (activeState.State == SessionState.Running)
                    {
                        var segment = activeState.LastHeartbeat - activeState.SegmentStartTime;
                        if (segment.TotalSeconds > 0) elapsed += segment.TotalSeconds;
                    }
                    elapsed = Math.Min(elapsed, activeState.TotalDurationSeconds);

                    if (elapsed >= 10 && activeState.SessionType == SessionType.Focus)
                    {
                        var rec = new SessionRecord
                        {
                            SessionType = activeState.SessionType,
                            StartTime = activeState.StartTime,
                            EndTime = activeState.LastHeartbeat,
                            PlannedDurationSeconds = (int)activeState.TotalDurationSeconds,
                            ActualDurationSeconds = (int)Math.Round(elapsed),
                            Completed = elapsed >= activeState.TotalDurationSeconds,
                            Interrupted = elapsed < activeState.TotalDurationSeconds,
                            TagId = activeState.TagId,
                            TagName = activeState.TagName,
                            TagColorHex = activeState.TagColorHex,
                            TaskId = activeState.TaskId,
                            TaskTitle = activeState.TaskTitle,
                            SubjectId = activeState.SubjectId,
                            SubjectName = activeState.SubjectName,
                            TopicId = activeState.TopicId,
                            Notes = "Recovered after unexpected application termination"
                        };
                        await _repository.AddSessionAsync(rec);
                        Log($"Crash Recovery: Saved session ({rec.ActualDurationSeconds}s).");
                    }
                    await _repository.ClearActiveSessionStateAsync();
                }
            }
            catch (Exception ex)
            {
                Log($"Crash recovery check error: {ex.Message}");
            }

            // Power mode and clock monitoring for Windows sleep/wake recovery
            try
            {
                Microsoft.Win32.SystemEvents.PowerModeChanged += OnPowerModeChanged;
                Microsoft.Win32.SystemEvents.TimeChanged += OnSystemTimeChanged;
            }
            catch { }

            // 3. Domain Services
            _analyticsService = new AnalyticsService(_repository);
            _goalsService = new GoalsService(_repository, _analyticsService);
            _studyPlannerService = new StudyPlannerService(_repository);
            _taskService = new TaskService(_repository);
            _audioService = new AudioNotificationService();
            _reportService = new ReportService(_repository, _analyticsService);

            // 4. Authoritative Timer Engine
            _timerEngine = new TimerEngine(_repository);
            _timerEngine.SessionsBeforeLongBreak = _settings.SessionsBeforeLongBreak;
            _timerEngine.CommitmentMode = _settings.CommitmentMode;
            _timerEngine.SetCustomDuration(SessionType.Focus, TimeSpan.FromMinutes(_settings.FocusMinutes));
            _timerEngine.SetCustomDuration(SessionType.ShortBreak, TimeSpan.FromMinutes(_settings.ShortBreakMinutes));
            _timerEngine.SetCustomDuration(SessionType.LongBreak, TimeSpan.FromMinutes(_settings.LongBreakMinutes));

            // Wire Timer Engine Events
            _timerEngine.Tick += OnTimerTick;
            _timerEngine.StateChanged += OnTimerStateChanged;
            _timerEngine.SessionCompleted += OnSessionCompleted;

            // 5. Initialize UI components on the UI Thread
            DispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    Log("Setting up UI components on DispatcherQueue...");

                    // System Tray
                    try
                    {
                        _trayManager = new SystemTrayManager();
                        _trayManager.OpenMainWindowRequested += ShowAndActivateMainWindow;
                        _trayManager.ToggleMiniTimerRequested += ToggleMiniTimer;
                        _trayManager.StartPauseRequested += () =>
                        {
                            if (_timerEngine.State == SessionState.Running) _timerEngine.Pause();
                            else _timerEngine.Start();
                        };
                        _trayManager.SkipRequested += () => _timerEngine.Skip();
                        _trayManager.ExitRequested += ExitApplication;
                        Log("TrayManager initialized.");
                    }
                    catch (Exception ex)
                    {
                        Log($"TrayManager init error: {ex.Message}");
                    }

                    // Hotkeys
                    try
                    {
                        _hotkeyManager = new GlobalHotkeyManager();
                        if (_settings.GlobalHotkeysEnabled)
                        {
                            _hotkeyManager.StartPauseTriggered += () =>
                            {
                                if (_timerEngine.State == SessionState.Running) _timerEngine.Pause();
                                else _timerEngine.Start();
                            };
                            _hotkeyManager.SkipTriggered += () => _timerEngine.Skip();
                            _hotkeyManager.ResetTriggered += () => _timerEngine.Reset();
                            _hotkeyManager.ToggleMiniTimerTriggered += ToggleMiniTimer;
                            _hotkeyManager.Register();
                        }
                        Log("HotkeyManager initialized.");
                    }
                    catch (Exception ex)
                    {
                        Log($"HotkeyManager init error: {ex.Message}");
                    }

                    // Mini Timer Window
                    try
                    {
                        _miniTimerWindow = new MiniTimerWindow(_timerEngine, _repository, _settings, ShowAndActivateMainWindow);
                        Log("MiniTimerWindow initialized.");
                    }
                    catch (Exception ex)
                    {
                        Log($"MiniTimerWindow init error: {ex.Message}");
                    }

                    // Main Page
                    var mainPage = new MainPage();
                    mainPage.InitializeServices(
                        _timerEngine,
                        _repository,
                        _analyticsService,
                        _studyPlannerService,
                        _taskService,
                        _goalsService,
                        _audioService,
                        _reportService,
                        ToggleMiniTimer
                    );
                    RootFrame.Content = mainPage;
                    Log("MainPage navigated and content set successfully.");
                }
                catch (Exception ex)
                {
                    Log($"UI Dispatcher error: {ex}");
                }
            });
        }
        catch (Exception ex)
        {
            Log($"FATAL INITIALIZATION ERROR: {ex}");
        }
    }

    private void OnTimerTick(object? sender, TimerTickEventArgs e)
    {
        // Update Taskbar Progress
        TaskbarManager.SetProgress(_hwnd, e.ProgressFraction, e.SessionType, e.State);

        // Update Tray Icon Status
        _trayManager.UpdateStatus(e.State, e.SessionType, e.RemainingTime);

        // Update Dynamic Taskbar Icon
        var timeText = TimeFormatHelper.FormatTimer(e.RemainingTime);
        if (_settings.ShowTimerOnTaskbar && (e.State == SessionState.Running || e.State == SessionState.Paused))
        {
            var hIcon = _taskbarIconService?.UpdateTimerIcon(timeText) ?? IntPtr.Zero;
            if (hIcon != IntPtr.Zero)
            {
                try
                {
                    var iconId = Microsoft.UI.Win32Interop.GetIconIdFromIcon(hIcon);
                    _appWindow.SetIcon(iconId);
                }
                catch { }
            }
            _appWindow.Title = $"{timeText} • Shift - Pomodoro";
        }
        else
        {
            _taskbarIconService?.RestoreDefaultIcon();
            _appWindow.Title = "Shift - Pomodoro";
        }
    }

    private void OnTimerStateChanged(object? sender, SessionState state)
    {
        if (state == SessionState.Running && _timerEngine.CurrentSessionType == SessionType.Focus)
        {
            if (_settings.AutoOpenMiniTimerOnFocus && _miniTimerWindow != null)
            {
                DispatcherQueue.TryEnqueue(() => _miniTimerWindow.ShowMiniTimer());
            }
        }
        else
        {
            if (state == SessionState.Idle || state == SessionState.Completed)
            {
                TaskbarManager.ClearProgress(_hwnd);
                _taskbarIconService?.RestoreDefaultIcon();
                _appWindow.Title = "Shift - Pomodoro";
                var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
                if (File.Exists(iconPath))
                {
                    try { _appWindow.SetIcon(iconPath); } catch { }
                }
            }
        }
    }

    private void OnSessionCompleted(object? sender, SessionCompletedEventArgs e)
    {
        var session = e.Session;

        // 1. Play Sound
        if (_settings.PlaySoundOnComplete)
        {
            if (session.SessionType == SessionType.Focus)
            {
                _audioService.PlaySessionCompleteSound(_settings.FocusCompleteSound, _settings.SoundVolumePercent);
            }
            else
            {
                _audioService.PlayBreakCompleteSound(_settings.BreakCompleteSound, _settings.SoundVolumePercent);
            }
        }

        // 2. Windows Notification
        if (_settings.ShowWindowsNotifications)
        {
            var title = session.SessionType == SessionType.Focus ? "Focus Session Complete!" : "Break Finished!";
            var msg = session.SessionType == SessionType.Focus
                ? "Excellent work! Take a deep breath and start your break."
                : "Break is over. Ready to dive back into your flow?";
            _trayManager.ShowNotification(title, msg);
        }

        // 3. Update Goals and Achievements
        Task.Run(async () =>
        {
            try
            {
                await _goalsService.CheckAndProgressGoalsAsync(session);
                await _goalsService.CheckAchievementsAsync();

                if (!string.IsNullOrEmpty(session.SubjectId))
                {
                    await _studyPlannerService.RecordStudyTimeAsync(session.SubjectId, session.TopicId, session.ActualDurationSeconds);
                }

                if (!string.IsNullOrEmpty(session.TaskId))
                {
                    var mins = (int)Math.Round(session.ActualDurationSeconds / 60.0);
                    await _taskService.RecordTimeOnTaskAsync(session.TaskId, mins);
                }
            }
            catch { }
        });
    }

    public void ShowAndActivateMainWindow()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            const int SW_RESTORE = 9;
            ShowWindow(_hwnd, SW_RESTORE);
            _appWindow.Show();
            Activate();
            BringWindowToTop(_hwnd);
            SetForegroundWindow(_hwnd);
            SwitchToThisWindow(_hwnd, true);
        });
    }

    public void ToggleMiniTimer()
    {
        try
        {
            if (_miniTimerWindow == null)
            {
                _miniTimerWindow = new MiniTimerWindow(_timerEngine, _repository, _settings, ShowAndActivateMainWindow);
            }
            _miniTimerWindow.ToggleVisibility();
        }
        catch (Exception ex)
        {
            Log($"ToggleMiniTimer error: {ex.Message}");
            _miniTimerWindow = new MiniTimerWindow(_timerEngine, _repository, _settings, ShowAndActivateMainWindow);
            _miniTimerWindow.ShowMiniTimer();
        }
    }

    private void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        Log($"OnAppWindowClosing triggered. MinimizeToTrayOnClose={_settings.MinimizeToTrayOnClose}");
        if (_settings.MinimizeToTrayOnClose)
        {
            args.Cancel = true;
            _appWindow.Hide();
            ShowWindow(_hwnd, SW_HIDE);
            _trayManager.ShowNotification("Shift - Pomodoro", "Running in the system tray. Click the tray icon to restore.");
        }
        else
        {
            ExitApplication();
        }
    }

    private void OnPowerModeChanged(object sender, Microsoft.Win32.PowerModeChangedEventArgs e)
    {
        Log($"Windows PowerModeChanged: {e.Mode}");
        if (e.Mode == Microsoft.Win32.PowerModes.Resume)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                _timerEngine?.SyncExternalTick();
            });
        }
    }

    private void OnSystemTimeChanged(object? sender, EventArgs e)
    {
        Log("Windows SystemTimeChanged detected.");
        DispatcherQueue.TryEnqueue(() =>
        {
            _timerEngine?.SyncExternalTick();
        });
    }

    private void ExitApplication()
    {
        Log($"ExitApplication invoked! Stack: {Environment.StackTrace}");
        try
        {
            Microsoft.Win32.SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            Microsoft.Win32.SystemEvents.TimeChanged -= OnSystemTimeChanged;
        }
        catch { }

        _hotkeyManager?.Dispose();
        _trayManager?.Dispose();
        _taskbarIconService?.RestoreDefaultIcon();
        _taskbarIconService?.Dispose();
        _miniTimerWindow?.Close();
        _timerEngine?.Dispose();

        Application.Current.Exit();
    }
}
