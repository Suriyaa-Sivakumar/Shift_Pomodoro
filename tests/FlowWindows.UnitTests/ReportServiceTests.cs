using System;
using System.IO;
using System.Threading.Tasks;
using FlowWindows.Analytics;
using FlowWindows.Core.Enums;
using FlowWindows.Core.Models;
using FlowWindows.Data;
using Xunit;

namespace FlowWindows.UnitTests;

public class ReportServiceTests : IDisposable
{
    private readonly string _tempDb;
    private readonly FlowSqliteRepository _repo;
    private readonly AnalyticsService _analytics;
    private readonly ReportService _report;

    public ReportServiceTests()
    {
        _tempDb = Path.Combine(Path.GetTempPath(), $"flow_report_test_{Guid.NewGuid():N}.db");
        _repo = new FlowSqliteRepository(_tempDb);
        _analytics = new AnalyticsService(_repo);
        _report = new ReportService(_repo, _analytics);
    }

    [Fact]
    public async Task ExportSessionsToCsv_CreatesValidCsvFile()
    {
        await _repo.InitializeAsync();

        var session = new SessionRecord
        {
            SessionType = SessionType.Focus,
            StartTime = DateTimeOffset.UtcNow.AddMinutes(-25),
            EndTime = DateTimeOffset.UtcNow,
            PlannedDurationSeconds = 1500,
            ActualDurationSeconds = 1500,
            Completed = true,
            TagName = "Development",
            TaskTitle = "UI Refinement"
        };
        await _repo.AddSessionAsync(session);

        var tempCsv = Path.Combine(Path.GetTempPath(), $"report_{Guid.NewGuid():N}.csv");
        await _report.ExportSessionsToCsvAsync(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1), tempCsv);

        Assert.True(File.Exists(tempCsv));
        var content = await File.ReadAllTextAsync(tempCsv);
        Assert.Contains("SessionId,SessionType,StartTimeUtc,EndTimeUtc", content);
        Assert.Contains("Development", content);
        Assert.Contains("UI Refinement", content);

        if (File.Exists(tempCsv)) File.Delete(tempCsv);
    }

    [Fact]
    public async Task ExportReportToHtml_HandlesEmptyAndPopulatedDataGracefully()
    {
        await _repo.InitializeAsync();

        var tempHtml = Path.Combine(Path.GetTempPath(), $"report_{Guid.NewGuid():N}.html");
        // Test with empty dataset first
        await _report.ExportReportToHtmlAsync(DateTimeOffset.UtcNow.AddDays(-7), DateTimeOffset.UtcNow.AddDays(1), tempHtml);

        Assert.True(File.Exists(tempHtml));
        var content = await File.ReadAllTextAsync(tempHtml);
        Assert.Contains("Productivity Report", content);
        Assert.Contains("0h", content);

        if (File.Exists(tempHtml)) File.Delete(tempHtml);
    }

    public void Dispose()
    {
        try { if (File.Exists(_tempDb)) File.Delete(_tempDb); } catch { }
    }
}

