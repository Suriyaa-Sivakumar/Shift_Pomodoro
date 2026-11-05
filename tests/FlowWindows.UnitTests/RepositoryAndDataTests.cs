using System;
using System.IO;
using System.Threading.Tasks;
using FlowWindows.Core.Enums;
using FlowWindows.Core.Models;
using FlowWindows.Data;
using Xunit;

namespace FlowWindows.UnitTests;

public class RepositoryAndDataTests : IDisposable
{
    private readonly string _tempDb;
    private readonly FlowSqliteRepository _repo;

    public RepositoryAndDataTests()
    {
        _tempDb = Path.Combine(Path.GetTempPath(), $"flow_test_{Guid.NewGuid():N}.db");
        _repo = new FlowSqliteRepository(_tempDb);
    }

    [Fact]
    public async Task InitializeAsync_SeedsDefaultsProperly()
    {
        await _repo.InitializeAsync();

        var presets = await _repo.GetPresetsAsync();
        Assert.NotEmpty(presets);
        Assert.Contains(presets, p => p.Name == "Standard");
        Assert.Contains(presets, p => p.Name == "Deep Work");

        var tags = await _repo.GetTagsAsync();
        Assert.NotEmpty(tags);
        Assert.Contains(tags, t => t.Name == "Deep Work");

    }

    [Fact]
    public async Task Sessions_AddAndRetrieve_WorksCorrectly()
    {
        await _repo.InitializeAsync();

        var now = DateTimeOffset.UtcNow;
        var session = new SessionRecord
        {
            SessionType = SessionType.Focus,
            StartTime = now.AddMinutes(-25),
            EndTime = now,
            PlannedDurationSeconds = 1500,
            ActualDurationSeconds = 1500,
            Completed = true,
            TagName = "Study",
            TaskTitle = "Chapter 4"
        };

        await _repo.AddSessionAsync(session);

        var retrieved = await _repo.GetSessionsAsync(now.AddHours(-1), now.AddHours(1));
        Assert.Single(retrieved);
        Assert.Equal("Study", retrieved[0].TagName);
        Assert.Equal("Chapter 4", retrieved[0].TaskTitle);
    }

    [Fact]
    public async Task BackupAndRestore_CreatesValidCopy()
    {
        await _repo.InitializeAsync();

        var backupPath = Path.Combine(Path.GetTempPath(), $"flow_backup_{Guid.NewGuid():N}.db");
        await _repo.ExportDatabaseBackupAsync(backupPath);

        Assert.True(File.Exists(backupPath));

        var restored = await _repo.RestoreDatabaseBackupAsync(backupPath);
        Assert.True(restored);

        if (File.Exists(backupPath)) File.Delete(backupPath);
    }

    [Fact]
    public async Task ActiveSessionState_SaveRetrieveClear_WorksAccurately()
    {
        await _repo.InitializeAsync();

        var state = new ActiveSessionState
        {
            Id = "test-session-crash",
            SessionType = SessionType.Focus,
            StartTime = DateTimeOffset.UtcNow.AddMinutes(-10),
            SegmentStartTime = DateTimeOffset.UtcNow.AddMinutes(-5),
            AccumulatedElapsedSeconds = 300,
            TotalDurationSeconds = 1500,
            State = SessionState.Running,
            TagName = "Testing",
            TaskTitle = "Crash Recovery Verification"
        };

        await _repo.SaveActiveSessionStateAsync(state);

        var retrieved = await _repo.GetActiveSessionStateAsync();
        Assert.NotNull(retrieved);
        Assert.Equal("test-session-crash", retrieved.Id);
        Assert.Equal(SessionType.Focus, retrieved.SessionType);
        Assert.Equal(300, retrieved.AccumulatedElapsedSeconds);
        Assert.Equal("Testing", retrieved.TagName);
        Assert.Equal("Crash Recovery Verification", retrieved.TaskTitle);

        await _repo.ClearActiveSessionStateAsync();
        var cleared = await _repo.GetActiveSessionStateAsync();
        Assert.Null(cleared);
    }

    [Fact]
    public async Task Settings_GetUpdateAndReset_WorksAccurately()
    {
        await _repo.InitializeAsync();

        var settings = await _repo.GetSettingsAsync();
        Assert.NotNull(settings);
        Assert.Equal(25, settings.FocusMinutes);

        settings.FocusMinutes = 45;
        settings.AutoStartBreaks = true;
        settings.CommitmentMode = CommitmentModeLevel.Strict;
        await _repo.SaveSettingsAsync(settings);

        var updated = await _repo.GetSettingsAsync();
        Assert.Equal(45, updated.FocusMinutes);
        Assert.True(updated.AutoStartBreaks);
        Assert.Equal(CommitmentModeLevel.Strict, updated.CommitmentMode);
    }

    [Fact]
    public async Task Tags_AddAndRetrieve_WorksAccurately()
    {
        await _repo.InitializeAsync();

        var initialTags = await _repo.GetTagsAsync();
        var customTag = new Tag { Name = "Research & Design", ColorHex = "#3b82f6" };
        await _repo.SaveTagAsync(customTag);

        var updatedTags = await _repo.GetTagsAsync();
        Assert.True(updatedTags.Count > initialTags.Count);
        Assert.Contains(updatedTags, t => t.Name == "Research & Design");
    }

    [Fact]
    public async Task Database_LegacyBlockerTables_DoNotExistAfterInitialize()
    {
        await _repo.InitializeAsync();

        using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_tempDb}");
        await connection.OpenAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name IN ('BlockedApps', 'BlockedWebsites', 'BlockerLogs', 'BlockerProfiles')";
        using var reader = await cmd.ExecuteReaderAsync();
        Assert.False(reader.HasRows);
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

