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
            await RenderAnnualHeatmapAsync();
        }
        catch { }
    }

    private async Task RenderAnnualHeatmapAsync()
    {
        if (_analyticsService == null) return;

        try
        {
            var today = DateTime.Today;
            int dayOfWeekDiff = (7 + ((int)today.DayOfWeek - (int)DayOfWeek.Monday)) % 7;
            var currentWeekMonday = today.AddDays(-dayOfWeekDiff);
            // 52 weeks back:
            var startDate = currentWeekMonday.AddDays(-51 * 7);

            var annualStats = await _analyticsService.GetDailyStatsAsync(startDate, today);
            var statsDict = annualStats.ToDictionary(s => s.Date.Date, s => s);

            double totalSeconds = annualStats.Sum(s => s.TotalFocusSeconds);
            double totalHours = Math.Round(totalSeconds / 3600.0, 1);
            int activeDays = annualStats.Count(s => s.TotalFocusSeconds > 0);
            int streak = await _analyticsService.GetCurrentStreakDaysAsync();

            HeatmapTotalHoursText.Text = $"{totalHours:0.0} hours in the past year";
            HeatmapStatsText.Text = $"{activeDays} active days • {streak} day streak";

            HeatmapGrid.Children.Clear();
            HeatmapGrid.ColumnDefinitions.Clear();
            HeatmapGrid.RowDefinitions.Clear();

            HeatmapMonthsGrid.Children.Clear();
            HeatmapMonthsGrid.ColumnDefinitions.Clear();

            // 7 rows for Monday through Sunday
            for (int r = 0; r < 7; r++)
            {
                HeatmapGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(11) });
            }

            // 52 columns for weeks
            for (int c = 0; c < 52; c++)
            {
                HeatmapGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(11) });
                HeatmapMonthsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) }); // 11px cell + 3px spacing
            }

            bool isDark = this.ActualTheme != ElementTheme.Light;
            var textSecondaryBrush = ThemeHelper.GetBrush("ShiftTextSecondaryBrush", "#94A3B8");

            int lastMonth = -1;

            for (int col = 0; col < 52; col++)
            {
                var colMonday = startDate.AddDays(col * 7);

                // Add month label if month changed
                if (colMonday.Month != lastMonth)
                {
                    lastMonth = colMonday.Month;
                    var monthLabel = new TextBlock
                    {
                        Text = colMonday.ToString("MMM"),
                        FontSize = 9,
                        Foreground = textSecondaryBrush,
                        HorizontalAlignment = HorizontalAlignment.Left
                    };
                    Grid.SetColumn(monthLabel, col);
                    HeatmapMonthsGrid.Children.Add(monthLabel);
                }

                for (int row = 0; row < 7; row++)
                {
                    var cellDate = colMonday.AddDays(row);

                    if (cellDate > today)
                    {
                        var futureBorder = new Border
                        {
                            Width = 11,
                            Height = 11,
                            CornerRadius = new CornerRadius(2),
                            Background = new SolidColorBrush(Colors.Transparent),
                            Opacity = 0.1
                        };
                        Grid.SetColumn(futureBorder, col);
                        Grid.SetRow(futureBorder, row);
                        HeatmapGrid.Children.Add(futureBorder);
                        continue;
                    }

                    statsDict.TryGetValue(cellDate.Date, out var daySummary);
                    int focusSecs = daySummary?.TotalFocusSeconds ?? 0;
                    int completedShifts = daySummary?.CompletedSessions ?? 0;
                    int focusMins = focusSecs / 60;

                    Windows.UI.Color cellColor;
                    if (isDark)
                    {
                        cellColor = focusMins switch
                        {
                            0 => ThemeHelper.ParseColor("#272B33"),
                            <= 30 => ThemeHelper.ParseColor("#0E4429"),
                            <= 60 => ThemeHelper.ParseColor("#006D32"),
                            <= 120 => ThemeHelper.ParseColor("#26A641"),
                            _ => ThemeHelper.ParseColor("#39D353")
                        };
                    }
                    else
                    {
                        cellColor = focusMins switch
                        {
                            0 => ThemeHelper.ParseColor("#EBEDF0"),
                            <= 30 => ThemeHelper.ParseColor("#9BE9A8"),
                            <= 60 => ThemeHelper.ParseColor("#40C463"),
                            <= 120 => ThemeHelper.ParseColor("#30A14E"),
                            _ => ThemeHelper.ParseColor("#216E39")
                        };
                    }

                    var cellBorder = new Border
                    {
                        Width = 11,
                        Height = 11,
                        CornerRadius = new CornerRadius(2),
                        Background = new SolidColorBrush(cellColor)
                    };

                    string tooltip = focusMins > 0
                        ? $"{cellDate:dddd, MMM d, yyyy}\n{focusMins} mins focus • {completedShifts} shifts"
                        : $"{cellDate:dddd, MMM d, yyyy}\nNo focus time";

                    ToolTipService.SetToolTip(cellBorder, tooltip);

                    Grid.SetColumn(cellBorder, col);
                    Grid.SetRow(cellBorder, row);
                    HeatmapGrid.Children.Add(cellBorder);
                }
            }
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
