using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace OfflineProjectManager.Converters
{
    public class FileToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Value is full path or filename, or DirectoryInfo/FileInfo object if binding was direct.
            // In ProjectExplorerViewModel, Nodes likely have a "Name" or "Path" property.
            // Or the Node object itself. Assume input is "IsDirectory" + "Name" or we bind to the Node.
            // Let's assume we bind to the Node object or Tuple. 
            // Actually, we can likely bind to "IsDirectory" boolean and "Name" string using MultiBinding?
            // Or, simpler: Bind to the ViewModel node which has properties.

            // Let's verify ProjectExplorerViewModel Node structure.
            // If we don't know it, we'll try to handle common cases.
            
            // Assume binding is to the Node object, let's reflect "IsDirectory" and "Name".
            
            if (value == null) return System.Windows.Application.Current.Resources["Icon_File"];

            bool isDirectory = false;
            string name = "";

            // Reflection to be safe if we don't want to add dependency on ViewModel DLL in Converter (though we are in same project)
            var type = value.GetType();
            var propIsDirectory = type.GetProperty("IsDirectory");
            var propName = type.GetProperty("Name");

            if (propIsDirectory != null) isDirectory = (bool)propIsDirectory.GetValue(value);
            if (propName != null) name = (string)propName.GetValue(value);

            if (isDirectory)
            {
                return System.Windows.Application.Current.Resources["Icon_Folder"]; // Geometry
            }
            
            string ext = Path.GetExtension(name).ToLower();
            switch (ext)
            {
                case ".cs":
                case ".py":
                case ".xml":
                case ".json":
                case ".html":
                case ".css":
                case ".js":
                case ".xaml":
                    return System.Windows.Application.Current.Resources["Icon_Code"];
                case ".png":
                case ".jpg":
                case ".jpeg":
                case ".bmp":
                case ".gif":
                    return System.Windows.Application.Current.Resources["Icon_Image"];
                case ".xlsx":
                case ".xls":
                case ".csv":
                    return System.Windows.Application.Current.Resources["Icon_Excel"];
                case ".dwg":
                case ".dxf":
                    return System.Windows.Application.Current.Resources["Icon_Cad"];
                case ".kmz":
                case ".kml":
                    return System.Windows.Application.Current.Resources["Icon_Map"];
                default:
                    return System.Windows.Application.Current.Resources["Icon_File"];
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
