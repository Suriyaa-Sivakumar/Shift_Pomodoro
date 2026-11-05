using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using FlowWindows.Core.Interfaces;
using FlowWindows.Core.Models;

namespace FlowWindows_App.Pages;

public sealed partial class StatisticsPage : Page
{
    private enum PeriodMode { Day, Week, Month, Year }

    private IRepository? _repository;
    private IAnalyticsService? _analyticsService;
    private Action? _navigateBackAction;

    private PeriodMode _currentPeriod = PeriodMode.Week;
    private DateTime _referenceDate = DateTime.Today;

    public StatisticsPage()
    {
        InitializeComponent();
    }

    public void Initialize(IRepository repository, IAnalyticsService analyticsService, Action navigateBackAction)
    {
        _repository = repository;
        _analyticsService = analyticsService;
        _navigateBackAction = navigateBackAction;

        UpdatePeriodButtonStyles();
        _ = LoadAnalyticsAsync();
    }

    private void OnBackClicked(object sender, RoutedEventArgs e)
    {
        _navigateBackAction?.Invoke();
    }

    private void OnPeriodDayClicked(object sender, RoutedEventArgs e)
    {
        _currentPeriod = PeriodMode.Day;
        UpdatePeriodButtonStyles();
        _ = LoadAnalyticsAsync();
    }

    private void OnPeriodWeekClicked(object sender, RoutedEventArgs e)
    {
        _currentPeriod = PeriodMode.Week;
        UpdatePeriodButtonStyles();
        _ = LoadAnalyticsAsync();
    }

    private void OnPeriodMonthClicked(object sender, RoutedEventArgs e)
    {
        _currentPeriod = PeriodMode.Month;
        UpdatePeriodButtonStyles();
        _ = LoadAnalyticsAsync();
    }

    private void OnPeriodYearClicked(object sender, RoutedEventArgs e)
    {
        _currentPeriod = PeriodMode.Year;
        UpdatePeriodButtonStyles();
        _ = LoadAnalyticsAsync();
    }

    private void OnPrevPeriodClicked(object sender, RoutedEventArgs e)
    {
        _referenceDate = _currentPeriod switch
        {
            PeriodMode.Day => _referenceDate.AddDays(-1),
            PeriodMode.Week => _referenceDate.AddDays(-7),
            PeriodMode.Month => _referenceDate.AddMonths(-1),
            PeriodMode.Year => _referenceDate.AddYears(-1),
            _ => _referenceDate.AddDays(-7)
        };
        _ = LoadAnalyticsAsync();
    }

    private void OnNextPeriodClicked(object sender, RoutedEventArgs e)
    {
        _referenceDate = _currentPeriod switch
        {
            PeriodMode.Day => _referenceDate.AddDays(1),
            PeriodMode.Week => _referenceDate.AddDays(7),
            PeriodMode.Month => _referenceDate.AddMonths(1),
            PeriodMode.Year => _referenceDate.AddYears(1),
            _ => _referenceDate.AddDays(7)
        };
        _ = LoadAnalyticsAsync();
    }

    private void UpdatePeriodButtonStyles()
    {
        SetPeriodButtonStyle(PeriodDayBtn, _currentPeriod == PeriodMode.Day);
        SetPeriodButtonStyle(PeriodWeekBtn, _currentPeriod == PeriodMode.Week);
        SetPeriodButtonStyle(PeriodMonthBtn, _currentPeriod == PeriodMode.Month);
        SetPeriodButtonStyle(PeriodYearBtn, _currentPeriod == PeriodMode.Year);
    }

    private void SetPeriodButtonStyle(Button btn, bool isSelected)
    {
        if (isSelected)
        {
            btn.Background = ThemeHelper.GetBrush("ShiftCardBgBrush", "#1E2023");
            btn.Foreground = ThemeHelper.GetBrush("ShiftTextPrimaryBrush", "#F5F5F7");
            btn.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
        }
        else
        {
            btn.Background = new SolidColorBrush(Colors.Transparent);
            btn.Foreground = ThemeHelper.GetBrush("ShiftTextSecondaryBrush", "#8E8E93");
            btn.FontWeight = Microsoft.UI.Text.FontWeights.Normal;
        }
    }

