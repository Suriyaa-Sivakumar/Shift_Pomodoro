using System;
using System.Threading.Tasks;
using FlowWindows.Core.Enums;
using FlowWindows.Core.Models;
using FlowWindows.Data;
using FlowWindows.Timer;
using Xunit;

namespace FlowWindows.UnitTests;

public class TimerEngineTests
{
    [Fact]
    public void TimerEngine_InitialState_IsIdleWithDefaultDuration()
    {
        var engine = new TimerEngine();
        Assert.Equal(SessionState.Idle, engine.State);
        Assert.Equal(SessionType.Focus, engine.CurrentSessionType);
        Assert.Equal(TimeSpan.FromMinutes(25), engine.TotalDuration);
        Assert.Equal(TimeSpan.Zero, engine.ElapsedTime);
        Assert.Equal(TimeSpan.FromMinutes(25), engine.RemainingTime);
        Assert.Equal(0.0, engine.ProgressFraction);
        Assert.Equal(1, engine.CurrentCycleSession);
    }

    [Fact]
    public void TimerEngine_StartAndPause_TransitionsStateCorrectly()
    {
        var engine = new TimerEngine();
        engine.Start();
        Assert.Equal(SessionState.Running, engine.State);

        engine.Pause();
        Assert.Equal(SessionState.Paused, engine.State);

        engine.Resume();
        Assert.Equal(SessionState.Running, engine.State);

        engine.Reset();
        Assert.Equal(SessionState.Idle, engine.State);
        Assert.Equal(TimeSpan.Zero, engine.ElapsedTime);
    }

    [Fact]
    public void TimerEngine_SwitchSessionType_UpdatesTypeAndDuration()
    {
        var engine = new TimerEngine();
        engine.SetCustomDuration(SessionType.ShortBreak, TimeSpan.FromMinutes(5));
        engine.SwitchSessionType(SessionType.ShortBreak);

        Assert.Equal(SessionType.ShortBreak, engine.CurrentSessionType);
        Assert.Equal(SessionState.Idle, engine.State);
        Assert.Equal(TimeSpan.FromMinutes(5), engine.TotalDuration);
    }

    [Fact]
    public void TimerEngine_Skip_AdvancesCycleAndSessionType()
    {
        var engine = new TimerEngine();
        engine.SessionsBeforeLongBreak = 2;

        // Skip session 1 -> should go to ShortBreak, cycle session becomes 2
        engine.Skip();
        Assert.Equal(SessionType.ShortBreak, engine.CurrentSessionType);
        Assert.Equal(2, engine.CurrentCycleSession);

        // Skip break -> should go back to Focus
        engine.Skip();
        Assert.Equal(SessionType.Focus, engine.CurrentSessionType);

        // Skip session 2 (which is equal to SessionsBeforeLongBreak) -> should go to LongBreak, cycle resets to 1
        engine.Skip();
        Assert.Equal(SessionType.LongBreak, engine.CurrentSessionType);
        Assert.Equal(1, engine.CurrentCycleSession);
    }

    [Fact]
    public void TimerEngine_ApplyPreset_SetsDurationsAndCycle()
    {
        var engine = new TimerEngine();
        var preset = new TimerPreset
        {
            Name = "Deep Work",
            FocusMinutes = 50,
            ShortBreakMinutes = 10,
            LongBreakMinutes = 20,
            SessionsBeforeLongBreak = 3,
            AutoStartBreaks = true
        };

        engine.ApplyPreset(preset);
        Assert.Equal(TimeSpan.FromMinutes(50), engine.TotalDuration);
        Assert.Equal(3, engine.SessionsBeforeLongBreak);
    }

    [Fact]
    public async Task TimerEngine_SessionCompletion_FiresEvent()
    {
        var engine = new TimerEngine();
        // Set very short duration for test (150ms)
        engine.SetCustomDuration(SessionType.Focus, TimeSpan.FromMilliseconds(150));

        bool eventFired = false;
        engine.SessionCompleted += (s, e) =>
        {
            eventFired = true;
        };

        engine.Start();
        await Task.Delay(400);

        Assert.True(eventFired);
        Assert.Equal(SessionType.ShortBreak, engine.CurrentSessionType);
    }

    [Fact]
    public async Task TimerEngine_RepeatedPauseResume_MaintainsElapsedAccuracy()
    {
        var engine = new TimerEngine();
        engine.SetCustomDuration(SessionType.Focus, TimeSpan.FromMinutes(25));

        engine.Start();
        await Task.Delay(100);
        engine.Pause();
        var elapsed1 = engine.ElapsedTime;
        Assert.True(elapsed1.TotalMilliseconds >= 80);

        engine.Resume();
        await Task.Delay(100);
        engine.Pause();
        var elapsed2 = engine.ElapsedTime;
        Assert.True(elapsed2 > elapsed1);

        engine.Reset();
        Assert.Equal(SessionState.Idle, engine.State);
        Assert.Equal(TimeSpan.Zero, engine.ElapsedTime);
    }

    [Fact]
    public async Task TimerEngine_ActiveStatePersistedOnStart_AndClearedOnReset()
    {
        var tempDb = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"flow_timer_rec_{Guid.NewGuid():N}.db");
        var repo = new FlowSqliteRepository(tempDb);
        await repo.InitializeAsync();

        var engine = new TimerEngine(repo);
        engine.Start();
        await Task.Delay(100);

        var activeState = await repo.GetActiveSessionStateAsync();
        Assert.NotNull(activeState);
        Assert.Equal(SessionType.Focus, activeState.SessionType);

        engine.Reset();
        await Task.Delay(100);

        var clearedState = await repo.GetActiveSessionStateAsync();
        Assert.Null(clearedState);

        try { if (System.IO.File.Exists(tempDb)) System.IO.File.Delete(tempDb); } catch { }
    }
}

