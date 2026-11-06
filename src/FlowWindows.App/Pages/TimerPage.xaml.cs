using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using FlowWindows.Core.Common;
using FlowWindows.Core.Enums;
using FlowWindows.Core.Interfaces;
using FlowWindows.Core.Models;

namespace FlowWindows_App.Pages;

public sealed partial class TimerPage : Page
{
    private ITimerEngine? _timerEngine;
    private IRepository? _repository;
    private Action? _navigateToStatistics;
    private Action? _navigateToTasks;
    private Action? _navigateToStudyPlanner;
    private Action? _navigateToInsights;
    private Action? _navigateToSettings;
    private Action? _toggleMiniTimer;

    private List<Tag> _tags = new();
    private List<TaskItem> _tasks = new();
    private AppSettings _settings = new();

    public TimerPage()
    {
        InitializeComponent();
    }

    public void Initialize(
        ITimerEngine timerEngine,
        IRepository repository,
        Action navigateToStatistics,
        Action navigateToTasks,
        Action navigateToStudyPlanner,
        Action navigateToInsights,
        Action navigateToSettings,
        Action toggleMiniTimer)
    {
        _timerEngine = timerEngine;
        _repository = repository;
        _navigateToStatistics = navigateToStatistics;
        _navigateToTasks = navigateToTasks;
        _navigateToStudyPlanner = navigateToStudyPlanner;
        _navigateToInsights = navigateToInsights;
        _navigateToSettings = navigateToSettings;
        _toggleMiniTimer = toggleMiniTimer;

        _timerEngine.Tick += OnTimerTick;
        _timerEngine.StateChanged += OnTimerStateChanged;

        _ = LoadMetadataAsync();
        UpdateUi();
    }

    private async Task LoadMetadataAsync()
    {
        if (_repository == null) return;

        try
        {
            _settings = await _repository.GetSettingsAsync();
            _tags = await _repository.GetTagsAsync();
            _tasks = (await _repository.GetTasksAsync()).Where(t => t.Status != ItemStatus.Completed).ToList();

            TagComboBox.ItemsSource = _tags;
            TaskComboBox.ItemsSource = _tasks;

            if (!string.IsNullOrEmpty(_timerEngine?.ActiveTagId))
            {
                TagComboBox.SelectedItem = _tags.FirstOrDefault(t => t.Id == _timerEngine.ActiveTagId);
            }

            if (!string.IsNullOrEmpty(_timerEngine?.ActiveTaskId))
            {
                TaskComboBox.SelectedItem = _tasks.FirstOrDefault(t => t.Id == _timerEngine.ActiveTaskId);
            }

            AutoStartBreaksItem.IsChecked = _settings.AutoStartBreaks;
            AutoStartFocusItem.IsChecked = _settings.AutoStartFocus;
        }
        catch { }
    }

