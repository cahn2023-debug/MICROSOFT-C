using Microsoft.EntityFrameworkCore;
using OfflineProjectManager.Data;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace OfflineProjectManager.Services
{
    /// <summary>
    /// Phase 6: Database maintenance service for periodic VACUUM and health checks
    /// </summary>
    public class DatabaseMaintenanceService : IDisposable
    {
        private System.Threading.Timer _vacuumTimer;
        private readonly Func<AppDbContext> _contextFactory;
        private bool _disposed;

        public DatabaseMaintenanceService(Func<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        }

        public void StartScheduledMaintenance()
        {
            // Schedule daily VACUUM at 2 AM
            var now = DateTime.Now;
            var nextRun = now.Date.AddDays(1).AddHours(2); // Tomorrow 2 AM
            if (now.Hour < 2)
            {
                // If before 2 AM today, run today
                nextRun = now.Date.AddHours(2);
            }

            var delay = nextRun - now;

            _vacuumTimer = new System.Threading.Timer(
                async _ => await RunVacuumAsync().ConfigureAwait(false),
                null,
                delay,
                TimeSpan.FromDays(1));

            Debug.WriteLine($"[Maintenance] Scheduled VACUUM for {nextRun:yyyy-MM-dd HH:mm}");
        }

        public async Task RunVacuumAsync()
        {
            using var context = _contextFactory();

            try
            {
                var sw = Stopwatch.StartNew();

                // Get database size before
                var sizeBeforeMB = await GetDatabaseSizeMBAsync(context).ConfigureAwait(false);

                // Only vacuum if database > 100MB
                if (sizeBeforeMB > 100)
                {
                    Debug.WriteLine($"[Maintenance] Starting VACUUM (DB size: {sizeBeforeMB}MB)...");

                    await context.Database.ExecuteSqlRawAsync("VACUUM;").ConfigureAwait(false);

                    sw.Stop();
                    var sizeAfterMB = await GetDatabaseSizeMBAsync(context).ConfigureAwait(false);
                    var savedMB = sizeBeforeMB - sizeAfterMB;

                    Debug.WriteLine($"[Maintenance] VACUUM completed in {sw.ElapsedMilliseconds}ms. Saved {savedMB:F1}MB ({sizeAfterMB}MB remaining)");
                }
                else
                {
                    Debug.WriteLine($"[Maintenance] Skipped VACUUM (DB size {sizeBeforeMB}MB < 100MB threshold)");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Maintenance] VACUUM failed: {ex.Message}");
            }
        }

        private static async Task<long> GetDatabaseSizeMBAsync(AppDbContext context)
        {
            try
            {
                var connection = context.Database.GetDbConnection();
                var dbPath = connection.DataSource;

                if (File.Exists(dbPath))
                {
                    await Task.CompletedTask; // Make async
                    var fileInfo = new FileInfo(dbPath);
                    return fileInfo.Length / (1024 * 1024); // Convert to MB
                }
            }
            catch { }

            return 0;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _vacuumTimer?.Dispose();
                _disposed = true;
            }
        }
    }
}
