using System;
using System.Threading;
using System.Threading.Tasks;
using FlowWindows.Core.Enums;
using FlowWindows.Core.Interfaces;
using FlowWindows.Core.Models;

namespace FlowWindows.Timer;

public class TimerEngine : ITimerEngine, IDisposable
{
    private readonly IRepository? _repository;
    private readonly TimerCallback _timerCallback;
    private System.Threading.Timer? _periodicTimer;
    private readonly object _lock = new();

    private SessionState _state = SessionState.Idle;
    private SessionType _currentSessionType = SessionType.Focus;
    private TimeSpan _focusDuration = TimeSpan.FromMinutes(25);
    private TimeSpan _shortBreakDuration = TimeSpan.FromMinutes(5);
    private TimeSpan _longBreakDuration = TimeSpan.FromMinutes(15);
    private TimeSpan _totalDuration = TimeSpan.FromMinutes(25);
    private TimeSpan _accumulatedElapsed = TimeSpan.Zero;
    private DateTimeOffset _currentSegmentStartTime = DateTimeOffset.UtcNow;
    private DateTimeOffset _sessionStartTime = DateTimeOffset.UtcNow;
    private int _currentCycleSession = 1;
    private int _sessionsBeforeLongBreak = 4;
    private bool _autoStartBreaks = false;
    private bool _autoStartFocus = false;
    private string _sessionTitle = "Focus Session";
    private CommitmentModeLevel _commitmentMode = CommitmentModeLevel.Off;
    private DateTimeOffset _lastHeartbeatPersisted = DateTimeOffset.MinValue;

    private void PersistActiveState()
    {
        if (_repository == null) return;
        ActiveSessionState? activeState;
        lock (_lock)
        {
            if (_state != SessionState.Running && _state != SessionState.Paused) return;

            activeState = new ActiveSessionState
            {
                Id = "current-active-session",
                SessionType = _currentSessionType,
                StartTime = _sessionStartTime,
                SegmentStartTime = _currentSegmentStartTime,
                AccumulatedElapsedSeconds = _accumulatedElapsed.TotalSeconds,
                TotalDurationSeconds = _totalDuration.TotalSeconds,
                State = _state,
                TagId = ActiveTagId,
                TagName = ActiveTagName,
                TagColorHex = ActiveTagColorHex,
                TaskId = ActiveTaskId,
                TaskTitle = ActiveTaskTitle,
                SubjectId = ActiveSubjectId,
                SubjectName = ActiveSubjectName,
                TopicId = ActiveTopicId,
                CycleSession = _currentCycleSession,
                LastHeartbeat = DateTimeOffset.UtcNow
            };
        }

        Task.Run(async () =>
        {
            try { await _repository.SaveActiveSessionStateAsync(activeState); } catch { }
        });
    }

    private void ClearActiveState()
    {
        if (_repository == null) return;
        Task.Run(async () =>
        {
            try { await _repository.ClearActiveSessionStateAsync(); } catch { }
        });
    }

    public SessionState State
    {
        get { lock (_lock) return _state; }
        private set
        {
            lock (_lock) _state = value;
            StateChanged?.Invoke(this, value);
        }
    }

    public SessionType CurrentSessionType
    {
        get { lock (_lock) return _currentSessionType; }
        private set
        {
            lock (_lock) _currentSessionType = value;
            SessionTypeChanged?.Invoke(this, value);
        }
    }

    public TimeSpan TotalDuration
    {
        get { lock (_lock) return _totalDuration; }
        private set { lock (_lock) _totalDuration = value; }
    }

    public TimeSpan ElapsedTime
    {
        get
        {
            lock (_lock)
            {
                if (_state == SessionState.Running)
                {
                    var currentSegment = DateTimeOffset.UtcNow - _currentSegmentStartTime;
                    var total = _accumulatedElapsed + currentSegment;
                    return total > _totalDuration ? _totalDuration : total;
                }
                return _accumulatedElapsed > _totalDuration ? _totalDuration : _accumulatedElapsed;
            }
        }
    }

    public TimeSpan RemainingTime
    {
        get
        {
            var elapsed = ElapsedTime;
            var remaining = TotalDuration - elapsed;
            return remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
        }
    }

