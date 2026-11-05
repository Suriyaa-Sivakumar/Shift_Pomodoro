# Database Architecture & Schema Specification

Flow for Windows utilizes an embedded, zero-configuration **SQLite 3** database accessed via `Microsoft.Data.Sqlite`.

- **Database File Location**: `%LocalAppData%\FlowWindows\flow.db`
- **Concurrency & Journaling**: Write-Ahead Logging (`WAL`) mode enabled.
- **Data Integrity**: Foreign key constraints enabled.

---

## 1. Engine PRAGMA Settings

Upon opening connections, `FlowSqliteRepository` applies the following performance and durability pragmas:

```sql
PRAGMA journal_mode = WAL;
PRAGMA synchronous = NORMAL;
PRAGMA foreign_keys = ON;
PRAGMA temp_store = MEMORY;
```

---

## 2. Table Definitions

### 2.1 Table: `sessions`
Records all completed and interrupted focus and break sessions.

| Column | Type | Constraints | Description |
|---|---|---|---|
| `id` | TEXT | PRIMARY KEY | Unique GUID identifier |
| `session_type` | INTEGER | NOT NULL | 0 = Focus, 1 = ShortBreak, 2 = LongBreak |
| `duration_seconds` | INTEGER | NOT NULL | Actual elapsed duration in seconds |
| `planned_duration_seconds`| INTEGER | NOT NULL | Target duration configured when session began |
| `started_at` | TEXT | NOT NULL | ISO-8601 UTC timestamp of start |
| `completed_at` | TEXT | NOT NULL | ISO-8601 UTC timestamp of finish/interruption |
| `was_completed` | INTEGER | NOT NULL | 1 if completed to full duration, 0 if interrupted |
| `tag` | TEXT | | Optional category tag (e.g., "Deep Work", "Study") |
| `task_id` | TEXT | | Optional reference to linked `tasks.id` |
| `notes` | TEXT | | User reflection or session notes |

**Indexes**:
- `idx_sessions_started_at`: ON `sessions(started_at)` (for time-series reporting and streak queries)
- `idx_sessions_tag`: ON `sessions(tag)` (for category breakdown)

---

### 2.2 Table: `presets`
Contains custom and default Pomodoro timing configurations.

| Column | Type | Constraints | Description |
|---|---|---|---|
| `id` | TEXT | PRIMARY KEY | Unique GUID identifier |
| `name` | TEXT | NOT NULL | Preset title (e.g. "Deep Work", "Standard") |
| `focus_minutes` | INTEGER | NOT NULL | Focus duration in minutes |
| `short_break_minutes` | INTEGER | NOT NULL | Short break duration in minutes |
| `long_break_minutes` | INTEGER | NOT NULL | Long break duration in minutes |
| `sessions_before_long_break`| INTEGER | NOT NULL | Cycle length (default 4) |
| `is_default` | INTEGER | NOT NULL | 1 if active default preset |

**Default Seed Data**:
- Standard: 25 / 5 / 15 (4 cycles)
- Deep Work: 50 / 10 / 20 (3 cycles)
- Study: 45 / 10 / 20 (4 cycles)
- Quick Focus: 15 / 3 / 10 (2 cycles)

---

### 2.3 Table: `blocked_apps`
Defines Windows processes blocked during active focus intervals.

| Column | Type | Constraints | Description |
|---|---|---|---|
| `id` | TEXT | PRIMARY KEY | Unique GUID identifier |
| `process_name` | TEXT | NOT NULL UNIQUE | Process executable name without path (e.g., `discord.exe`) |
| `display_name` | TEXT | NOT NULL | User-friendly application title |
| `is_enabled` | INTEGER | NOT NULL | 1 if rule is active |
| `created_at` | TEXT | NOT NULL | ISO-8601 UTC timestamp |

---

### 2.4 Table: `blocked_websites`
Contains internet domain names blocked during active focus sessions.

| Column | Type | Constraints | Description |
|---|---|---|---|
| `id` | TEXT | PRIMARY KEY | Unique GUID identifier |
| `domain` | TEXT | NOT NULL UNIQUE | Hostname domain (e.g., `reddit.com`) |
| `is_enabled` | INTEGER | NOT NULL | 1 if rule is active |
| `created_at` | TEXT | NOT NULL | ISO-8601 UTC timestamp |

---

### 2.5 Table: `tasks`
Stores actionable tasks and connects them to Pomodoro intervals.

