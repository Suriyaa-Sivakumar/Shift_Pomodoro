using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using FlowWindows.Core.Enums;
using FlowWindows.Core.Interfaces;
using FlowWindows.Core.Models;
using FlowWindows.WindowsIntegration;

namespace FlowWindows_App.Pages;

public sealed partial class SettingsPage : Page
{
    private IRepository? _repository;
    private ITimerEngine? _timerEngine;
    private IAudioNotificationService? _audioService;
    private IReportService? _reportService;
    private Action? _navigateToTimerAction;
    private AppSettings _settings = new();
    private bool _isLoading = true;

    public SettingsPage()
    {
        InitializeComponent();
    }

    public void Initialize(IRepository repository, ITimerEngine timerEngine, IAudioNotificationService audioService, IReportService reportService, Action navigateToTimerAction)
    {
        _repository = repository;
        _timerEngine = timerEngine;
        _audioService = audioService;
        _reportService = reportService;
        _navigateToTimerAction = navigateToTimerAction;
        _ = LoadSettingsAsync();
    }

    private void OnBackClicked(object sender, RoutedEventArgs e)
    {
        _navigateToTimerAction?.Invoke();
    }

    private async Task LoadSettingsAsync()
    {
        if (_repository == null) return;

        _isLoading = true;
        _settings = await _repository.GetSettingsAsync();

        // Appearance
        ThemeModeCombo.SelectedIndex = Math.Clamp((int)_settings.Theme, 0, 2);

        // Timer Configuration
        FocusDurationBox.Value = _settings.FocusMinutes;
        ShortBreakDurationBox.Value = _settings.ShortBreakMinutes;
        LongBreakDurationBox.Value = _settings.LongBreakMinutes;
        CyclesBeforeLongBreakBox.Value = _settings.SessionsBeforeLongBreak;
        AutoStartBreaksSwitch.IsOn = _settings.AutoStartBreaks;
        AutoStartFocusSwitch.IsOn = _settings.AutoStartFocus;

        // Commitment
        CommitmentModeCombo.SelectedIndex = Math.Clamp((int)_settings.CommitmentMode, 0, 3);

        // Sounds
        PlaySoundSwitch.IsOn = _settings.PlaySoundOnComplete;
        SoundVolumeSlider.Value = _settings.SoundVolumePercent;
        SetComboIndexByTag(FocusSoundCombo, (int)_settings.FocusCompleteSound);
        SetComboIndexByTag(BreakSoundCombo, (int)_settings.BreakCompleteSound);

        // Taskbar, Window, Startup & Mini Timer
        LaunchOnStartupSwitch.IsOn = StartupManager.IsStartupEnabled();
        ShowTimerOnTaskbarSwitch.IsOn = _settings.ShowTimerOnTaskbar;
        MinimizeToTraySwitch.IsOn = _settings.MinimizeToTrayOnClose;
        WindowsNotificationsSwitch.IsOn = _settings.ShowWindowsNotifications;
        MiniTimerAlwaysOnTopSwitch.IsOn = _settings.MiniTimerAlwaysOnTop;
        AutoOpenMiniTimerSwitch.IsOn = _settings.AutoOpenMiniTimerOnFocus;
        AutoHideMiniTimerSwitch.IsOn = _settings.AutoHideMiniTimerOnBreak;

        _isLoading = false;
    }

    private static void SetComboIndexByTag(ComboBox combo, int targetTag)
    {
        for (int i = 0; i < combo.Items.Count; i++)
        {
            if (combo.Items[i] is ComboBoxItem item && int.TryParse(item.Tag?.ToString(), out var val) && val == targetTag)
            {
                combo.SelectedIndex = i;
                return;
            }
        }
        if (combo.Items.Count > 0) combo.SelectedIndex = 0;
    }

    private static int GetSelectedComboTag(ComboBox combo, int fallback)
    {
        if (combo.SelectedItem is ComboBoxItem item && int.TryParse(item.Tag?.ToString(), out var val))
        {
            return val;
        }
        return fallback;
    }

