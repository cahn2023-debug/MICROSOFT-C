using System;
using System.Collections.Generic;
using System.Linq;
using OfflineProjectManager.Models;

namespace OfflineProjectManager.Services
{
    /// <summary>
    /// Calculates Gantt chart layout independently of UI
    /// Phase 5: Extracted logic for performance and testability
    /// </summary>
    public class GanttCalculator
    {
        public class GanttBar
        {
            public int TaskId { get; set; }
            public string TaskName { get; set; }
            public DateTime StartDate { get; set; }
            public DateTime EndDate { get; set; }
            public int Row { get; set; }
            public double StartX { get; set; }      // Days from project start
            public double Duration { get; set; }     // Duration in days
            public string Status { get; set; }
            public string Priority { get; set; }
            public List<int> Dependencies { get; set; } = [];
            public string Color { get; set; }       // Status-based color
        }

        /// <summary>
        /// Calculate Gantt bar positions for all tasks
        /// </summary>
        public List<GanttBar> CalculateLayout(List<ProjectTask> tasks)
        {
            if (tasks == null || tasks.Count == 0)
                return [];

            // Filter tasks with valid dates
            var validTasks = tasks.Where(t => t.StartDate.HasValue && t.EndDate.HasValue).ToList();
            if (validTasks.Count == 0)
                return [];

            var minDate = validTasks.Min(t => t.StartDate.Value);

            return validTasks.Select((task, index) => new GanttBar
            {
                TaskId = task.Id,
                TaskName = task.Name ?? "Unnamed Task",
                StartDate = task.StartDate.Value,
                EndDate = task.EndDate.Value,
                Row = index,
                StartX = (task.StartDate.Value - minDate).TotalDays,
                Duration = (task.EndDate.Value - task.StartDate.Value).TotalDays,
                Status = task.Status ?? "Pending",
                Priority = task.Priority ?? "Normal",
                Dependencies = ParseDependencies(task.Dependencies),
                Color = GetColorForStatus(task.Status)
            }).ToList();
        }

        /// <summary>
        /// Calculate visible bars for virtualization (only render what's on screen)
        /// </summary>
        public List<GanttBar> GetVisibleBars(List<GanttBar> allBars, int startRow, int endRow)
        {
            return allBars
                .Where(b => b.Row >= startRow && b.Row <= endRow)
                .ToList();
        }

        /// <summary>
        /// Update single task without full recalculation
        /// </summary>
        public void UpdateBar(GanttBar bar, ProjectTask updatedTask, DateTime projectStartDate)
        {
            if (!updatedTask.StartDate.HasValue || !updatedTask.EndDate.HasValue)
                return;

            bar.TaskName = updatedTask.Name;
            bar.StartDate = updatedTask.StartDate.Value;
            bar.EndDate = updatedTask.EndDate.Value;
            bar.StartX = (updatedTask.StartDate.Value - projectStartDate).TotalDays;
            bar.Duration = (bar.EndDate - bar.StartDate).TotalDays;
            bar.Status = updatedTask.Status ?? "Pending";
            bar.Color = GetColorForStatus(updatedTask.Status);
        }

        /// <summary>
        /// Parse dependency string (e.g., "1,3,5" → [1,3,5])
        /// </summary>
        private static List<int> ParseDependencies(string dependencies)
        {
            if (string.IsNullOrWhiteSpace(dependencies))
                return [];

            try
            {
                return dependencies
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => int.Parse(s.Trim()))
                    .ToList();
            }
            catch
            {
                return [];
            }
        }

        /// <summary>
        /// Get color based on task status
        /// </summary>
        private static string GetColorForStatus(string status)
        {
            return status?.ToLower() switch
            {
                "completed" => "#4CAF50",    // Green
                "in progress" => "#2196F3",  // Blue
                "pending" => "#FFC107",      // Amber
                "blocked" => "#F44336",      // Red
                "on hold" => "#9E9E9E",      // Grey
                _ => "#607D8B"               // Blue Grey (default)
            };
        }

        /// <summary>
        /// Calculate project timeline statistics
        /// </summary>
        public (DateTime Start, DateTime End, int TotalDays) GetProjectTimeline(List<GanttBar> bars)
        {
            if (bars == null || bars.Count == 0)
                return (DateTime.Now, DateTime.Now, 0);

            var start = bars.Min(b => b.StartDate);
            var end = bars.Max(b => b.EndDate);
            var totalDays = (int)(end - start).TotalDays;

            return (start, end, totalDays);
        }
    }
}
