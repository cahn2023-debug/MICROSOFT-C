using System.Windows;
using System.Windows.Input;

namespace OfflineProjectManager.Views
{
    public partial class ProjectActionDialog : Window
    {
        public ProjectActionDialog()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Focus the window for keyboard input
            this.Focus();
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // Close on Escape
            if (e.Key == Key.Escape)
            {
                this.Close();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