    private async void OnSettingChanged(object sender, object e)
    {
        if (_isLoading || _repository == null) return;

        // Theme
        _settings.Theme = (ThemeMode)Math.Clamp(ThemeModeCombo.SelectedIndex, 0, 2);
        if (this.XamlRoot?.Content is FrameworkElement root)
        {
            ThemeHelper.ApplyTheme(root, _settings.Theme);
        }

        // Timer Configuration
        _settings.FocusMinutes = (int)FocusDurationBox.Value;
        _settings.ShortBreakMinutes = (int)ShortBreakDurationBox.Value;
        _settings.LongBreakMinutes = (int)LongBreakDurationBox.Value;
        _settings.SessionsBeforeLongBreak = (int)CyclesBeforeLongBreakBox.Value;
        _settings.AutoStartBreaks = AutoStartBreaksSwitch.IsOn;
        _settings.AutoStartFocus = AutoStartFocusSwitch.IsOn;

        // Commitment
        _settings.CommitmentMode = (CommitmentModeLevel)CommitmentModeCombo.SelectedIndex;

        // Sound
        _settings.PlaySoundOnComplete = PlaySoundSwitch.IsOn;
        _settings.SoundVolumePercent = (int)SoundVolumeSlider.Value;
        _settings.FocusCompleteSound = (SoundEffect)GetSelectedComboTag(FocusSoundCombo, (int)SoundEffect.Chime);
        _settings.BreakCompleteSound = (SoundEffect)GetSelectedComboTag(BreakSoundCombo, (int)SoundEffect.Bell);

        // Taskbar, Window & Mini Timer
        _settings.ShowTimerOnTaskbar = ShowTimerOnTaskbarSwitch.IsOn;
        _settings.MinimizeToTrayOnClose = MinimizeToTraySwitch.IsOn;
        _settings.ShowWindowsNotifications = WindowsNotificationsSwitch.IsOn;
        _settings.MiniTimerAlwaysOnTop = MiniTimerAlwaysOnTopSwitch.IsOn;
        _settings.AutoOpenMiniTimerOnFocus = AutoOpenMiniTimerSwitch.IsOn;
        _settings.AutoHideMiniTimerOnBreak = AutoHideMiniTimerSwitch.IsOn;

        // Apply immediately to active timer engine
        if (_timerEngine != null)
        {
            _timerEngine.CommitmentMode = _settings.CommitmentMode;
            _timerEngine.SessionsBeforeLongBreak = _settings.SessionsBeforeLongBreak;
            _timerEngine.SetCustomDuration(SessionType.Focus, TimeSpan.FromMinutes(_settings.FocusMinutes));
            _timerEngine.SetCustomDuration(SessionType.ShortBreak, TimeSpan.FromMinutes(_settings.ShortBreakMinutes));
            _timerEngine.SetCustomDuration(SessionType.LongBreak, TimeSpan.FromMinutes(_settings.LongBreakMinutes));
            _timerEngine.SyncExternalTick();
        }

        await _repository.SaveSettingsAsync(_settings);
    }

    private async void OnStartupToggled(object sender, RoutedEventArgs e)
    {
        if (_isLoading || _repository == null) return;
        bool enable = LaunchOnStartupSwitch.IsOn;
        StartupManager.SetStartupEnabled(enable);
        _settings.LaunchOnStartup = enable;
        await _repository.SaveSettingsAsync(_settings);
        SettingsStatusInfoBar.Severity = InfoBarSeverity.Success;
        SettingsStatusInfoBar.Message = enable
            ? "Shift configured to launch on Windows startup. You can also view or toggle this in Windows Task Manager > Startup apps."
            : "Shift removed from Windows startup.";
        SettingsStatusInfoBar.IsOpen = true;
    }

    private void OnPreviewFocusSoundClicked(object sender, RoutedEventArgs e)
    {
        var sound = (SoundEffect)GetSelectedComboTag(FocusSoundCombo, (int)SoundEffect.Chime);
        _audioService?.PlaySessionCompleteSound(sound, (int)SoundVolumeSlider.Value);
    }

