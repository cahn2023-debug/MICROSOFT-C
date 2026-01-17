using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace OfflineProjectManager.Utils
{
    public class HighlightConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null) return System.Windows.Media.Brushes.Transparent;

            string text = value.ToString();
            string keyword = parameter.ToString();

            if (string.IsNullOrEmpty(keyword)) return System.Windows.Media.Brushes.Transparent;

            string textNoAccent = VietnameseTextHelper.RemoveAccents(text);
            string keywordNoAccent = VietnameseTextHelper.RemoveAccents(keyword);

            if (textNoAccent.Contains(keywordNoAccent, StringComparison.OrdinalIgnoreCase))
            {
                return new SolidColorBrush(System.Windows.Media.Color.FromArgb(100, 255, 255, 0)); // Light Yellow
            }

            return System.Windows.Media.Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
