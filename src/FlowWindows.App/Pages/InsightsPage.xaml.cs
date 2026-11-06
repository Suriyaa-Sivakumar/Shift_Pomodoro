using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using FlowWindows.Core.Enums;
using FlowWindows.Core.Interfaces;
using FlowWindows.Core.Models;

namespace FlowWindows_App.Pages;

public sealed partial class InsightsPage : Page
{
    private IAnalyticsService? _analyticsService;
    private IGoalsService? _goalsService;
    private Action? _navigateToTimerAction;
    private List<GoalRecord> _rawGoals = new();

    public InsightsPage()
    {
        InitializeComponent();
    }

    public void Initialize(IAnalyticsService analyticsService, IGoalsService goalsService, Action navigateToTimerAction)
    {
        _analyticsService = analyticsService;
        _goalsService = goalsService;
        _navigateToTimerAction = navigateToTimerAction;
        _ = LoadDataAsync();
    }

    private void OnBackClicked(object sender, RoutedEventArgs e)
    {
        _navigateToTimerAction?.Invoke();
    }

    private async Task LoadDataAsync()
    {
        if (_analyticsService == null || _goalsService == null) return;

        // 1. Insights
        var insights = await _analyticsService.GenerateInsightsAsync();
        InsightsItemsControl.ItemsSource = insights;

        // 2. Goals
        _rawGoals = await _goalsService.GetActiveGoalsAsync();
        RefreshGoalsList();

        // 3. Achievements
        var achievements = await _goalsService.CheckAchievementsAsync();
        var displayAchievements = achievements.Select(a => new
        {
            a.Id,
            a.Title,
            a.Description,
            BadgeBackground = a.IsUnlocked ? new SolidColorBrush(Color.FromArgb(40, 255, 215, 64)) : new SolidColorBrush(Color.FromArgb(30, 148, 163, 184)),
            BadgeForeground = a.IsUnlocked ? new SolidColorBrush(Color.FromArgb(255, 255, 215, 64)) : new SolidColorBrush(Color.FromArgb(255, 203, 213, 225)),
            StatusBackground = a.IsUnlocked ? new SolidColorBrush(Color.FromArgb(35, 0, 230, 118)) : new SolidColorBrush(Color.FromArgb(30, 148, 163, 184)),
            StatusForeground = a.IsUnlocked ? new SolidColorBrush(Color.FromArgb(255, 0, 230, 118)) : new SolidColorBrush(Color.FromArgb(255, 203, 213, 225)),
            StatusText = a.IsUnlocked ? "UNLOCKED" : $"{Math.Round(a.CurrentProgress, 0)} / {Math.Round(a.TargetProgress, 0)}"
        }).ToList();
        AchievementsListView.ItemsSource = displayAchievements;
    }

    private void RefreshGoalsList()
    {
        var displayGoals = _rawGoals.Select(g =>
        {
            var unit = g.Type == GoalType.DailyFocusMinutes || g.Type == GoalType.WeeklyFocusMinutes || g.Type == GoalType.MonthlyFocusMinutes ? "min" : "sessions";
            return new
            {
                g.Id,
                g.Title,
                g.TargetValue,
                g.CurrentValue,
                g.ProgressPercentage,
                ProgressText = $"{Math.Round(g.CurrentValue, 0)} / {Math.Round(g.TargetValue, 0)} {unit} ({Math.Round(g.ProgressPercentage, 0)}%)"
            };
        }).ToList();
        GoalsListView.ItemsSource = displayGoals;
    }

    private async void OnAddGoalClicked(object sender, RoutedEventArgs e)
    {
        if (_goalsService == null) return;

        var title = NewGoalTitleBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            GoalStatusInfoBar.Severity = InfoBarSeverity.Warning;
            GoalStatusInfoBar.Message = "Please enter a valid title for the goal.";
            GoalStatusInfoBar.IsOpen = true;
            return;
        }

