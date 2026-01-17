using System;
using System.Windows;
using System.Windows.Controls;
using OfflineProjectManager.Models;

// Explicit alias for WPF types to avoid conflicts with System.Windows.Forms
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;

namespace OfflineProjectManager.Features.Preview.Controls
{
    /// <summary>
    /// Preview control for image files.
    /// Supports region selection for "Add to Task".
    /// </summary>
    public class ImagePreviewControl : IPreviewControl
    {
        private readonly Grid _container;
        private readonly System.Windows.Controls.Image _image;
        private readonly Canvas _selectionCanvas;
        private readonly string _filePath;
        private Rect _selectedRegion;
        private bool _disposed;

        public FrameworkElement View => _container;
        public string FilePath => _filePath;
        public string PreviewType => "Image";
        public bool SupportsHighlighting => false;
        public bool SupportsSelection => true;

        public ImagePreviewControl(string filePath)
        {
            _filePath = filePath;

            _container = new Grid();

            _image = new System.Windows.Controls.Image
            {
                Stretch = System.Windows.Media.Stretch.Uniform,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            };

            _selectionCanvas = new Canvas
            {
                Background = WpfBrushes.Transparent,
                IsHitTestVisible = true
            };

            try
            {
                var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(filePath);
                bitmap.EndInit();
                bitmap.Freeze();

                _image.Source = bitmap;
            }
            catch (Exception ex)
            {
                _image.Source = null;
                var errorText = new TextBlock
                {
                    Text = $"Error loading image: {ex.Message}",
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = System.Windows.VerticalAlignment.Center
                };
                _container.Children.Add(errorText);
                return;
            }

            _container.Children.Add(_image);
            _container.Children.Add(_selectionCanvas);
        }

        public void Highlight(string text)
        {
            // Images don't support text highlighting
        }

        public void ScrollToLine(int line)
        {
            // Images don't have lines
        }

        public string GetSelectedText()
        {
            // Images return "Region" for selected areas
            if (_selectedRegion.Width > 10 && _selectedRegion.Height > 10)
            {
                return "Region";
            }
            return null;
        }

        public SelectionContext GetSelectionContext()
        {
            if (_selectedRegion.Width < 10 || _selectedRegion.Height < 10) return null;

            return new SelectionContext
            {
                FilePath = _filePath,
                PreviewType = PreviewType,
                SelectedText = "Region",
                RectX = _selectedRegion.X,
                RectY = _selectedRegion.Y,
                RectWidth = _selectedRegion.Width,
                RectHeight = _selectedRegion.Height
            };
        }

        /// <summary>
        /// Set the selected region (called from UI interaction).
        /// </summary>
        public void SetSelectedRegion(Rect region)
        {
            _selectedRegion = region;

            // Draw selection rectangle
            _selectionCanvas.Children.Clear();
            if (region.Width > 0 && region.Height > 0)
            {
                var rect = new System.Windows.Shapes.Rectangle
                {
                    Stroke = WpfBrushes.Red,
                    StrokeThickness = 2,
                    Fill = new System.Windows.Media.SolidColorBrush(WpfColor.FromArgb(50, 255, 0, 0)),
                    Width = region.Width,
                    Height = region.Height
                };
                Canvas.SetLeft(rect, region.X);
                Canvas.SetTop(rect, region.Y);
                _selectionCanvas.Children.Add(rect);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _selectionCanvas.Children.Clear();
            _container.Children.Clear();
        }
    }
}
