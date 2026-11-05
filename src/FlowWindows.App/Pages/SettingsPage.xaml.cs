using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using FlowWindows.Core.Enums;
using FlowWindows.Core.Interfaces;
using FlowWindows.Core.Models;

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

        FocusDurationBox.Value = _settings.FocusMinutes;
        ShortBreakDurationBox.Value = _settings.ShortBreakMinutes;
        LongBreakDurationBox.Value = _settings.LongBreakMinutes;
        CyclesBeforeLongBreakBox.Value = _settings.SessionsBeforeLongBreak;
        AutoStartBreaksSwitch.IsOn = _settings.AutoStartBreaks;
        AutoStartFocusSwitch.IsOn = _settings.AutoStartFocus;

        CommitmentModeCombo.SelectedIndex = Math.Clamp((int)_settings.CommitmentMode, 0, 3);

        PlaySoundSwitch.IsOn = _settings.PlaySoundOnComplete;
        SoundVolumeSlider.Value = _settings.SoundVolumePercent;

        SetComboIndexByTag(FocusSoundCombo, (int)_settings.FocusCompleteSound);
        SetComboIndexByTag(BreakSoundCombo, (int)_settings.BreakCompleteSound);

        MinimizeToTraySwitch.IsOn = _settings.MinimizeToTrayOnClose;
        WindowsNotificationsSwitch.IsOn = _settings.ShowWindowsNotifications;
        MiniTimerAlwaysOnTopSwitch.IsOn = _settings.MiniTimerAlwaysOnTop;

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

        _settings.FocusMinutes = (int)FocusDurationBox.Value;
        _settings.ShortBreakMinutes = (int)ShortBreakDurationBox.Value;
        _settings.LongBreakMinutes = (int)LongBreakDurationBox.Value;
        _settings.SessionsBeforeLongBreak = (int)CyclesBeforeLongBreakBox.Value;
        _settings.AutoStartBreaks = AutoStartBreaksSwitch.IsOn;
        _settings.AutoStartFocus = AutoStartFocusSwitch.IsOn;

        _settings.CommitmentMode = (CommitmentModeLevel)CommitmentModeCombo.SelectedIndex;
        _settings.PlaySoundOnComplete = PlaySoundSwitch.IsOn;
        _settings.SoundVolumePercent = (int)SoundVolumeSlider.Value;

        _settings.FocusCompleteSound = (SoundEffect)GetSelectedComboTag(FocusSoundCombo, (int)SoundEffect.Chime);
        _settings.BreakCompleteSound = (SoundEffect)GetSelectedComboTag(BreakSoundCombo, (int)SoundEffect.Bell);

        _settings.MinimizeToTrayOnClose = MinimizeToTraySwitch.IsOn;
        _settings.ShowWindowsNotifications = WindowsNotificationsSwitch.IsOn;
        _settings.MiniTimerAlwaysOnTop = MiniTimerAlwaysOnTopSwitch.IsOn;

        // Apply to active timer engine
        if (_timerEngine != null)
        {
            _timerEngine.CommitmentMode = _settings.CommitmentMode;
            _timerEngine.SessionsBeforeLongBreak = _settings.SessionsBeforeLongBreak;
            _timerEngine.SetCustomDuration(SessionType.Focus, TimeSpan.FromMinutes(_settings.FocusMinutes));
            _timerEngine.SetCustomDuration(SessionType.ShortBreak, TimeSpan.FromMinutes(_settings.ShortBreakMinutes));
            _timerEngine.SetCustomDuration(SessionType.LongBreak, TimeSpan.FromMinutes(_settings.LongBreakMinutes));
        }

        await _repository.SaveSettingsAsync(_settings);
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
            var filePath = Path.Combine(exportFolder, $"flow_sessions_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

            await _reportService.ExportSessionsToCsvAsync(DateTimeOffset.UtcNow.AddYears(-5), DateTimeOffset.UtcNow.AddYears(1), filePath);

            ShowMessage($"Exported successfully to: {filePath}");
        }
        catch (Exception ex)
        {
            ShowMessage($"Export error: {ex.Message}", isError: true);
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
            var filePath = Path.Combine(exportFolder, $"flow_productivity_report_{DateTime.Now:yyyyMMdd_HHmmss}.html");

            await _reportService.ExportReportToHtmlAsync(DateTimeOffset.UtcNow.AddMonths(-3), DateTimeOffset.UtcNow.AddDays(1), filePath);

            ShowMessage($"Report generated successfully: {filePath}");
        }
        catch (Exception ex)
        {
            ShowMessage($"Report error: {ex.Message}", isError: true);
        }
    }

    private async void OnBackupDatabaseClicked(object sender, RoutedEventArgs e)
    {
        if (_repository == null) return;
        try
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var backupDir = Path.Combine(appData, "FlowWindows", "Backups");
            Directory.CreateDirectory(backupDir);
            var backupFile = Path.Combine(backupDir, $"flow_backup_{DateTime.Now:yyyyMMdd_HHmmss}.db");

            await _repository.ExportDatabaseBackupAsync(backupFile);
            ShowMessage($"Database backup created: {backupFile}");
        }
        catch (Exception ex)
        {
            ShowMessage($"Backup error: {ex.Message}", isError: true);
        }
    }

    private async void OnRestoreDatabaseClicked(object sender, RoutedEventArgs e)
    {
        if (_repository == null) return;
        try
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var backupDir = Path.Combine(appData, "FlowWindows", "Backups");
            if (Directory.Exists(backupDir))
            {
                var files = Directory.GetFiles(backupDir, "*.db");
                if (files.Length > 0)
                {
                    var latest = files[^1];
                    await _repository.RestoreDatabaseBackupAsync(latest);
                    ShowMessage($"Restored latest backup from: {Path.GetFileName(latest)}");
                    await LoadSettingsAsync();
                    return;
                }
            }
            ShowMessage("No backup files found in backup directory.", isError: true);
        }
        catch (Exception ex)
        {
            ShowMessage($"Restore error: {ex.Message}", isError: true);
        }
    }

    private void ShowMessage(string message, bool isError = false)
    {
        SettingsStatusInfoBar.Severity = isError ? InfoBarSeverity.Error : InfoBarSeverity.Success;
        SettingsStatusInfoBar.Message = message;
        SettingsStatusInfoBar.IsOpen = true;
    }
}

