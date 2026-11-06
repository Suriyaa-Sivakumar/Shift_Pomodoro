namespace FlowWindows.Core.Enums;

public enum SessionType
{
    Focus = 0,
    ShortBreak = 1,
    LongBreak = 2
}

public enum SessionState
{
    Idle = 0,
    Running = 1,
    Paused = 2,
    Completed = 3
}

public enum PriorityLevel
{
    Low = 0,
    Medium = 1,
    High = 2
}

public enum ItemStatus
{
    Pending = 0,
    InProgress = 1,
    Completed = 2
}

public enum CommitmentModeLevel
{
    Off = 0,
    Gentle = 1,     // Warns on skip/reset
    Strict = 2,     // Blocks skip/reset, requires emergency confirmation
    Hardcore = 3    // Blocks skip, reset, settings change until session ends
}

public enum ThemeMode
{
    System = 0,
    Light = 1,
    Dark = 2
}

public enum GoalType
{
    DailyFocusMinutes = 0,
    WeeklyFocusMinutes = 1,
    MonthlyFocusMinutes = 2,
    DailySessions = 3,
    SubjectHours = 4,
    TasksCompleted = 5,
    WeeklySessions = 6
}

public enum SoundEffect
{
    None = 0,
    Bell = 1,
    Chime = 2,
    GentleGong = 3,
    DigitalAlert = 4,
    MinimalBeep = 5
}

