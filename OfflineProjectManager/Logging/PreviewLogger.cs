using Serilog;
using System;
using System.Diagnostics;

namespace OfflineProjectManager.Logging
{
    /// <summary>
    /// Static logger wrapper for Preview System components
    /// Provides structured logging with context
    /// </summary>
    public static class PreviewLogger
    {
        /// <summary>
        /// Log successful preview creation
        /// </summary>
        public static void LogPreviewCreated(string providerName, string filePath, long elapsedMs)
        {
            Log.Information(
                "Preview created | Provider={Provider} | File={FilePath} | Duration={Elapsed}ms",
                providerName,
                filePath,
                elapsedMs
            );
        }

        /// <summary>
        /// Log preview creation failure
        /// </summary>
        public static void LogPreviewFailed(Exception ex, string providerName, string filePath, string extension = null)
        {
            Log.Error(ex,
                "Preview failed | Provider={Provider} | File={FilePath} | Extension={Extension}",
                providerName,
                filePath,
                extension ?? System.IO.Path.GetExtension(filePath)
            );
        }

        /// <summary>
        /// Log cache hit
        /// </summary>
        public static void LogCacheHit(string filePath)
        {
            Log.Debug(
                "Cache HIT | File={FilePath}",
                filePath
            );
        }

        /// <summary>
        /// Log cache miss
        /// </summary>
        public static void LogCacheMiss(string filePath, string reason = null)
        {
            Log.Debug(
                "Cache MISS | File={FilePath} | Reason={Reason}",
                filePath,
                reason ?? "Not cached"
            );
        }

        /// <summary>
        /// Log cache eviction
        /// </summary>
        public static void LogCacheEviction(string filePath, string reason)
        {
            Log.Information(
                "Cache eviction | File={FilePath} | Reason={Reason}",
                filePath,
                reason
            );
        }

        /// <summary>
        /// Log provider selection
        /// </summary>
        public static void LogProviderSelected(string extension, string providerName)
        {
            Log.Debug(
                "Provider selected | Extension={Extension} | Provider={Provider}",
                extension,
                providerName
            );
        }

        /// <summary>
        /// Log highlight operation
        /// </summary>
        public static void LogHighlight(string filePath, string keyword, int matchCount)
        {
            Log.Debug(
                "Highlight applied | File={FilePath} | Keyword={Keyword} | Matches={MatchCount}",
                filePath,
                keyword,
                matchCount
            );
        }

        /// <summary>
        /// Log operation start with timing
        /// </summary>
        public static Stopwatch StartOperation(string operationName, string context = null)
        {
            Log.Debug(
                "Operation START | Operation={Operation} | Context={Context}",
                operationName,
                context
            );
            return Stopwatch.StartNew();
        }

        /// <summary>
        /// Log operation completion
        /// </summary>
        public static void EndOperation(string operationName, Stopwatch sw, string context = null)
        {
            sw.Stop();
            Log.Debug(
                "Operation END | Operation={Operation} | Context={Context} | Duration={Elapsed}ms",
                operationName,
                context,
                sw.ElapsedMilliseconds
            );
        }

        /// <summary>
        /// Log warning
        /// </summary>
        public static void LogWarning(string message, params object[] propertyValues)
        {
            Log.Warning(message, propertyValues);
        }

        /// <summary>
        /// Log generic error
        /// </summary>
        public static void LogError(Exception ex, string message, params object[] propertyValues)
        {
            Log.Error(ex, message, propertyValues);
        }

        /// <summary>
        /// Log info message
        /// </summary>
        public static void LogInfo(string message, params object[] propertyValues)
        {
            Log.Information(message, propertyValues);
        }
    }
}
