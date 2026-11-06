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

public sealed partial class TasksPage : Page
{
    private ITaskService? _taskService;
    private ITimerEngine? _timerEngine;
    private Action? _navigateToTimerAction;
    private List<TaskItem> _allTasks = new();
    private string _currentFilter = "Pending";
    private string _searchQuery = string.Empty;

    public TasksPage()
    {
        InitializeComponent();
    }

    public void Initialize(ITaskService taskService, ITimerEngine timerEngine, Action navigateToTimerAction)
    {
        _taskService = taskService;
        _timerEngine = timerEngine;
        _navigateToTimerAction = navigateToTimerAction;
        _ = LoadTasksAsync();
    }

    private void OnBackClicked(object sender, RoutedEventArgs e)
    {
        _navigateToTimerAction?.Invoke();
    }

    private async Task LoadTasksAsync()
    {
        if (_taskService == null) return;

        _allTasks = await _taskService.GetAllTasksAsync();

        var query = _allTasks.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(_searchQuery))
        {
            query = query.Where(t => t.Title.Contains(_searchQuery, StringComparison.OrdinalIgnoreCase));
        }

        var filtered = _currentFilter switch
        {
            "Completed" => query.Where(t => t.Status == ItemStatus.Completed).ToList(),
            "Pending" => query.Where(t => t.Status != ItemStatus.Completed).ToList(),
            _ => query.ToList()
        };

        var displayItems = filtered.Select(t => new
        {
            t.Id,
            t.Title,
            t.EstimatedMinutes,
            PriorityIndex = (int)t.Priority,
            IsChecked = t.Status == ItemStatus.Completed,
            PriorityText = t.Priority.ToString().ToUpperInvariant(),
            PriorityBadgeBrush = GetPriorityBadgeBrush(t.Priority),
            PriorityTextBrush = GetPriorityTextBrush(t.Priority),
            DurationText = $"{t.TimeSpentMinutes} / {t.EstimatedMinutes} min"
        }).ToList();

        TasksListView.ItemsSource = displayItems;
        EmptyTasksBorder.Visibility = displayItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        TasksListView.Visibility = displayItems.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private static SolidColorBrush GetPriorityBadgeBrush(PriorityLevel priority) => priority switch
    {
        PriorityLevel.High => new SolidColorBrush(Color.FromArgb(40, 255, 82, 82)),
        PriorityLevel.Medium => new SolidColorBrush(Color.FromArgb(40, 255, 215, 64)),
        _ => new SolidColorBrush(Color.FromArgb(40, 68, 138, 255))
    };

    private static SolidColorBrush GetPriorityTextBrush(PriorityLevel priority) => priority switch
    {
        PriorityLevel.High => new SolidColorBrush(Color.FromArgb(255, 255, 82, 82)),
        PriorityLevel.Medium => new SolidColorBrush(Color.FromArgb(255, 255, 215, 64)),
        _ => new SolidColorBrush(Color.FromArgb(255, 68, 138, 255))
    };

    private async void OnAddTaskClicked(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NewTaskTitleBox.Text) || _taskService == null) return;

        var priority = PriorityLevel.Medium;
        if (PriorityComboBox.SelectedItem is ComboBoxItem item)
        {
            if (Enum.TryParse<PriorityLevel>(item.Content.ToString(), out var p)) priority = p;
        }

        int estMins = (int)EstMinutesBox.Value;
        if (estMins <= 0) estMins = 25;

        var newTask = new TaskItem
        {
            Title = NewTaskTitleBox.Text.Trim(),
            Priority = priority,
            EstimatedMinutes = estMins,
            Status = ItemStatus.Pending
        };

        await _taskService.AddOrUpdateTaskAsync(newTask);
        NewTaskTitleBox.Text = string.Empty;

        TaskStatusInfoBar.Severity = InfoBarSeverity.Success;
        TaskStatusInfoBar.Message = $"Task '{newTask.Title}' created.";
        TaskStatusInfoBar.IsOpen = true;

        await LoadTasksAsync();
    }

    private async void OnSaveTaskEditedClicked(object sender, RoutedEventArgs e)
    {
        if (_taskService == null || sender is not Button btn || btn.Tag is not string taskId) return;

        var task = _allTasks.FirstOrDefault(t => t.Id == taskId);
        if (task == null) return;

        if (btn.Parent is StackPanel panel)
        {
            var titleBox = panel.Children.OfType<TextBox>().FirstOrDefault(t => t.Name == "EditTaskTitleBox");
            var priorityCombo = panel.Children.OfType<ComboBox>().FirstOrDefault(c => c.Name == "EditPriorityCombo");
            var estBox = panel.Children.OfType<NumberBox>().FirstOrDefault(n => n.Name == "EditEstMinutesBox");

            if (titleBox != null && !string.IsNullOrWhiteSpace(titleBox.Text))
            {
                task.Title = titleBox.Text.Trim();
            }

            if (priorityCombo != null && priorityCombo.SelectedIndex >= 0)
            {
                task.Priority = (PriorityLevel)priorityCombo.SelectedIndex;
            }

            if (estBox != null && estBox.Value >= 5)
            {
                task.EstimatedMinutes = (int)estBox.Value;
            }

            await _taskService.AddOrUpdateTaskAsync(task);

            TaskStatusInfoBar.Severity = InfoBarSeverity.Success;
            TaskStatusInfoBar.Message = $"Task '{task.Title}' updated.";
            TaskStatusInfoBar.IsOpen = true;

            await LoadTasksAsync();
        }
    }

    private async void OnTaskCheckboxClicked(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox cb && cb.Tag is string taskId && _taskService != null)
        {
            await _taskService.ToggleTaskCompletedAsync(taskId);
            await LoadTasksAsync();
        }
    }

    private async void OnDeleteTaskClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string taskId && _taskService != null)
        {
            await _taskService.RemoveTaskAsync(taskId);
            TaskStatusInfoBar.Severity = InfoBarSeverity.Informational;
            TaskStatusInfoBar.Message = "Task deleted.";
            TaskStatusInfoBar.IsOpen = true;
            await LoadTasksAsync();
        }
    }

    private async void OnClearCompletedClicked(object sender, RoutedEventArgs e)
    {
        if (_taskService == null) return;
        await _taskService.ClearCompletedTasksAsync();
        TaskStatusInfoBar.Severity = InfoBarSeverity.Success;
        TaskStatusInfoBar.Message = "All completed tasks cleared.";
        TaskStatusInfoBar.IsOpen = true;
        await LoadTasksAsync();
    }

    private void OnSearchTasksChanged(object sender, TextChangedEventArgs e)
    {
        _searchQuery = SearchTasksBox.Text?.Trim() ?? string.Empty;
        _ = LoadTasksAsync();
    }

    private void OnFocusTaskClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string taskId && _timerEngine != null)
        {
            var task = _allTasks.FirstOrDefault(t => t.Id == taskId);
            if (task != null)
            {
                _timerEngine.ActiveTaskId = task.Id;
                _timerEngine.ActiveTaskTitle = task.Title;
                _timerEngine.SessionTitle = task.Title;
                _navigateToTimerAction?.Invoke();
            }
        }
    }

    private void OnFilterAllClicked(object sender, RoutedEventArgs e)
    {
        _currentFilter = "All";
        _ = LoadTasksAsync();
    }

    private void OnFilterPendingClicked(object sender, RoutedEventArgs e)
    {
        _currentFilter = "Pending";
        _ = LoadTasksAsync();
    }

    private void OnFilterCompletedClicked(object sender, RoutedEventArgs e)
    {
        _currentFilter = "Completed";
        _ = LoadTasksAsync();
    }
}
