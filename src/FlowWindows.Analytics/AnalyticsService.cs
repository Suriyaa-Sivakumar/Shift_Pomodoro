using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FlowWindows.Core.Enums;
using FlowWindows.Core.Interfaces;
using FlowWindows.Core.Models;

namespace FlowWindows.Analytics;

public class AnalyticsService : IAnalyticsService
{
    private readonly IRepository _repository;

    public AnalyticsService(IRepository repository)
    {
        _repository = repository;
    }

    public async Task<DailyStatSummary> GetTodayStatsAsync()
    {
        var localToday = DateTime.Today;
        var startOfToday = new DateTimeOffset(localToday, DateTimeOffset.Now.Offset);
        var endOfToday = startOfToday.AddDays(1).AddTicks(-1);

        var sessions = await _repository.GetSessionsAsync(startOfToday.ToUniversalTime(), endOfToday.ToUniversalTime());
        var focusSessions = sessions.Where(s => s.SessionType == SessionType.Focus).ToList();

        return new DailyStatSummary
        {
            Date = localToday,
            TotalFocusSeconds = focusSessions.Where(s => s.Completed).Sum(s => s.ActualDurationSeconds),
            CompletedSessions = focusSessions.Count(s => s.Completed),
            InterruptedSessions = focusSessions.Count(s => s.Interrupted)
        };
    }

    public async Task<List<DailyStatSummary>> GetDailyStatsAsync(DateTime fromDate, DateTime toDate)
    {
        var localOffset = DateTimeOffset.Now.Offset;
        var fromOffset = new DateTimeOffset(fromDate.Date, localOffset);
        var toOffset = new DateTimeOffset(toDate.Date.AddDays(1).AddTicks(-1), localOffset);

        var sessions = await _repository.GetSessionsAsync(fromOffset.ToUniversalTime(), toOffset.ToUniversalTime());
        var focusSessions = sessions.Where(s => s.SessionType == SessionType.Focus).ToList();

        var summaries = new List<DailyStatSummary>();
        for (var day = fromDate.Date; day <= toDate.Date; day = day.AddDays(1))
        {
            var daySessions = focusSessions.Where(s => s.StartTime.ToLocalTime().Date == day).ToList();
            summaries.Add(new DailyStatSummary
            {
                Date = day,
                TotalFocusSeconds = daySessions.Where(s => s.Completed).Sum(s => s.ActualDurationSeconds),
                CompletedSessions = daySessions.Count(s => s.Completed),
                InterruptedSessions = daySessions.Count(s => s.Interrupted)
            });
        }

        return summaries;
    }

    public async Task<Dictionary<string, int>> GetTagDurationDistributionAsync(DateTimeOffset from, DateTimeOffset to)
    {
        var sessions = await _repository.GetSessionsAsync(from, to);
        var focusSessions = sessions.Where(s => s.SessionType == SessionType.Focus && s.Completed).ToList();

        var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in focusSessions)
        {
            var tagName = string.IsNullOrWhiteSpace(s.TagName) ? "General" : s.TagName;
            if (!dict.ContainsKey(tagName)) dict[tagName] = 0;
            dict[tagName] += s.ActualDurationSeconds;
        }

