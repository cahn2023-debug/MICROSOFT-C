using CommunityToolkit.Mvvm.ComponentModel;
using OfflineProjectManager.Models;
using OfflineProjectManager.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;

namespace OfflineProjectManager.ViewModels
{
    /// <summary>
    /// ViewModel for individual Gantt item (task or milestone)
    /// </summary>
    public partial class GanttItemViewModel : ObservableObject
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Status { get; set; } = "Pending";
        public double Progress { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public bool IsMilestone { get; set; }
        public DateTime? MilestoneDate { get; set; }
        public string Priority { get; set; } = "Normal";
        public string Dependencies { get; set; }

        public string Indicator => IsMilestone ? "🚩" : Priority?.ToLower() switch
        {
            "high" => "🔴",
            "low" => "🟢",
            _ => "🔵"
        };

        public string ProgressText => IsMilestone ? "" : $"{Progress:0}%";
    }

    /// <summary>
    /// Professional flat-design Gantt chart ViewModel
    /// </summary>
    public partial class GanttChartViewModel : ObservableObject
    {
        private readonly ITaskService _taskService;
        private readonly IProjectService _projectService;
        private List<ProjectTask> _allTasks = [];

        #region Gantt Items Collection
        [ObservableProperty]
        private ObservableCollection<GanttItemViewModel> _ganttItems = [];
        #endregion

        #region Timeline Properties
        [ObservableProperty]
        private string _timelineHeader = "No project loaded";

        [ObservableProperty]
        private DateTime _projectStartDate = DateTime.Today;

        [ObservableProperty]
        private DateTime _projectEndDate = DateTime.Today.AddDays(30);
        #endregion

        #region Summary Panel Properties
        [ObservableProperty]
        private string _overallProgressText = "0%";

        [ObservableProperty]
        private string _completedTasksText = "0 of 0 Tasks";

        [ObservableProperty]
        private int _daysRemaining = 0;

        [ObservableProperty]
        private string _deadlineText = "No deadline";

        [ObservableProperty]
        private string _overdueTasksText = "0";

        [ObservableProperty]
        private string _healthMessage = "No tasks";

        [ObservableProperty]
        private System.Windows.Media.Brush _healthBackground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(240, 253, 244));

        [ObservableProperty]
        private System.Windows.Media.Brush _healthForeground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(22, 163, 74));
        #endregion

        /// <summary>
        /// Constructor with dependency injection
        /// </summary>
        public GanttChartViewModel(ITaskService taskService, IProjectService projectService)
        {
            _taskService = taskService ?? throw new ArgumentNullException(nameof(taskService));
            _projectService = projectService ?? throw new ArgumentNullException(nameof(projectService));

            // Subscribe to TasksChanged for real-time sync
            _taskService.TasksChanged += OnTasksChanged;
            _projectService.ProjectChanged += OnProjectChanged;
        }

        /// <summary>
        /// Parameterless constructor for XAML design-time support
        /// </summary>
        public GanttChartViewModel()
        {
        }

        private void OnTasksChanged()
        {
            System.Windows.Application.Current?.Dispatcher?.InvokeAsync(async () =>
            {
                await LoadTasksFromDatabaseAsync();
            });
        }

        private void OnProjectChanged()
        {
            System.Windows.Application.Current?.Dispatcher?.InvokeAsync(async () =>
            {
                await LoadTasksFromDatabaseAsync();
            });
        }

        /// <summary>
        /// Load tasks from database and refresh Gantt chart
        /// </summary>
        public async Task LoadTasksFromDatabaseAsync()
        {
            if (_taskService == null || _projectService == null || !_projectService.IsProjectOpen)
            {
                Clear();
                return;
            }

            _allTasks = await _taskService.GetTasksByProjectAsync(_projectService.CurrentProject.Id);
            ProcessTasks();
        }

        private void ProcessTasks()
        {
            GanttItems.Clear();

            if (_allTasks == null || _allTasks.Count == 0)
            {
                TimelineHeader = "No tasks in project";
                UpdateSummary([], [], 0, 0);
                OnPropertyChanged(nameof(GanttItems));
                return;
            }

            // Filter tasks with valid dates
            var validTasks = _allTasks
                .Where(t => t.StartDate.HasValue && t.EndDate.HasValue)
                .OrderBy(t => t.StartDate)
                .ToList();

            // Calculate timeline range
            if (validTasks.Any())
            {
                ProjectStartDate = validTasks.Min(t => t.StartDate!.Value).AddDays(-7);
                ProjectEndDate = validTasks.Max(t => t.EndDate!.Value).AddDays(14);
                TimelineHeader = $"{ProjectStartDate:MMM yyyy} - {ProjectEndDate:MMM yyyy}";
            }

            // Create GanttItemViewModels
            foreach (var task in validTasks)
            {
                GanttItems.Add(new GanttItemViewModel
                {
                    Id = task.Id,
                    Name = task.Name ?? "Unnamed",
                    Status = task.Status ?? "Pending",
                    Progress = task.Progress,
                    StartDate = task.StartDate,
                    EndDate = task.EndDate,
                    Priority = task.Priority ?? "Normal",
                    Dependencies = task.Dependencies,
                    IsMilestone = false
                });
            }

            // Calculate summary statistics
            var completedTasks = _allTasks.Where(t => t.Status?.ToLower() == "done" || t.Status?.ToLower() == "completed").ToList();
            var overdueTasks = _allTasks.Where(t =>
                t.EndDate.HasValue &&
                t.EndDate.Value < DateTime.Today &&
                t.Status?.ToLower() != "done" &&
                t.Status?.ToLower() != "completed").ToList();

            UpdateSummary(_allTasks, completedTasks, overdueTasks.Count,
                validTasks.Any() ? (int)(ProjectEndDate - DateTime.Today).TotalDays : 0);

            OnPropertyChanged(nameof(GanttItems));
            System.Diagnostics.Debug.WriteLine($"[GanttChart] Loaded {GanttItems.Count} tasks");
        }

        private void UpdateSummary(List<ProjectTask> allTasks, List<ProjectTask> completedTasks, int overdueCount, int daysLeft)
        {
            var totalCount = allTasks.Count;
            var completedCount = completedTasks.Count;

            // Overall Progress
            var progressPercent = totalCount > 0 ? (double)completedCount / totalCount * 100 : 0;
            OverallProgressText = $"{progressPercent:0}%";
            CompletedTasksText = $"{completedCount} of {totalCount} Tasks Completed";

            // Time Remaining
            DaysRemaining = Math.Max(0, daysLeft);
            DeadlineText = daysLeft > 0 ? $"Deadline: {ProjectEndDate:MMM dd, yyyy}" : "No deadline set";

            // Project Health
            OverdueTasksText = overdueCount > 0 ? $"{overdueCount} Overdue ⚠" : "On Track ✓";
            HealthMessage = overdueCount > 0 ? "Action required" : "All tasks on schedule";

            if (overdueCount > 0)
            {
                HealthBackground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(254, 242, 242)); // Red bg
                HealthForeground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 38, 38));   // Red text
            }
            else
            {
                HealthBackground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(240, 253, 244)); // Green bg
                HealthForeground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(22, 163, 74));   // Green text
            }
        }

        public void Clear()
        {
            _allTasks.Clear();
            GanttItems.Clear();
            TimelineHeader = "No project loaded";
            UpdateSummary([], [], 0, 0);
            OnPropertyChanged(nameof(GanttItems));
        }
    }
}
