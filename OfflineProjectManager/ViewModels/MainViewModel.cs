using System;
using System.Windows.Input;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using OfflineProjectManager.Services;
using System.Windows;
using System.Windows.Controls;
using OfflineProjectManager.Features.Project.ViewModels;
using OfflineProjectManager.Features.Task.ViewModels;
using OfflineProjectManager.Features.Preview.Models;
using System.Collections.Generic;

namespace OfflineProjectManager.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly IProjectService _projectService;
        private readonly IPreviewService _previewService;

        public ProjectExplorerViewModel ExplorerViewModel { get; private set; }
        public TaskPanelViewModel TaskViewModel { get; private set; }
        public ResourceManagerViewModel ResourceViewModel { get; private set; } // Added

        private string _title = "Offline Project Manager";
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        private object _currentPreview;
        public object CurrentPreview
        {
            get => _currentPreview;
            set
            {
                // Dispose the previous preview control properly
                if (_currentPreview is FrameworkElement fe && fe.Tag is IDisposable prevControl)
                {
                    try { prevControl.Dispose(); } catch { }
                }
                else if (_currentPreview is IDisposable disposable)
                {
                    try { disposable.Dispose(); } catch { }
                }
                SetProperty(ref _currentPreview, value);
            }
        }

        private string _currentPreviewFilePath;
        public string CurrentPreviewFilePath
        {
            get => _currentPreviewFilePath;
            set
            {
                if (SetProperty(ref _currentPreviewFilePath, value))
                {
                    // Update IsRegionSelectionSupported based on file type
                    OnPropertyChanged(nameof(IsRegionSelectionSupported));
                }
            }
        }

        public bool IsRegionSelectionSupported
        {
            get
            {
                if (string.IsNullOrEmpty(CurrentPreviewFilePath)) return false;
                var ext = System.IO.Path.GetExtension(CurrentPreviewFilePath)?.ToLowerInvariant();
                return ext switch
                {
                    ".pdf" => true,
                    ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".ico" or ".webp" => true,
                    _ => false
                };
            }
        }

        public ICommand CreateProjectCommand { get; }
        public ICommand LoadProjectCommand { get; }
        public ICommand SaveProjectCommand { get; }
        public ICommand ShowProjectActionsCommand { get; }
        public ICommand OpenResourceManagerCommand { get; }

        public MainViewModel(IProjectService projectService, IPreviewService previewService, ISearchService searchService, ITaskService taskService)
        {
            _projectService = projectService;
            _previewService = previewService;

            ExplorerViewModel = new Features.Project.ViewModels.ProjectExplorerViewModel(_projectService, searchService, taskService, this);
            TaskViewModel = new TaskPanelViewModel(taskService, _projectService, this);
            ResourceViewModel = new ResourceManagerViewModel(_projectService); // Helper Init

            CreateProjectCommand = new AsyncRelayCommand(async () => await CreateProject());
            LoadProjectCommand = new AsyncRelayCommand(async () => await LoadProject());
            SaveProjectCommand = new AsyncRelayCommand(async () => await _projectService.SaveProjectAsync());
            ShowProjectActionsCommand = new RelayCommand(ShowProjectActions);
            OpenResourceManagerCommand = new AsyncRelayCommand(OpenResourceManager);
        }

        private async System.Threading.Tasks.Task OpenResourceManager()
        {
            if (!_projectService.IsProjectOpen)
            {
                System.Windows.MessageBox.Show("Please open a project first.");
                return;
            }

            await ResourceViewModel.LoadData();

            var dialog = new Views.ResourceManagerDialog
            {
                Owner = System.Windows.Application.Current.MainWindow,
                DataContext = ResourceViewModel
            };
            dialog.ShowDialog();
        }

        public OfflineProjectManager.Models.SelectionContext CaptureCurrentSelection()
        {
            if (CurrentPreview == null) return null;
            return SelectionExtractionService.GetSelection(CurrentPreview, CurrentPreviewFilePath);
        }

        public static OfflineProjectManager.Models.SelectionContext CreateContextFromPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            return new OfflineProjectManager.Models.SelectionContext
            {
                FilePath = path,
                PreviewType = "Native",
                SelectedText = ""
            };
        }

        public async System.Threading.Tasks.Task AddFolder()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select Folder to Add to Project",
                Multiselect = false
            };

            if (dialog.ShowDialog() == true)
            {
                string path = dialog.FolderName;
                try
                {
                    await _projectService.AddFolderAsync(path);
                    await ExplorerViewModel.LoadTree();
                }
                catch (System.Exception ex)
                {
                    System.Windows.MessageBox.Show($"Error adding folder: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        public async System.Threading.Tasks.Task PreviewFile(string path, string searchQuery = null, AnchorData anchor = null)
        {
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] PreviewFile called: {path}, query: {searchQuery ?? "(none)"}");

            CurrentPreviewFilePath = path;

            // Filter tasks by path
            TaskViewModel?.FilterByPath(path);

            try
            {
                // Pass searchQuery to enable highlight and auto-scroll
                System.Diagnostics.Debug.WriteLine("[MainViewModel] Calling CreatePreviewAsync...");
                var preview = await _previewService.CreatePreviewAsync(path, searchQuery);
                System.Diagnostics.Debug.WriteLine($"[MainViewModel] Preview result: {preview?.GetType().Name ?? "null"}");
                CurrentPreview = preview;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainViewModel] PreviewFile error: {ex}");
                CurrentPreview = null;
            }
        }

        public void RefreshTasks()
        {
            _ = TaskViewModel.LoadData();
        }

        public void ClearPreviewCache()
        {
            // No-op: simple preview service doesn't have cache
        }

        public async System.Threading.Tasks.Task RefreshPreview()
        {
            if (!string.IsNullOrEmpty(CurrentPreviewFilePath))
            {
                // No cache to clear in simple service
                await PreviewFile(CurrentPreviewFilePath);
            }
        }

        private readonly OfflineProjectManager.Models.SelectionContext _pendingContext;

        public async System.Threading.Tasks.Task RestoreContextAsync(string description, string anchorData = null)
        {
            if (string.IsNullOrWhiteSpace(description) && string.IsNullOrWhiteSpace(anchorData)) return;

            string json = anchorData;
            if (string.IsNullOrEmpty(json))
            {
                string marker = "<!--CONTEXT:";
                int start = description.IndexOf(marker);
                if (start >= 0)
                {
                    int jsonStart = start + marker.Length;
                    int end = description.IndexOf("-->", jsonStart);
                    if (end >= 0)
                    {
                        json = description[jsonStart..end];
                    }
                }
            }

            if (string.IsNullOrEmpty(json)) return;

            var context = OfflineProjectManager.Models.SelectionContext.FromJson(json);
            if (context != null && !string.IsNullOrEmpty(context.FilePath))
            {
                // Convert old SelectionContext to AnchorData
                var anchor = new AnchorData
                {
                    FileType = context.PreviewType,
                    ParagraphIndex = context.LineNumber > 0 ? context.LineNumber - 1 : null, // Mapped loosely
                    LineNumber = context.LineNumber,
                    CharOffset = context.SelectionStart,
                    CharLength = context.SelectionLength,
                    SheetName = context.SheetName,
                    CellRow = string.IsNullOrEmpty(context.CellRange) ? null : 1, // Basic mapping
                    SearchKeyword = "" // Not from search
                };

                await PreviewFile(context.FilePath, null, anchor);
            }
        }

        private async System.Threading.Tasks.Task CreateProject()
        {
            var dialog = new Views.CreateProjectDialog
            {
                Owner = System.Windows.Application.Current.MainWindow
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    await _projectService.CreateProjectAsync(dialog.ProjectName, dialog.FolderPath);
                    await _projectService.AddFolderAsync(dialog.FolderPath);
                    await ExplorerViewModel.LoadTree();
                    await TaskViewModel.LoadData();
                    await ResourceViewModel.LoadData(); // Added
                }
                catch (System.Exception ex)
                {
                    System.Windows.MessageBox.Show($"Error creating project: {ex.Message}");
                }
            }
        }

        private async System.Threading.Tasks.Task LoadProject()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Project Manager Files (*.pmp)|*.pmp"
            };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    await _projectService.LoadProjectAsync(dialog.FileName);
                    await ExplorerViewModel.LoadTree();
                    await TaskViewModel.LoadData();
                    await ResourceViewModel.LoadData(); // Added
                }
                catch (System.Exception ex)
                {
                    System.Windows.MessageBox.Show($"Error loading project: {ex.Message}");
                }
            }
        }

        public void ShowProjectActions()
        {
            var dialog = new Views.ProjectActionDialog
            {
                Owner = System.Windows.Application.Current.MainWindow,
                DataContext = new ProjectActionDialogViewModel(this)
            };
            dialog.ShowDialog();
        }

        public void CloseProject()
        {
            ExplorerViewModel?.Nodes?.Clear();
            TaskViewModel?.Tasks?.Clear();
            TaskViewModel?.Notes?.Clear();
            CurrentPreview = null;
            CurrentPreviewFilePath = null;
            Title = "Offline Project Manager";
        }
    }
}
