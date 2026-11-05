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

        var filtered = _currentFilter switch
        {
            "Completed" => _allTasks.Where(t => t.Status == ItemStatus.Completed).ToList(),
            "Pending" => _allTasks.Where(t => t.Status != ItemStatus.Completed).ToList(),
            _ => _allTasks
        };

        var displayItems = filtered.Select(t => new
        {
            t.Id,
            t.Title,
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
        await LoadTasksAsync();
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
            await LoadTasksAsync();
        }
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

