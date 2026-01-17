using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.DependencyInjection;
using OfflineProjectManager.Models;
using OfflineProjectManager.Services;

namespace OfflineProjectManager.Views
{
    /// <summary>
    /// Panel for displaying extended file metadata including thumbnails.
    /// </summary>
    public partial class FilePropertiesPanel : System.Windows.Controls.UserControl
    {
        private readonly IMetadataExtractorService _metadataService;
        private readonly IThumbnailCacheService _thumbnailService;

        public FilePropertiesPanel()
        {
            InitializeComponent();

            _metadataService = App.ServiceProvider?.GetService<IMetadataExtractorService>();
            _thumbnailService = App.ServiceProvider?.GetService<IThumbnailCacheService>();
        }

        /// <summary>
        /// Load and display properties for the specified file.
        /// </summary>
        public async void LoadProperties(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                ClearProperties();
                return;
            }

            var fileInfo = new FileInfo(filePath);

            // Basic info
            FileName.Text = fileInfo.Name;
            FileSize.Text = FormatFileSize(fileInfo.Length);
            FileType.Text = GetFileTypeDescription(fileInfo.Extension);
            FileCreated.Text = fileInfo.CreationTime.ToString("g");
            FileModified.Text = fileInfo.LastWriteTime.ToString("g");

            // Hide optional panels
            ImageInfoPanel.Visibility = Visibility.Collapsed;
            DocInfoPanel.Visibility = Visibility.Collapsed;
            ThumbnailBorder.Visibility = Visibility.Collapsed;

            // Extract metadata
            if (_metadataService != null)
            {
                var metadata = await _metadataService.ExtractMetadataAsync(filePath);
                if (metadata != null)
                {
                    DisplayMetadata(metadata);
                }
            }

            // Load thumbnail for images
            if (_thumbnailService != null && IsImageFile(fileInfo.Extension))
            {
                var thumbPath = await _thumbnailService.GetOrCreateThumbnailAsync(filePath);
                if (!string.IsNullOrEmpty(thumbPath) && File.Exists(thumbPath))
                {
                    try
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.UriSource = new Uri(thumbPath);
                        bitmap.EndInit();
                        bitmap.Freeze();

                        ThumbnailImage.Source = bitmap;
                        ThumbnailBorder.Visibility = Visibility.Visible;
                    }
                    catch { }
                }
            }
        }

        private void DisplayMetadata(FileMetadata metadata)
        {
            // Image info
            if (metadata.ImageWidth.HasValue && metadata.ImageHeight.HasValue)
            {
                ImageDimensions.Text = $"{metadata.ImageWidth} × {metadata.ImageHeight} px";
                ImageCamera.Text = metadata.CameraModel ?? "—";
                ImageDateTaken.Text = metadata.DateTaken?.ToString("g") ?? "—";
                ImageInfoPanel.Visibility = Visibility.Visible;
            }

            // Document info
            if (!string.IsNullOrEmpty(metadata.Author) ||
                !string.IsNullOrEmpty(metadata.Title) ||
                metadata.PageCount.HasValue)
            {
                DocAuthor.Text = metadata.Author ?? "—";
                DocTitle.Text = metadata.Title ?? "—";
                DocSubject.Text = metadata.Subject ?? "—";
                DocPages.Text = metadata.PageCount?.ToString() ?? "—";
                DocInfoPanel.Visibility = Visibility.Visible;
            }
        }

        public void ClearProperties()
        {
            FileName.Text = "";
            FileSize.Text = "";
            FileType.Text = "";
            FileCreated.Text = "";
            FileModified.Text = "";
            ImageInfoPanel.Visibility = Visibility.Collapsed;
            DocInfoPanel.Visibility = Visibility.Collapsed;
            ThumbnailBorder.Visibility = Visibility.Collapsed;
            ThumbnailImage.Source = null;
        }

        private static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        private static string GetFileTypeDescription(string extension)
        {
            return extension.ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => "JPEG Image",
                ".png" => "PNG Image",
                ".gif" => "GIF Image",
                ".bmp" => "Bitmap Image",
                ".pdf" => "PDF Document",
                ".docx" => "Word Document",
                ".xlsx" => "Excel Spreadsheet",
                ".pptx" => "PowerPoint Presentation",
                ".txt" => "Text File",
                ".cs" => "C# Source File",
                ".py" => "Python Script",
                ".js" => "JavaScript File",
                ".json" => "JSON File",
                ".xml" => "XML File",
                ".dwg" => "AutoCAD Drawing",
                _ => $"{extension.TrimStart('.').ToUpperInvariant()} File"
            };
        }

        private static bool IsImageFile(string extension)
        {
            return extension.ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".tiff" or ".tif" or ".webp" => true,
                _ => false
            };
        }
    }
}
