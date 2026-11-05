using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FlowWindows.Core.Enums;
using FlowWindows.Core.Interfaces;
using FlowWindows.Core.Models;

namespace FlowWindows.StudyPlanner;

public class StudyPlannerService : IStudyPlannerService
{
    private readonly IRepository _repository;

    public StudyPlannerService(IRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<StudySubject>> GetAllSubjectsWithTopicsAsync()
    {
        return await _repository.GetSubjectsAsync();
    }

    public async Task AddOrUpdateSubjectAsync(StudySubject subject)
    {
        await _repository.SaveSubjectAsync(subject);
    }

    public async Task RemoveSubjectAsync(string subjectId)
    {
        await _repository.DeleteSubjectAsync(subjectId);
    }

    public async Task AddOrUpdateTopicAsync(StudyTopic topic)
    {
        await _repository.SaveTopicAsync(topic);
    }

    public async Task RemoveTopicAsync(string topicId)
    {
        await _repository.DeleteTopicAsync(topicId);
    }

    public async Task RecordStudyTimeAsync(string subjectId, string? topicId, int durationSeconds)
    {
        var subject = await _repository.GetSubjectByIdAsync(subjectId);
        if (subject == null) return;

        var hours = durationSeconds / 3600.0;
        subject.LoggedHours += hours;
        await _repository.SaveSubjectAsync(subject);

        if (!string.IsNullOrEmpty(topicId))
        {
            var topic = subject.Topics.FirstOrDefault(t => t.Id == topicId);
            if (topic != null && topic.Status == ItemStatus.Pending)
            {
                topic.Status = ItemStatus.InProgress;
                await _repository.SaveTopicAsync(topic);
            }
        }
    }
}

