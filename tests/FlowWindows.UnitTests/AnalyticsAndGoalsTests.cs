using System;
using System.IO;
using System.Threading.Tasks;
using FlowWindows.Analytics;
using FlowWindows.Core.Enums;
using FlowWindows.Core.Models;
using FlowWindows.Data;
using Xunit;

namespace FlowWindows.UnitTests;

public class AnalyticsAndGoalsTests : IDisposable
{
    private readonly string _tempDb;
    private readonly FlowSqliteRepository _repo;
    private readonly AnalyticsService _analytics;
    private readonly GoalsService _goals;

    public AnalyticsAndGoalsTests()
    {
        _tempDb = Path.Combine(Path.GetTempPath(), $"flow_analytics_test_{Guid.NewGuid():N}.db");
        _repo = new FlowSqliteRepository(_tempDb);
        _analytics = new AnalyticsService(_repo);
        _goals = new GoalsService(_repo, _analytics);
    }

    [Fact]
    public async Task Analytics_CalculatesDailyStatsAndStreaks()
    {
        await _repo.InitializeAsync();

        var today = DateTime.UtcNow.Date;
        var now = DateTimeOffset.UtcNow;

        // Add 2 completed focus sessions today
        await _repo.AddSessionAsync(new SessionRecord
        {
            SessionType = SessionType.Focus,
            StartTime = now.AddMinutes(-50),
            EndTime = now.AddMinutes(-25),
            PlannedDurationSeconds = 1500,
            ActualDurationSeconds = 1500,
            Completed = true,
            TagName = "Coding"
        });

        await _repo.AddSessionAsync(new SessionRecord
        {
            SessionType = SessionType.Focus,
            StartTime = now.AddMinutes(-20),
            EndTime = now,
            PlannedDurationSeconds = 1200,
            ActualDurationSeconds = 1200,
            Completed = true,
            TagName = "Study"
        });

        var todayStats = await _analytics.GetTodayStatsAsync();
        Assert.Equal(2, todayStats.CompletedSessions);
        Assert.Equal(2700, todayStats.TotalFocusSeconds);

        var streak = await _analytics.GetCurrentStreakDaysAsync();
        Assert.Equal(1, streak);

        var tags = await _analytics.GetTagDurationDistributionAsync(now.AddDays(-1), now.AddDays(1));
        Assert.True(tags.ContainsKey("Coding"));
        Assert.True(tags.ContainsKey("Study"));
    }

    [Fact]
    public async Task GoalsService_ProgressesGoalsOnCompletion()
    {
        await _repo.InitializeAsync();

        var session = new SessionRecord
        {
            SessionType = SessionType.Focus,
            PlannedDurationSeconds = 1800,
            ActualDurationSeconds = 1800, // 30 minutes
            Completed = true
        };

        await _goals.CheckAndProgressGoalsAsync(session);

        var goals = await _goals.GetActiveGoalsAsync();
        var timeGoal = goals.Find(g => g.Type == GoalType.DailyFocusMinutes);
        Assert.NotNull(timeGoal);
        Assert.Equal(30, timeGoal.CurrentValue);

        var sessionGoal = goals.Find(g => g.Type == GoalType.DailySessions);
        Assert.NotNull(sessionGoal);
        Assert.Equal(1, sessionGoal.CurrentValue);
    }

    [Fact]
    public async Task Analytics_MultiDayStreak_CalculatesCorrectly()
    {
        await _repo.InitializeAsync();

        var localToday = DateTime.Now.Date;

        // Add sessions for Today, Yesterday, and 2 days ago
        for (int i = 0; i < 3; i++)
        {
            var sessionDate = localToday.AddDays(-i).AddHours(14); // 2 PM local
            var dto = new DateTimeOffset(sessionDate, DateTimeOffset.Now.Offset);

            await _repo.AddSessionAsync(new SessionRecord
            {
                SessionType = SessionType.Focus,
                StartTime = dto,
                EndTime = dto.AddMinutes(25),
                PlannedDurationSeconds = 1500,
                ActualDurationSeconds = 1500,
                Completed = true,
                TagName = "Streak Test"
            });
        }

        var streak = await _analytics.GetCurrentStreakDaysAsync();
        Assert.Equal(3, streak);
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_tempDb)) File.Delete(_tempDb);
        }
        catch { }
    }
}

