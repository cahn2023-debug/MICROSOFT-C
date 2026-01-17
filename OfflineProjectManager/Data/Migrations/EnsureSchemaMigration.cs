using Microsoft.EntityFrameworkCore;
using OfflineProjectManager.Data;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OfflineProjectManager.Data.Migrations
{
    public static class EnsureSchemaMigration
    {
        public static async Task ApplyAsync(AppDbContext context)
        {
            var connection = context.Database.GetDbConnection();

            // Ensure connection is open
            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync().ConfigureAwait(false);
            }

            // Ensure 'tasks' table schema
            await EnsureColumnAsync(context, "tasks", "target_file_path", "TEXT");
            await EnsureColumnAsync(context, "tasks", "anchor_data", "TEXT");
            await EnsureColumnAsync(context, "tasks", "progress", "REAL DEFAULT 0");
            await EnsureColumnAsync(context, "tasks", "dependencies", "TEXT");

            // Ensure 'notes' table schema
            await EnsureColumnAsync(context, "notes", "target_file_path", "TEXT");
            await EnsureColumnAsync(context, "notes", "anchor_data", "TEXT");
        }

        private static async Task EnsureColumnAsync(AppDbContext context, string tableName, string columnName, string columnType)
        {
            var connection = context.Database.GetDbConnection();

            // Create command
            using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info({tableName})";

            bool exists = false;
            using (var reader = await command.ExecuteReaderAsync().ConfigureAwait(false))
            {
                while (await reader.ReadAsync().ConfigureAwait(false))
                {
                    var name = reader.GetString(1); // name is index 1
                    if (string.Equals(name, columnName, StringComparison.OrdinalIgnoreCase))
                    {
                        exists = true;
                        break;
                    }
                }
            }

            if (!exists)
            {
                System.Diagnostics.Debug.WriteLine($"[Migration] Adding missing column {tableName}.{columnName}");

                // ALTER TABLE to add column
#pragma warning disable EF1002 // Method inserts interpolated strings directly into the SQL
                await context.Database.ExecuteSqlRawAsync($"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnType}").ConfigureAwait(false);
#pragma warning restore EF1002 // Method inserts interpolated strings directly into the SQL

                System.Diagnostics.Debug.WriteLine($"[Migration] Successfully added {tableName}.{columnName}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[Migration] Column {tableName}.{columnName} already exists");
            }
        }
    }
}
