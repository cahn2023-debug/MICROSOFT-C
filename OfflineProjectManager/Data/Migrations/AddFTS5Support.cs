using Microsoft.EntityFrameworkCore;
using OfflineProjectManager.Data;
using System;
using System.Threading.Tasks;

namespace OfflineProjectManager.Migrations
{
    /// <summary>
    /// Migration to add FTS5 support and hash-based indexing
    /// </summary>
    public static class AddFTS5Support
    {
        public static async Task ApplyAsync(AppDbContext context)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[Migration] Starting FTS5 migration...");

                // 2.1 FTS for Files (External Content)
                await SetupFilesFTS(context).ConfigureAwait(false);

                // 2.2 FTS for Notes (External Content)
                await SetupNotesFTS(context).ConfigureAwait(false);

                System.Diagnostics.Debug.WriteLine("[Migration] ✅ FTS5 migration completed successfully!");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Migration] ❌ FTS5 migration failed: {ex.Message}");
            }
        }

        private static async Task SetupFilesFTS(AppDbContext context)
        {
            // Drop old tables if they exist (cleanup)
            await context.Database.ExecuteSqlRawAsync("DROP TABLE IF EXISTS files_fts").ConfigureAwait(false);
            await context.Database.ExecuteSqlRawAsync("DROP TABLE IF EXISTS content_index_fts").ConfigureAwait(false); // Remove legacy

            // Create FTS5 Virtual Table linked to 'files'
            // content='files', content_rowid='id' means no data duplication
            string createSql = @"
                CREATE VIRTUAL TABLE IF NOT EXISTS files_fts
                USING fts5(
                    filename,
                    content_index,
                    path_noaccent,
                    content='files',
                    content_rowid='id'
                );";
            await context.Database.ExecuteSqlRawAsync(createSql).ConfigureAwait(false);

            // Rebuild FTS index from existing data
            await context.Database.ExecuteSqlRawAsync("INSERT INTO files_fts(files_fts) VALUES('rebuild')").ConfigureAwait(false);

            // Triggers for Sync
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TRIGGER IF NOT EXISTS files_ai AFTER INSERT ON files BEGIN
                    INSERT INTO files_fts(rowid, filename, content_index, path_noaccent)
                    VALUES (new.id, new.filename, new.content_index, new.path_noaccent);
                END;").ConfigureAwait(false);

            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TRIGGER IF NOT EXISTS files_ad AFTER DELETE ON files BEGIN
                    DELETE FROM files_fts WHERE rowid = old.id;
                END;").ConfigureAwait(false);

            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TRIGGER IF NOT EXISTS files_au AFTER UPDATE ON files BEGIN
                    INSERT INTO files_fts(files_fts, rowid, filename, content_index, path_noaccent)
                    VALUES('delete', old.id, old.filename, old.content_index, old.path_noaccent);
                    INSERT INTO files_fts(rowid, filename, content_index, path_noaccent)
                    VALUES (new.id, new.filename, new.content_index, new.path_noaccent);
                END;").ConfigureAwait(false);
        }

        private static async Task SetupNotesFTS(AppDbContext context)
        {
            await context.Database.ExecuteSqlRawAsync("DROP TABLE IF EXISTS notes_fts").ConfigureAwait(false);

            // Create FTS5 for Notes
            string createSql = @"
                CREATE VIRTUAL TABLE IF NOT EXISTS notes_fts
                USING fts5(
                    title,
                    content,
                    content='notes',
                    content_rowid='id'
                );";
            await context.Database.ExecuteSqlRawAsync(createSql).ConfigureAwait(false);

            // Rebuild
            await context.Database.ExecuteSqlRawAsync("INSERT INTO notes_fts(notes_fts) VALUES('rebuild')").ConfigureAwait(false);

            // Triggers
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TRIGGER IF NOT EXISTS notes_ai AFTER INSERT ON notes BEGIN
                    INSERT INTO notes_fts(rowid, title, content)
                    VALUES (new.id, new.title, new.content);
                END;").ConfigureAwait(false);

            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TRIGGER IF NOT EXISTS notes_ad AFTER DELETE ON notes BEGIN
                    DELETE FROM notes_fts WHERE rowid = old.id;
                END;").ConfigureAwait(false);

            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TRIGGER IF NOT EXISTS notes_au AFTER UPDATE ON notes BEGIN
                    INSERT INTO notes_fts(notes_fts, rowid, title, content)
                    VALUES('delete', old.id, old.title, old.content);
                    INSERT INTO notes_fts(rowid, title, content)
                    VALUES (new.id, new.title, new.content);
                END;").ConfigureAwait(false);
        }
    }
}
