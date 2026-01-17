using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using OfflineProjectManager.Models;
using OfflineProjectManager.Services;
using Microsoft.Extensions.DependencyInjection;

namespace OfflineProjectManager.Views
{
    /// <summary>
    /// Dialog for editing task details including dates, progress, and dependencies.
    /// </summary>
    public partial class TaskEditDialog : Window
    {
        private readonly ProjectTask _task;
        private readonly ITaskService _taskService;
        private bool _isDeleted = false;

        public bool WasDeleted => _isDeleted;
        public bool WasSaved { get; private set; } = false;

        public TaskEditDialog(ProjectTask task)
        {
            InitializeComponent();

            _task = task ?? throw new ArgumentNullException(nameof(task));
            _taskService = App.ServiceProvider?.GetService<ITaskService>();

            LoadTaskData();
        }

        private void LoadTaskData()
        {
            TaskNameInput.Text = _task.Name ?? "";
            DescriptionInput.Text = _task.GetCleanDescription();

            // Status
            foreach (ComboBoxItem item in StatusCombo.Items)
            {
                if (string.Equals(item.Content?.ToString(), _task.Status, StringComparison.OrdinalIgnoreCase))
                {
                    StatusCombo.SelectedItem = item;
                    break;
                }
            }

            // Priority
            foreach (ComboBoxItem item in PriorityCombo.Items)
            {
                if (string.Equals(item.Content?.ToString(), _task.Priority, StringComparison.OrdinalIgnoreCase))
                {
                    PriorityCombo.SelectedItem = item;
                    break;
                }
            }

            // Dates
            StartDatePicker.SelectedDate = _task.StartDate;
            EndDatePicker.SelectedDate = _task.EndDate;

            // Progress
            ProgressSlider.Value = _task.Progress;

            // Dependencies
            if (!string.IsNullOrEmpty(_task.Dependencies))
            {
                try
                {
                    var deps = System.Text.Json.JsonSerializer.Deserialize<int[]>(_task.Dependencies);
                    if (deps != null)
                    {
                        DependenciesInput.Text = string.Join(", ", deps);
                    }
                }
                catch
                {
                    DependenciesInput.Text = _task.Dependencies;
                }
            }
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (_taskService == null) return;

            // Validate
            if (string.IsNullOrWhiteSpace(TaskNameInput.Text))
            {
                System.Windows.MessageBox.Show("Task name is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (StartDatePicker.SelectedDate.HasValue && EndDatePicker.SelectedDate.HasValue)
            {
                if (EndDatePicker.SelectedDate < StartDatePicker.SelectedDate)
                {
                    System.Windows.MessageBox.Show("End date cannot be before start date.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            // Update task object
            _task.Name = TaskNameInput.Text.Trim();
            _task.Description = DescriptionInput.Text?.Trim();
            _task.Status = (StatusCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Todo";
            _task.Priority = (PriorityCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Normal";
            _task.StartDate = StartDatePicker.SelectedDate;
            _task.EndDate = EndDatePicker.SelectedDate;
            _task.Progress = ProgressSlider.Value;
            _task.UpdatedAt = DateTime.UtcNow;

            // Parse dependencies
            if (!string.IsNullOrWhiteSpace(DependenciesInput.Text))
            {
                try
                {
                    var depIds = DependenciesInput.Text
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => int.Parse(s.Trim()))
                        .ToArray();
                    _task.Dependencies = System.Text.Json.JsonSerializer.Serialize(depIds);
                }
                catch
                {
                    System.Windows.MessageBox.Show("Invalid dependency format. Use comma-separated numbers.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }
            else
            {
                _task.Dependencies = null;
            }

            try
            {
                await _taskService.UpdateTaskAsync(_task);
                WasSaved = true;
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error saving task: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (_taskService == null) return;

            var result = System.Windows.MessageBox.Show(
                $"Are you sure you want to delete task '{_task.Name}'?",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    await _taskService.DeleteTaskAsync(_task.Id);
                    _isDeleted = true;
                    DialogResult = true;
                    Close();
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Error deleting task: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
