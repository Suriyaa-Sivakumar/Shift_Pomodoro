using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using FlowWindows.Core.Enums;
using FlowWindows.Core.Interfaces;
using FlowWindows.Core.Models;
using Microsoft.Data.Sqlite;

namespace FlowWindows.Data;

public class FlowSqliteRepository : IRepository
{
    private readonly string _databasePath;
    private readonly string _connectionString;

    public FlowSqliteRepository(string? customDbPath = null)
    {
        if (!string.IsNullOrWhiteSpace(customDbPath))
        {
            _databasePath = customDbPath;
        }
        else
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var folder = Path.Combine(appData, "FlowWindows", "Database");
            Directory.CreateDirectory(folder);
            _databasePath = Path.Combine(folder, "flow.db");
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
    }

    private SqliteConnection CreateConnection() => new(_connectionString);

    public async Task InitializeAsync()
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        var sql = @"
            PRAGMA journal_mode = WAL;
            PRAGMA foreign_keys = ON;

            CREATE TABLE IF NOT EXISTS AppSettings (
                Key TEXT PRIMARY KEY,
                JsonValue TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Tags (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                ColorHex TEXT NOT NULL,
                Icon TEXT NOT NULL,
                CreatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS TimerPresets (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                FocusMinutes INTEGER NOT NULL,
                ShortBreakMinutes INTEGER NOT NULL,
                LongBreakMinutes INTEGER NOT NULL,
                SessionsBeforeLongBreak INTEGER NOT NULL,
                AutoStartBreaks INTEGER NOT NULL,
                AutoStartFocus INTEGER NOT NULL,
                TagId TEXT,
                IsDefault INTEGER NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Sessions (
                Id TEXT PRIMARY KEY,
                SessionType INTEGER NOT NULL,
                StartTime TEXT NOT NULL,
                EndTime TEXT NOT NULL,
                PlannedDurationSeconds INTEGER NOT NULL,
                ActualDurationSeconds INTEGER NOT NULL,
                Completed INTEGER NOT NULL,
                Interrupted INTEGER NOT NULL,
                TagId TEXT,
                TagName TEXT,
                TagColorHex TEXT,
                TaskId TEXT,
                TaskTitle TEXT,
                SubjectId TEXT,
                SubjectName TEXT,
                TopicId TEXT,
                Notes TEXT,
                CreatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS ActiveSessionState (
                Id TEXT PRIMARY KEY,
                SessionType INTEGER NOT NULL,
                StartTime TEXT NOT NULL,
                SegmentStartTime TEXT NOT NULL,
                AccumulatedElapsedSeconds REAL NOT NULL,
                TotalDurationSeconds REAL NOT NULL,
                State INTEGER NOT NULL,
                TagId TEXT,
                TagName TEXT,
                TagColorHex TEXT,
                TaskId TEXT,
                TaskTitle TEXT,
                SubjectId TEXT,
                SubjectName TEXT,
                TopicId TEXT,
                CycleSession INTEGER NOT NULL,
                LastHeartbeat TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS StudySubjects (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Description TEXT,
                ColorHex TEXT NOT NULL,
                TargetHours REAL NOT NULL,
                ExamDate TEXT,
                TagId TEXT,
                LoggedHours REAL NOT NULL,
                CreatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS StudyTopics (
                Id TEXT PRIMARY KEY,
                SubjectId TEXT NOT NULL,
                Title TEXT NOT NULL,
                Description TEXT,
                Status INTEGER NOT NULL,
                Priority INTEGER NOT NULL,
                EstimatedHours REAL NOT NULL,
                CompletedAt TEXT,
                CreatedAt TEXT NOT NULL,
                FOREIGN KEY (SubjectId) REFERENCES StudySubjects(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS Tasks (
                Id TEXT PRIMARY KEY,
                Title TEXT NOT NULL,
                Description TEXT,
                Priority INTEGER NOT NULL,
                DueDate TEXT,
                Status INTEGER NOT NULL,
                EstimatedMinutes INTEGER NOT NULL,
                TimeSpentMinutes INTEGER NOT NULL,
                TagId TEXT,
                TagName TEXT,
                SubjectId TEXT,
                TopicId TEXT,
                CompletedAt TEXT,
                CreatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Subtasks (
                Id TEXT PRIMARY KEY,
                TaskId TEXT NOT NULL,
                Title TEXT NOT NULL,
                IsCompleted INTEGER NOT NULL,
                CreatedAt TEXT NOT NULL,
                FOREIGN KEY (TaskId) REFERENCES Tasks(Id) ON DELETE CASCADE
            );

            DROP TABLE IF EXISTS BlockedApps;
            DROP TABLE IF EXISTS BlockedWebsites;
            DROP TABLE IF EXISTS BlockerLogs;
            DROP TABLE IF EXISTS BlockerProfiles;

            CREATE TABLE IF NOT EXISTS Goals (
                Id TEXT PRIMARY KEY,
                Title TEXT NOT NULL,
                Type INTEGER NOT NULL,
                TargetValue REAL NOT NULL,
                CurrentValue REAL NOT NULL,
                SubjectId TEXT,
                PeriodStart TEXT NOT NULL,
                PeriodEnd TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Achievements (
                Id TEXT PRIMARY KEY,
                Key TEXT UNIQUE NOT NULL,
                Title TEXT NOT NULL,
                Description TEXT NOT NULL,
                Icon TEXT NOT NULL,
                CurrentProgress REAL NOT NULL,
                TargetProgress REAL NOT NULL,
                IsUnlocked INTEGER NOT NULL,
                UnlockedAt TEXT
            );

            CREATE INDEX IF NOT EXISTS idx_sessions_time ON Sessions (StartTime, EndTime);
            CREATE INDEX IF NOT EXISTS idx_tasks_status ON Tasks (Status);
        ";

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();

        await SeedDefaultsAsync(conn);
    }

    private async Task SeedDefaultsAsync(SqliteConnection conn)
    {
        // Check if presets exist
        using var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = "SELECT COUNT(*) FROM TimerPresets;";
        var presetCount = Convert.ToInt64(await checkCmd.ExecuteScalarAsync());

        if (presetCount == 0)
        {
            var presets = new[]
            {
                new TimerPreset { Id = "preset-standard", Name = "Standard", FocusMinutes = 25, ShortBreakMinutes = 5, LongBreakMinutes = 15, SessionsBeforeLongBreak = 4, IsDefault = true },
                new TimerPreset { Id = "preset-deepwork", Name = "Deep Work", FocusMinutes = 50, ShortBreakMinutes = 10, LongBreakMinutes = 20, SessionsBeforeLongBreak = 4, IsDefault = false },
                new TimerPreset { Id = "preset-study", Name = "Study", FocusMinutes = 45, ShortBreakMinutes = 10, LongBreakMinutes = 20, SessionsBeforeLongBreak = 4, IsDefault = false },
                new TimerPreset { Id = "preset-quick", Name = "Quick Focus", FocusMinutes = 15, ShortBreakMinutes = 3, LongBreakMinutes = 10, SessionsBeforeLongBreak = 4, IsDefault = false }
            };

            foreach (var p in presets)
            {
                using var ins = conn.CreateCommand();
                ins.CommandText = @"INSERT INTO TimerPresets (Id, Name, FocusMinutes, ShortBreakMinutes, LongBreakMinutes, SessionsBeforeLongBreak, AutoStartBreaks, AutoStartFocus, TagId, IsDefault)
                                    VALUES (@Id, @Name, @FocusMinutes, @ShortBreakMinutes, @LongBreakMinutes, @SessionsBeforeLongBreak, @AutoStartBreaks, @AutoStartFocus, @TagId, @IsDefault);";
                ins.Parameters.AddWithValue("@Id", p.Id);
                ins.Parameters.AddWithValue("@Name", p.Name);
                ins.Parameters.AddWithValue("@FocusMinutes", p.FocusMinutes);
                ins.Parameters.AddWithValue("@ShortBreakMinutes", p.ShortBreakMinutes);
                ins.Parameters.AddWithValue("@LongBreakMinutes", p.LongBreakMinutes);
                ins.Parameters.AddWithValue("@SessionsBeforeLongBreak", p.SessionsBeforeLongBreak);
                ins.Parameters.AddWithValue("@AutoStartBreaks", p.AutoStartBreaks ? 1 : 0);
                ins.Parameters.AddWithValue("@AutoStartFocus", p.AutoStartFocus ? 1 : 0);
                ins.Parameters.AddWithValue("@TagId", (object?)p.TagId ?? DBNull.Value);
                ins.Parameters.AddWithValue("@IsDefault", p.IsDefault ? 1 : 0);
                await ins.ExecuteNonQueryAsync();
            }
        }

        // Check if tags exist
        using var checkTagCmd = conn.CreateCommand();
        checkTagCmd.CommandText = "SELECT COUNT(*) FROM Tags;";
        var tagCount = Convert.ToInt64(await checkTagCmd.ExecuteScalarAsync());

        if (tagCount == 0)
        {
            var defaultTags = new[]
            {
                new Tag { Id = "tag-deepwork", Name = "Deep Work", ColorHex = "#FF5252", Icon = "Zap" },
                new Tag { Id = "tag-study", Name = "Study", ColorHex = "#448AFF", Icon = "Book" },
                new Tag { Id = "tag-reading", Name = "Reading", ColorHex = "#00E676", Icon = "FileText" },
                new Tag { Id = "tag-coding", Name = "Coding", ColorHex = "#7C4DFF", Icon = "Code" },
                new Tag { Id = "tag-writing", Name = "Writing", ColorHex = "#FFD740", Icon = "Edit" }
            };

            foreach (var t in defaultTags)
            {
                using var insTag = conn.CreateCommand();
                insTag.CommandText = @"INSERT INTO Tags (Id, Name, ColorHex, Icon, CreatedAt)
                                      VALUES (@Id, @Name, @ColorHex, @Icon, @CreatedAt);";
                insTag.Parameters.AddWithValue("@Id", t.Id);
                insTag.Parameters.AddWithValue("@Name", t.Name);
                insTag.Parameters.AddWithValue("@ColorHex", t.ColorHex);
                insTag.Parameters.AddWithValue("@Icon", t.Icon);
                insTag.Parameters.AddWithValue("@CreatedAt", t.CreatedAt.ToString("O"));
                await insTag.ExecuteNonQueryAsync();
            }
        }


        // Check Achievements
        using var checkAchCmd = conn.CreateCommand();
        checkAchCmd.CommandText = "SELECT COUNT(*) FROM Achievements;";
        var achCount = Convert.ToInt64(await checkAchCmd.ExecuteScalarAsync());

        if (achCount == 0)
        {
            var defaultAchievements = new[]
            {
                new Achievement { Id = "ach-first-step", Key = "first_step", Title = "First Step", Description = "Complete your first focus session.", Icon = "Rocket", TargetProgress = 1 },
                new Achievement { Id = "ach-streak-3", Key = "streak_3", Title = "Momentum", Description = "Achieve a 3-day productivity streak.", Icon = "Flame", TargetProgress = 3 },
                new Achievement { Id = "ach-streak-7", Key = "streak_7", Title = "Habit Builder", Description = "Maintain a 7-day focus streak.", Icon = "Zap", TargetProgress = 7 },
                new Achievement { Id = "ach-centurion", Key = "centurion", Title = "Centurion", Description = "Log 100 total focus sessions.", Icon = "Award", TargetProgress = 100 },
                new Achievement { Id = "ach-marathon", Key = "marathon", Title = "Deep Flow Marathon", Description = "Complete 4 uninterrupted focus sessions in one day.", Icon = "Compass", TargetProgress = 4 }
            };

            foreach (var ach in defaultAchievements)
            {
                using var insAch = conn.CreateCommand();
                insAch.CommandText = @"INSERT INTO Achievements (Id, Key, Title, Description, Icon, CurrentProgress, TargetProgress, IsUnlocked, UnlockedAt)
                                      VALUES (@Id, @Key, @Title, @Description, @Icon, 0, @TargetProgress, 0, NULL);";
                insAch.Parameters.AddWithValue("@Id", ach.Id);
                insAch.Parameters.AddWithValue("@Key", ach.Key);
                insAch.Parameters.AddWithValue("@Title", ach.Title);
                insAch.Parameters.AddWithValue("@Description", ach.Description);
                insAch.Parameters.AddWithValue("@Icon", ach.Icon);
                insAch.Parameters.AddWithValue("@TargetProgress", ach.TargetProgress);
                await insAch.ExecuteNonQueryAsync();
            }
        }
    }

    #region AppSettings
    public async Task<AppSettings> GetSettingsAsync()
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT JsonValue FROM AppSettings WHERE Key = 'MainSettings';";
        var result = await cmd.ExecuteScalarAsync();

        if (result != null && result is string json)
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(json);
            if (settings != null) return settings;
        }

        var defaultSettings = new AppSettings();
        await SaveSettingsAsync(defaultSettings);
        return defaultSettings;
    }

    public async Task SaveSettingsAsync(AppSettings settings)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        var json = JsonSerializer.Serialize(settings);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"INSERT INTO AppSettings (Key, JsonValue, UpdatedAt)
                            VALUES ('MainSettings', @JsonValue, @UpdatedAt)
                            ON CONFLICT(Key) DO UPDATE SET JsonValue = @JsonValue, UpdatedAt = @UpdatedAt;";
        cmd.Parameters.AddWithValue("@JsonValue", json);
        cmd.Parameters.AddWithValue("@UpdatedAt", DateTimeOffset.UtcNow.ToString("O"));
        await cmd.ExecuteNonQueryAsync();
    }
    #endregion

    #region Sessions
    public async Task AddSessionAsync(SessionRecord session)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"INSERT INTO Sessions (
            Id, SessionType, StartTime, EndTime, PlannedDurationSeconds, ActualDurationSeconds,
            Completed, Interrupted, TagId, TagName, TagColorHex, TaskId, TaskTitle,
            SubjectId, SubjectName, TopicId, Notes, CreatedAt
        ) VALUES (
            @Id, @SessionType, @StartTime, @EndTime, @PlannedDurationSeconds, @ActualDurationSeconds,
            @Completed, @Interrupted, @TagId, @TagName, @TagColorHex, @TaskId, @TaskTitle,
            @SubjectId, @SubjectName, @TopicId, @Notes, @CreatedAt
        );";

        cmd.Parameters.AddWithValue("@Id", session.Id);
        cmd.Parameters.AddWithValue("@SessionType", (int)session.SessionType);
        cmd.Parameters.AddWithValue("@StartTime", session.StartTime.ToString("O"));
        cmd.Parameters.AddWithValue("@EndTime", session.EndTime.ToString("O"));
        cmd.Parameters.AddWithValue("@PlannedDurationSeconds", session.PlannedDurationSeconds);
        cmd.Parameters.AddWithValue("@ActualDurationSeconds", session.ActualDurationSeconds);
        cmd.Parameters.AddWithValue("@Completed", session.Completed ? 1 : 0);
        cmd.Parameters.AddWithValue("@Interrupted", session.Interrupted ? 1 : 0);
        cmd.Parameters.AddWithValue("@TagId", (object?)session.TagId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@TagName", (object?)session.TagName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@TagColorHex", (object?)session.TagColorHex ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@TaskId", (object?)session.TaskId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@TaskTitle", (object?)session.TaskTitle ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@SubjectId", (object?)session.SubjectId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@SubjectName", (object?)session.SubjectName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@TopicId", (object?)session.TopicId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Notes", (object?)session.Notes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CreatedAt", session.CreatedAt.ToString("O"));

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<SessionRecord>> GetSessionsAsync(DateTimeOffset from, DateTimeOffset to)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT * FROM Sessions 
                            WHERE StartTime >= @From AND StartTime <= @To 
                            ORDER BY StartTime DESC;";
        cmd.Parameters.AddWithValue("@From", from.ToString("O"));
        cmd.Parameters.AddWithValue("@To", to.ToString("O"));

        var list = new List<SessionRecord>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(ReadSession(reader));
        }
        return list;
    }

    public async Task<List<SessionRecord>> GetAllSessionsAsync()
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Sessions ORDER BY StartTime DESC;";

        var list = new List<SessionRecord>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(ReadSession(reader));
        }
        return list;
    }

    public async Task DeleteSessionAsync(string id)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Sessions WHERE Id = @Id;";
        cmd.Parameters.AddWithValue("@Id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task ClearAllSessionsAsync()
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Sessions;";
        await cmd.ExecuteNonQueryAsync();
    }

    private static SessionRecord ReadSession(SqliteDataReader r)
    {
        return new SessionRecord
        {
            Id = r.GetString(r.GetOrdinal("Id")),
            SessionType = (SessionType)r.GetInt32(r.GetOrdinal("SessionType")),
            StartTime = DateTimeOffset.Parse(r.GetString(r.GetOrdinal("StartTime"))),
            EndTime = DateTimeOffset.Parse(r.GetString(r.GetOrdinal("EndTime"))),
            PlannedDurationSeconds = r.GetInt32(r.GetOrdinal("PlannedDurationSeconds")),
            ActualDurationSeconds = r.GetInt32(r.GetOrdinal("ActualDurationSeconds")),
            Completed = r.GetInt32(r.GetOrdinal("Completed")) == 1,
            Interrupted = r.GetInt32(r.GetOrdinal("Interrupted")) == 1,
            TagId = r.IsDBNull(r.GetOrdinal("TagId")) ? null : r.GetString(r.GetOrdinal("TagId")),
            TagName = r.IsDBNull(r.GetOrdinal("TagName")) ? null : r.GetString(r.GetOrdinal("TagName")),
            TagColorHex = r.IsDBNull(r.GetOrdinal("TagColorHex")) ? null : r.GetString(r.GetOrdinal("TagColorHex")),
            TaskId = r.IsDBNull(r.GetOrdinal("TaskId")) ? null : r.GetString(r.GetOrdinal("TaskId")),
            TaskTitle = r.IsDBNull(r.GetOrdinal("TaskTitle")) ? null : r.GetString(r.GetOrdinal("TaskTitle")),
            SubjectId = r.IsDBNull(r.GetOrdinal("SubjectId")) ? null : r.GetString(r.GetOrdinal("SubjectId")),
            SubjectName = r.IsDBNull(r.GetOrdinal("SubjectName")) ? null : r.GetString(r.GetOrdinal("SubjectName")),
            TopicId = r.IsDBNull(r.GetOrdinal("TopicId")) ? null : r.GetString(r.GetOrdinal("TopicId")),
            Notes = r.IsDBNull(r.GetOrdinal("Notes")) ? null : r.GetString(r.GetOrdinal("Notes")),
            CreatedAt = DateTimeOffset.Parse(r.GetString(r.GetOrdinal("CreatedAt")))
        };
    }

    public async Task SaveActiveSessionStateAsync(ActiveSessionState state)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"INSERT INTO ActiveSessionState (
            Id, SessionType, StartTime, SegmentStartTime, AccumulatedElapsedSeconds,
            TotalDurationSeconds, State, TagId, TagName, TagColorHex,
            TaskId, TaskTitle, SubjectId, SubjectName, TopicId,
            CycleSession, LastHeartbeat
        ) VALUES (
            @Id, @SessionType, @StartTime, @SegmentStartTime, @AccumulatedElapsedSeconds,
            @TotalDurationSeconds, @State, @TagId, @TagName, @TagColorHex,
            @TaskId, @TaskTitle, @SubjectId, @SubjectName, @TopicId,
            @CycleSession, @LastHeartbeat
        ) ON CONFLICT(Id) DO UPDATE SET
            SegmentStartTime = @SegmentStartTime,
            AccumulatedElapsedSeconds = @AccumulatedElapsedSeconds,
            TotalDurationSeconds = @TotalDurationSeconds,
            State = @State,
            TagId = @TagId, TagName = @TagName, TagColorHex = @TagColorHex,
            TaskId = @TaskId, TaskTitle = @TaskTitle,
            SubjectId = @SubjectId, SubjectName = @SubjectName, TopicId = @TopicId,
            CycleSession = @CycleSession,
            LastHeartbeat = @LastHeartbeat;";

        cmd.Parameters.AddWithValue("@Id", state.Id);
        cmd.Parameters.AddWithValue("@SessionType", (int)state.SessionType);
        cmd.Parameters.AddWithValue("@StartTime", state.StartTime.ToString("O"));
        cmd.Parameters.AddWithValue("@SegmentStartTime", state.SegmentStartTime.ToString("O"));
        cmd.Parameters.AddWithValue("@AccumulatedElapsedSeconds", state.AccumulatedElapsedSeconds);
        cmd.Parameters.AddWithValue("@TotalDurationSeconds", state.TotalDurationSeconds);
        cmd.Parameters.AddWithValue("@State", (int)state.State);
        cmd.Parameters.AddWithValue("@TagId", (object?)state.TagId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@TagName", (object?)state.TagName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@TagColorHex", (object?)state.TagColorHex ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@TaskId", (object?)state.TaskId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@TaskTitle", (object?)state.TaskTitle ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@SubjectId", (object?)state.SubjectId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@SubjectName", (object?)state.SubjectName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@TopicId", (object?)state.TopicId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CycleSession", state.CycleSession);
        cmd.Parameters.AddWithValue("@LastHeartbeat", state.LastHeartbeat.ToString("O"));

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<ActiveSessionState?> GetActiveSessionStateAsync()
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM ActiveSessionState LIMIT 1;";
        using var r = await cmd.ExecuteReaderAsync();
        if (await r.ReadAsync())
        {
            return new ActiveSessionState
            {
                Id = r.GetString(r.GetOrdinal("Id")),
                SessionType = (SessionType)r.GetInt32(r.GetOrdinal("SessionType")),
                StartTime = DateTimeOffset.Parse(r.GetString(r.GetOrdinal("StartTime"))),
                SegmentStartTime = DateTimeOffset.Parse(r.GetString(r.GetOrdinal("SegmentStartTime"))),
                AccumulatedElapsedSeconds = r.GetDouble(r.GetOrdinal("AccumulatedElapsedSeconds")),
                TotalDurationSeconds = r.GetDouble(r.GetOrdinal("TotalDurationSeconds")),
                State = (SessionState)r.GetInt32(r.GetOrdinal("State")),
                TagId = r.IsDBNull(r.GetOrdinal("TagId")) ? null : r.GetString(r.GetOrdinal("TagId")),
                TagName = r.IsDBNull(r.GetOrdinal("TagName")) ? null : r.GetString(r.GetOrdinal("TagName")),
                TagColorHex = r.IsDBNull(r.GetOrdinal("TagColorHex")) ? null : r.GetString(r.GetOrdinal("TagColorHex")),
                TaskId = r.IsDBNull(r.GetOrdinal("TaskId")) ? null : r.GetString(r.GetOrdinal("TaskId")),
                TaskTitle = r.IsDBNull(r.GetOrdinal("TaskTitle")) ? null : r.GetString(r.GetOrdinal("TaskTitle")),
                SubjectId = r.IsDBNull(r.GetOrdinal("SubjectId")) ? null : r.GetString(r.GetOrdinal("SubjectId")),
                SubjectName = r.IsDBNull(r.GetOrdinal("SubjectName")) ? null : r.GetString(r.GetOrdinal("SubjectName")),
                TopicId = r.IsDBNull(r.GetOrdinal("TopicId")) ? null : r.GetString(r.GetOrdinal("TopicId")),
                CycleSession = r.GetInt32(r.GetOrdinal("CycleSession")),
                LastHeartbeat = DateTimeOffset.Parse(r.GetString(r.GetOrdinal("LastHeartbeat")))
            };
        }
        return null;
    }

