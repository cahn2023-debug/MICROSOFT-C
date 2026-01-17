using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OfflineProjectManager.Features.Preview
{
    public static class PreviewHelper
    {
        public static void ClearChildren(System.Windows.Controls.Panel panel)
        {
            panel.Children.Clear();
        }

        public static void DisconnectFromParent(UIElement element)
        {
            if (element == null) return;

            DependencyObject parent = LogicalTreeHelper.GetParent(element);
            if (parent is ContentControl cc) cc.Content = null;
            else if (parent is System.Windows.Controls.Panel p) p.Children.Remove(element);
            else if (parent is Decorator d) d.Child = null;

            DependencyObject visualParent = VisualTreeHelper.GetParent(element);
            if (visualParent is System.Windows.Controls.Panel vp) vp.Children.Remove(element);
        }
    }
}