        var targetValue = Math.Max(1, (int)NewGoalTargetBox.Value);
        var typeIndex = NewGoalTypeCombo.SelectedIndex;
        var goalType = typeIndex switch
        {
            0 => GoalType.DailyFocusMinutes,
            1 => GoalType.WeeklyFocusMinutes,
            2 => GoalType.DailySessions,
            3 => GoalType.WeeklySessions,
            _ => GoalType.DailyFocusMinutes
        };

        var startOfToday = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero);
        var endOfPeriod = goalType == GoalType.WeeklyFocusMinutes || goalType == GoalType.WeeklySessions
            ? startOfToday.AddDays(7)
            : startOfToday.AddDays(1).AddTicks(-1);

        var newGoal = new GoalRecord
        {
            Title = title,
            Type = goalType,
            TargetValue = targetValue,
            CurrentValue = 0,
            PeriodStart = startOfToday,
            PeriodEnd = endOfPeriod
        };

        await _goalsService.AddOrUpdateGoalAsync(newGoal);
        NewGoalTitleBox.Text = string.Empty;

        GoalStatusInfoBar.Severity = InfoBarSeverity.Success;
        GoalStatusInfoBar.Message = $"Goal '{title}' created successfully.";
        GoalStatusInfoBar.IsOpen = true;

        await LoadDataAsync();
    }

    private async void OnSaveGoalEditedClicked(object sender, RoutedEventArgs e)
    {
        if (_goalsService == null || sender is not Button btn || btn.Tag is not string goalId) return;

        var goal = _rawGoals.FirstOrDefault(g => g.Id == goalId);
        if (goal == null) return;

        if (btn.Parent is StackPanel panel)
        {
            var titleBox = panel.Children.OfType<TextBox>().FirstOrDefault(t => t.Name == "EditTitleBox");
            var targetBox = panel.Children.OfType<NumberBox>().FirstOrDefault(n => n.Name == "EditTargetBox");

            if (titleBox != null && !string.IsNullOrWhiteSpace(titleBox.Text))
            {
                goal.Title = titleBox.Text.Trim();
            }

            if (targetBox != null && targetBox.Value > 0)
            {
                goal.TargetValue = targetBox.Value;
            }

            await _goalsService.AddOrUpdateGoalAsync(goal);

            GoalStatusInfoBar.Severity = InfoBarSeverity.Success;
            GoalStatusInfoBar.Message = $"Goal '{goal.Title}' updated successfully.";
            GoalStatusInfoBar.IsOpen = true;

            await LoadDataAsync();
        }
    }

    private async void OnResetGoalClicked(object sender, RoutedEventArgs e)
    {
        if (_goalsService == null || sender is not Button btn || btn.Tag is not string goalId) return;

        await _goalsService.ResetGoalProgressAsync(goalId);

        GoalStatusInfoBar.Severity = InfoBarSeverity.Informational;
        GoalStatusInfoBar.Message = "Goal progress reset to 0.";
        GoalStatusInfoBar.IsOpen = true;

        await LoadDataAsync();
    }

    private async void OnDeleteGoalClicked(object sender, RoutedEventArgs e)
    {
        if (_goalsService == null || sender is not Button btn || btn.Tag is not string goalId) return;

        await _goalsService.RemoveGoalAsync(goalId);

        GoalStatusInfoBar.Severity = InfoBarSeverity.Informational;
        GoalStatusInfoBar.Message = "Goal removed.";
        GoalStatusInfoBar.IsOpen = true;

        await LoadDataAsync();
    }

    private async void OnRefreshInsightsClicked(object sender, RoutedEventArgs e)
    {
        if (_analyticsService == null) return;
        var insights = await _analyticsService.GenerateInsightsAsync();
        InsightsItemsControl.ItemsSource = insights;

        GoalStatusInfoBar.Severity = InfoBarSeverity.Success;
        GoalStatusInfoBar.Message = "Productivity insights updated with the latest session data.";
        GoalStatusInfoBar.IsOpen = true;
    }
}
