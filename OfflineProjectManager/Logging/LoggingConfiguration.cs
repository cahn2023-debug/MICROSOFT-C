using Serilog;
using Serilog.Events;
using System;
using System.IO;

namespace OfflineProjectManager.Logging
{
    /// <summary>
    /// Centralized logging configuration for Preview System
    /// </summary>
    public static class LoggingConfiguration
    {
        private static bool _isInitialized;

        /// <summary>
        /// Initialize Serilog with file and console sinks
        /// </summary>
        public static void Initialize()
        {
            if (_isInitialized)
                return;

            // Ensure logs directory exists
            var logsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            Directory.CreateDirectory(logsDirectory);

            // Configure Serilog
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .Enrich.WithThreadId()
                .Enrich.WithEnvironmentName()
                .WriteTo.File(
                    path: Path.Combine(logsDirectory, "preview-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] ({ThreadId}) {Message:lj}{NewLine}{Exception}"
                )
#if DEBUG
                .WriteTo.Console(
                    outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
                )
#endif
                .CreateLogger();

            _isInitialized = true;

            Log.Information("=== Preview System Logging Initialized ===");
            Log.Information("Logs Directory: {LogsDirectory}", logsDirectory);
        }

        /// <summary>
        /// Flush and close all sinks
        /// </summary>
        public static void Shutdown()
        {
            Log.Information("=== Preview System Logging Shutdown ===");
            Log.CloseAndFlush();
        }
    }
}