        return dict;
    }

    public async Task<int> GetCurrentStreakDaysAsync()
    {
        var allSessions = await _repository.GetAllSessionsAsync();
        var completedDays = allSessions
            .Where(s => s.SessionType == SessionType.Focus && s.Completed)
            .Select(s => s.StartTime.ToLocalTime().Date)
            .Distinct()
            .OrderByDescending(d => d)
            .ToList();

        if (completedDays.Count == 0) return 0;

        var today = DateTime.Today;
        var current = today;

        // If no session today yet, check if yesterday was completed
        if (!completedDays.Contains(today))
        {
            current = today.AddDays(-1);
            if (!completedDays.Contains(current))
            {
                return 0;
            }
        }

        int streak = 0;
        while (completedDays.Contains(current))
        {
            streak++;
            current = current.AddDays(-1);
        }

        return streak;
    }

    public async Task<int> GetBestStreakDaysAsync()
    {
        var allSessions = await _repository.GetAllSessionsAsync();
        var dates = allSessions
            .Where(s => s.SessionType == SessionType.Focus && s.Completed)
            .Select(s => s.StartTime.ToLocalTime().Date)
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        if (dates.Count == 0) return 0;

        int maxStreak = 1;
        int currentStreak = 1;

        for (int i = 1; i < dates.Count; i++)
        {
            if (dates[i] == dates[i - 1].AddDays(1))
            {
                currentStreak++;
                if (currentStreak > maxStreak) maxStreak = currentStreak;
            }
            else
            {
                currentStreak = 1;
            }
        }

        return maxStreak;
    }

    public async Task<double> GetCompletionRateAsync(DateTimeOffset from, DateTimeOffset to)
    {
        var sessions = await _repository.GetSessionsAsync(from, to);
        var focusSessions = sessions.Where(s => s.SessionType == SessionType.Focus).ToList();

        if (focusSessions.Count == 0) return 100.0;
        var completed = focusSessions.Count(s => s.Completed);
        return Math.Round(((double)completed / focusSessions.Count) * 100.0, 1);
    }

    public async Task<List<ProductivityInsight>> GenerateInsightsAsync()
    {
        var insights = new List<ProductivityInsight>();
        var allSessions = await _repository.GetAllSessionsAsync();
        var focusSessions = allSessions.Where(s => s.SessionType == SessionType.Focus).ToList();

        if (focusSessions.Count < 3)
        {
            insights.Add(new ProductivityInsight
            {
                Title = "Gathering Your Flow Patterns",
                Description = "Complete at least 3 focus sessions to unlock personalized productivity trends and peak focus hours.",
                Icon = "Compass",
                Category = "Onboarding"
            });
            return insights;
        }

        // 1. Peak Hour Analysis in Local Time
        var hourGroups = focusSessions
            .Where(s => s.Completed)
            .GroupBy(s => s.StartTime.ToLocalTime().Hour)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault();

        if (hourGroups != null)
        {
            var peakHour = hourGroups.Key;
            var ampm = peakHour >= 12 ? "PM" : "AM";
            var displayHour = peakHour % 12 == 0 ? 12 : peakHour % 12;
            insights.Add(new ProductivityInsight
            {
                Title = "Prime Focus Window",
                Description = $"Your concentration peaks around {displayHour}:00 {ampm}. Schedule your most demanding tasks during this window.",
                Icon = "Clock",
                Category = "Focus Rhythm"
            });
        }

        // 2. Day of Week Analysis in Local Time
        var dayGroups = focusSessions
            .Where(s => s.Completed)
            .GroupBy(s => s.StartTime.ToLocalTime().DayOfWeek)
            .OrderByDescending(g => g.Sum(s => s.ActualDurationSeconds))
            .FirstOrDefault();

        if (dayGroups != null)
        {
            insights.Add(new ProductivityInsight
            {
                Title = "Power Day",
                Description = $"{dayGroups.Key}s are your highest output days, totaling {Math.Round(dayGroups.Sum(s => s.ActualDurationSeconds) / 3600.0, 1)} hours of deep work.",
                Icon = "Calendar",
                Category = "Consistency"
            });
        }

        // 3. Average Session Length
        var avgMins = Math.Round(focusSessions.Where(s => s.Completed).Average(s => s.ActualDurationSeconds) / 60.0, 1);
        insights.Add(new ProductivityInsight
        {
            Title = "Session Stamina",
            Description = $"Your completed sessions average {avgMins} minutes of uninterrupted attention.",
            Icon = "Zap",
            Category = "Metrics"
        });

        // 4. Completion Rate
        var compRate = Math.Round(((double)focusSessions.Count(s => s.Completed) / focusSessions.Count) * 100.0, 1);
        if (compRate >= 80)
        {
            insights.Add(new ProductivityInsight
            {
                Title = "High Execution Rate",
                Description = $"Outstanding discipline! You finish {compRate}% of all initiated focus sessions without early termination.",
                Icon = "Award",
                Category = "Discipline"
            });
        }

        return insights;
    }
}