    public async Task ClearActiveSessionStateAsync()
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM ActiveSessionState;";
        await cmd.ExecuteNonQueryAsync();
    }
    #endregion

    #region Tags
    public async Task<List<Tag>> GetTagsAsync()
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Tags ORDER BY Name ASC;";
        var list = new List<Tag>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new Tag
            {
                Id = reader.GetString(reader.GetOrdinal("Id")),
                Name = reader.GetString(reader.GetOrdinal("Name")),
                ColorHex = reader.GetString(reader.GetOrdinal("ColorHex")),
                Icon = reader.GetString(reader.GetOrdinal("Icon")),
                CreatedAt = DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("CreatedAt")))
            });
        }
        return list;
    }

    public async Task<Tag?> GetTagByIdAsync(string id)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Tags WHERE Id = @Id;";
        cmd.Parameters.AddWithValue("@Id", id);
        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new Tag
            {
                Id = reader.GetString(reader.GetOrdinal("Id")),
                Name = reader.GetString(reader.GetOrdinal("Name")),
                ColorHex = reader.GetString(reader.GetOrdinal("ColorHex")),
                Icon = reader.GetString(reader.GetOrdinal("Icon")),
                CreatedAt = DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("CreatedAt")))
            };
        }
        return null;
    }

    public async Task SaveTagAsync(Tag tag)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"INSERT INTO Tags (Id, Name, ColorHex, Icon, CreatedAt)
                            VALUES (@Id, @Name, @ColorHex, @Icon, @CreatedAt)
                            ON CONFLICT(Id) DO UPDATE SET Name = @Name, ColorHex = @ColorHex, Icon = @Icon;";
        cmd.Parameters.AddWithValue("@Id", tag.Id);
        cmd.Parameters.AddWithValue("@Name", tag.Name);
        cmd.Parameters.AddWithValue("@ColorHex", tag.ColorHex);
        cmd.Parameters.AddWithValue("@Icon", tag.Icon);
        cmd.Parameters.AddWithValue("@CreatedAt", tag.CreatedAt.ToString("O"));
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteTagAsync(string id)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Tags WHERE Id = @Id;";
        cmd.Parameters.AddWithValue("@Id", id);
        await cmd.ExecuteNonQueryAsync();
    }
    #endregion

    #region Presets
    public async Task<List<TimerPreset>> GetPresetsAsync()
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM TimerPresets ORDER BY IsDefault DESC, Name ASC;";
        var list = new List<TimerPreset>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new TimerPreset
            {
                Id = reader.GetString(reader.GetOrdinal("Id")),
                Name = reader.GetString(reader.GetOrdinal("Name")),
                FocusMinutes = reader.GetInt32(reader.GetOrdinal("FocusMinutes")),
                ShortBreakMinutes = reader.GetInt32(reader.GetOrdinal("ShortBreakMinutes")),
                LongBreakMinutes = reader.GetInt32(reader.GetOrdinal("LongBreakMinutes")),
                SessionsBeforeLongBreak = reader.GetInt32(reader.GetOrdinal("SessionsBeforeLongBreak")),
                AutoStartBreaks = reader.GetInt32(reader.GetOrdinal("AutoStartBreaks")) == 1,
                AutoStartFocus = reader.GetInt32(reader.GetOrdinal("AutoStartFocus")) == 1,
                TagId = reader.IsDBNull(reader.GetOrdinal("TagId")) ? null : reader.GetString(reader.GetOrdinal("TagId")),
                IsDefault = reader.GetInt32(reader.GetOrdinal("IsDefault")) == 1
            });
        }
        return list;
    }

    public async Task SavePresetAsync(TimerPreset preset)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"INSERT INTO TimerPresets (
            Id, Name, FocusMinutes, ShortBreakMinutes, LongBreakMinutes,
            SessionsBeforeLongBreak, AutoStartBreaks, AutoStartFocus, TagId, IsDefault
        ) VALUES (
            @Id, @Name, @FocusMinutes, @ShortBreakMinutes, @LongBreakMinutes,
            @SessionsBeforeLongBreak, @AutoStartBreaks, @AutoStartFocus, @TagId, @IsDefault
        ) ON CONFLICT(Id) DO UPDATE SET
            Name = @Name, FocusMinutes = @FocusMinutes, ShortBreakMinutes = @ShortBreakMinutes,
            LongBreakMinutes = @LongBreakMinutes, SessionsBeforeLongBreak = @SessionsBeforeLongBreak,
            AutoStartBreaks = @AutoStartBreaks, AutoStartFocus = @AutoStartFocus, TagId = @TagId, IsDefault = @IsDefault;";

        cmd.Parameters.AddWithValue("@Id", preset.Id);
        cmd.Parameters.AddWithValue("@Name", preset.Name);
        cmd.Parameters.AddWithValue("@FocusMinutes", preset.FocusMinutes);
        cmd.Parameters.AddWithValue("@ShortBreakMinutes", preset.ShortBreakMinutes);
        cmd.Parameters.AddWithValue("@LongBreakMinutes", preset.LongBreakMinutes);
        cmd.Parameters.AddWithValue("@SessionsBeforeLongBreak", preset.SessionsBeforeLongBreak);
        cmd.Parameters.AddWithValue("@AutoStartBreaks", preset.AutoStartBreaks ? 1 : 0);
        cmd.Parameters.AddWithValue("@AutoStartFocus", preset.AutoStartFocus ? 1 : 0);
        cmd.Parameters.AddWithValue("@TagId", (object?)preset.TagId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@IsDefault", preset.IsDefault ? 1 : 0);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeletePresetAsync(string id)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM TimerPresets WHERE Id = @Id;";
        cmd.Parameters.AddWithValue("@Id", id);
        await cmd.ExecuteNonQueryAsync();
    }
    #endregion

    #region Study Planner
    public async Task<List<StudySubject>> GetSubjectsAsync()
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM StudySubjects ORDER BY Name ASC;";
        var subjects = new List<StudySubject>();
        using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                subjects.Add(new StudySubject
                {
                    Id = reader.GetString(reader.GetOrdinal("Id")),
                    Name = reader.GetString(reader.GetOrdinal("Name")),
                    Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? string.Empty : reader.GetString(reader.GetOrdinal("Description")),
                    ColorHex = reader.GetString(reader.GetOrdinal("ColorHex")),
                    TargetHours = reader.GetDouble(reader.GetOrdinal("TargetHours")),
                    ExamDate = reader.IsDBNull(reader.GetOrdinal("ExamDate")) ? null : DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("ExamDate"))),
                    TagId = reader.IsDBNull(reader.GetOrdinal("TagId")) ? null : reader.GetString(reader.GetOrdinal("TagId")),
                    LoggedHours = reader.GetDouble(reader.GetOrdinal("LoggedHours")),
                    CreatedAt = DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("CreatedAt")))
                });
            }
        }

        foreach (var s in subjects)
        {
            using var topicCmd = conn.CreateCommand();
            topicCmd.CommandText = "SELECT * FROM StudyTopics WHERE SubjectId = @SubjectId ORDER BY Priority DESC, Title ASC;";
            topicCmd.Parameters.AddWithValue("@SubjectId", s.Id);
            using var tr = await topicCmd.ExecuteReaderAsync();
            while (await tr.ReadAsync())
            {
                s.Topics.Add(new StudyTopic
                {
                    Id = tr.GetString(tr.GetOrdinal("Id")),
                    SubjectId = tr.GetString(tr.GetOrdinal("SubjectId")),
                    Title = tr.GetString(tr.GetOrdinal("Title")),
                    Description = tr.IsDBNull(tr.GetOrdinal("Description")) ? string.Empty : tr.GetString(tr.GetOrdinal("Description")),
                    Status = (ItemStatus)tr.GetInt32(tr.GetOrdinal("Status")),
                    Priority = (PriorityLevel)tr.GetInt32(tr.GetOrdinal("Priority")),
                    EstimatedHours = tr.GetDouble(tr.GetOrdinal("EstimatedHours")),
                    CompletedAt = tr.IsDBNull(tr.GetOrdinal("CompletedAt")) ? null : DateTimeOffset.Parse(tr.GetString(tr.GetOrdinal("CompletedAt"))),
                    CreatedAt = DateTimeOffset.Parse(tr.GetString(tr.GetOrdinal("CreatedAt")))
                });
            }
        }

        return subjects;
    }

    public async Task<StudySubject?> GetSubjectByIdAsync(string id)
    {
        var all = await GetSubjectsAsync();
        return all.Find(s => s.Id == id);
    }

    public async Task SaveSubjectAsync(StudySubject subject)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"INSERT INTO StudySubjects (
            Id, Name, Description, ColorHex, TargetHours, ExamDate, TagId, LoggedHours, CreatedAt
        ) VALUES (
            @Id, @Name, @Description, @ColorHex, @TargetHours, @ExamDate, @TagId, @LoggedHours, @CreatedAt
        ) ON CONFLICT(Id) DO UPDATE SET
            Name = @Name, Description = @Description, ColorHex = @ColorHex,
            TargetHours = @TargetHours, ExamDate = @ExamDate, TagId = @TagId, LoggedHours = @LoggedHours;";

        cmd.Parameters.AddWithValue("@Id", subject.Id);
        cmd.Parameters.AddWithValue("@Name", subject.Name);
        cmd.Parameters.AddWithValue("@Description", subject.Description);
        cmd.Parameters.AddWithValue("@ColorHex", subject.ColorHex);
        cmd.Parameters.AddWithValue("@TargetHours", subject.TargetHours);
        cmd.Parameters.AddWithValue("@ExamDate", subject.ExamDate.HasValue ? subject.ExamDate.Value.ToString("O") : DBNull.Value);
        cmd.Parameters.AddWithValue("@TagId", (object?)subject.TagId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@LoggedHours", subject.LoggedHours);
        cmd.Parameters.AddWithValue("@CreatedAt", subject.CreatedAt.ToString("O"));

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteSubjectAsync(string id)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM StudySubjects WHERE Id = @Id;";
        cmd.Parameters.AddWithValue("@Id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task SaveTopicAsync(StudyTopic topic)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"INSERT INTO StudyTopics (
            Id, SubjectId, Title, Description, Status, Priority, EstimatedHours, CompletedAt, CreatedAt
        ) VALUES (
            @Id, @SubjectId, @Title, @Description, @Status, @Priority, @EstimatedHours, @CompletedAt, @CreatedAt
        ) ON CONFLICT(Id) DO UPDATE SET
            Title = @Title, Description = @Description, Status = @Status,
            Priority = @Priority, EstimatedHours = @EstimatedHours, CompletedAt = @CompletedAt;";

        cmd.Parameters.AddWithValue("@Id", topic.Id);
        cmd.Parameters.AddWithValue("@SubjectId", topic.SubjectId);
        cmd.Parameters.AddWithValue("@Title", topic.Title);
        cmd.Parameters.AddWithValue("@Description", topic.Description);
        cmd.Parameters.AddWithValue("@Status", (int)topic.Status);
        cmd.Parameters.AddWithValue("@Priority", (int)topic.Priority);
        cmd.Parameters.AddWithValue("@EstimatedHours", topic.EstimatedHours);
        cmd.Parameters.AddWithValue("@CompletedAt", topic.CompletedAt.HasValue ? topic.CompletedAt.Value.ToString("O") : DBNull.Value);
        cmd.Parameters.AddWithValue("@CreatedAt", topic.CreatedAt.ToString("O"));

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteTopicAsync(string topicId)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM StudyTopics WHERE Id = @Id;";
        cmd.Parameters.AddWithValue("@Id", topicId);
        await cmd.ExecuteNonQueryAsync();
    }
    #endregion

    #region Tasks
    public async Task<List<TaskItem>> GetTasksAsync()
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Tasks ORDER BY Status ASC, Priority DESC, CreatedAt DESC;";
        var tasks = new List<TaskItem>();
        using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                tasks.Add(new TaskItem
                {
                    Id = reader.GetString(reader.GetOrdinal("Id")),
                    Title = reader.GetString(reader.GetOrdinal("Title")),
                    Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? string.Empty : reader.GetString(reader.GetOrdinal("Description")),
                    Priority = (PriorityLevel)reader.GetInt32(reader.GetOrdinal("Priority")),
                    DueDate = reader.IsDBNull(reader.GetOrdinal("DueDate")) ? null : DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("DueDate"))),
                    Status = (ItemStatus)reader.GetInt32(reader.GetOrdinal("Status")),
                    EstimatedMinutes = reader.GetInt32(reader.GetOrdinal("EstimatedMinutes")),
                    TimeSpentMinutes = reader.GetInt32(reader.GetOrdinal("TimeSpentMinutes")),
                    TagId = reader.IsDBNull(reader.GetOrdinal("TagId")) ? null : reader.GetString(reader.GetOrdinal("TagId")),
                    TagName = reader.IsDBNull(reader.GetOrdinal("TagName")) ? null : reader.GetString(reader.GetOrdinal("TagName")),
                    SubjectId = reader.IsDBNull(reader.GetOrdinal("SubjectId")) ? null : reader.GetString(reader.GetOrdinal("SubjectId")),
                    TopicId = reader.IsDBNull(reader.GetOrdinal("TopicId")) ? null : reader.GetString(reader.GetOrdinal("TopicId")),
                    CompletedAt = reader.IsDBNull(reader.GetOrdinal("CompletedAt")) ? null : DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("CompletedAt"))),
                    CreatedAt = DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("CreatedAt")))
                });
            }
        }

        foreach (var t in tasks)
        {
            using var subCmd = conn.CreateCommand();
            subCmd.CommandText = "SELECT * FROM Subtasks WHERE TaskId = @TaskId ORDER BY CreatedAt ASC;";
            subCmd.Parameters.AddWithValue("@TaskId", t.Id);
            using var sr = await subCmd.ExecuteReaderAsync();
            while (await sr.ReadAsync())
            {
                t.Subtasks.Add(new SubtaskItem
                {
                    Id = sr.GetString(sr.GetOrdinal("Id")),
                    TaskId = sr.GetString(sr.GetOrdinal("TaskId")),
                    Title = sr.GetString(sr.GetOrdinal("Title")),
                    IsCompleted = sr.GetInt32(sr.GetOrdinal("IsCompleted")) == 1,
                    CreatedAt = DateTimeOffset.Parse(sr.GetString(sr.GetOrdinal("CreatedAt")))
                });
            }
        }

        return tasks;
    }

    public async Task<TaskItem?> GetTaskByIdAsync(string id)
    {
        var all = await GetTasksAsync();
        return all.Find(t => t.Id == id);
    }

    public async Task SaveTaskAsync(TaskItem task)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"INSERT INTO Tasks (
            Id, Title, Description, Priority, DueDate, Status, EstimatedMinutes, TimeSpentMinutes,
            TagId, TagName, SubjectId, TopicId, CompletedAt, CreatedAt
        ) VALUES (
            @Id, @Title, @Description, @Priority, @DueDate, @Status, @EstimatedMinutes, @TimeSpentMinutes,
            @TagId, @TagName, @SubjectId, @TopicId, @CompletedAt, @CreatedAt
        ) ON CONFLICT(Id) DO UPDATE SET
            Title = @Title, Description = @Description, Priority = @Priority, DueDate = @DueDate,
            Status = @Status, EstimatedMinutes = @EstimatedMinutes, TimeSpentMinutes = @TimeSpentMinutes,
            TagId = @TagId, TagName = @TagName, SubjectId = @SubjectId, TopicId = @TopicId, CompletedAt = @CompletedAt;";

        cmd.Parameters.AddWithValue("@Id", task.Id);
        cmd.Parameters.AddWithValue("@Title", task.Title);
        cmd.Parameters.AddWithValue("@Description", task.Description);
        cmd.Parameters.AddWithValue("@Priority", (int)task.Priority);
        cmd.Parameters.AddWithValue("@DueDate", task.DueDate.HasValue ? task.DueDate.Value.ToString("O") : DBNull.Value);
        cmd.Parameters.AddWithValue("@Status", (int)task.Status);
        cmd.Parameters.AddWithValue("@EstimatedMinutes", task.EstimatedMinutes);
        cmd.Parameters.AddWithValue("@TimeSpentMinutes", task.TimeSpentMinutes);
        cmd.Parameters.AddWithValue("@TagId", (object?)task.TagId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@TagName", (object?)task.TagName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@SubjectId", (object?)task.SubjectId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@TopicId", (object?)task.TopicId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CompletedAt", task.CompletedAt.HasValue ? task.CompletedAt.Value.ToString("O") : DBNull.Value);
        cmd.Parameters.AddWithValue("@CreatedAt", task.CreatedAt.ToString("O"));

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteTaskAsync(string id)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Tasks WHERE Id = @Id;";
        cmd.Parameters.AddWithValue("@Id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task SaveSubtaskAsync(SubtaskItem subtask)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"INSERT INTO Subtasks (Id, TaskId, Title, IsCompleted, CreatedAt)
                            VALUES (@Id, @TaskId, @Title, @IsCompleted, @CreatedAt)
                            ON CONFLICT(Id) DO UPDATE SET Title = @Title, IsCompleted = @IsCompleted;";
        cmd.Parameters.AddWithValue("@Id", subtask.Id);
        cmd.Parameters.AddWithValue("@TaskId", subtask.TaskId);
        cmd.Parameters.AddWithValue("@Title", subtask.Title);
        cmd.Parameters.AddWithValue("@IsCompleted", subtask.IsCompleted ? 1 : 0);
        cmd.Parameters.AddWithValue("@CreatedAt", subtask.CreatedAt.ToString("O"));
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteSubtaskAsync(string subtaskId)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Subtasks WHERE Id = @Id;";
        cmd.Parameters.AddWithValue("@Id", subtaskId);
        await cmd.ExecuteNonQueryAsync();
    }
    #endregion


    #region Goals & Achievements
    public async Task<List<GoalRecord>> GetGoalsAsync()
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Goals ORDER BY PeriodStart DESC;";
        var list = new List<GoalRecord>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new GoalRecord
            {
                Id = reader.GetString(reader.GetOrdinal("Id")),
                Title = reader.GetString(reader.GetOrdinal("Title")),
                Type = (GoalType)reader.GetInt32(reader.GetOrdinal("Type")),
                TargetValue = reader.GetDouble(reader.GetOrdinal("TargetValue")),
                CurrentValue = reader.GetDouble(reader.GetOrdinal("CurrentValue")),
                SubjectId = reader.IsDBNull(reader.GetOrdinal("SubjectId")) ? null : reader.GetString(reader.GetOrdinal("SubjectId")),
                PeriodStart = DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("PeriodStart"))),
                PeriodEnd = DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("PeriodEnd")))
            });
        }
        return list;
    }

    public async Task SaveGoalAsync(GoalRecord goal)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"INSERT INTO Goals (Id, Title, Type, TargetValue, CurrentValue, SubjectId, PeriodStart, PeriodEnd)
                            VALUES (@Id, @Title, @Type, @TargetValue, @CurrentValue, @SubjectId, @PeriodStart, @PeriodEnd)
                            ON CONFLICT(Id) DO UPDATE SET Title = @Title, TargetValue = @TargetValue,
                            CurrentValue = @CurrentValue, SubjectId = @SubjectId, PeriodEnd = @PeriodEnd;";
        cmd.Parameters.AddWithValue("@Id", goal.Id);
        cmd.Parameters.AddWithValue("@Title", goal.Title);
        cmd.Parameters.AddWithValue("@Type", (int)goal.Type);
        cmd.Parameters.AddWithValue("@TargetValue", goal.TargetValue);
        cmd.Parameters.AddWithValue("@CurrentValue", goal.CurrentValue);
        cmd.Parameters.AddWithValue("@SubjectId", (object?)goal.SubjectId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@PeriodStart", goal.PeriodStart.ToString("O"));
        cmd.Parameters.AddWithValue("@PeriodEnd", goal.PeriodEnd.ToString("O"));
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteGoalAsync(string id)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Goals WHERE Id = @Id;";
        cmd.Parameters.AddWithValue("@Id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<Achievement>> GetAchievementsAsync()
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Achievements ORDER BY IsUnlocked DESC, Key ASC;";
        var list = new List<Achievement>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new Achievement
            {
                Id = reader.GetString(reader.GetOrdinal("Id")),
                Key = reader.GetString(reader.GetOrdinal("Key")),
                Title = reader.GetString(reader.GetOrdinal("Title")),
                Description = reader.GetString(reader.GetOrdinal("Description")),
                Icon = reader.GetString(reader.GetOrdinal("Icon")),
                CurrentProgress = reader.GetDouble(reader.GetOrdinal("CurrentProgress")),
                TargetProgress = reader.GetDouble(reader.GetOrdinal("TargetProgress")),
                IsUnlocked = reader.GetInt32(reader.GetOrdinal("IsUnlocked")) == 1,
                UnlockedAt = reader.IsDBNull(reader.GetOrdinal("UnlockedAt")) ? null : DateTimeOffset.Parse(reader.GetString(reader.GetOrdinal("UnlockedAt")))
            });
        }
        return list;
    }

    public async Task SaveAchievementAsync(Achievement ach)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"INSERT INTO Achievements (Id, Key, Title, Description, Icon, CurrentProgress, TargetProgress, IsUnlocked, UnlockedAt)
                            VALUES (@Id, @Key, @Title, @Description, @Icon, @CurrentProgress, @TargetProgress, @IsUnlocked, @UnlockedAt)
                            ON CONFLICT(Key) DO UPDATE SET CurrentProgress = @CurrentProgress,
                            IsUnlocked = @IsUnlocked, UnlockedAt = @UnlockedAt;";
        cmd.Parameters.AddWithValue("@Id", ach.Id);
        cmd.Parameters.AddWithValue("@Key", ach.Key);
        cmd.Parameters.AddWithValue("@Title", ach.Title);
        cmd.Parameters.AddWithValue("@Description", ach.Description);
        cmd.Parameters.AddWithValue("@Icon", ach.Icon);
        cmd.Parameters.AddWithValue("@CurrentProgress", ach.CurrentProgress);
        cmd.Parameters.AddWithValue("@TargetProgress", ach.TargetProgress);
        cmd.Parameters.AddWithValue("@IsUnlocked", ach.IsUnlocked ? 1 : 0);
        cmd.Parameters.AddWithValue("@UnlockedAt", ach.UnlockedAt.HasValue ? ach.UnlockedAt.Value.ToString("O") : DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }
    #endregion

    #region Backup & Restore
    public async Task<string> ExportDatabaseBackupAsync(string targetPath)
    {
        if (File.Exists(_databasePath))
        {
            // Execute checkpoint to ensure WAL is flushed
            using (var conn = CreateConnection())
            {
                await conn.OpenAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                await cmd.ExecuteNonQueryAsync();
            }

            var dir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            File.Copy(_databasePath, targetPath, overwrite: true);
            return targetPath;
        }
        throw new FileNotFoundException("Database file not found.", _databasePath);
    }

    public async Task<bool> RestoreDatabaseBackupAsync(string backupPath)
    {
        if (!File.Exists(backupPath)) return false;

        SqliteConnection.ClearAllPools();
        File.Copy(backupPath, _databasePath, overwrite: true);
        await InitializeAsync();
        return true;
    }
    #endregion
}

