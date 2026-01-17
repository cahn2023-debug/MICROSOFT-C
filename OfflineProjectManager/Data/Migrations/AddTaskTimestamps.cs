using Microsoft.EntityFrameworkCore;
using OfflineProjectManager.Data;
using System;
using System.Threading.Tasks;

namespace OfflineProjectManager.Data.Migrations
{
    /// <summary>
    /// Migration to add missing timestamp columns to tasks table
    /// </summary>
    public static class AddTaskTimestamps
    {
        public static async Task ApplyAsync(AppDbContext context)
        {
            try
            {
                var connection = context.Database.GetDbConnection();
                await connection.OpenAsync().ConfigureAwait(false);

                using var command = connection.CreateCommand();

                // Check if columns exist before adding
                command.CommandText = "PRAGMA table_info(tasks)";
                var hasCreatedAt = false;
                var hasUpdatedAt = false;
                var hasCreatedAtl = false; // Check for typo

                using (var reader = await command.ExecuteReaderAsync().ConfigureAwait(false))
                {
                    while (await reader.ReadAsync().ConfigureAwait(false))
                    {
                        var colName = reader.GetString(1); // Column name is at index 1
                        if (colName == "created_at") hasCreatedAt = true;
                        if (colName == "updated_at") hasUpdatedAt = true;
                        if (colName == "created_atl") hasCreatedAtl = true; // Detect typo
                    }
                }

                // Fix typo if found
                if (hasCreatedAtl && !hasCreatedAt)
                {
                    System.Diagnostics.Debug.WriteLine("[Migration] Detected incorrect column 'created_atl', renaming to 'created_at'");

                    // Rename column: SQLite doesn't support direct column rename, need to recreate table
                    await context.Database.ExecuteSqlRawAsync(@"
                        CREATE TABLE tasks_backup AS SELECT * FROM tasks;
                        DROP TABLE tasks;
                        CREATE TABLE tasks (
                            id INTEGER PRIMARY KEY AUTOINCREMENT,
                            project_id INTEGER NOT NULL,
                            related_file_id INTEGER,
                            name TEXT NOT NULL,
                            description TEXT,
                            status TEXT DEFAULT 'Todo',
                            priority TEXT DEFAULT 'Normal',
                            start_date TIMESTAMP,
                            end_date TIMESTAMP,
                            target_file_path TEXT,
                            anchor_data TEXT,
                            progress REAL DEFAULT 0,
                            dependencies TEXT,
                            created_at TEXT,
                            updated_at TEXT,
                            FOREIGN KEY(project_id) REFERENCES projects(id),
                            FOREIGN KEY(related_file_id) REFERENCES files(id)
                        );
                        INSERT INTO tasks SELECT 
                            id, project_id, related_file_id, name, description, status, priority,
                            start_date, end_date, target_file_path, anchor_data, progress, dependencies,
                            created_atl as created_at, updated_at
                        FROM tasks_backup;
                        DROP TABLE tasks_backup;
                    ").ConfigureAwait(false);

                    hasCreatedAt = true;
                }


                // Add missing columns with constant defaults
                if (!hasCreatedAt)
                {
                    // SQLite doesn't allow function calls in ALTER TABLE ADD COLUMN DEFAULT
                    await context.Database.ExecuteSqlRawAsync(
                        "ALTER TABLE tasks ADD COLUMN created_at TEXT"
                    ).ConfigureAwait(false);

                    // Update existing rows
                    await context.Database.ExecuteSqlRawAsync(
                        "UPDATE tasks SET created_at = datetime('now') WHERE created_at IS NULL"
                    ).ConfigureAwait(false);

                    System.Diagnostics.Debug.WriteLine("[Migration] Added created_at column to tasks table");
                }

                if (!hasUpdatedAt)
                {
                    await context.Database.ExecuteSqlRawAsync(
                        "ALTER TABLE tasks ADD COLUMN updated_at TEXT"
                    ).ConfigureAwait(false);

                    // Update existing rows
                    await context.Database.ExecuteSqlRawAsync(
                        "UPDATE tasks SET updated_at = datetime('now') WHERE updated_at IS NULL"
                    ).ConfigureAwait(false);

                    System.Diagnostics.Debug.WriteLine("[Migration] Added updated_at column to tasks table");
                }

                if (hasCreatedAt && hasUpdatedAt)
                {
                    System.Diagnostics.Debug.WriteLine("[Migration] Task timestamp columns already exist");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Migration] AddTaskTimestamps failed: {ex.Message}");
                throw;
            }
        }
    }
}
