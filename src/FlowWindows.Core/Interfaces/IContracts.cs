using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FlowWindows.Core.Enums;
using FlowWindows.Core.Models;

namespace FlowWindows.Core.Interfaces;

public class TimerTickEventArgs : EventArgs
{
    public TimeSpan RemainingTime { get; set; }
    public TimeSpan ElapsedTime { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public double ProgressFraction { get; set; }
    public SessionType SessionType { get; set; }
    public SessionState State { get; set; }
    public int CurrentCycleSession { get; set; }
    public int SessionsBeforeLongBreak { get; set; }
    public string SessionTitle { get; set; } = string.Empty;
}

public class SessionCompletedEventArgs : EventArgs
{
    public SessionRecord Session { get; set; } = null!;
    public bool AutoTransitionScheduled { get; set; }
    public SessionType NextSessionType { get; set; }
}

public interface ITimerEngine
{
    SessionState State { get; }
    SessionType CurrentSessionType { get; }
    TimeSpan RemainingTime { get; }
    TimeSpan TotalDuration { get; }
    TimeSpan ElapsedTime { get; }
    double ProgressFraction { get; }
    int CurrentCycleSession { get; }
    int SessionsBeforeLongBreak { get; set; }
    string SessionTitle { get; set; }
    string? ActiveTagId { get; set; }
    string? ActiveTagName { get; set; }
    string? ActiveTagColorHex { get; set; }
    string? ActiveTaskId { get; set; }
    string? ActiveTaskTitle { get; set; }
    string? ActiveSubjectId { get; set; }
    string? ActiveSubjectName { get; set; }
    string? ActiveTopicId { get; set; }
    CommitmentModeLevel CommitmentMode { get; set; }

    event EventHandler<TimerTickEventArgs>? Tick;
    event EventHandler<SessionState>? StateChanged;
    event EventHandler<SessionType>? SessionTypeChanged;
    event EventHandler<SessionCompletedEventArgs>? SessionCompleted;
    event EventHandler<int>? CycleChanged;

    void Start();
    void Pause();
    void Resume();
    void Reset();
    void Skip();
    void SwitchSessionType(SessionType sessionType, bool resetTimer = true);
    void SetCustomDuration(SessionType type, TimeSpan duration);
    void ApplyPreset(TimerPreset preset);
    void SyncExternalTick();
}

public interface IRepository
{
    Task InitializeAsync();
    
    // Settings
    Task<AppSettings> GetSettingsAsync();
    Task SaveSettingsAsync(AppSettings settings);

    // Sessions
    Task AddSessionAsync(SessionRecord session);
    Task<List<SessionRecord>> GetSessionsAsync(DateTimeOffset from, DateTimeOffset to);
    Task<List<SessionRecord>> GetAllSessionsAsync();
    Task DeleteSessionAsync(string id);
    Task ClearAllSessionsAsync();

    // Active Session Crash Recovery State
    Task SaveActiveSessionStateAsync(ActiveSessionState state);
    Task<ActiveSessionState?> GetActiveSessionStateAsync();
    Task ClearActiveSessionStateAsync();

    // Tags
    Task<List<Tag>> GetTagsAsync();
    Task<Tag?> GetTagByIdAsync(string id);
    Task SaveTagAsync(Tag tag);
    Task DeleteTagAsync(string id);

    // Presets
    Task<List<TimerPreset>> GetPresetsAsync();
    Task SavePresetAsync(TimerPreset preset);
    Task DeletePresetAsync(string id);

    // Study Planner
    Task<List<StudySubject>> GetSubjectsAsync();
    Task<StudySubject?> GetSubjectByIdAsync(string id);
    Task SaveSubjectAsync(StudySubject subject);
    Task DeleteSubjectAsync(string id);
    Task SaveTopicAsync(StudyTopic topic);
    Task DeleteTopicAsync(string topicId);

    // Tasks
    Task<List<TaskItem>> GetTasksAsync();
    Task<TaskItem?> GetTaskByIdAsync(string id);
    Task SaveTaskAsync(TaskItem task);
    Task DeleteTaskAsync(string id);
    Task SaveSubtaskAsync(SubtaskItem subtask);
    Task DeleteSubtaskAsync(string subtaskId);

    // Goals & Achievements
    Task<List<GoalRecord>> GetGoalsAsync();
    Task SaveGoalAsync(GoalRecord goal);
    Task DeleteGoalAsync(string id);
    Task<List<Achievement>> GetAchievementsAsync();
    Task SaveAchievementAsync(Achievement achievement);

    // Backup & Restore
    Task<string> ExportDatabaseBackupAsync(string targetPath);
    Task<bool> RestoreDatabaseBackupAsync(string backupPath);
}

public interface IAnalyticsService
{
    Task<DailyStatSummary> GetTodayStatsAsync();
    Task<List<DailyStatSummary>> GetDailyStatsAsync(DateTime fromDate, DateTime toDate);
    Task<Dictionary<string, int>> GetTagDurationDistributionAsync(DateTimeOffset from, DateTimeOffset to);
    Task<int> GetCurrentStreakDaysAsync();
    Task<int> GetBestStreakDaysAsync();
    Task<double> GetCompletionRateAsync(DateTimeOffset from, DateTimeOffset to);
    Task<List<ProductivityInsight>> GenerateInsightsAsync();
}

public interface IStudyPlannerService
{
    Task<List<StudySubject>> GetAllSubjectsWithTopicsAsync();
    Task AddOrUpdateSubjectAsync(StudySubject subject);
    Task RemoveSubjectAsync(string subjectId);
    Task AddOrUpdateTopicAsync(StudyTopic topic);
    Task RemoveTopicAsync(string topicId);
    Task RecordStudyTimeAsync(string subjectId, string? topicId, int durationSeconds);
}

public interface ITaskService
{
    Task<List<TaskItem>> GetAllTasksAsync();
    Task<TaskItem?> GetTaskByIdAsync(string id);
    Task AddOrUpdateTaskAsync(TaskItem task);
    Task RemoveTaskAsync(string taskId);
    Task ToggleTaskCompletedAsync(string taskId);
    Task RecordTimeOnTaskAsync(string taskId, int minutes);
}

public interface IGoalsService
{
    Task<List<GoalRecord>> GetActiveGoalsAsync();
    Task CheckAndProgressGoalsAsync(SessionRecord completedSession);
    Task<List<Achievement>> CheckAchievementsAsync();
}

public interface IWindowsIntegrationService
{
    void Initialize(nint mainWindowHandle);
    void SetTaskbarProgress(double fraction, SessionType sessionType, SessionState state);
    void ClearTaskbarProgress();
    void RegisterGlobalHotkeys();
    void UnregisterGlobalHotkeys();
    void ShowNotification(string title, string message);
    event Action? StartPauseHotkeyTriggered;
    event Action? SkipHotkeyTriggered;
    event Action? ResetHotkeyTriggered;
    event Action? ToggleMiniTimerHotkeyTriggered;
}

public interface IAudioNotificationService
{
    void PlaySessionCompleteSound(SoundEffect effect, int volumePercent = 80);
    void PlayBreakCompleteSound(SoundEffect effect, int volumePercent = 80);
    void PlayTickSound();
    void PlayMetronomeSound();
}

public interface IReportService
{
    Task<string> ExportSessionsToCsvAsync(DateTimeOffset from, DateTimeOffset to, string targetFilePath);
    Task<string> ExportReportToHtmlAsync(DateTimeOffset from, DateTimeOffset to, string targetFilePath);
}

