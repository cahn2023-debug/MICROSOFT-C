using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using OfflineProjectManager.Services;
using CommunityToolkit.Mvvm.Input;
using OfflineProjectManager.ViewModels;
using OfflineProjectManager.Models;

namespace OfflineProjectManager.Features.Task.ViewModels
{
    public class TaskItemViewModel : ViewModelBase
    {
        private readonly ProjectTask _model;
        public ProjectTask Model => _model;

        public int Id => _model.Id;

        public string Name
        {
            get => _model.Name;
            set { if (_model.Name != value) { _model.Name = value; OnPropertyChanged(); } }
        }

        public string Status
        {
            get => _model.Status;
            set { if (_model.Status != value) { _model.Status = value; OnPropertyChanged(); } }
        }

        public string Priority
        {
            get => _model.Priority;
            set { if (_model.Priority != value) { _model.Priority = value; OnPropertyChanged(); } }
        }

        public TaskItemViewModel(ProjectTask model) => _model = model;
    }

    public class TaskPanelViewModel : ViewModelBase
    {
        private readonly ITaskService _taskService;
        private readonly IProjectService _projectService;
        private readonly MainViewModel _mainViewModel;

        public ObservableCollection<TaskItemViewModel> Tasks { get; set; } = [];
        public ObservableCollection<Note> Notes { get; set; } = [];

        private TaskItemViewModel _selectedTask;
        public TaskItemViewModel SelectedTask
        {
            get => _selectedTask;
            set
            {
                if (SetProperty(ref _selectedTask, value) && value != null)
                {
                    // Cancel Edit mode if active
                    if (IsEditing)
                    {
                        IsEditing = false;
                        _editingTaskId = null;
                        _editingNoteId = null;

                        // Optional: Clear form or keep it? 
                        // User said "hủy việc edit", usually implies reverting or clearing.
                        // I will clear to allow fresh Add.
                        NewItemName = "";
                        NewItemDescription = "";
                    }

                    // Only restore preview context, don't overwrite form inputs
                    _ = _mainViewModel.RestoreContextAsync(value.Model.Description, value.Model.AnchorData);
                }
            }
        }

        private Note _selectedNote;
        public Note SelectedNote
        {
            get => _selectedNote;
            set
            {
                if (SetProperty(ref _selectedNote, value) && value != null)
                {
                    // Cancel Edit mode if active
                    if (IsEditing)
                    {
                        IsEditing = false;
                        _editingTaskId = null;
                        _editingNoteId = null;
                        NewItemName = "";
                        NewItemDescription = "";
                    }

                    // Only restore preview context, don't overwrite form inputs
                    _ = _mainViewModel.RestoreContextAsync(value.Content, value.AnchorData);
                }
            }
        }

        private string _newItemName;
        public string NewItemName { get => _newItemName; set => SetProperty(ref _newItemName, value); }

        private string _newItemDescription;
        public string NewItemDescription { get => _newItemDescription; set => SetProperty(ref _newItemDescription, value); }

        private DateTime _newItemStartDate = DateTime.UtcNow;
        public DateTime NewItemStartDate { get => _newItemStartDate; set => SetProperty(ref _newItemStartDate, value); }

        private DateTime _newItemEndDate = DateTime.UtcNow.AddDays(1);
        public DateTime NewItemEndDate { get => _newItemEndDate; set => SetProperty(ref _newItemEndDate, value); }

        // Edit mode tracking
        private bool _isEditing;
        public bool IsEditing
        {
            get => _isEditing;
            set
            {
                if (SetProperty(ref _isEditing, value))
                    OnPropertyChanged(nameof(IsReadOnly));
            }
        }
        public bool IsReadOnly => !_isEditing;

        private int? _editingTaskId;
        private int? _editingNoteId;

        private string _currentFilterPath;
        public string CurrentFilterPath
        {
            get => _currentFilterPath;
            set { if (SetProperty(ref _currentFilterPath, value)) { _ = LoadData(); } }
        }

