using System;
using Windows.Foundation;
using Windows.UI;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using FlowWindows.Core.Common;
using FlowWindows.Core.Enums;

namespace FlowWindows_App.Controls;

public sealed partial class CircularTimerControl : UserControl
{
    private double _progressFraction = 0.0;
    private SessionType _sessionType = SessionType.Focus;

    public CircularTimerControl()
    {
        InitializeComponent();
        UpdateColors();
        UpdateArc();
        UpdateCycleDots(1, 4);
    }

    public void UpdateTime(TimeSpan remaining, TimeSpan total, double fraction, SessionType type, int currentCycle, int maxCycle, string? tagOrTask)
    {
        _progressFraction = Math.Clamp(fraction, 0.0, 1.0);
        _sessionType = type;

        // Update text
        TimeText.Text = TimeFormatHelper.FormatTimer(remaining);
        SessionTypeLabel.Text = type switch
        {
            SessionType.Focus => "FOCUS SESSION",
            SessionType.ShortBreak => "SHORT BREAK",
            SessionType.LongBreak => "LONG BREAK",
            _ => "SHIFT"
        };

        if (!string.IsNullOrWhiteSpace(tagOrTask))
        {
            TagPillBorder.Visibility = Visibility.Visible;
            TagPillText.Text = tagOrTask;
        }
        else
        {
            TagPillBorder.Visibility = Visibility.Collapsed;
        }

        UpdateColors();
        UpdateArc();
        UpdateCycleDots(currentCycle, maxCycle);
    }

    private void UpdateColors()
    {
        Color accentColor = _sessionType switch
        {
            SessionType.Focus => Color.FromArgb(255, 255, 82, 82),      // #FF5252
            SessionType.ShortBreak => Color.FromArgb(255, 68, 138, 255), // #448AFF
            SessionType.LongBreak => Color.FromArgb(255, 124, 77, 255),  // #7C4DFF
            _ => Color.FromArgb(255, 255, 82, 82)
        };

        ProgressPath.Stroke = new SolidColorBrush(accentColor);
        BackgroundPath.Stroke = new SolidColorBrush(Color.FromArgb(35, accentColor.R, accentColor.G, accentColor.B));
    }

    private void UpdateArc()
    {
        double width = TimerCanvas.Width;
        double height = TimerCanvas.Height;
        double stroke = ProgressPath.StrokeThickness;
        double radius = (Math.Min(width, height) - stroke) / 2.0;
        double cx = width / 2.0;
        double cy = height / 2.0;

        // Background circle
        BgFigure.StartPoint = new Point(cx, cy - radius);
        BgArc1.Point = new Point(cx, cy + radius);
        BgArc1.Size = new Size(radius, radius);
        BgArc2.Point = new Point(cx, cy - radius);
        BgArc2.Size = new Size(radius, radius);

        // Progress arc
        // 0% -> full circle remaining or start from 0
        // Standard Flow behavior: start at top (-90 degrees), sweep clockwise by progress
        ProgressFigure.StartPoint = new Point(cx, cy - radius);
        ProgressArc.Size = new Size(radius, radius);

        if (_progressFraction <= 0.001)
        {
            ProgressPath.Visibility = Visibility.Collapsed;
            ProgressArc.Point = new Point(cx + 0.01, cy - radius);
            ProgressArc.IsLargeArc = false;
        }
        else if (_progressFraction >= 0.999)
        {
            ProgressPath.Visibility = Visibility.Visible;
            ProgressArc.Point = new Point(cx - 0.01, cy - radius);
            ProgressArc.IsLargeArc = true;
        }
        else
        {
            ProgressPath.Visibility = Visibility.Visible;
            double angleDeg = (_progressFraction * 360.0) - 90.0;
            double angleRad = angleDeg * Math.PI / 180.0;
            double endX = cx + radius * Math.Cos(angleRad);
            double endY = cy + radius * Math.Sin(angleRad);

            ProgressArc.Point = new Point(endX, endY);
            ProgressArc.IsLargeArc = _progressFraction > 0.5;
        }
    }

    private void UpdateCycleDots(int current, int max)
    {
        CycleDotsPanel.Children.Clear();
        max = Math.Max(1, max);

        Color accentColor = _sessionType switch
        {
            SessionType.Focus => Color.FromArgb(255, 255, 82, 82),
            SessionType.ShortBreak => Color.FromArgb(255, 68, 138, 255),
            SessionType.LongBreak => Color.FromArgb(255, 124, 77, 255),
            _ => Color.FromArgb(255, 255, 82, 82)
        };

        for (int i = 1; i <= max; i++)
        {
            var dot = new Ellipse
            {
                Width = 8,
                Height = 8,
                Margin = new Thickness(2, 0, 2, 0)
            };

            if (i <= current)
            {
                dot.Fill = new SolidColorBrush(accentColor);
            }
            else
            {
                dot.Fill = new SolidColorBrush(Color.FromArgb(40, 150, 150, 150));
            }

            CycleDotsPanel.Children.Add(dot);
        }
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateArc();
    }
}

