using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using OfflineProjectManager.Models;

namespace OfflineProjectManager.Views.Controls
{
    public partial class ActionItemControl : System.Windows.Controls.UserControl
    {
        public static readonly DependencyProperty IsPressedProperty =
            DependencyProperty.Register(nameof(IsPressed), typeof(bool), typeof(ActionItemControl));

        public bool IsPressed
        {
            get => (bool)GetValue(IsPressedProperty);
            set => SetValue(IsPressedProperty, value);
        }

        public ActionItemControl()
        {
            InitializeComponent();
        }

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            IsPressed = true;
        }

        private void Border_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            IsPressed = false;

            // Execute the command if available
            if (DataContext is ActionItem actionItem && actionItem.Command != null)
            {
                if (actionItem.Command.CanExecute(null))
                {
                    actionItem.Command.Execute(null);

                    // Close the parent dialog
                    Window.GetWindow(this)?.Close();
                }
            }
        }
    }
}
