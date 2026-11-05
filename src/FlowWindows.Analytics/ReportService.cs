using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FlowWindows.Core.Enums;
using FlowWindows.Core.Interfaces;

namespace FlowWindows.Analytics;

public class ReportService : IReportService
{
    private readonly IRepository _repository;
    private readonly IAnalyticsService _analyticsService;

    public ReportService(IRepository repository, IAnalyticsService analyticsService)
    {
        _repository = repository;
        _analyticsService = analyticsService;
    }

    public async Task<string> ExportSessionsToCsvAsync(DateTimeOffset from, DateTimeOffset to, string targetFilePath)
    {
        var sessions = await _repository.GetSessionsAsync(from, to);
        var dir = Path.GetDirectoryName(targetFilePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var sb = new StringBuilder();
        sb.AppendLine("SessionId,SessionType,StartTimeUtc,EndTimeUtc,PlannedMinutes,ActualMinutes,Completed,Interrupted,Tag,Task,Subject,Notes");

        foreach (var s in sessions.OrderBy(s => s.StartTime))
        {
            var line = string.Join(",",
                EscapeCsv(s.Id),
                EscapeCsv(s.SessionType.ToString()),
                EscapeCsv(s.StartTime.ToString("yyyy-MM-dd HH:mm:ss")),
                EscapeCsv(s.EndTime.ToString("yyyy-MM-dd HH:mm:ss")),
                Math.Round(s.PlannedDurationSeconds / 60.0, 1),
                Math.Round(s.ActualDurationSeconds / 60.0, 1),
                s.Completed ? "Yes" : "No",
                s.Interrupted ? "Yes" : "No",
                EscapeCsv(s.TagName ?? ""),
                EscapeCsv(s.TaskTitle ?? ""),
                EscapeCsv(s.SubjectName ?? ""),
                EscapeCsv(s.Notes ?? "")
            );
            sb.AppendLine(line);
        }

        await File.WriteAllTextAsync(targetFilePath, sb.ToString(), Encoding.UTF8);
        return targetFilePath;
    }

    public async Task<string> ExportReportToHtmlAsync(DateTimeOffset from, DateTimeOffset to, string targetFilePath)
    {
        var sessions = await _repository.GetSessionsAsync(from, to);
        var focusSessions = sessions.Where(s => s.SessionType == SessionType.Focus).ToList();
        var totalFocusSeconds = focusSessions.Where(s => s.Completed).Sum(s => s.ActualDurationSeconds);
        var totalHours = Math.Round(totalFocusSeconds / 3600.0, 2);
        var completionRate = await _analyticsService.GetCompletionRateAsync(from, to);
        var streak = await _analyticsService.GetCurrentStreakDaysAsync();
        var tagDistribution = await _analyticsService.GetTagDurationDistributionAsync(from, to);

        var dir = Path.GetDirectoryName(targetFilePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang='en'>");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset='utf-8'>");
        sb.AppendLine("<title>Flow for Windows - Productivity Report</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; background: #0D0E12; color: #F3F4F6; padding: 40px; margin: 0; }");
        sb.AppendLine(".container { max-width: 900px; margin: 0 auto; background: #181920; border-radius: 16px; padding: 32px; box-shadow: 0 8px 32px rgba(0,0,0,0.4); }");
        sb.AppendLine("h1 { font-size: 28px; margin-bottom: 4px; font-weight: 700; color: #FFF; }");
        sb.AppendLine(".subtitle { font-size: 14px; color: #9CA3AF; margin-bottom: 28px; }");
        sb.AppendLine(".stats-grid { display: grid; grid-template-columns: repeat(4, 1fr); gap: 16px; margin-bottom: 32px; }");
        sb.AppendLine(".card { background: #232530; border-radius: 12px; padding: 20px; text-align: center; border: 1px solid rgba(255,255,255,0.05); }");
        sb.AppendLine(".card-val { font-size: 32px; font-weight: 700; color: #FF5252; margin-bottom: 4px; }");
        sb.AppendLine(".card-lbl { font-size: 12px; color: #9CA3AF; text-transform: uppercase; letter-spacing: 0.05em; }");
        sb.AppendLine("h2 { font-size: 18px; margin-top: 32px; margin-bottom: 16px; border-bottom: 1px solid rgba(255,255,255,0.1); padding-bottom: 8px; }");
        sb.AppendLine("table { width: 100%; border-collapse: collapse; margin-top: 12px; font-size: 14px; }");
        sb.AppendLine("th, td { padding: 12px 14px; text-align: left; border-bottom: 1px solid rgba(255,255,255,0.06); }");
        sb.AppendLine("th { color: #9CA3AF; font-weight: 600; font-size: 12px; text-transform: uppercase; }");
        sb.AppendLine(".pill { display: inline-block; padding: 2px 8px; border-radius: 10px; font-size: 11px; font-weight: 600; background: rgba(255,82,82,0.15); color: #FF5252; }");
        sb.AppendLine("@media print { body { background: #FFF; color: #000; } .container { background: #FFF; box-shadow: none; } }");
        sb.AppendLine("</style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("<div class='container'>");
        sb.AppendLine("<h1>Productivity Report</h1>");
        sb.AppendLine($"<div class='subtitle'>Flow for Windows | Range: {from:MMM dd, yyyy} - {to:MMM dd, yyyy}</div>");

        sb.AppendLine("<div class='stats-grid'>");
        sb.AppendLine($"<div class='card'><div class='card-val'>{totalHours}h</div><div class='card-lbl'>Total Focus</div></div>");
        sb.AppendLine($"<div class='card'><div class='card-val'>{focusSessions.Count(s => s.Completed)}</div><div class='card-lbl'>Completed Sessions</div></div>");
        sb.AppendLine($"<div class='card'><div class='card-val'>{completionRate}%</div><div class='card-lbl'>Completion Rate</div></div>");
        sb.AppendLine($"<div class='card'><div class='card-val'>{streak} d</div><div class='card-lbl'>Current Streak</div></div>");
        sb.AppendLine("</div>");

        // Tags Breakdown
        if (tagDistribution.Count > 0)
        {
            sb.AppendLine("<h2>Tag Distribution</h2>");
            sb.AppendLine("<table><tr><th>Tag</th><th>Focus Hours</th><th>Percentage</th></tr>");
            foreach (var kvp in tagDistribution.OrderByDescending(k => k.Value))
            {
                var tagHours = Math.Round(kvp.Value / 3600.0, 2);
                var pct = totalFocusSeconds > 0 ? Math.Round((kvp.Value / (double)totalFocusSeconds) * 100.0, 1) : 0;
                sb.AppendLine($"<tr><td><strong>{kvp.Key}</strong></td><td>{tagHours}h</td><td>{pct}%</td></tr>");
            }
            sb.AppendLine("</table>");
        }

        // Sessions Table
        sb.AppendLine("<h2>Recent Session History</h2>");
        sb.AppendLine("<table>");
        sb.AppendLine("<tr><th>Date & Time</th><th>Type</th><th>Duration</th><th>Tag</th><th>Task / Subject</th><th>Status</th></tr>");

        foreach (var s in sessions.Take(50))
        {
            var dateStr = s.StartTime.ToString("yyyy-MM-dd HH:mm");
            var mins = Math.Round(s.ActualDurationSeconds / 60.0, 0);
            var tag = string.IsNullOrWhiteSpace(s.TagName) ? "-" : s.TagName;
            var taskSub = !string.IsNullOrWhiteSpace(s.TaskTitle) ? s.TaskTitle : (!string.IsNullOrWhiteSpace(s.SubjectName) ? s.SubjectName : "-");
            var status = s.Completed ? "<span class='pill' style='background:rgba(0,230,118,0.15);color:#00E676;'>Completed</span>" : "<span class='pill' style='background:rgba(255,82,82,0.15);color:#FF5252;'>Interrupted</span>";

            sb.AppendLine($"<tr><td>{dateStr}</td><td>{s.SessionType}</td><td>{mins} min</td><td>{tag}</td><td>{taskSub}</td><td>{status}</td></tr>");
        }

        sb.AppendLine("</table>");
        sb.AppendLine("</div>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        await File.WriteAllTextAsync(targetFilePath, sb.ToString(), Encoding.UTF8);
        return targetFilePath;
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }
}