    private void OnTimerTick(object? sender, TimerTickEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            TimeDisplay.Text = TimeFormatHelper.FormatTimer(e.RemainingTime);
            PlayPauseIcon.Glyph = e.State == SessionState.Running ? "\uE769" : "\uE768";
            UpdateSessionTitle();
            RenderCycleDots(e.CurrentCycleSession, e.SessionsBeforeLongBreak);
            UpdateCommitmentModeState();
        });
    }

    private void OnTimerStateChanged(object? sender, SessionState state)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            PlayPauseIcon.Glyph = state == SessionState.Running ? "\uE769" : "\uE768";
            UpdateCommitmentModeState();
            UpdateModeSwitcherState();
            UpdateSessionTitle();
        });
    }

    private void UpdateUi()
    {
        if (_timerEngine == null) return;

        TimeDisplay.Text = TimeFormatHelper.FormatTimer(_timerEngine.RemainingTime);
        PlayPauseIcon.Glyph = _timerEngine.State == SessionState.Running ? "\uE769" : "\uE768";

        UpdateSessionTitle();
        RenderCycleDots(_timerEngine.CurrentCycleSession, _timerEngine.SessionsBeforeLongBreak);
        UpdateCommitmentModeState();
        UpdateModeSwitcherState();
    }

    private void UpdateSessionTitle()
    {
        if (_timerEngine == null) return;

        if (!string.IsNullOrWhiteSpace(_timerEngine.ActiveTaskTitle))
        {
            SessionTitleText.Text = _timerEngine.ActiveTaskTitle;
        }
        else if (!string.IsNullOrWhiteSpace(_timerEngine.ActiveTagName))
        {
            SessionTitleText.Text = _timerEngine.ActiveTagName;
        }
        else
        {
            SessionTitleText.Text = _timerEngine.CurrentSessionType switch
            {
                SessionType.Focus => "Shift",
                SessionType.ShortBreak => "Short Break",
                SessionType.LongBreak => "Long Break",
                _ => "Shift"
            };
        }
    }

    private void RenderCycleDots(int currentSession, int totalSessions)
    {
        CycleDotsPanel.Children.Clear();
        if (totalSessions <= 0) totalSessions = 4;
        if (currentSession <= 0) currentSession = 1;

        var greenBrush = ThemeHelper.GetBrush("ShiftFlowGreenBrush", "#2DB88F");
        var primaryTextBrush = ThemeHelper.GetBrush("ShiftTextPrimaryBrush", "#F5F5F7");
        var mutedBrush = ThemeHelper.GetBrush("ShiftTrackBrush", "#2A2D32");

        for (int i = 1; i <= totalSessions; i++)
        {
            if (i == currentSession)
            {
                // Active elongated pill
                var pill = new Border
                {
                    Width = 22,
                    Height = 7,
                    CornerRadius = new CornerRadius(3.5),
                    Background = greenBrush,
                    VerticalAlignment = VerticalAlignment.Center
                };
                CycleDotsPanel.Children.Add(pill);
            }
            else if (i < currentSession)
            {
                // Completed dot
                var dot = new Border
                {
                    Width = 7,
                    Height = 7,
                    CornerRadius = new CornerRadius(3.5),
                    Background = primaryTextBrush,
                    Opacity = 0.8,
                    VerticalAlignment = VerticalAlignment.Center
                };
                CycleDotsPanel.Children.Add(dot);
            }
            else
            {
                // Upcoming dot
                var dot = new Border
                {
                    Width = 7,
                    Height = 7,
                    CornerRadius = new CornerRadius(3.5),
                    Background = mutedBrush,
                    VerticalAlignment = VerticalAlignment.Center
                };
                CycleDotsPanel.Children.Add(dot);
            }
        }
    }

    private void UpdateModeSwitcherState()
    {
        if (_timerEngine == null) return;

        var type = _timerEngine.CurrentSessionType;
        bool isRunning = _timerEngine.State == SessionState.Running;

        ApplyModeButtonStyle(ModeFocusBtn, type == SessionType.Focus);
        ApplyModeButtonStyle(ModeShortBreakBtn, type == SessionType.ShortBreak);
        ApplyModeButtonStyle(ModeLongBreakBtn, type == SessionType.LongBreak);

        bool lockModes = isRunning && _timerEngine.CommitmentMode >= CommitmentModeLevel.Strict;
        ModeFocusBtn.IsEnabled = !lockModes;
        ModeShortBreakBtn.IsEnabled = !lockModes;
        ModeLongBreakBtn.IsEnabled = !lockModes;
    }

    private void ApplyModeButtonStyle(Button btn, bool isActive)
    {
        try
        {
            if (isActive)
            {
                btn.Background = ThemeHelper.GetBrush("ShiftFlowGreenBrush", "#2DB88F");
                btn.Foreground = new SolidColorBrush(Colors.White);
                btn.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
            }
            else
            {
                btn.Background = new SolidColorBrush(Colors.Transparent);
                btn.Foreground = ThemeHelper.GetBrush("ShiftTextSecondaryBrush", "#8E8E93");
                btn.FontWeight = Microsoft.UI.Text.FontWeights.Normal;
            }
        }
        catch { }
    }

    private void OnModeFocusClicked(object sender, RoutedEventArgs e)
    {
        if (_timerEngine == null) return;
        if (_timerEngine.State == SessionState.Running && _timerEngine.CommitmentMode >= CommitmentModeLevel.Strict) return;
        _timerEngine.SwitchSessionType(SessionType.Focus);
        UpdateUi();
    }

    private void OnModeShortBreakClicked(object sender, RoutedEventArgs e)
    {
        if (_timerEngine == null) return;
        if (_timerEngine.State == SessionState.Running && _timerEngine.CommitmentMode >= CommitmentModeLevel.Strict) return;
        _timerEngine.SwitchSessionType(SessionType.ShortBreak);
        UpdateUi();
    }

    private void OnModeLongBreakClicked(object sender, RoutedEventArgs e)
    {
        if (_timerEngine == null) return;
        if (_timerEngine.State == SessionState.Running && _timerEngine.CommitmentMode >= CommitmentModeLevel.Strict) return;
        _timerEngine.SwitchSessionType(SessionType.LongBreak);
        UpdateUi();
    }

    private void UpdateCommitmentModeState()
    {
        if (_timerEngine == null) return;

        bool isRunningFocus = _timerEngine.State == SessionState.Running && _timerEngine.CurrentSessionType == SessionType.Focus;
        bool isStrict = _timerEngine.CommitmentMode >= CommitmentModeLevel.Strict;

        CommitmentBanner.Visibility = (isRunningFocus && isStrict) ? Visibility.Visible : Visibility.Collapsed;

        if (isRunningFocus && isStrict)
        {
            ResetButton.IsEnabled = false;
            SkipButton.IsEnabled = false;
        }
        else
        {
            ResetButton.IsEnabled = true;
            SkipButton.IsEnabled = true;
        }
    }

    private void OnPlayPauseClicked(object sender, RoutedEventArgs e)
    {
        if (_timerEngine == null) return;

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
        if (_timerEngine == null) return;
        _timerEngine.Skip();
        UpdateUi();
    }

    private void OnResetClicked(object sender, RoutedEventArgs e)
    {
        if (_timerEngine == null) return;
        _timerEngine.Reset();
        UpdateUi();
    }

    private void OnStatsClicked(object sender, RoutedEventArgs e)
    {
        _navigateToStatistics?.Invoke();
    }

    private void OnMoreMenuClicked(object sender, RoutedEventArgs e)
    {
        // Flyout opens automatically
    }

    private void OnDurationPresetClicked(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && int.TryParse(item.Tag?.ToString(), out int minutes))
        {
            if (_timerEngine != null)
            {
                _timerEngine.SetCustomDuration(SessionType.Focus, TimeSpan.FromMinutes(minutes));
                if (_repository != null)
                {
                    _settings.FocusMinutes = minutes;
                    _ = _repository.SaveSettingsAsync(_settings);
                }
                _timerEngine.SyncExternalTick();
                UpdateUi();
            }
        }
    }

    private void OnBreakDurationClicked(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && int.TryParse(item.Tag?.ToString(), out int minutes))
        {
            if (_timerEngine != null)
            {
                _timerEngine.SetCustomDuration(SessionType.ShortBreak, TimeSpan.FromMinutes(minutes));
                if (_repository != null)
                {
                    _settings.ShortBreakMinutes = minutes;
                    _ = _repository.SaveSettingsAsync(_settings);
                }
                _timerEngine.SyncExternalTick();
                UpdateUi();
            }
        }
    }

    private void OnLongBreakDurationClicked(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && int.TryParse(item.Tag?.ToString(), out int minutes))
        {
            if (_timerEngine != null)
            {
                _timerEngine.SetCustomDuration(SessionType.LongBreak, TimeSpan.FromMinutes(minutes));
                if (_repository != null)
                {
                    _settings.LongBreakMinutes = minutes;
                    _ = _repository.SaveSettingsAsync(_settings);
                }
                _timerEngine.SyncExternalTick();
                UpdateUi();
            }
        }
    }

    private void OnCyclesPresetClicked(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && int.TryParse(item.Tag?.ToString(), out int cycles))
        {
            if (_timerEngine != null)
            {
                _timerEngine.SessionsBeforeLongBreak = cycles;
                if (_repository != null)
                {
                    _settings.SessionsBeforeLongBreak = cycles;
                    _ = _repository.SaveSettingsAsync(_settings);
                }
                _timerEngine.SyncExternalTick();
                UpdateUi();
            }
        }
    }

    private async void OnToggleAutoStartBreaks(object sender, RoutedEventArgs e)
    {
        if (_repository != null)
        {
            _settings.AutoStartBreaks = AutoStartBreaksItem.IsChecked;
            await _repository.SaveSettingsAsync(_settings);
        }
    }

    private async void OnToggleAutoStartFocus(object sender, RoutedEventArgs e)
    {
        if (_repository != null)
        {
            _settings.AutoStartFocus = AutoStartFocusItem.IsChecked;
            await _repository.SaveSettingsAsync(_settings);
        }
    }

    private void OnNavigateTasksClicked(object sender, RoutedEventArgs e) => _navigateToTasks?.Invoke();
    private void OnNavigateStudyPlannerClicked(object sender, RoutedEventArgs e) => _navigateToStudyPlanner?.Invoke();
    private void OnNavigateInsightsClicked(object sender, RoutedEventArgs e) => _navigateToInsights?.Invoke();
    private void OnNavigateStatisticsClicked(object sender, RoutedEventArgs e) => _navigateToStatistics?.Invoke();
    private void OnNavigateSettingsClicked(object sender, RoutedEventArgs e) => _navigateToSettings?.Invoke();
    private void OnToggleMiniTimerClicked(object sender, RoutedEventArgs e) => _toggleMiniTimer?.Invoke();

    private async void OnAboutClicked(object sender, RoutedEventArgs e)
    {
        if (this.XamlRoot == null) return;
        var dialog = new ContentDialog
        {
            XamlRoot = this.XamlRoot,
            Title = "About Shift - Pomodoro",
            Content = "Shift - Pomodoro v1.0.0\n\nA serene, distraction-free productivity timer crafted for Windows with local-first offline analytics, study planner, and floating mini timer.",
            CloseButtonText = "OK"
        };
        await dialog.ShowAsync();
    }

    private void OnQuitClicked(object sender, RoutedEventArgs e)
    {
        Application.Current.Exit();
    }

    private void OnPrevCycleClicked(object sender, RoutedEventArgs e)
    {
        if (_timerEngine != null)
        {
            _timerEngine.PreviousCycle();
            UpdateUi();
        }
    }

    private void OnNextCycleClicked(object sender, RoutedEventArgs e)
    {
        if (_timerEngine != null)
        {
            _timerEngine.NextCycle();
            UpdateUi();
        }
    }

    private void OnTagSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_timerEngine == null) return;
        if (TagComboBox.SelectedItem is Tag selectedTag)
        {
            _timerEngine.ActiveTagId = selectedTag.Id;
            _timerEngine.ActiveTagName = selectedTag.Name;
            _timerEngine.ActiveTagColorHex = selectedTag.ColorHex;
        }
        else
        {
            _timerEngine.ActiveTagId = null;
            _timerEngine.ActiveTagName = null;
            _timerEngine.ActiveTagColorHex = null;
        }
    }

    private void OnTaskSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_timerEngine == null) return;
        if (TaskComboBox.SelectedItem is TaskItem selectedTask)
        {
            _timerEngine.ActiveTaskId = selectedTask.Id;
            _timerEngine.ActiveTaskTitle = selectedTask.Title;
        }
        else
        {
            _timerEngine.ActiveTaskId = null;
            _timerEngine.ActiveTaskTitle = null;
        }
    }

    private void OnApplySessionDetailsClicked(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(CustomSessionTitleBox.Text) && _timerEngine != null)
        {
            _timerEngine.ActiveTaskTitle = CustomSessionTitleBox.Text.Trim();
        }
        EditSessionFlyout.Hide();
        UpdateSessionTitle();
    }
}
