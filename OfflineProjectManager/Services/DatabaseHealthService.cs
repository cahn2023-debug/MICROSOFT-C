using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using OfflineProjectManager.Data;
using System;
using System.IO;
using System.Threading.Tasks;

namespace OfflineProjectManager.Services
{
    /// <summary>
    /// Phase 6: Database health monitoring and diagnostics
    /// </summary>
    public class DatabaseHealthService
    {
        private readonly Func<AppDbContext> _contextFactory;

        public DatabaseHealthService(Func<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        }

        public class DatabaseHealth
        {
            public long SizeMB { get; set; }
            public long WalSizeMB { get; set; }
            public bool WALEnabled { get; set; }
            public int TableCount { get; set; }
            public int IndexCount { get; set; }
            public string JournalMode { get; set; }
            public string SynchronousMode { get; set; }
            public long CacheSizeKB { get; set; }
        }

        public async Task<DatabaseHealth> CheckHealthAsync()
        {
            using var context = _contextFactory();

            var health = new DatabaseHealth();

            try
            {
                await context.Database.OpenConnectionAsync().ConfigureAwait(false);
                var connection = context.Database.GetDbConnection() as SqliteConnection;

                // Database size
                var dbPath = connection.DataSource;
                if (File.Exists(dbPath))
                {
                    health.SizeMB = new FileInfo(dbPath).Length / (1024 * 1024);

                    // WAL file size
                    var walPath = dbPath + "-wal";
                    if (File.Exists(walPath))
                    {
                        health.WalSizeMB = new FileInfo(walPath).Length / (1024 * 1024);
                    }
                }

                // Journal mode
                var journalMode = await ExecuteScalarAsync(context, "PRAGMA journal_mode").ConfigureAwait(false);
                health.JournalMode = journalMode?.ToString() ?? "unknown";
                health.WALEnabled = health.JournalMode.Equals("wal", StringComparison.OrdinalIgnoreCase);

                // Synchronous mode
                var syncMode = await ExecuteScalarAsync(context, "PRAGMA synchronous").ConfigureAwait(false);
                health.SynchronousMode = syncMode?.ToString() ?? "unknown";

                // Cache size (in pages, convert to KB assuming 4KB pages)
                var cacheSize = await ExecuteScalarAsync(context, "PRAGMA cache_size").ConfigureAwait(false);
                if (long.TryParse(cacheSize?.ToString(), out var cacheSizePages))
                {
                    health.CacheSizeKB = Math.Abs(cacheSizePages); // Negative means KB already
                }

                // Table count
                var tableCount = await ExecuteScalarAsync(context,
                    "SELECT COUNT(*) FROM sqlite_master WHERE type='table'").ConfigureAwait(false);
                health.TableCount = int.TryParse(tableCount?.ToString(), out var tc) ? tc : 0;

                // Index count
                var indexCount = await ExecuteScalarAsync(context,
                    "SELECT COUNT(*) FROM sqlite_master WHERE type='index'").ConfigureAwait(false);
                health.IndexCount = int.TryParse(indexCount?.ToString(), out var ic) ? ic : 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Health] Check failed: {ex.Message}");
            }

            return health;
        }

        private static async Task<object> ExecuteScalarAsync(AppDbContext context, string sql)
        {
            using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = sql;
            return await command.ExecuteScalarAsync().ConfigureAwait(false);
        }

        public async Task<string> GetHealthReportAsync()
        {
            var health = await CheckHealthAsync().ConfigureAwait(false);

            return $@"Database Health Report
======================
Size: {health.SizeMB} MB
WAL Size: {health.WalSizeMB} MB
Journal Mode: {health.JournalMode} {(health.WALEnabled ? "✓" : "✗")}
Synchronous: {health.SynchronousMode}
Cache Size: {health.CacheSizeKB} KB
Tables: {health.TableCount}
Indexes: {health.IndexCount}";
        }
    }
}