        public ICommand RefreshCommand { get; }
        public ICommand AddTaskCommand { get; }
        public ICommand AddNoteCommand { get; }
        public ICommand ToggleCompletedCommand { get; }
        public ICommand OpenTaskViewCommand { get; }
        public ICommand EditTaskCommand { get; }
        public ICommand DeleteItemCommand { get; }
        public ICommand ViewOriginalContentCommand { get; }
        public ICommand SaveCommand { get; }

        public TaskPanelViewModel(ITaskService taskService, IProjectService projectService, MainViewModel mainViewModel)
        {
            _taskService = taskService;
            _projectService = projectService;
            _mainViewModel = mainViewModel;

            RefreshCommand = new AsyncRelayCommand(async () => await LoadData());
            AddTaskCommand = new AsyncRelayCommand(AddTask);
            AddNoteCommand = new AsyncRelayCommand(AddNote);

            ToggleCompletedCommand = new AsyncRelayCommand(ToggleCompleted);
            OpenTaskViewCommand = new AsyncRelayCommand(OpenTaskView);
            EditTaskCommand = new AsyncRelayCommand(EditTask);
            DeleteItemCommand = new AsyncRelayCommand(DeleteItem);
            ViewOriginalContentCommand = new RelayCommand(ViewOriginalContent);
            SaveCommand = new AsyncRelayCommand(SaveEdit);

            _taskService.TasksChanged += () =>
            {
                if (_suppressLoadData) return;
                System.Windows.Application.Current.Dispatcher.Invoke(async () => await LoadData());
            };
        }

        private bool _suppressLoadData = false;

