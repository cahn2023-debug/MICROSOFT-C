using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using OfflineProjectManager.ViewModels;
using OfflineProjectManager.Services;
using OfflineProjectManager.Utils;

namespace OfflineProjectManager.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // Phase 5: Blazor WebView removed, now using LiveCharts2 Gantt

            // Subscribe to DataContext changed to update menu state
            this.DataContextChanged += MainWindow_DataContextChanged;
        }

        private void MainWindow_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                // Subscribe to project changes
                vm.ExplorerViewModel.PropertyChanged += (s, args) =>
                {
                    if (args.PropertyName == nameof(vm.ExplorerViewModel.Nodes))
                    {
                        // Menu state tracking removed
                    }
                };


            }
        }



        private void Preview_MouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            if (System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Control)
            {
                if (e.Delta > 0)
                {
                    ZoomSlider.Value = System.Math.Min(ZoomSlider.Maximum, ZoomSlider.Value + 0.1);
                }
                else
                {
                    ZoomSlider.Value = System.Math.Max(ZoomSlider.Minimum, ZoomSlider.Value - 0.1);
                }
                e.Handled = true;
            }
        }

        private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (PreviewScaleTransform != null)
            {
                PreviewScaleTransform.ScaleX = e.NewValue;
                PreviewScaleTransform.ScaleY = e.NewValue;
            }
        }

        private void FindCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            // Delegate focus to Project Explorer's Search Box
            if (ProjectExplorerPanel.Content is Grid)
            {
                // Execute find command on the UserControl, let its internal binding handle it
                ApplicationCommands.Find.Execute(null, ProjectExplorerPanel);
            }
        }

        // ========== Menu Handlers ==========

        private void ProjectMenuButton_Click(object sender, RoutedEventArgs e)
        {
            // Open dropdown menu below the button (VS Code style)
            if (sender is System.Windows.Controls.Button button && button.ContextMenu != null)
            {
                // Ensure window is restored if minimized
                if (this.WindowState == WindowState.Minimized)
                {
                    this.WindowState = WindowState.Normal;
                }

                button.ContextMenu.PlacementTarget = button;
                // Use Bottom placement to show menu below the button
                button.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                button.ContextMenu.HorizontalOffset = 0;
                button.ContextMenu.VerticalOffset = 0;
                button.ContextMenu.IsOpen = true;
            }
        }

        private void MenuExit_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Application.Current.Shutdown();
        }

        private void MenuCloseProject_Click(object sender, RoutedEventArgs e)
        {
            // Close project - clear UI state
            if (DataContext is MainViewModel vm)
            {
                vm.CloseProject();
            }
        }

        private async void MenuAddFolder_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                await vm.AddFolder();
            }
        }

        private void TogglePanel_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is string panelName)
            {
                TogglePanelVisibility(panelName, Visibility.Visible);
            }
        }

        private void TogglePanel_Unchecked(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is string panelName)
            {
                TogglePanelVisibility(panelName, Visibility.Collapsed);
            }
        }

        private void TogglePanelVisibility(string panelName, Visibility visibility)
        {
            switch (panelName)
            {
                case "ProjectExplorerPanel":
                    ProjectExplorerPanel.Visibility = visibility;
                    break;
                case "FilePreviewPanel":
                    FilePreviewPanel.Visibility = visibility;
                    break;
                case "TaskManagerPanel":
                    TaskManagerPanel.Visibility = visibility;
                    break;
                case "TimelinePanel":
                    TimelinePanel.Visibility = visibility;
                    ZoomSlider.Value = 1.0;
                    break;
            }
        }

        private void Command_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm && vm.CurrentPreview != null)
            {
                e.CanExecute = true;
            }
        }

        private void AddToTask_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                var context = vm.CaptureCurrentSelection();

                // Fallback: If no selection (e.g. clicked inside Native Preview), try to use CommandParameter
                if (context == null && e.Parameter is string path)
                {
                    // Attempt to create context from path
                    context = MainViewModel.CreateContextFromPath(path);
                }

                if (context != null)
                {
                    vm.TaskViewModel.PrepareNewTask(context);

                    // Show Task Panel
                    if (TaskManagerPanel != null)
                    {
                        TaskManagerPanel.Visibility = Visibility.Visible;
                    }
                }
            }
        }

        private void AddToNote_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                var context = vm.CaptureCurrentSelection();
                if (context != null)
                {
                    vm.TaskViewModel.PrepareNewNote(context);

                    // Show Task Panel
                    if (TaskManagerPanel != null)
                    {
                        TaskManagerPanel.Visibility = Visibility.Visible;
                    }
                }
            }
        }

        private void MenuToggleTheme_Click(object sender, RoutedEventArgs e)
        {
            var appResources = System.Windows.Application.Current.Resources;
            var mergedDicts = appResources.MergedDictionaries;

            // Identify current theme by looking for specific key or source
            // We assume Dark is default. Logic: If Light is present, switch to Dark, else Light.
            // Since we can't easily check Source URI if added dynamically without tracking, we can check a Color key value?
            // Or just check if we have loaded LightTheme.

            // Better: Check a flag or tag. Or simply toggle based on Assumption.
            // Let's check "BackgroundColor" color.
            bool isDark = true;
            if (appResources["BackgroundBrush"] is System.Windows.Media.SolidColorBrush bgBrush)
            {
                // Dark background is approx #1E1E1E (30,30,30)
                // Light is White #FFFFFFFF
                if (bgBrush.Color.R > 200) isDark = false;
            }

            mergedDicts.Clear();

            // Always reload icons (generic) - Wait, Icons.xaml is separate.
            // We should only clear Theme dictionaries.
            // But we didn't separate them in code logic perfectly.
            // Re-add Icons.
            mergedDicts.Add(new ResourceDictionary { Source = new System.Uri("Resources/Icons.xaml", System.UriKind.Relative) });

            if (isDark)
            {
                // Switch to Light
                mergedDicts.Add(new ResourceDictionary { Source = new System.Uri("Resources/LightTheme.xaml", System.UriKind.Relative) });
                ThemeService.IsDarkMode = false;
            }
            else
            {
                // Switch to Dark
                mergedDicts.Add(new ResourceDictionary { Source = new System.Uri("Resources/DarkTheme.xaml", System.UriKind.Relative) });
                ThemeService.IsDarkMode = true;
            }

            // Trigger preview refresh if available
            if (DataContext is MainViewModel vm && vm.CurrentPreview != null)
            {
                // Re-preview current file to apply new theme
                _ = vm.RefreshPreview();
            }
        }

        // ========== Activity Bar Handlers ==========
        private void ActivityButton_Checked(object sender, RoutedEventArgs e)
        {
            // Panel visibility is handled by XAML bindings
            // This handler can be used to add additional logic when switching views
        }

        // ========== Region Selection Toggle Handlers ==========
        private void SelectRegionToggle_Checked(object sender, RoutedEventArgs e)
        {
            // Enable region selection mode
            Features.Preview.PreviewContextMenuHelper.SetRegionSelectionActive(true);
        }

        private void SelectRegionToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            // Disable region selection mode
            Features.Preview.PreviewContextMenuHelper.SetRegionSelectionActive(false);
        }

        // ========== Gantt Expand Toggle ==========
        private bool _isGanttExpanded = false;
        private GridLength _savedRow0Height;
        private GridLength _savedRow2Height;

        /// <summary>
        /// Toggle Gantt chart between normal and full-screen mode
        /// </summary>
        public void ToggleGanttExpand()
        {
            if (this.Content is not DockPanel mainGrid) return;

            // Find the main content Grid (child of DockPanel)
            Grid contentGrid = null;
            foreach (var child in mainGrid.Children)
            {
                if (child is Grid g)
                {
                    contentGrid = g;
                    break;
                }
            }
            if (contentGrid == null || contentGrid.RowDefinitions.Count < 3) return;

            _isGanttExpanded = !_isGanttExpanded;

            if (_isGanttExpanded)
            {
                // Save current row heights
                _savedRow0Height = contentGrid.RowDefinitions[0].Height;
                _savedRow2Height = contentGrid.RowDefinitions[2].Height;

                // Hide row 0 (main content area), maximize row 2 (Gantt)
                contentGrid.RowDefinitions[0].Height = new GridLength(0);
                contentGrid.RowDefinitions[1].Height = new GridLength(0); // splitter
                contentGrid.RowDefinitions[2].Height = new GridLength(1, GridUnitType.Star);

                // Hide other columns to focus on Gantt
                ProjectExplorerPanel.Visibility = Visibility.Collapsed;
                FilePreviewPanel.Visibility = Visibility.Collapsed;
                TaskManagerPanel.Visibility = Visibility.Collapsed;

                // Update button state
                GanttChart.SetExpandedState(true);

                // Force redraw and reset scroll position
                GanttChart.ForceRedraw();
            }
            else
            {
                // Restore original layout
                contentGrid.RowDefinitions[0].Height = _savedRow0Height;
                contentGrid.RowDefinitions[1].Height = GridLength.Auto;
                contentGrid.RowDefinitions[2].Height = _savedRow2Height;

                // Show all panels again
                ProjectExplorerPanel.Visibility = Visibility.Visible;
                FilePreviewPanel.Visibility = Visibility.Visible;
                TaskManagerPanel.Visibility = Visibility.Visible;

                // Update button state
                GanttChart.SetExpandedState(false);
            }
        }
    }
}
