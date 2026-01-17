using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using OfflineProjectManager.Features.Task.ViewModels;

namespace OfflineProjectManager.Views
{
    public partial class TaskPanelView : System.Windows.Controls.UserControl
    {
        public TaskPanelView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Double-click on Notes list triggers Edit command directly
        /// </summary>
        private void NotesListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is System.Windows.Controls.ListBox listBox && listBox.SelectedItem != null)
            {
                // Execute Edit command directly on double-click
                if (DataContext is TaskPanelViewModel vm && vm.EditTaskCommand.CanExecute(null))
                {
                    vm.EditTaskCommand.Execute(null);
                }
            }
        }

        /// <summary>
        /// Double-click on Tasks list triggers Edit command directly
        /// </summary>
        private void TasksListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is System.Windows.Controls.ListBox listBox && listBox.SelectedItem != null)
            {
                // Execute Edit command directly on double-click
                if (DataContext is TaskPanelViewModel vm && vm.EditTaskCommand.CanExecute(null))
                {
                    vm.EditTaskCommand.Execute(null);
                }
            }
        }
    }
}
