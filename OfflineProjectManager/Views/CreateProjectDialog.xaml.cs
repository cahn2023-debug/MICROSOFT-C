using System.Windows;

namespace OfflineProjectManager.Views
{
    public partial class CreateProjectDialog : Window
    {
        public string ProjectName { get; private set; }
        public string FolderPath { get; private set; }

        public CreateProjectDialog()
        {
            InitializeComponent();
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select Folder for Project"
            };

            if (dialog.ShowDialog() == true)
            {
                FolderPath = dialog.FolderName;
                FolderPathTextBox.Text = FolderPath;
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ProjectNameTextBox.Text))
            {
                System.Windows.MessageBox.Show("Please enter a project name.", "Validation Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(FolderPathTextBox.Text))
            {
                System.Windows.MessageBox.Show("Please select a folder.", "Validation Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            ProjectName = ProjectNameTextBox.Text.Trim();
            FolderPath = FolderPathTextBox.Text.Trim();
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