        private async System.Threading.Tasks.Task SaveEdit()
        {
            if (!IsEditing) return;

            try
            {
                bool saved = false;
                _suppressLoadData = true;

                if (_editingTaskId.HasValue)
                {
                    var taskVM = Tasks.FirstOrDefault(t => t.Id == _editingTaskId.Value);
                    if (taskVM?.Model != null)
                    {
                        var task = taskVM.Model;
                        task.Name = NewItemName ?? "";
                        task.Description = NewItemDescription ?? "";
                        task.StartDate = NewItemStartDate;
                        task.EndDate = NewItemEndDate;

                        await _taskService.UpdateTaskAsync(task);
                        saved = true;
                    }
                }
                else if (_editingNoteId.HasValue)
                {
                    var note = Notes.FirstOrDefault(n => n.Id == _editingNoteId.Value);
                    if (note != null)
                    {
                        note.Title = NewItemName ?? "";
                        note.Content = NewItemDescription ?? "";

                        await _taskService.UpdateNoteAsync(note);
                        saved = true;
                    }
                }

                _suppressLoadData = false;

                if (saved)
                {
                    // Manually reload data to ensure sync with DB
                    await LoadData();

                    // Restore selection to the item we just edited
                    if (_editingTaskId.HasValue)
                    {
                        SelectedTask = Tasks.FirstOrDefault(t => t.Id == _editingTaskId.Value);
                    }
                    else if (_editingNoteId.HasValue)
                    {
                        SelectedNote = Notes.FirstOrDefault(n => n.Id == _editingNoteId.Value);
                    }

                    // Reset editing state after successful save
                    IsEditing = false;
                    _editingTaskId = null;
                    _editingNoteId = null;

                    // Clear form inputs
                    NewItemName = "";
                    NewItemDescription = "";
                    NewItemStartDate = DateTime.Today;
                    NewItemEndDate = DateTime.Today.AddDays(1);

                    // Show standard info only
                    // User might prefer no popup? "Saved successfully and Reloaded!". 
                    // Keeping to confirm persistence success visibly for now.
                    System.Windows.MessageBox.Show("Saved successfully and Reloaded!", "Success", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                _suppressLoadData = false;
                System.Windows.MessageBox.Show($"Error saving: {ex.Message}", "Save Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void ViewOriginalContent()
        {
            var selectedTask = _selectedTask;
            var selectedNote = _selectedNote;

            if (selectedTask?.Model != null)
            {
                var anchorData = selectedTask.Model.GetAnchorData();
                if (!string.IsNullOrEmpty(anchorData))
                {
                    _ = _mainViewModel.RestoreContextAsync(selectedTask.Model.Description, anchorData);
                }
                else if (!string.IsNullOrEmpty(selectedTask.Model.TargetFilePath))
                {
                    // Fallback to file preview if anchor is missing
                    _ = _mainViewModel.PreviewFile(selectedTask.Model.TargetFilePath);
                }
                else
                {
                    // Legacy fallback
                    _ = _mainViewModel.RestoreContextAsync(selectedTask.Model.Description, selectedTask.Model.Description);
                }
            }
            else if (selectedNote != null)
            {
                var anchorData = selectedNote.GetAnchorData();

                if (!string.IsNullOrEmpty(anchorData))
                {
                    _ = _mainViewModel.RestoreContextAsync(selectedNote.Content, anchorData);
                }
                else if (!string.IsNullOrEmpty(selectedNote.TargetFilePath))
                {
                    // Fallback to file preview if anchor is missing
                    _ = _mainViewModel.PreviewFile(selectedNote.TargetFilePath);
                }
                else
                {
                    System.Windows.MessageBox.Show("Cannot view original content: No file path or anchor data linked to this note.", "Info", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
            }
        }

        public async System.Threading.Tasks.Task LoadData()
        {
            Tasks.Clear();
            Notes.Clear();
            if (!_projectService.IsProjectOpen) return;

            int projectId = _projectService.CurrentProject.Id;
            List<ProjectTask> tasks;
            List<Note> notes;

            if (string.IsNullOrEmpty(CurrentFilterPath))
            {
                tasks = await _taskService.GetTasksByProjectAsync(projectId);
                notes = await _taskService.GetNotesByProjectAsync(projectId);
            }
            else
            {
                bool isFolder = System.IO.Directory.Exists(CurrentFilterPath);
                tasks = await _taskService.GetTasksByPathAsync(projectId, CurrentFilterPath, includeParents: true, includeChildren: isFolder);
                notes = await _taskService.GetNotesByPathAsync(projectId, CurrentFilterPath, includeParents: true, includeChildren: isFolder);
            }

            foreach (var t in tasks) Tasks.Add(new TaskItemViewModel(t));
            foreach (var n in notes) Notes.Add(n);
        }

        public void PrepareNewTaskFromPreview(string selection, string filePath)
        {
            if (string.IsNullOrWhiteSpace(selection)) return;

            // Switch to Tasks Tab
            // SelectedTabIndex = 1; // Property does not exist in ViewModel currently

            NewItemName = selection.Length > 100 ? selection.Substring(0, 97) + "..." : selection;
            NewItemDescription = ""; // Clean description as requested

            // Create a synthetic context so AddTask knows which file this came from!
            _preFilledContext = new SelectionContext
            {
                FilePath = filePath,
                SelectedText = selection,
                PreviewType = "Web" // Marker
            };
        }

        public void PrepareNewTask(SelectionContext context)
        {
            _preFilledContext = context;
            if (!string.IsNullOrWhiteSpace(context.SelectedText))
            {
                var text = context.SelectedText.Trim();
                if (text.Length > 100)
                    text = string.Concat(text.AsSpan(0, 100), "...");
                NewItemName = text;
                NewItemDescription = ""; // Link is stored in DB
            }
            else
            {
                NewItemName = System.IO.Path.GetFileName(context.FilePath);
                NewItemDescription = ""; // Link is stored in DB
            }
        }

        public void PrepareNewNote(SelectionContext context)
        {
            _preFilledContext = context;
            NewItemName = System.IO.Path.GetFileName(context.FilePath); // Note title usually filename or short summary
            // Pre-fill content with selection if available
            if (!string.IsNullOrWhiteSpace(context.SelectedText))
            {
                NewItemDescription = context.SelectedText;
            }
            else
            {
                NewItemDescription = "";
            }
        }

        public void FilterByPath(string path)
        {
            CurrentFilterPath = path;
        }

        private SelectionContext _preFilledContext;

        private async System.Threading.Tasks.Task AddTask()
        {
            if (!_projectService.IsProjectOpen || string.IsNullOrWhiteSpace(NewItemName)) return;

            string description = NewItemDescription ?? "";
            string targetPath = CurrentFilterPath;
            int? relatedFileId = null;

            var context = _preFilledContext ?? _mainViewModel.CaptureCurrentSelection();
            if (context != null)
            {
                targetPath = context.FilePath;
                relatedFileId = await _taskService.GetFileIdByPathAsync(_projectService.CurrentProject.Id, targetPath);
            }

            var task = await _taskService.CreateTaskAsync(_projectService.CurrentProject.Id, NewItemName, description, NewItemStartDate, NewItemEndDate, relatedFileId);

            if (task != null && !string.IsNullOrEmpty(targetPath))
            {
                task.TargetFilePath = targetPath;
                task.AnchorData = context?.ToJson();
                await _taskService.UpdateTaskAsync(task);
            }

            Tasks.Add(new TaskItemViewModel(task));

            NewItemName = "";
            NewItemDescription = "";
            _preFilledContext = null;
        }

        private async System.Threading.Tasks.Task AddNote()
        {
            if (!_projectService.IsProjectOpen || string.IsNullOrWhiteSpace(NewItemName)) return;

            string targetPath = CurrentFilterPath;

            var context = _preFilledContext ?? _mainViewModel.CaptureCurrentSelection();
            if (context != null)
            {
                targetPath = context.FilePath;
            }

            var note = await _taskService.CreateNoteAsync(_projectService.CurrentProject.Id, NewItemName, NewItemDescription);

            if (note != null && !string.IsNullOrEmpty(targetPath))
            {
                note.TargetFilePath = targetPath;
                note.AnchorData = context?.ToJson();
                await _taskService.UpdateNoteAsync(note);
            }

            Notes.Add(note);

            NewItemName = "";
            NewItemDescription = "";
            _preFilledContext = null;
        }

        private async System.Threading.Tasks.Task ToggleCompleted()
        {
            var selectedTask = SelectedTask;
            var selectedNote = SelectedNote;

            if (selectedTask?.Model != null)
            {
                string newStatus = selectedTask.Status == "Done" ? "Todo" : "Done";
                var task = selectedTask.Model;
                task.Status = newStatus;
                await _taskService.UpdateTaskAsync(task);
                selectedTask.Status = newStatus;
            }
            else if (selectedNote != null)
            {
                await LoadData();
            }
        }

        private async System.Threading.Tasks.Task OpenTaskView()
        {
            if (SelectedTask != null)
            {
                await _mainViewModel.RestoreContextAsync(SelectedTask.Model.Description, SelectedTask.Model.AnchorData);
            }
            else if (SelectedNote != null)
            {
                await _mainViewModel.RestoreContextAsync(SelectedNote.Content, SelectedNote.AnchorData);
            }
        }

        private async System.Threading.Tasks.Task EditTask()
        {
            if (_selectedTask != null)
            {
                _editingTaskId = _selectedTask.Id;
                _editingNoteId = null;

                // Populate form with selected task data for editing
                NewItemName = _selectedTask.Name ?? "";
                NewItemDescription = _selectedTask.Model?.Description ?? "";
                NewItemStartDate = _selectedTask.Model?.StartDate ?? DateTime.Today;
                NewItemEndDate = _selectedTask.Model?.EndDate ?? DateTime.Today.AddDays(1);
            }
            else if (_selectedNote != null)
            {
                _editingNoteId = _selectedNote.Id;
                _editingTaskId = null;

                // Populate form with selected note data for editing
                NewItemName = _selectedNote.Title ?? "";
                NewItemDescription = _selectedNote.Content ?? "";
            }
            else
            {
                return;
            }

            IsEditing = true;
            await System.Threading.Tasks.Task.CompletedTask;
        }

        private async System.Threading.Tasks.Task DeleteItem()
        {
            if (SelectedTask != null)
            {
                var result = System.Windows.MessageBox.Show(
                    $"Are you sure you want to delete task '{SelectedTask.Name}'?",
                    "Confirm Delete",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning);

                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    await _taskService.DeleteTaskAsync(SelectedTask.Id);
                    Tasks.Remove(SelectedTask);
                }
            }
            else if (SelectedNote != null)
            {
                var result = System.Windows.MessageBox.Show(
                    $"Are you sure you want to delete note '{SelectedNote.Title}'?",
                    "Confirm Delete",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning);

                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    await _taskService.DeleteNoteAsync(SelectedNote.Id);
                    Notes.Remove(SelectedNote);
                }
            }
        }
    }
}
