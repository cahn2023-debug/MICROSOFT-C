using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using OfflineProjectManager.Services;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using OfflineProjectManager.ViewModels;
using OfflineProjectManager.Models;
using CommunityToolkit.Mvvm.Input;

namespace OfflineProjectManager.Features.Project.ViewModels
{
    public class FileNode : ViewModelBase
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string Type { get; set; } // "File" or "Folder"

        private ObservableCollection<FileNode> _children = [];
        public ObservableCollection<FileNode> Children { get => _children; set => SetProperty(ref _children, value); }

        private bool _isExpanded;
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (SetProperty(ref _isExpanded, value) && value && Type == "Folder")
                {
                    // Fire and forget async callback with proper exception handling
                    _ = InvokeOnExpandedAsync();
                }
            }
        }

        private async System.Threading.Tasks.Task InvokeOnExpandedAsync()
        {
            try
            {
                if (OnExpandedAsync != null)
                {
                    await OnExpandedAsync(this);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FileNode] Error in OnExpandedAsync: {ex.Message}");
            }
        }

        public bool HasDummy => Children.Count == 1 && Children[0].Name == "Loading...";
        public bool IsLoaded { get; set; }

        /// <summary>
        /// Async callback invoked when node is expanded
        /// </summary>
        public Func<FileNode, System.Threading.Tasks.Task> OnExpandedAsync { get; set; }

        public void AddDummy()
        {
            Children.Clear();
            Children.Add(new FileNode { Name = "Loading...", Type = "Dummy" });
            IsLoaded = false;
        }
    }

    public class ProjectExplorerViewModel : ViewModelBase
    {
        private readonly IProjectService _projectService;
        private readonly ISearchService _searchService;
        private readonly ITaskService _taskService;
        private readonly MainViewModel _mainViewModel;

        private CancellationTokenSource _searchCts;
        private readonly Dictionary<string, List<OfflineProjectManager.Services.SearchMatch>> _searchMatchCache = new(StringComparer.OrdinalIgnoreCase);
        private System.Threading.Timer _debounceTimer;

        private string _selectedScopePath;

        private ObservableCollection<FileNode> _nodes;
        public ObservableCollection<FileNode> Nodes
        {
            get => _nodes;
            set => SetProperty(ref _nodes, value);
        }

        private string _searchQuery;
        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                if (SetProperty(ref _searchQuery, value))
                {
                    RestartSearchDebounce();
                }
            }
        }

        private void RestartSearchDebounce()
        {
            _debounceTimer?.Dispose();
            _debounceTimer = new System.Threading.Timer(_ =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(async () => await PerformSearch());
            }, null, 500, Timeout.Infinite);
        }

        private string _searchResultCount;
        public string SearchResultCount
        {
            get => _searchResultCount;
            set => SetProperty(ref _searchResultCount, value);
        }

        private string _currentSearchQuery;
        public string CurrentSearchQuery
        {
            get => _currentSearchQuery;
            set => SetProperty(ref _currentSearchQuery, value);
        }

        public ICommand SearchCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand FileSelectedCommand { get; }
        public ICommand AddFolderCommand { get; }
        public ICommand RemoveFolderCommand { get; }
        public ICommand AddFileCommand { get; }
        public ICommand OpenFileCommand { get; }
        public ICommand OpenContainingFolderCommand { get; }
        public ICommand CreateTaskCommand { get; }
        public ICommand SearchInFolderCommand { get; }

        public ProjectExplorerViewModel(IProjectService projectService, ISearchService searchService, ITaskService taskService, MainViewModel mainViewModel)
        {
            _projectService = projectService;
            _searchService = searchService;
            _taskService = taskService;
            _mainViewModel = mainViewModel;

            Nodes = [];

            SearchCommand = new AsyncRelayCommand(async () => await PerformSearch());
            RefreshCommand = new AsyncRelayCommand(async () => await LoadTree());
            FileSelectedCommand = new RelayCommand<object>(OnFileSelected);
            AddFolderCommand = new AsyncRelayCommand(async () => await _mainViewModel.AddFolder());

            RemoveFolderCommand = new AsyncRelayCommand(async (param) => await RemoveFolder(param));
            AddFileCommand = new AsyncRelayCommand(async (param) => await AddFile(param));
            OpenFileCommand = new RelayCommand<object>(OpenFile);
            OpenContainingFolderCommand = new RelayCommand<object>(OpenContainingFolder);
            CreateTaskCommand = new AsyncRelayCommand(async (param) => await CreateTask(param));
            SearchInFolderCommand = new RelayCommand<object>(SetSearchScope);
        }

        public async System.Threading.Tasks.Task LoadTree()
        {
            if (!_projectService.IsProjectOpen)
            {
                Nodes.Clear();
                return;
            }

            var roots = new ObservableCollection<FileNode>();
            foreach (var folder in _projectService.GetProjectFolders())
            {
                var root = CreateFolderNode(folder);

                // CRITICAL FIX: Load children BEFORE IsExpanded
                // Otherwise callback won't fire (property already true)
                await LoadSubNodes(root);

                root.IsExpanded = true;
                roots.Add(root);
            }
            Nodes = roots;
        }

        private FileNode CreateFolderNode(string path)
        {
            var node = new FileNode
            {
                Name = System.IO.Path.GetFileName(path) ?? path,
                Path = path,
                Type = "Folder",
                OnExpandedAsync = LoadSubNodes
            };
            node.AddDummy();
            return node;
        }

        private async System.Threading.Tasks.Task LoadSubNodes(FileNode node)
        {
            if (node.IsLoaded) return;

            try
            {
                if (!System.IO.Directory.Exists(node.Path))
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        node.Children.Clear();
                        node.IsLoaded = true;
                    });
                    return;
                }

                var dirs = await _projectService.GetDirectoriesAsync(node.Path).ConfigureAwait(false);
                var files = await _projectService.GetFilesAsync(node.Path).ConfigureAwait(false);

                var newChildren = new List<FileNode>();
                foreach (var d in dirs)
                {
                    newChildren.Add(CreateFolderNode(d));
                }
                foreach (var f in files)
                {
                    newChildren.Add(new FileNode
                    {
                        Name = System.IO.Path.GetFileName(f),
                        Path = f,
                        Type = "File"
                    });
                }

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    node.Children.Clear(); // Clears dummy or old items
                    foreach (var child in newChildren)
                    {
                        node.Children.Add(child);
                    }
                    node.IsLoaded = true;
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TreeLoad ERROR] Failed to load {node.Path}: {ex.ToString()}");
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    node.Children.Clear();
                    node.Children.Add(new FileNode { Name = $"Error: {ex.Message}", Type = "Error" });
                    node.IsLoaded = true;
                });
            }
        }

        private async System.Threading.Tasks.Task PerformSearch()
        {
            if (!_projectService.IsProjectOpen) return;

            if (string.IsNullOrWhiteSpace(SearchQuery))
            {
                _searchCts?.Cancel();
                _searchCts?.Dispose();
                _searchCts = null;
                SearchResultCount = string.Empty;
                CurrentSearchQuery = null;
                _searchMatchCache.Clear();

                // FIX SH-04: Clear preview cache to remove stale highlights
                _mainViewModel.ClearPreviewCache();

                // CRITICAL FIX: Reset search scope to global when clearing search
                _selectedScopePath = null;

                await LoadTree();
                return;
            }

            _searchCts?.Cancel();
            _searchCts?.Dispose();
            _searchCts = new CancellationTokenSource();
            var token = _searchCts.Token;
            CurrentSearchQuery = SearchQuery;
            _searchMatchCache.Clear();

            string scopePath = _selectedScopePath;

            var rootMap = new Dictionary<string, FileNode>(StringComparer.OrdinalIgnoreCase);
            var matchedFilesSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var lastUpdate = DateTime.MinValue;
            var matchBuffer = new List<(string path, List<OfflineProjectManager.Services.SearchMatch> matches)>();

            void FlushResults()
            {
                if (matchBuffer.Count == 0) return;

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    foreach (var (path, matches) in matchBuffer)
                    {
                        if (matchedFilesSet.Add(path))
                        {
                            _searchMatchCache[path] = matches;
                            AddPathToTree(rootMap, path, matches.Count);
                        }
                    }

                    // Auto-refresh preview if the current file is in the search results
                    // This creates a seamless "Find in Preview" experience via the Global Search
                    if (!string.IsNullOrEmpty(_mainViewModel?.CurrentPreviewFilePath))
                    {
                        var normalizedCurrent = System.IO.Path.GetFullPath(_mainViewModel.CurrentPreviewFilePath);
                        // Check if any of the NEWLY added items match the current file
                        var match = matchBuffer.FirstOrDefault(x => string.Equals(System.IO.Path.GetFullPath(x.path), normalizedCurrent, StringComparison.OrdinalIgnoreCase));

                        if (match.path != null) // Found in this batch
                        {
                            // Fire and forget async preview update
                            _ = _mainViewModel.PreviewFile(match.path, SearchQuery);
                        }
                    }

                    matchBuffer.Clear();
                    Nodes = new ObservableCollection<FileNode>(rootMap.Values.OrderBy(n => n.Name));
                    SearchResultCount = $"{matchedFilesSet.Count} file{(matchedFilesSet.Count != 1 ? "s" : "")} found";
                });
            }

            void EmitMatchToTree(string path, List<OfflineProjectManager.Services.SearchMatch> matches)
            {
                lock (matchBuffer) { matchBuffer.Add((path, matches)); }
                if (matchBuffer.Count >= 25 || (DateTime.UtcNow - lastUpdate).TotalMilliseconds > 200)
                {
                    lastUpdate = DateTime.UtcNow;
                    FlushResults();
                }
            }

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                Nodes.Clear();
                SearchResultCount = "Searching...";
            });

            try
            {
                await _searchService.SearchFilesAsync(
                    SearchQuery,
                    _projectService.CurrentProject.Id,
                    scopePath,
                    token,
                    (file, matches) =>
                    {
                        if (file == null) return;
                        if (matches != null) _searchMatchCache[file.Path] = matches;
                        EmitMatchToTree(file.Path, matches);
                    },
                    maxResults: 5000);

                FlushResults();

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    if (matchedFilesSet.Count == 0) SearchResultCount = "No files found";
                    else SearchResultCount = $"{matchedFilesSet.Count} file{(matchedFilesSet.Count != 1 ? "s" : "")} found";
                });
            }
            catch (OperationCanceledException)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() => { SearchResultCount = "Search cancelled"; });
            }
        }

        private void AddPathToTree(Dictionary<string, FileNode> rootMap, string filePath, int matchCount)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return;
            var projectFolders = _projectService.GetProjectFolders();
            string projectRoot = null;

            foreach (var folder in projectFolders)
            {
                var normalizedFolder = System.IO.Path.GetFullPath(folder).TrimEnd(System.IO.Path.DirectorySeparatorChar);
                var normalizedFile = System.IO.Path.GetFullPath(filePath);
                if (normalizedFile.StartsWith(normalizedFolder, StringComparison.OrdinalIgnoreCase)) { projectRoot = folder; break; }
            }

            if (projectRoot == null) return;

            var dir = System.IO.Path.GetDirectoryName(filePath);
            if (string.IsNullOrEmpty(dir)) return;

            if (!rootMap.TryGetValue(projectRoot, out var rootNode))
            {
                rootNode = new FileNode
                {
                    Name = System.IO.Path.GetFileName(projectRoot.TrimEnd(System.IO.Path.DirectorySeparatorChar)) ?? projectRoot,
                    Path = projectRoot,
                    Type = "Folder",
                    IsExpanded = true
                };
                rootMap[projectRoot] = rootNode;
            }

            var current = rootNode;
            var normalizedRoot = System.IO.Path.GetFullPath(projectRoot).TrimEnd(System.IO.Path.DirectorySeparatorChar);
            var normalizedDir = System.IO.Path.GetFullPath(dir).TrimEnd(System.IO.Path.DirectorySeparatorChar);

            if (normalizedDir.Length > normalizedRoot.Length)
            {
                var relative = normalizedDir[normalizedRoot.Length..].Trim(System.IO.Path.DirectorySeparatorChar);
                if (!string.IsNullOrEmpty(relative))
                {
                    var parts = relative.Split(System.IO.Path.DirectorySeparatorChar);
                    var currentPath = projectRoot;
                    foreach (var p in parts)
                    {
                        if (string.IsNullOrWhiteSpace(p)) continue;
                        currentPath = System.IO.Path.Combine(currentPath, p);
                        var next = current.Children.FirstOrDefault(x => x.Type == "Folder" && x.Path.Equals(currentPath, StringComparison.OrdinalIgnoreCase));
                        if (next == null)
                        {
                            next = new FileNode
                            {
                                Name = p,
                                Path = currentPath,
                                Type = "Folder",
                                IsExpanded = true,
                                IsLoaded = true
                            };
                            current.Children.Add(next);
                        }
                        current = next;
                    }
                }
            }

            var fileName = System.IO.Path.GetFileName(filePath);
            var display = matchCount > 0 ? $"{fileName} ({matchCount})" : fileName;
            var existingFile = current.Children.FirstOrDefault(x => x.Type == "File" && x.Path.Equals(filePath, StringComparison.OrdinalIgnoreCase));
            if (existingFile == null) current.Children.Add(new FileNode
            {
                Name = display,
                Path = filePath,
                Type = "File"
            });
            else existingFile.Name = display;
        }

        private void OnFileSelected(object nodeObj)
        {
            System.Diagnostics.Debug.WriteLine($"[ProjectExplorerVM] OnFileSelected called: {nodeObj?.GetType().Name ?? "null"}");

            if (nodeObj is FileNode node)
            {
                System.Diagnostics.Debug.WriteLine($"[ProjectExplorerVM] FileNode: Type={node.Type}, Path={node.Path}");

                if (node.Type == "File")
                {
                    _searchMatchCache.TryGetValue(node.Path, out var matches);
                    System.Diagnostics.Debug.WriteLine($"[ProjectExplorerVM] Calling PreviewFile for: {node.Path}");

                    // FIX: Use CurrentSearchQuery with fallback to SearchQuery for highlighting
                    string queryToHighlight = !string.IsNullOrWhiteSpace(CurrentSearchQuery)
                        ? CurrentSearchQuery
                        : SearchQuery;

                    // Fire and forget async call
                    _ = _mainViewModel.PreviewFile(node.Path, queryToHighlight);
                }
                else if (node.Type == "Folder")
                {
                    System.Diagnostics.Debug.WriteLine("[ProjectExplorerVM] Folder selected, clearing preview");
                    // Clear preview content
                    _mainViewModel.CurrentPreview = null;
                    _mainViewModel.CurrentPreviewFilePath = null;

                    // Show ALL tasks/notes in the project (not filtered by path)
                    _mainViewModel.TaskViewModel.FilterByPath(null);
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[ProjectExplorerVM] nodeObj is not FileNode");
            }
        }

        // CRITICAL FIX: Explicit method to set search scope (only called from context menu)
        private void SetSearchScope(object parameter)
        {
            if (parameter is FileNode node && node.Type == "Folder")
            {
                _selectedScopePath = node.Path;
                // Trigger search with new scope
                SearchCommand.Execute(null);
            }
        }

        private async System.Threading.Tasks.Task AddFile(object param)
        {
            if (param is FileNode node && node.Type == "Folder")
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Create New File",
                    InitialDirectory = node.Path,
                    FileName = "NewFile.txt",
                    Filter = "All files (*.*)|*.*|Text files (*.txt)|*.txt|Python files (*.py)|*.py|C# files (*.cs)|*.cs"
                };

                if (dialog.ShowDialog() == true)
                {
                    try
                    {
                        var path = dialog.FileName;
                        if (!System.IO.File.Exists(path)) System.IO.File.Create(path).Close();
                        await LoadTree();
                    }
                    catch (Exception ex)
                    {
                        System.Windows.MessageBox.Show($"Error creating file: {ex.Message}");
                    }
                }
            }
        }

        private async System.Threading.Tasks.Task RemoveFolder(object param)
        {
            if (param is FileNode node && node.Type == "Folder")
            {
                var nodePath = System.IO.Path.GetFullPath(node.Path).TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
                var roots = _projectService.GetProjectFolders();

                bool isRoot = roots.Any(r =>
                {
                    var rootPath = System.IO.Path.GetFullPath(r).TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
                    return string.Equals(rootPath, nodePath, StringComparison.OrdinalIgnoreCase);
                });

                if (isRoot)
                {
                    var result = System.Windows.MessageBox.Show(
                        $"Are you sure you want to remove folder '{node.Name}' from the project?\n(Files on disk will NOT be deleted)",
                        "Confirm Remove Folder",
                        System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Question);

                    if (result == System.Windows.MessageBoxResult.Yes)
                    {
                        await _projectService.RemoveFolderAsync(node.Path);
                        await LoadTree();
                    }
                }
                else
                {
                    System.Windows.MessageBox.Show("Cannot remove sub-folders. Only top-level project folders can be removed.");
                }
            }
        }

        private void OpenFile(object param)
        {
            if (param is FileNode node && node.Type == "File")
            {
                try
                {
                    if (System.IO.File.Exists(node.Path))
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = node.Path, UseShellExecute = true });
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Error opening file: {ex.Message}");
                }
            }
        }

        private void OpenContainingFolder(object param)
        {
            string path = (param as FileNode)?.Path;
            if (!string.IsNullOrEmpty(path))
            {
                try
                {
                    path = path.Replace('/', '\\');
                    if (System.IO.File.Exists(path) || System.IO.Directory.Exists(path))
                        System.Diagnostics.Process.Start("explorer.exe", $"/select, \"{path}\"");
                }
                catch { }
            }
        }

        private async System.Threading.Tasks.Task CreateTask(object param)
        {
            if (param is FileNode node && node.Type == "File")
            {
                var projectId = _projectService.CurrentProject.Id;
                if (_taskService != null)
                {
                    await _taskService.CreateTaskFromFileAsync(projectId, node.Path);
                    _mainViewModel.RefreshTasks();
                }
            }
        }
    }
}
