using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using OfflineProjectManager.Models;
using OfflineProjectManager.ViewModels;

namespace OfflineProjectManager.ViewModels
{
    /// <summary>
    /// ViewModel for the ProjectActionDialog
    /// </summary>
    public class ProjectActionDialogViewModel
    {
        public ObservableCollection<ActionGroup> ActionGroups { get; set; }

        public ProjectActionDialogViewModel(MainViewModel mainViewModel)
        {
            ActionGroups = new ObservableCollection<ActionGroup>
            {
                // PROJECT group
                new ActionGroup
                {
                    Header = "PROJECT",
                    Items = new System.Collections.Generic.List<ActionItem>
                    {
                        new ActionItem
                        {
                            Icon = "🆕",
                            Name = "New Project...",
                            Shortcut = "Ctrl+Shift+N",
                            Command = mainViewModel.CreateProjectCommand
                        },
                        new ActionItem
                        {
                            Icon = "📂",
                            Name = "Open Project...",
                            Shortcut = "Ctrl+O",
                            Command = mainViewModel.LoadProjectCommand
                        },
                        new ActionItem
                        {
                            Icon = "💾",
                            Name = "Save Project",
                            Shortcut = "Ctrl+S",
                            Command = mainViewModel.SaveProjectCommand
                        },
                        new ActionItem
                        {
                            Icon = "❌",
                            Name = "Close Project",
                            Shortcut = "Ctrl+Shift+W",
                            Command = new RelayCommand(() => mainViewModel.CloseProject())
                        }
                    }
                },
                // WORKSPACE group
                new ActionGroup
                {
                    Header = "WORKSPACE",
                    Items = new System.Collections.Generic.List<ActionItem>
                    {
                        new ActionItem
                        {
                            Icon = "➕",
                            Name = "Add Folder...",
                            Shortcut = "Ctrl+K",
                            Command = new RelayCommand(async () => await mainViewModel.AddFolder())
                        }
                    }
                },
                // SYSTEM group
                new ActionGroup
                {
                    Header = "SYSTEM",
                    Items = new System.Collections.Generic.List<ActionItem>
                    {
                        new ActionItem
                        {
                            Icon = "🚪",
                            Name = "Exit Application",
                            Shortcut = "Alt+F4",
                            Command = new RelayCommand(() => System.Windows.Application.Current.Shutdown())
                        }
                    }
                }
            };
        }
    }
}