    private void OnPreviewBreakSoundClicked(object sender, RoutedEventArgs e)
    {
        var sound = (SoundEffect)GetSelectedComboTag(BreakSoundCombo, (int)SoundEffect.Bell);
        _audioService?.PlayBreakCompleteSound(sound, (int)SoundVolumeSlider.Value);
    }

    private async void OnExportCsvClicked(object sender, RoutedEventArgs e)
    {
        if (_reportService == null) return;
        try
        {
            var userDocs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var exportFolder = Path.Combine(userDocs, "FlowWindows_Exports");
            Directory.CreateDirectory(exportFolder);
            var filePath = Path.Combine(exportFolder, $"shift_sessions_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

            await _reportService.ExportSessionsToCsvAsync(DateTimeOffset.UtcNow.AddMonths(-12), DateTimeOffset.UtcNow, filePath);

            SettingsStatusInfoBar.Severity = InfoBarSeverity.Success;
            SettingsStatusInfoBar.Message = $"Session history exported to: {filePath}";
            SettingsStatusInfoBar.IsOpen = true;
        }
        catch (Exception ex)
        {
            SettingsStatusInfoBar.Severity = InfoBarSeverity.Error;
            SettingsStatusInfoBar.Message = $"Export failed: {ex.Message}";
            SettingsStatusInfoBar.IsOpen = true;
        }
    }

    private async void OnExportReportClicked(object sender, RoutedEventArgs e)
    {
        if (_reportService == null) return;
        try
        {
            var userDocs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var exportFolder = Path.Combine(userDocs, "FlowWindows_Exports");
            Directory.CreateDirectory(exportFolder);
            var filePath = Path.Combine(exportFolder, $"shift_report_{DateTime.Now:yyyyMMdd_HHmmss}.html");

            await _reportService.ExportReportToHtmlAsync(DateTimeOffset.UtcNow.AddMonths(-1), DateTimeOffset.UtcNow, filePath);

            SettingsStatusInfoBar.Severity = InfoBarSeverity.Success;
            SettingsStatusInfoBar.Message = $"Printable HTML report generated at: {filePath}";
            SettingsStatusInfoBar.IsOpen = true;
        }
        catch (Exception ex)
        {
            SettingsStatusInfoBar.Severity = InfoBarSeverity.Error;
            SettingsStatusInfoBar.Message = $"Report generation failed: {ex.Message}";
            SettingsStatusInfoBar.IsOpen = true;
        }
    }

    private async void OnBackupDatabaseClicked(object sender, RoutedEventArgs e)
    {
        if (_repository == null) return;
        try
        {
            var userDocs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var backupFolder = Path.Combine(userDocs, "FlowWindows_Backups");
            Directory.CreateDirectory(backupFolder);
            var backupPath = Path.Combine(backupFolder, $"shift_backup_{DateTime.Now:yyyyMMdd_HHmmss}.db");

            await _repository.ExportDatabaseBackupAsync(backupPath);

            SettingsStatusInfoBar.Severity = InfoBarSeverity.Success;
            SettingsStatusInfoBar.Message = $"Local SQLite database backup saved to: {backupPath}";
            SettingsStatusInfoBar.IsOpen = true;
        }
        catch (Exception ex)
        {
            SettingsStatusInfoBar.Severity = InfoBarSeverity.Error;
            SettingsStatusInfoBar.Message = $"Backup failed: {ex.Message}";
            SettingsStatusInfoBar.IsOpen = true;
        }
    }

    private void OnRestoreDatabaseClicked(object sender, RoutedEventArgs e)
    {
        if (_repository == null) return;

        SettingsStatusInfoBar.Severity = InfoBarSeverity.Informational;
        SettingsStatusInfoBar.Message = "Place a backup file into %USERPROFILE%\\Documents\\FlowWindows_Backups and click restore.";
        SettingsStatusInfoBar.IsOpen = true;
    }
}