    public double ProgressFraction
    {
        get
        {
            var totalMs = TotalDuration.TotalMilliseconds;
            if (totalMs <= 0) return 0.0;
            var elapsedMs = ElapsedTime.TotalMilliseconds;
            return Math.Clamp(elapsedMs / totalMs, 0.0, 1.0);
        }
    }

    public int CurrentCycleSession
    {
        get { lock (_lock) return _currentCycleSession; }
        private set
        {
            lock (_lock) _currentCycleSession = value;
            CycleChanged?.Invoke(this, value);
        }
    }

    public int SessionsBeforeLongBreak
    {
        get { lock (_lock) return _sessionsBeforeLongBreak; }
        set { lock (_lock) _sessionsBeforeLongBreak = Math.Max(1, value); }
    }

    public string SessionTitle
    {
        get { lock (_lock) return _sessionTitle; }
        set { lock (_lock) _sessionTitle = value; }
    }

    public string? ActiveTagId { get; set; }
    public string? ActiveTagName { get; set; }
    public string? ActiveTagColorHex { get; set; }
    public string? ActiveTaskId { get; set; }
    public string? ActiveTaskTitle { get; set; }
    public string? ActiveSubjectId { get; set; }
    public string? ActiveSubjectName { get; set; }
    public string? ActiveTopicId { get; set; }

    public CommitmentModeLevel CommitmentMode
    {
        get { lock (_lock) return _commitmentMode; }
        set { lock (_lock) _commitmentMode = value; }
    }

    public event EventHandler<TimerTickEventArgs>? Tick;
    public event EventHandler<SessionState>? StateChanged;
    public event EventHandler<SessionType>? SessionTypeChanged;
    public event EventHandler<SessionCompletedEventArgs>? SessionCompleted;
    public event EventHandler<int>? CycleChanged;

    public TimerEngine(IRepository? repository = null)
    {
        _repository = repository;
        _timerCallback = OnTimerCallback;
        _periodicTimer = new System.Threading.Timer(_timerCallback, null, Timeout.Infinite, Timeout.Infinite);
    }

    public void Start()
    {
        lock (_lock)
        {
            if (_state == SessionState.Running) return;

            if (_state == SessionState.Idle || _state == SessionState.Completed)
            {
                _accumulatedElapsed = TimeSpan.Zero;
                _sessionStartTime = DateTimeOffset.UtcNow;
            }

            _currentSegmentStartTime = DateTimeOffset.UtcNow;
            State = SessionState.Running;
        }

        _periodicTimer?.Change(0, 200);
        PersistActiveState();
        DispatchTick();
    }

    public void Pause()
    {
        lock (_lock)
        {
            if (_state != SessionState.Running) return;

            var currentSegment = DateTimeOffset.UtcNow - _currentSegmentStartTime;
            _accumulatedElapsed += currentSegment;
            State = SessionState.Paused;
        }

        _periodicTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        PersistActiveState();
        DispatchTick();
    }

    public void Resume()
    {
        lock (_lock)
        {
            if (_state != SessionState.Paused) return;

            _currentSegmentStartTime = DateTimeOffset.UtcNow;
            State = SessionState.Running;
        }

        _periodicTimer?.Change(0, 200);
        PersistActiveState();
        DispatchTick();
    }