    private async Task LoadAnalyticsAsync()
    {
        if (_analyticsService == null || _repository == null) return;

        try
        {
            DateTime start, end;
            string dateRangeLabel;

            switch (_currentPeriod)
            {
                case PeriodMode.Day:
                    start = _referenceDate.Date;
                    end = start.AddDays(1).AddTicks(-1);
                    dateRangeLabel = _referenceDate.ToString("MMMM d, yyyy");
                    break;

                case PeriodMode.Week:
                default:
                    int diff = (7 + (_referenceDate.DayOfWeek - DayOfWeek.Monday)) % 7;
                    start = _referenceDate.AddDays(-1 * diff).Date;
                    end = start.AddDays(7).AddTicks(-1);
                    dateRangeLabel = $"{start:MMMM d}–{start.AddDays(6):d, yyyy}";
                    break;

                case PeriodMode.Month:
                    start = new DateTime(_referenceDate.Year, _referenceDate.Month, 1);
                    end = start.AddMonths(1).AddTicks(-1);
                    dateRangeLabel = _referenceDate.ToString("MMMM yyyy");
                    break;

                case PeriodMode.Year:
                    start = new DateTime(_referenceDate.Year, 1, 1);
                    end = start.AddYears(1).AddTicks(-1);
                    dateRangeLabel = _referenceDate.ToString("yyyy");
                    break;
            }

            var daily = await _analyticsService.GetDailyStatsAsync(start, end);
            var completedCount = daily.Sum(d => d.CompletedSessions);
            var totalHours = Math.Round(daily.Sum(d => d.TotalFocusSeconds) / 3600.0, 1);
            var streak = await _analyticsService.GetCurrentStreakDaysAsync();

            DateRangeSummaryText.Text = $"{dateRangeLabel} — {completedCount} Shifts";
            KpiTotalHours.Text = $"{totalHours:0.0} hrs";
            KpiCompletedCount.Text = completedCount.ToString();
            KpiStreakDays.Text = $"{streak} days";

            RenderCapsuleBarChart(start, end, daily);
        }
        catch { }
    }

    private void RenderCapsuleBarChart(DateTime start, DateTime end, List<DailyStatSummary> summaries)
    {
        ChartGrid.Children.Clear();
        ChartGrid.ColumnDefinitions.Clear();

        // 7 days for weekly view (Monday through Sunday)
        var days = new List<(string Label, double Hours)>();
        var dayNames = new[] { "M", "T", "W", "T", "F", "S", "S" };

        for (int i = 0; i < 7; i++)
        {
            var dayDate = start.AddDays(i).Date;
            var summary = summaries.FirstOrDefault(s => s.Date.Date == dayDate);
            double hrs = summary != null ? Math.Round(summary.TotalFocusSeconds / 3600.0, 2) : 0;
            days.Add((dayNames[i], hrs));
        }

        double maxHours = Math.Max(days.Max(d => d.Hours), 3.0); // minimum scale 3 hrs
        const double trackHeight = 130.0;
        const double capsuleWidth = 32.0;

        var greenBrush = ThemeHelper.GetBrush("ShiftFlowGreenBrush", "#2DB88F");
        var mutedBrush = ThemeHelper.GetBrush("ShiftFlowGreenMutedBrush", "#1F3D34");
        var labelBrush = ThemeHelper.GetBrush("ShiftTextPrimaryBrush", "#F5F5F7");

        var containerStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 16
        };

        foreach (var day in days)
        {
            var column = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                Spacing = 8
            };

            // Outer capsule track
            var trackBorder = new Border
            {
                Width = capsuleWidth,
                Height = trackHeight,
                CornerRadius = new CornerRadius(16),
                Background = mutedBrush,
                VerticalAlignment = VerticalAlignment.Bottom
            };

            // Inner green fill capsule
            double fillHeight = Math.Max(0, Math.Min(trackHeight, (day.Hours / maxHours) * trackHeight));
            if (fillHeight > 0)
            {
                if (fillHeight < 16) fillHeight = 16; // ensure round cap looks good
                var fillBorder = new Border
                {
                    Width = capsuleWidth,
                    Height = fillHeight,
                    CornerRadius = new CornerRadius(16),
                    Background = greenBrush,
                    VerticalAlignment = VerticalAlignment.Bottom
                };
                trackBorder.Child = fillBorder;
            }

            column.Children.Add(trackBorder);

            // Day label below (M, T, W, T, F, S, S)
            var label = new TextBlock
            {
                Text = day.Label,
                FontSize = 13,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = labelBrush
            };
            column.Children.Add(label);

            containerStack.Children.Add(column);
        }

        ChartGrid.Children.Add(containerStack);
    }
}
