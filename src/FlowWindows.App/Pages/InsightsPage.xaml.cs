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
        var goals = await _goalsService.GetActiveGoalsAsync();
        var displayGoals = goals.Select(g =>
        {
            var unit = g.Type == GoalType.DailyFocusMinutes || g.Type == GoalType.WeeklyFocusMinutes || g.Type == GoalType.MonthlyFocusMinutes ? "min" : "sessions";
            return new
            {
                g.Id,
                g.Title,
                g.ProgressPercentage,
                ProgressText = $"{Math.Round(g.CurrentValue, 0)} / {Math.Round(g.TargetValue, 0)} {unit} ({Math.Round(g.ProgressPercentage, 0)}%)"
            };
        }).ToList();
        GoalsListView.ItemsSource = displayGoals;

        // 3. Achievements
        var achievements = await _goalsService.CheckAchievementsAsync();
        var displayAchievements = achievements.Select(a => new
        {
            a.Id,
            a.Title,
            a.Description,
            BadgeBackground = a.IsUnlocked ? new SolidColorBrush(Color.FromArgb(40, 255, 215, 64)) : new SolidColorBrush(Color.FromArgb(20, 150, 150, 150)),
            BadgeForeground = a.IsUnlocked ? new SolidColorBrush(Color.FromArgb(255, 255, 215, 64)) : new SolidColorBrush(Color.FromArgb(100, 150, 150, 150)),
            StatusBackground = a.IsUnlocked ? new SolidColorBrush(Color.FromArgb(35, 0, 230, 118)) : new SolidColorBrush(Color.FromArgb(20, 150, 150, 150)),
            StatusForeground = a.IsUnlocked ? new SolidColorBrush(Color.FromArgb(255, 0, 230, 118)) : new SolidColorBrush(Color.FromArgb(150, 150, 150, 150)),
            StatusText = a.IsUnlocked ? "UNLOCKED" : $"{Math.Round(a.CurrentProgress, 0)} / {Math.Round(a.TargetProgress, 0)}"
        }).ToList();
        AchievementsListView.ItemsSource = displayAchievements;
    }
}

