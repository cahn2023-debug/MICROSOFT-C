using System.Windows.Controls;

namespace OfflineProjectManager.Views
{
    public partial class ProjectExplorerView : System.Windows.Controls.UserControl
    {
        public ProjectExplorerView()
        {
            InitializeComponent();
        }

        private void Find_Executed(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
        {
            SearchTextBox.Focus();
            SearchTextBox.SelectAll();
        }
    }
}
