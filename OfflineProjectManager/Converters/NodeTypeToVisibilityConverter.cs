using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using OfflineProjectManager.ViewModels;
using OfflineProjectManager.Features.Project.ViewModels;

namespace OfflineProjectManager.Converters
{
    public class NodeTypeToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Value should be the FileNode or the Type string
            // Parameter should be the target type ("File" or "Folder")

            if (value is string typeStr && parameter is string targetStr)
            {
                if (string.Equals(typeStr, targetStr, StringComparison.OrdinalIgnoreCase))
                    return Visibility.Visible;
            }

            // If value is FileNode, checking property
            if (value is FileNode node && parameter is string targetStr2)
            {
                if (string.Equals(node.Type, targetStr2, StringComparison.OrdinalIgnoreCase))
                    return Visibility.Visible;
            }

            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