| Column | Type | Constraints | Description |
|---|---|---|---|
| `id` | TEXT | PRIMARY KEY | Unique GUID identifier |
| `title` | TEXT | NOT NULL | Task description |
| `description` | TEXT | | Extended task notes |
| `priority` | INTEGER | NOT NULL | 0 = Low, 1 = Medium, 2 = High |
| `is_completed` | INTEGER | NOT NULL | 1 if task is marked complete |
| `estimated_pomodoros` | INTEGER | NOT NULL | Estimated Pomodoro count |
| `completed_pomodoros` | INTEGER | NOT NULL | Number of focus sessions completed for this task |
| `created_at` | TEXT | NOT NULL | ISO-8601 UTC timestamp |
| `completed_at` | TEXT | | ISO-8601 UTC timestamp of completion |

---

### 2.6 Table: `study_subjects`
Manages academic courses, exams, deadlines, and study targets.

| Column | Type | Constraints | Description |
|---|---|---|---|
| `id` | TEXT | PRIMARY KEY | Unique GUID identifier |
| `name` | TEXT | NOT NULL | Subject name (e.g., "Organic Chemistry") |
| `exam_date` | TEXT | NOT NULL | Scheduled exam/deadline date (ISO-8601) |
| `target_hours` | REAL | NOT NULL | Target study hours |
| `completed_hours` | REAL | NOT NULL | Completed focus hours logged |
| `color_hex` | TEXT | NOT NULL | Hex color code for UI cards |
| `notes` | TEXT | | Syllabus syllabus notes or study guide references |
| `created_at` | TEXT | NOT NULL | ISO-8601 UTC timestamp |

---

### 2.7 Table: `goals`
Tracks daily, weekly, and monthly focus goals.

| Column | Type | Constraints | Description |
|---|---|---|---|
| `id` | TEXT | PRIMARY KEY | Unique GUID identifier |
| `title` | TEXT | NOT NULL | Goal title (e.g. "Daily 4-Hour Target") |
| `target_type` | TEXT | NOT NULL | "Hours" or "Sessions" |
| `target_value` | REAL | NOT NULL | Numerical target |
| `current_value` | REAL | NOT NULL | Accumulated value |
| `period` | TEXT | NOT NULL | "Daily", "Weekly", "Monthly" |
| `is_completed` | INTEGER | NOT NULL | 1 if target achieved |
| `reset_date` | TEXT | NOT NULL | Date when period resets |

---

### 2.8 Table: `achievements`
Gamification badges awarded for sustained focus.

| Column | Type | Constraints | Description |
|---|---|---|---|
| `id` | TEXT | PRIMARY KEY | Unique GUID identifier |
| `key` | TEXT | NOT NULL UNIQUE | Unique badge key (e.g. `first_step`, `deep_diver`) |
| `title` | TEXT | NOT NULL | Badge name |
| `description` | TEXT | NOT NULL | Unlock requirements |
| `icon` | TEXT | NOT NULL | Glyph or icon identifier |
| `is_unlocked` | INTEGER | NOT NULL | 1 if unlocked |
| `unlocked_at` | TEXT | | ISO-8601 UTC timestamp of unlock |

---

### 2.9 Table: `settings`
Key-value store for application configuration and user preferences.

| Column | Type | Constraints | Description |
|---|---|---|---|
| `key` | TEXT | PRIMARY KEY | Setting key name |
| `value` | TEXT | NOT NULL | JSON or string value |

**Common Keys**:
- `Theme`: `"Light"`, `"Dark"`, `"Default"`
- `AutoStartBreaks`: `"true"`, `"false"`
- `AutoStartFocus`: `"true"`, `"false"`
- `SoundAlert`: `"Bell"`, `"Chime"`, `"Digital"`, `"Mute"`
- `Volume`: `"0.8"`
- `CommitmentMode`: `"false"`, `"true"`
- `CloseToTray`: `"true"`, `"false"`
- `MiniTimerPosition`: `{"X": 100, "Y": 100}`

---

## 3. Backup and Migration Protocol

The `FlowSqliteRepository.ExportJsonBackupAsync()` method extracts all table records into an RFC-8259 JSON structure:

```json
{
  "ExportedAt": "2026-11-05T15:00:00Z",
  "Version": "1.0.0",
  "Sessions": [...],
  "Presets": [...],
  "Tasks": [...],
  "StudySubjects": [...],
  "Goals": [...],
  "Achievements": [...],
  "Settings": { ... }
}
```

Restoring this JSON file triggers a transactional batch insert with foreign key constraints temporarily deferred, guaranteeing zero orphaned or corrupted records.

