using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FlowWindows.Core.Enums;
using FlowWindows.Core.Interfaces;
using FlowWindows.Core.Models;

namespace FlowWindows.Analytics;

public class GoalsService : IGoalsService
{
    private readonly IRepository _repository;
    private readonly IAnalyticsService _analyticsService;

    public GoalsService(IRepository repository, IAnalyticsService analyticsService)
    {
        _repository = repository;
        _analyticsService = analyticsService;
    }

    public async Task<List<GoalRecord>> GetActiveGoalsAsync()
    {
        var goals = await _repository.GetGoalsAsync();
        var now = DateTimeOffset.UtcNow;

        if (goals.Count == 0)
        {
            // Seed standard daily focus target (120 mins) & daily session target (4 sessions)
            var startOfToday = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero);
            var endOfToday = startOfToday.AddDays(1).AddTicks(-1);

            var defaultGoals = new List<GoalRecord>
            {
                new GoalRecord
                {
                    Title = "Daily Focus Time",
                    Type = GoalType.DailyFocusMinutes,
                    TargetValue = 120, // 2 hours
                    CurrentValue = 0,
                    PeriodStart = startOfToday,
                    PeriodEnd = endOfToday
                },
                new GoalRecord
                {
                    Title = "Daily Focus Sessions",
                    Type = GoalType.DailySessions,
                    TargetValue = 4,
                    CurrentValue = 0,
                    PeriodStart = startOfToday,
                    PeriodEnd = endOfToday
                }
            };

            foreach (var g in defaultGoals)
            {
                await _repository.SaveGoalAsync(g);
            }
            return defaultGoals;
        }

        return goals;
    }

    public async Task CheckAndProgressGoalsAsync(SessionRecord completedSession)
    {
        if (completedSession.SessionType != SessionType.Focus || !completedSession.Completed) return;

        var goals = await GetActiveGoalsAsync();
        var sessionMinutes = completedSession.ActualDurationSeconds / 60.0;
        var now = DateTimeOffset.UtcNow;

        foreach (var goal in goals)
        {
            if (now >= goal.PeriodStart && now <= goal.PeriodEnd)
            {
                switch (goal.Type)
                {
                    case GoalType.DailyFocusMinutes:
                    case GoalType.WeeklyFocusMinutes:
                    case GoalType.MonthlyFocusMinutes:
                        goal.CurrentValue += sessionMinutes;
                        await _repository.SaveGoalAsync(goal);
                        break;

                    case GoalType.DailySessions:
                    case GoalType.WeeklySessions:
                        goal.CurrentValue += 1;
                        await _repository.SaveGoalAsync(goal);
                        break;

                    case GoalType.SubjectHours:
                        if (!string.IsNullOrEmpty(goal.SubjectId) && goal.SubjectId == completedSession.SubjectId)
                        {
                            goal.CurrentValue += (sessionMinutes / 60.0);
                            await _repository.SaveGoalAsync(goal);
                        }
                        break;
                }
            }
        }
    }

    public async Task<List<Achievement>> CheckAchievementsAsync()
    {
        var achievements = await _repository.GetAchievementsAsync();
        var allSessions = await _repository.GetAllSessionsAsync();
        var focusSessions = allSessions.Where(s => s.SessionType == SessionType.Focus && s.Completed).ToList();
        var streak = await _analyticsService.GetCurrentStreakDaysAsync();

        var todaySessions = focusSessions.Count(s => s.StartTime.ToLocalTime().Date == DateTime.Today);

        foreach (var ach in achievements)
        {
            if (ach.IsUnlocked) continue;

            bool unlocked = false;
            switch (ach.Key)
            {
                case "first_step":
                    ach.CurrentProgress = focusSessions.Count >= 1 ? 1 : 0;
                    if (focusSessions.Count >= 1) unlocked = true;
                    break;

                case "streak_3":
                    ach.CurrentProgress = Math.Min(3, streak);
                    if (streak >= 3) unlocked = true;
                    break;

                case "streak_7":
                    ach.CurrentProgress = Math.Min(7, streak);
                    if (streak >= 7) unlocked = true;
                    break;

                case "centurion":
                    ach.CurrentProgress = Math.Min(100, focusSessions.Count);
                    if (focusSessions.Count >= 100) unlocked = true;
                    break;

                case "marathon":
                    ach.CurrentProgress = Math.Min(4, todaySessions);
                    if (todaySessions >= 4) unlocked = true;
                    break;
            }

            if (unlocked)
            {
                ach.IsUnlocked = true;
                ach.UnlockedAt = DateTimeOffset.UtcNow;
            }

            await _repository.SaveAchievementAsync(ach);
        }

        return achievements;
    }

    public async Task AddOrUpdateGoalAsync(GoalRecord goal)
    {
        await _repository.SaveGoalAsync(goal);
    }

    public async Task RemoveGoalAsync(string goalId)
    {
        await _repository.DeleteGoalAsync(goalId);
    }

    public async Task ResetGoalProgressAsync(string goalId)
    {
        var goals = await _repository.GetGoalsAsync();
        var target = goals.FirstOrDefault(g => g.Id == goalId);
        if (target != null)
        {
            target.CurrentValue = 0;
            await _repository.SaveGoalAsync(target);
        }
    }
}

