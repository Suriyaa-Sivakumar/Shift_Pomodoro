using System;
using System.IO;
using System.Threading.Tasks;
using FlowWindows.Core.Enums;
using FlowWindows.Core.Models;
using FlowWindows.Data;
using FlowWindows.StudyPlanner;
using FlowWindows.TaskManagement;
using Xunit;

namespace FlowWindows.UnitTests;

public class TaskAndStudyTests : IDisposable
{
    private readonly string _tempDb;
    private readonly FlowSqliteRepository _repo;
    private readonly TaskService _taskService;
    private readonly StudyPlannerService _studyService;

    public TaskAndStudyTests()
    {
        _tempDb = Path.Combine(Path.GetTempPath(), $"flow_task_study_test_{Guid.NewGuid():N}.db");
        _repo = new FlowSqliteRepository(_tempDb);
        _taskService = new TaskService(_repo);
        _studyService = new StudyPlannerService(_repo);
    }

    [Fact]
    public async Task TaskService_CreateAndToggleComplete_UpdatesStatus()
    {
        await _repo.InitializeAsync();

        var task = new TaskItem
        {
            Title = "Implement Circular Timer",
            Priority = PriorityLevel.High,
            EstimatedMinutes = 60
        };

        await _taskService.AddOrUpdateTaskAsync(task);

        var retrieved = await _taskService.GetTaskByIdAsync(task.Id);
        Assert.NotNull(retrieved);
        Assert.Equal(ItemStatus.Pending, retrieved.Status);

        await _taskService.ToggleTaskCompletedAsync(task.Id);

        var completed = await _taskService.GetTaskByIdAsync(task.Id);
        Assert.NotNull(completed);
        Assert.Equal(ItemStatus.Completed, completed.Status);
        Assert.NotNull(completed.CompletedAt);

        // Record time
        await _taskService.RecordTimeOnTaskAsync(task.Id, 25);
        var updated = await _taskService.GetTaskByIdAsync(task.Id);
        Assert.NotNull(updated);
        Assert.Equal(25, updated.TimeSpentMinutes);
    }

    [Fact]
    public async Task StudyPlanner_SubjectAndTopicTracking_RecordsProgress()
    {
        await _repo.InitializeAsync();

        var subject = new StudySubject
        {
            Name = "Operating Systems",
            TargetHours = 30,
            ExamDate = DateTimeOffset.UtcNow.AddDays(14)
        };

        await _studyService.AddOrUpdateSubjectAsync(subject);

        var topic = new StudyTopic
        {
            SubjectId = subject.Id,
            Title = "Virtual Memory & Paging",
            EstimatedHours = 4,
            Status = ItemStatus.Pending
        };

        await _studyService.AddOrUpdateTopicAsync(topic);

        // Record 1 hour study session (3600 seconds)
        await _studyService.RecordStudyTimeAsync(subject.Id, topic.Id, 3600);

        var subjects = await _studyService.GetAllSubjectsWithTopicsAsync();
        var foundSubject = subjects.Find(s => s.Id == subject.Id);
        Assert.NotNull(foundSubject);
        Assert.Equal(1.0, foundSubject.LoggedHours);
        Assert.Single(foundSubject.Topics);
        Assert.Equal(ItemStatus.InProgress, foundSubject.Topics[0].Status);
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_tempDb)) File.Delete(_tempDb);
        }
        catch { }
    }
}

