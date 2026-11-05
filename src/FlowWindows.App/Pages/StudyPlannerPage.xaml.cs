using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using FlowWindows.Core.Interfaces;
using FlowWindows.Core.Models;

namespace FlowWindows_App.Pages;

public sealed partial class StudyPlannerPage : Page
{
    private IStudyPlannerService? _studyService;
    private ITimerEngine? _timerEngine;
    private Action? _navigateToTimerAction;
    private List<StudySubject> _subjects = new();

    public StudyPlannerPage()
    {
        InitializeComponent();
        ExamDatePicker.Date = DateTimeOffset.UtcNow.AddDays(30);
    }

    public void Initialize(IStudyPlannerService studyService, ITimerEngine timerEngine, Action navigateToTimerAction)
    {
        _studyService = studyService;
        _timerEngine = timerEngine;
        _navigateToTimerAction = navigateToTimerAction;
        _ = LoadSubjectsAsync();
    }

    private void OnBackClicked(object sender, RoutedEventArgs e)
    {
        _navigateToTimerAction?.Invoke();
    }

    private async Task LoadSubjectsAsync()
    {
        if (_studyService == null) return;

        _subjects = await _studyService.GetAllSubjectsWithTopicsAsync();

        var displayItems = _subjects.Select(s =>
        {
            var examText = "No exam scheduled";
            if (s.ExamDate.HasValue)
            {
                var days = (int)Math.Ceiling((s.ExamDate.Value.Date - DateTime.UtcNow.Date).TotalDays);
                if (days > 0) examText = $"Exam in {days} days";
                else if (days == 0) examText = "Exam Today!";
                else examText = "Exam Completed";
            }

            var pct = s.TargetHours > 0 ? Math.Min(100.0, (s.LoggedHours / s.TargetHours) * 100.0) : 0;

            Color accent = Color.FromArgb(255, 79, 70, 229);
            try
            {
                if (!string.IsNullOrEmpty(s.ColorHex) && s.ColorHex.StartsWith("#"))
                {
                    accent = Color.FromArgb(255, 124, 77, 255);
                }
            }
            catch { }

            return new
            {
                s.Id,
                s.Name,
                ExamCountdownText = examText,
                ColorBrush = new SolidColorBrush(accent),
                HoursProgressText = $"{Math.Round(s.LoggedHours, 1)} / {Math.Round(s.TargetHours, 1)} hrs ({Math.Round(pct, 0)}%)",
                ProgressPercent = pct,
                Topics = s.Topics
            };
        }).ToList();

        SubjectsListView.ItemsSource = displayItems;
        EmptySubjectsBorder.Visibility = displayItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        SubjectsListView.Visibility = displayItems.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void OnAddSubjectClicked(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(SubjectNameBox.Text) || _studyService == null) return;

        var name = SubjectNameBox.Text.Trim();
        var target = (double)TargetHoursBox.Value;
        if (target <= 0) target = 20.0;

        DateTimeOffset? examDate = ExamDatePicker.Date;

        var subject = new StudySubject
        {
            Name = name,
            TargetHours = target,
            ExamDate = examDate,
            ColorHex = "#7C4DFF"
        };

        // Add 2 initial sample topics
        subject.Topics.Add(new StudyTopic { Title = "Module 1: Foundations & Core Principles", EstimatedHours = target / 3.0 });
        subject.Topics.Add(new StudyTopic { Title = "Module 2: Advanced Applications & Problem Solving", EstimatedHours = target / 3.0 });

        await _studyService.AddOrUpdateSubjectAsync(subject);
        SubjectNameBox.Text = string.Empty;
        await LoadSubjectsAsync();
    }

    private async void OnDeleteSubjectClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string subjectId && _studyService != null)
        {
            await _studyService.RemoveSubjectAsync(subjectId);
            await LoadSubjectsAsync();
        }
    }

    private void OnStudySubjectClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string subjectId && _timerEngine != null)
        {
            var subject = _subjects.FirstOrDefault(s => s.Id == subjectId);
            if (subject != null)
            {
                _timerEngine.ActiveSubjectId = subject.Id;
                _timerEngine.ActiveSubjectName = subject.Name;
                _timerEngine.SessionTitle = $"Study: {subject.Name}";
                _navigateToTimerAction?.Invoke();
            }
        }
    }
}