    public void Reset()
    {
        bool wasRunningOrPaused;
        SessionRecord? interruptedRecord = null;

        lock (_lock)
        {
            wasRunningOrPaused = _state == SessionState.Running || _state == SessionState.Paused;
            if (wasRunningOrPaused && _accumulatedElapsed.TotalSeconds >= 10 && _currentSessionType == SessionType.Focus)
            {
                // Record interrupted session if ran for at least 10 seconds
                interruptedRecord = new SessionRecord
                {
                    SessionType = _currentSessionType,
                    StartTime = _sessionStartTime,
                    EndTime = DateTimeOffset.UtcNow,
                    PlannedDurationSeconds = (int)_totalDuration.TotalSeconds,
                    ActualDurationSeconds = (int)ElapsedTime.TotalSeconds,
                    Completed = false,
                    Interrupted = true,
                    TagId = ActiveTagId,
                    TagName = ActiveTagName,
                    TagColorHex = ActiveTagColorHex,
                    TaskId = ActiveTaskId,
                    TaskTitle = ActiveTaskTitle,
                    SubjectId = ActiveSubjectId,
                    SubjectName = ActiveSubjectName,
                    TopicId = ActiveTopicId,
                    Notes = "Reset by user"
                };
            }

            _accumulatedElapsed = TimeSpan.Zero;
            State = SessionState.Idle;
        }

        _periodicTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        ClearActiveState();

        if (interruptedRecord != null && _repository != null)
        {
            Task.Run(async () =>
            {
                try { await _repository.AddSessionAsync(interruptedRecord); } catch { }
            });
        }

        DispatchTick();
    }

    public void Skip()
    {
        bool shouldTransition;
        SessionType nextType;

        lock (_lock)
        {
            _accumulatedElapsed = TimeSpan.Zero;
            _periodicTimer?.Change(Timeout.Infinite, Timeout.Infinite);

            if (_currentSessionType == SessionType.Focus)
            {
                // Advance cycle or go to break
                if (_currentCycleSession >= _sessionsBeforeLongBreak)
                {
                    nextType = SessionType.LongBreak;
                    CurrentCycleSession = 1;
                }
                else
                {
                    nextType = SessionType.ShortBreak;
                    CurrentCycleSession++;
                }
            }
            else
            {
                nextType = SessionType.Focus;
            }

            shouldTransition = true;
        }

        if (shouldTransition)
        {
            SwitchSessionType(nextType, resetTimer: true);
        }
    }

    public void SwitchSessionType(SessionType sessionType, bool resetTimer = true)
    {
        lock (_lock)
        {
            _currentSessionType = sessionType;
            _totalDuration = sessionType switch
            {
                SessionType.Focus => _focusDuration,
                SessionType.ShortBreak => _shortBreakDuration,
                SessionType.LongBreak => _longBreakDuration,
                _ => _focusDuration
            };

            if (resetTimer)
            {
                _accumulatedElapsed = TimeSpan.Zero;
                _state = SessionState.Idle;
            }

            // Update title
            _sessionTitle = sessionType switch
            {
                SessionType.Focus => "Focus Session",
                SessionType.ShortBreak => "Short Break",
                SessionType.LongBreak => "Long Break",
                _ => "Flow"
            };
        }

        _periodicTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        SessionTypeChanged?.Invoke(this, sessionType);
        StateChanged?.Invoke(this, State);
        DispatchTick();
    }

    public void SetCustomDuration(SessionType type, TimeSpan duration)
    {
        lock (_lock)
        {
            switch (type)
            {
                case SessionType.Focus: _focusDuration = duration; break;
                case SessionType.ShortBreak: _shortBreakDuration = duration; break;
                case SessionType.LongBreak: _longBreakDuration = duration; break;
            }

            if (_currentSessionType == type)
            {
                _totalDuration = duration;
                if (_state == SessionState.Idle)
                {
                    _accumulatedElapsed = TimeSpan.Zero;
                }
            }
        }
        DispatchTick();
    }

    public void ApplyPreset(TimerPreset preset)
    {
        lock (_lock)
        {
            _sessionsBeforeLongBreak = preset.SessionsBeforeLongBreak;
            _autoStartBreaks = preset.AutoStartBreaks;
            _autoStartFocus = preset.AutoStartFocus;

            _focusDuration = TimeSpan.FromMinutes(preset.FocusMinutes);
            _shortBreakDuration = TimeSpan.FromMinutes(preset.ShortBreakMinutes);
            _longBreakDuration = TimeSpan.FromMinutes(preset.LongBreakMinutes);

            _totalDuration = _currentSessionType switch
            {
                SessionType.Focus => _focusDuration,
                SessionType.ShortBreak => _shortBreakDuration,
                SessionType.LongBreak => _longBreakDuration,
                _ => _focusDuration
            };

            if (_state == SessionState.Idle)
            {
                _accumulatedElapsed = TimeSpan.Zero;
            }
        }
        DispatchTick();
    }

