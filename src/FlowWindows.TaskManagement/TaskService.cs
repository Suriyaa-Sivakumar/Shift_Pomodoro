using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FlowWindows.Core.Enums;
using FlowWindows.Core.Interfaces;
using FlowWindows.Core.Models;

namespace FlowWindows.TaskManagement;

public class TaskService : ITaskService
{
    private readonly IRepository _repository;

    public TaskService(IRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<TaskItem>> GetAllTasksAsync()
    {
        return await _repository.GetTasksAsync();
    }

    public async Task<TaskItem?> GetTaskByIdAsync(string id)
    {
        return await _repository.GetTaskByIdAsync(id);
    }

    public async Task AddOrUpdateTaskAsync(TaskItem task)
    {
        await _repository.SaveTaskAsync(task);
        foreach (var sub in task.Subtasks)
        {
            await _repository.SaveSubtaskAsync(sub);
        }
    }

    public async Task RemoveTaskAsync(string taskId)
    {
        await _repository.DeleteTaskAsync(taskId);
    }

    public async Task ToggleTaskCompletedAsync(string taskId)
    {
        var task = await _repository.GetTaskByIdAsync(taskId);
        if (task == null) return;

        if (task.Status == ItemStatus.Completed)
        {
            task.Status = ItemStatus.Pending;
            task.CompletedAt = null;
        }
        else
        {
            task.Status = ItemStatus.Completed;
            task.CompletedAt = DateTimeOffset.UtcNow;
            foreach (var s in task.Subtasks)
            {
                s.IsCompleted = true;
                await _repository.SaveSubtaskAsync(s);
            }
        }

        await _repository.SaveTaskAsync(task);
    }

    public async Task RecordTimeOnTaskAsync(string taskId, int minutes)
    {
        var task = await _repository.GetTaskByIdAsync(taskId);
        if (task == null) return;

        task.TimeSpentMinutes += minutes;
        if (task.Status == ItemStatus.Pending && minutes > 0)
        {
            task.Status = ItemStatus.InProgress;
        }

        await _repository.SaveTaskAsync(task);
    }

    public async Task ClearCompletedTasksAsync()
    {
        var tasks = await _repository.GetTasksAsync();
        foreach (var task in tasks.Where(t => t.Status == ItemStatus.Completed))
        {
            await _repository.DeleteTaskAsync(task.Id);
        }
    }
}

