using System;
using System.Collections.Generic;
using FlowWindows.Core.Enums;

namespace FlowWindows.Core.Models;

public class Tag
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string ColorHex { get; set; } = "#FF4B4B";
    public string Icon { get; set; } = "Tag";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class TimerPreset
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Custom";
    public int FocusMinutes { get; set; } = 25;
    public int ShortBreakMinutes { get; set; } = 5;
    public int LongBreakMinutes { get; set; } = 15;
    public int SessionsBeforeLongBreak { get; set; } = 4;
    public bool AutoStartBreaks { get; set; } = false;
    public bool AutoStartFocus { get; set; } = false;
    public string? TagId { get; set; }
    public bool IsDefault { get; set; } = false;
}

public class SessionRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public SessionType SessionType { get; set; } = SessionType.Focus;
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset EndTime { get; set; }
    public int PlannedDurationSeconds { get; set; }
    public int ActualDurationSeconds { get; set; }
    public bool Completed { get; set; } = true;
    public bool Interrupted { get; set; } = false;
    public string? TagId { get; set; }
    public string? TagName { get; set; }
    public string? TagColorHex { get; set; }
    public string? TaskId { get; set; }
    public string? TaskTitle { get; set; }
    public string? SubjectId { get; set; }
    public string? SubjectName { get; set; }
    public string? TopicId { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class StudySubject
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ColorHex { get; set; } = "#4F46E5";
    public double TargetHours { get; set; } = 20.0;
    public DateTimeOffset? ExamDate { get; set; }
    public string? TagId { get; set; }
    public List<StudyTopic> Topics { get; set; } = new();
    public double LoggedHours { get; set; } = 0.0;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class StudyTopic
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string SubjectId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ItemStatus Status { get; set; } = ItemStatus.Pending;
    public PriorityLevel Priority { get; set; } = PriorityLevel.Medium;
    public double EstimatedHours { get; set; } = 2.0;
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class TaskItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public PriorityLevel Priority { get; set; } = PriorityLevel.Medium;
    public DateTimeOffset? DueDate { get; set; }
    public ItemStatus Status { get; set; } = ItemStatus.Pending;
    public int EstimatedMinutes { get; set; } = 50;
    public int TimeSpentMinutes { get; set; } = 0;
    public string? TagId { get; set; }
    public string? TagName { get; set; }
    public string? SubjectId { get; set; }
    public string? TopicId { get; set; }
    public List<SubtaskItem> Subtasks { get; set; } = new();
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class SubtaskItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TaskId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public bool IsCompleted { get; set; } = false;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}


public class ActiveSessionState
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public SessionType SessionType { get; set; } = SessionType.Focus;
    public DateTimeOffset StartTime { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset SegmentStartTime { get; set; } = DateTimeOffset.UtcNow;
    public double AccumulatedElapsedSeconds { get; set; } = 0;
    public double TotalDurationSeconds { get; set; } = 1500;
    public SessionState State { get; set; } = SessionState.Idle;
    public string? TagId { get; set; }
    public string? TagName { get; set; }
    public string? TagColorHex { get; set; }
    public string? TaskId { get; set; }
    public string? TaskTitle { get; set; }
    public string? SubjectId { get; set; }
    public string? SubjectName { get; set; }
    public string? TopicId { get; set; }
    public int CycleSession { get; set; } = 1;
    public DateTimeOffset LastHeartbeat { get; set; } = DateTimeOffset.UtcNow;
}

public class GoalRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = string.Empty;
    public GoalType Type { get; set; } = GoalType.DailyFocusMinutes;
    public double TargetValue { get; set; } = 120; // e.g. 120 minutes or 4 sessions
    public double CurrentValue { get; set; } = 0;
    public string? SubjectId { get; set; }
    public DateTimeOffset PeriodStart { get; set; } = DateTimeOffset.UtcNow.Date;
    public DateTimeOffset PeriodEnd { get; set; } = DateTimeOffset.UtcNow.Date.AddDays(1).AddTicks(-1);
    public bool IsAchieved => CurrentValue >= TargetValue;
    public double ProgressPercentage => TargetValue > 0 ? Math.Min(100.0, (CurrentValue / TargetValue) * 100.0) : 0;
}

public class Achievement
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = "Emoji";
    public double CurrentProgress { get; set; } = 0;
    public double TargetProgress { get; set; } = 1;
    public bool IsUnlocked { get; set; } = false;
    public DateTimeOffset? UnlockedAt { get; set; }
}

public class AppSettings
{
    public ThemeMode Theme { get; set; } = ThemeMode.System;
    public int FocusMinutes { get; set; } = 25;
    public int ShortBreakMinutes { get; set; } = 5;
    public int LongBreakMinutes { get; set; } = 15;
    public int SessionsBeforeLongBreak { get; set; } = 4;
    public bool AutoStartBreaks { get; set; } = false;
    public bool AutoStartFocus { get; set; } = false;
    public CommitmentModeLevel CommitmentMode { get; set; } = CommitmentModeLevel.Off;
    public bool MinimizeToTrayOnClose { get; set; } = true;
    public bool PlaySoundOnComplete { get; set; } = true;
    public SoundEffect FocusCompleteSound { get; set; } = SoundEffect.Chime;
    public SoundEffect BreakCompleteSound { get; set; } = SoundEffect.Bell;
    public int SoundVolumePercent { get; set; } = 80;
    public bool EnableTickingSound { get; set; } = false;
    public bool ShowWindowsNotifications { get; set; } = true;
    public bool MiniTimerAlwaysOnTop { get; set; } = true;
    public bool AutoOpenMiniTimerOnFocus { get; set; } = false;
    public bool AutoHideMiniTimerOnBreak { get; set; } = false;
    public double MiniTimerX { get; set; } = -1;
    public double MiniTimerY { get; set; } = -1;
    public bool MiniTimerExpanded { get; set; } = false;
    public bool GlobalHotkeysEnabled { get; set; } = true;
    public string ActivePresetId { get; set; } = string.Empty;
    public string SelectedTagId { get; set; } = string.Empty;
    public string SelectedTaskId { get; set; } = string.Empty;
}

public class ProductivityInsight
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = "Lightbulb";
    public string Category { get; set; } = "General";
}

public class DailyStatSummary
{
    public DateTime Date { get; set; }
    public int TotalFocusSeconds { get; set; }
    public int CompletedSessions { get; set; }
    public int InterruptedSessions { get; set; }
    public double FocusHours => Math.Round(TotalFocusSeconds / 3600.0, 2);
}

