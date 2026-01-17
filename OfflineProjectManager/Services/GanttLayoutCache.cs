using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using OfflineProjectManager.Models;

namespace OfflineProjectManager.Services
{
    /// <summary>
    /// Phase 5: Caches Gantt layout calculations to avoid expensive recomputation
    /// </summary>
    public class GanttLayoutCache
    {
        private List<GanttCalculator.GanttBar> _cachedBars;
        private string _cacheKey;
        private readonly GanttCalculator _calculator;

        public GanttLayoutCache(GanttCalculator calculator)
        {
            _calculator = calculator ?? throw new ArgumentNullException(nameof(calculator));
        }

        public List<GanttCalculator.GanttBar> GetOrCalculate(List<ProjectTask> tasks)
        {
            var newKey = ComputeCacheKey(tasks);

            if (_cacheKey == newKey && _cachedBars != null)
            {
                System.Diagnostics.Debug.WriteLine("[GanttCache] HIT - Reusing cached layout");
                return _cachedBars;
            }

            System.Diagnostics.Debug.WriteLine("[GanttCache] MISS - Recalculating layout");
            _cachedBars = _calculator.CalculateLayout(tasks);
            _cacheKey = newKey;

            return _cachedBars;
        }

        public void Invalidate()
        {
            _cachedBars = null;
            _cacheKey = null;
            System.Diagnostics.Debug.WriteLine("[GanttCache] Invalidated");
        }

        /// <summary>
        /// Compute cache key based on task count and modification times
        /// Fast hash that detects most changes without deep comparison
        /// </summary>
        private static string ComputeCacheKey(List<ProjectTask> tasks)
        {
            if (tasks == null || tasks.Count == 0)
                return "empty";

            // Simple but effective: count + first/last task IDs + hash of all UpdatedAt
            var sb = new StringBuilder();
            sb.Append(tasks.Count);
            sb.Append('-');
            sb.Append(tasks.First().Id);
            sb.Append('-');
            sb.Append(tasks.Last().Id);
            sb.Append('-');

            // Hash all modification times
            foreach (var task in tasks.Take(100)) // Limit to first 100 for performance
            {
                sb.Append(task.UpdatedAt.Ticks);
                sb.Append(',');
            }

            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
            return Convert.ToBase64String(hashBytes);
        }
    }
}
