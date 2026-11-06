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

            var displayTopics = s.Topics.Select(t => new
            {
                t.Id,
                t.SubjectId,
                t.Title,
                t.EstimatedHours,
                IsCompleted = t.Status == ItemStatus.Completed,
                EstimatedHoursText = $"{t.EstimatedHours:0.#} hrs"
            }).ToList();

            var completedTopics = s.Topics.Count(t => t.Status == ItemStatus.Completed);

            return new
            {
                s.Id,
                s.Name,
                s.TargetHours,
                ExamCountdownText = examText,
                ColorBrush = new SolidColorBrush(accent),
                HoursProgressText = $"{Math.Round(s.LoggedHours, 1)} / {Math.Round(s.TargetHours, 1)} hrs ({Math.Round(pct, 0)}%)",
                ProgressPercent = pct,
                TopicsCountText = $"{completedTopics}/{s.Topics.Count} completed",
                Topics = displayTopics
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
        subject.Topics.Add(new StudyTopic { SubjectId = subject.Id, Title = "Module 1: Foundations & Core Principles", EstimatedHours = Math.Round(target / 3.0, 1) });
        subject.Topics.Add(new StudyTopic { SubjectId = subject.Id, Title = "Module 2: Practice & Applications", EstimatedHours = Math.Round(target / 3.0, 1) });

        await _studyService.AddOrUpdateSubjectAsync(subject);
        SubjectNameBox.Text = string.Empty;

        StudyStatusInfoBar.Severity = InfoBarSeverity.Success;
        StudyStatusInfoBar.Message = $"Subject '{subject.Name}' created.";
        StudyStatusInfoBar.IsOpen = true;

        await LoadSubjectsAsync();
    }

    private async void OnSaveSubjectEditedClicked(object sender, RoutedEventArgs e)
    {
        if (_studyService == null || sender is not Button btn || btn.Tag is not string subjectId) return;

        var subject = _subjects.FirstOrDefault(s => s.Id == subjectId);
        if (subject == null) return;

        if (btn.Parent is StackPanel panel)
        {
            var nameBox = panel.Children.OfType<TextBox>().FirstOrDefault(t => t.Name == "EditSubjectNameBox");
            var hoursBox = panel.Children.OfType<NumberBox>().FirstOrDefault(n => n.Name == "EditTargetHoursBox");

            if (nameBox != null && !string.IsNullOrWhiteSpace(nameBox.Text))
            {
                subject.Name = nameBox.Text.Trim();
            }

            if (hoursBox != null && hoursBox.Value > 0)
            {
                subject.TargetHours = hoursBox.Value;
            }

            await _studyService.AddOrUpdateSubjectAsync(subject);

            StudyStatusInfoBar.Severity = InfoBarSeverity.Success;
            StudyStatusInfoBar.Message = $"Subject '{subject.Name}' updated.";
            StudyStatusInfoBar.IsOpen = true;

            await LoadSubjectsAsync();
        }
    }

    private async void OnDeleteSubjectClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string subjectId && _studyService != null)
        {
            await _studyService.RemoveSubjectAsync(subjectId);
            StudyStatusInfoBar.Severity = InfoBarSeverity.Informational;
            StudyStatusInfoBar.Message = "Subject deleted.";
            StudyStatusInfoBar.IsOpen = true;
            await LoadSubjectsAsync();
        }
    }

    private async void OnAddTopicClicked(object sender, RoutedEventArgs e)
    {
        if (_studyService == null || sender is not Button btn || btn.Tag is not string subjectId) return;

        if (btn.Parent is Grid grid)
        {
            var titleBox = grid.Children.OfType<TextBox>().FirstOrDefault(t => t.Name == "NewTopicTitleBox");
            var hoursBox = grid.Children.OfType<NumberBox>().FirstOrDefault(n => n.Name == "NewTopicHoursBox");

            var title = titleBox?.Text?.Trim();
            if (string.IsNullOrWhiteSpace(title)) return;

            var hours = hoursBox?.Value > 0 ? hoursBox.Value : 2.0;

            var newTopic = new StudyTopic
            {
                SubjectId = subjectId,
                Title = title,
                EstimatedHours = hours,
                Status = ItemStatus.Pending
            };

            await _studyService.AddOrUpdateTopicAsync(newTopic);
            if (titleBox != null) titleBox.Text = string.Empty;

            StudyStatusInfoBar.Severity = InfoBarSeverity.Success;
            StudyStatusInfoBar.Message = $"Topic '{title}' added.";
            StudyStatusInfoBar.IsOpen = true;

            await LoadSubjectsAsync();
        }
    }

    private async void OnTopicCheckboxClicked(object sender, RoutedEventArgs e)
    {
        if (_studyService == null || sender is not CheckBox cb || cb.Tag is not string topicId) return;

        foreach (var sub in _subjects)
        {
            var topic = sub.Topics.FirstOrDefault(t => t.Id == topicId);
            if (topic != null)
            {
                topic.Status = cb.IsChecked == true ? ItemStatus.Completed : ItemStatus.Pending;
                topic.CompletedAt = cb.IsChecked == true ? DateTimeOffset.UtcNow : null;
                await _studyService.AddOrUpdateTopicAsync(topic);
                await LoadSubjectsAsync();
                break;
            }
        }
    }

    private async void OnDeleteTopicClicked(object sender, RoutedEventArgs e)
    {
        if (_studyService == null || sender is not Button btn || btn.Tag is not string topicId) return;

        await _studyService.RemoveTopicAsync(topicId);
        StudyStatusInfoBar.Severity = InfoBarSeverity.Informational;
        StudyStatusInfoBar.Message = "Topic deleted.";
        StudyStatusInfoBar.IsOpen = true;

        await LoadSubjectsAsync();
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