    public void SyncExternalTick()
    {
        DispatchTick();
    }

    private void OnTimerCallback(object? state)
    {
        bool isCompleted = false;
        SessionCompletedEventArgs? completedArgs = null;

        lock (_lock)
        {
            if (_state != SessionState.Running) return;

            var currentSegment = DateTimeOffset.UtcNow - _currentSegmentStartTime;
            var currentElapsed = _accumulatedElapsed + currentSegment;

            if (currentElapsed >= _totalDuration)
            {
                _accumulatedElapsed = _totalDuration;
                _state = SessionState.Completed;
                isCompleted = true;

                SessionType nextType;
                bool shouldAutoStart;

                if (_currentSessionType == SessionType.Focus)
                {
                    if (_currentCycleSession >= _sessionsBeforeLongBreak)
                    {
                        nextType = SessionType.LongBreak;
                        CurrentCycleSession = 1;
                    }
                    else
                    {
                        nextType = SessionType.ShortBreak;
                        CurrentCycleSession++;
                    }
                    shouldAutoStart = _autoStartBreaks;
                }
                else
                {
                    nextType = SessionType.Focus;
                    shouldAutoStart = _autoStartFocus;
                }

                var record = new SessionRecord
                {
                    SessionType = _currentSessionType,
                    StartTime = _sessionStartTime,
                    EndTime = DateTimeOffset.UtcNow,
                    PlannedDurationSeconds = (int)_totalDuration.TotalSeconds,
                    ActualDurationSeconds = (int)_totalDuration.TotalSeconds,
                    Completed = true,
                    Interrupted = false,
                    TagId = ActiveTagId,
                    TagName = ActiveTagName,
                    TagColorHex = ActiveTagColorHex,
                    TaskId = ActiveTaskId,
                    TaskTitle = ActiveTaskTitle,
                    SubjectId = ActiveSubjectId,
                    SubjectName = ActiveSubjectName,
                    TopicId = ActiveTopicId,
                    Notes = _sessionTitle
                };

                completedArgs = new SessionCompletedEventArgs
                {
                    Session = record,
                    AutoTransitionScheduled = shouldAutoStart,
                    NextSessionType = nextType
                };
            }
        }

        if (isCompleted && completedArgs != null)
        {
            _periodicTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            ClearActiveState();

            // Persist session
            if (_repository != null)
            {
                Task.Run(async () =>
                {
                    try { await _repository.AddSessionAsync(completedArgs.Session); } catch { }
                });
            }

            StateChanged?.Invoke(this, SessionState.Completed);
            SessionCompleted?.Invoke(this, completedArgs);
            DispatchTick();

            // Handle transition
            SwitchSessionType(completedArgs.NextSessionType, resetTimer: true);

            if (completedArgs.AutoTransitionScheduled)
            {
                Start();
            }
            return;
        }

        // Heartbeat active session state to database every 5 seconds
        if (_state == SessionState.Running && (DateTimeOffset.UtcNow - _lastHeartbeatPersisted).TotalSeconds >= 5)
        {
            _lastHeartbeatPersisted = DateTimeOffset.UtcNow;
            PersistActiveState();
        }

        DispatchTick();
    }

    private void DispatchTick()
    {
        var remaining = RemainingTime;
        var elapsed = ElapsedTime;
        var total = TotalDuration;
        var fraction = ProgressFraction;
        var type = CurrentSessionType;
        var state = State;
        var cycle = CurrentCycleSession;
        var maxCycle = SessionsBeforeLongBreak;
        var title = SessionTitle;

        var args = new TimerTickEventArgs
        {
            RemainingTime = remaining,
            ElapsedTime = elapsed,
            TotalDuration = total,
            ProgressFraction = fraction,
            SessionType = type,
            State = state,
            CurrentCycleSession = cycle,
            SessionsBeforeLongBreak = maxCycle,
            SessionTitle = title
        };

        Tick?.Invoke(this, args);
    }

    public void Dispose()
    {
        _periodicTimer?.Dispose();
        _periodicTimer = null;
    }
}
