using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using OfflineProjectManager.Models;
using OfflineProjectManager.Utils;
using ContextMenu = System.Windows.Controls.ContextMenu;
using MenuItem = System.Windows.Controls.MenuItem;

namespace OfflineProjectManager.Features.Preview
{
    /// <summary>
    /// Helper class for adding "Add to Task" context menu to preview controls.
    /// </summary>
    public static class PreviewContextMenuHelper
    {
        /// <summary>
        /// Data class to store WebView2 selection information in Grid.Tag
        /// </summary>
        public class WebView2SelectionData
        {
            public WebView2 WebView { get; set; }
            public string FilePath { get; set; }
            public string SelectedText { get; set; }
            public string PreviewType { get; set; }
        }

        /// <summary>
        /// Data class to store region selection information in Grid.Tag
        /// </summary>
        public class RegionSelectionData
        {
            public string FilePath { get; set; }
            public string PreviewType { get; set; }
            public double RectX { get; set; }
            public double RectY { get; set; }
            public double RectWidth { get; set; }
            public double RectHeight { get; set; }
            public int PageNumber { get; set; } = 1;
            public string SelectedText { get; set; }
        }

        // Static state for region selection activation
        private static bool _isRegionSelectionActive = false;
        private static readonly System.Collections.Generic.List<System.Windows.Controls.Border> _activeOverlays = new();
        private static readonly System.Collections.Generic.List<Microsoft.Web.WebView2.Wpf.WebView2> _webViewsToHide = new();
        private static readonly System.Collections.Generic.Dictionary<System.Windows.Controls.Border, System.Windows.Controls.Image> _overlayScreenshots = new();

        /// <summary>
        /// Sets the region selection mode active or inactive.
        /// Called from MainWindow toggle button.
        /// </summary>
        public static async void SetRegionSelectionActive(bool active)
        {
            _isRegionSelectionActive = active;
            System.Diagnostics.Debug.WriteLine($"SetRegionSelectionActive: {active}, overlays: {_activeOverlays.Count}, webviews: {_webViewsToHide.Count}");

            if (active)
            {
                // CAPTURE SCREENSHOTS before hiding WebView2
                for (int i = 0; i < _webViewsToHide.Count && i < _activeOverlays.Count; i++)
                {
                    var webView = _webViewsToHide[i];
                    var overlay = _activeOverlays[i];

                    try
                    {
                        // Capture screenshot from WebView2
                        using var stream = new System.IO.MemoryStream();
                        await webView.CoreWebView2.CapturePreviewAsync(
                            Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png, stream);
                        stream.Position = 0;

                        // Create Image from screenshot
                        var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                        bitmap.BeginInit();
                        bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                        bitmap.StreamSource = stream;
                        bitmap.EndInit();
                        bitmap.Freeze();

                        // Create or update screenshot Image
                        if (!_overlayScreenshots.TryGetValue(overlay, out var screenshotImage))
                        {
                            screenshotImage = new System.Windows.Controls.Image
                            {
                                Stretch = System.Windows.Media.Stretch.Fill,
                                IsHitTestVisible = false
                            };
                            _overlayScreenshots[overlay] = screenshotImage;
                        }
                        screenshotImage.Source = bitmap;

                        // Set screenshot as overlay background using VisualBrush workaround
                        overlay.Background = new System.Windows.Media.ImageBrush(bitmap)
                        {
                            Stretch = System.Windows.Media.Stretch.Fill
                        };

                        System.Diagnostics.Debug.WriteLine($"  Screenshot captured and set as overlay background");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"  Failed to capture screenshot: {ex.Message}");
                        // Fallback to semi-transparent overlay
                        overlay.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(200, 50, 50, 50));
                    }
                }
            }

            // Hide/Show WebView2
            foreach (var webView in _webViewsToHide)
            {
                webView.Visibility = active ? System.Windows.Visibility.Hidden : System.Windows.Visibility.Visible;
                System.Diagnostics.Debug.WriteLine($"  WebView2 Visibility: {webView.Visibility}");
            }

            foreach (var overlay in _activeOverlays)
            {
                overlay.IsHitTestVisible = active;
                if (!active)
                {
                    // Reset to transparent when inactive
                    overlay.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(1, 0, 0, 0));
                }
                overlay.Cursor = active ? System.Windows.Input.Cursors.Cross : System.Windows.Input.Cursors.Arrow;

                System.Diagnostics.Debug.WriteLine($"  Overlay IsHitTestVisible: {overlay.IsHitTestVisible}");
            }
        }

        /// <summary>
        /// Sets up WebView2 context menu with "Add to Task" option.
        /// Call this after CoreWebView2 is initialized.
        /// </summary>
        public static void SetupWebView2ContextMenu(WebView2 webView, Grid container, string filePath, string previewType)
        {
            if (webView?.CoreWebView2 == null) return;

            // Store data in container Tag for SelectionExtractionService
            container.Tag = new WebView2SelectionData
            {
                WebView = webView,
                FilePath = filePath,
                PreviewType = previewType,
                SelectedText = string.Empty
            };

            webView.CoreWebView2.ContextMenuRequested += async (sender, args) =>
            {
                // IMPORTANT: Get deferral to pause context menu until async operation completes
                var deferral = args.GetDeferral();

                try
                {
                    // Get selected text via JavaScript
                    string selectedText = string.Empty;
                    try
                    {
                        var result = await webView.ExecuteScriptAsync("window.getSelection().toString()");
                        // Result is JSON-encoded, so it has quotes around it
                        if (!string.IsNullOrEmpty(result) && result != "\"\"")
                        {
                            selectedText = System.Text.Json.JsonSerializer.Deserialize<string>(result) ?? string.Empty;
                        }
                    }
                    catch { }

                    // Update stored selection
                    if (container.Tag is WebView2SelectionData data)
                    {
                        data.SelectedText = selectedText;
                    }

                    // Only add custom menu if text is selected
                    if (!string.IsNullOrWhiteSpace(selectedText))
                    {
                        // Create custom menu item
                        var menuItem = webView.CoreWebView2.Environment.CreateContextMenuItem(
                            "📋 Add to Task",
                            null,
                            CoreWebView2ContextMenuItemKind.Command);

                        menuItem.CustomItemSelected += (s, e) =>
                        {
                            // Execute AddToTask command on main window
                            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                            {
                                ProjectCommands.AddToTask.Execute(null, System.Windows.Application.Current.MainWindow);
                            });
                        };

                        // Insert at beginning of context menu
                        args.MenuItems.Insert(0, menuItem);

                        // Add separator
                        var separator = webView.CoreWebView2.Environment.CreateContextMenuItem(
                            string.Empty, null, CoreWebView2ContextMenuItemKind.Separator);
                        args.MenuItems.Insert(1, separator);
                    }
                }
                finally
                {
                    // Complete deferral to allow context menu to show
                    deferral.Complete();
                }
            };
        }

        /// <summary>
        /// Sets up region selection with Canvas overlay for images and PDFs.
        /// Use "Select Region" toggle button to enable selection mode.
        /// </summary>
        /// <param name="container">The Grid container holding the preview control</param>
        /// <param name="filePath">Path to the file being previewed</param>
        /// <param name="previewType">Type of preview (PDF, Image, etc.)</param>
        /// <param name="webViewToHide">Optional WebView2 to hide when selection mode is active</param>
        public static void SetupRegionSelection(Grid container, string filePath, string previewType, Microsoft.Web.WebView2.Wpf.WebView2 webViewToHide = null)
        {
            // Register WebView2 to hide when selection mode is active (needed for native HWND controls)
            if (webViewToHide != null)
            {
                _webViewsToHide.Add(webViewToHide);
                System.Diagnostics.Debug.WriteLine($"SetupRegionSelection: WebView2 registered for hiding, total: {_webViewsToHide.Count}");
            }

            // Create overlay border that captures mouse when selection mode is active
            var overlay = new System.Windows.Controls.Border
            {
                Background = _isRegionSelectionActive
                    ? new SolidColorBrush(System.Windows.Media.Color.FromArgb(200, 30, 30, 30)) // Dark when active
                    : new SolidColorBrush(System.Windows.Media.Color.FromArgb(1, 0, 0, 0)), // Almost invisible
                IsHitTestVisible = _isRegionSelectionActive,
                Cursor = System.Windows.Input.Cursors.Cross
            };

            // CRITICAL: Set ZIndex to ensure overlay is ABOVE other elements
            System.Windows.Controls.Panel.SetZIndex(overlay, 100);

            System.Diagnostics.Debug.WriteLine($"SetupRegionSelection: overlay created for {previewType}, IsHitTestVisible={overlay.IsHitTestVisible}");

            // Register overlay to static list for activation control
            _activeOverlays.Add(overlay);
            System.Diagnostics.Debug.WriteLine($"SetupRegionSelection: overlay added, total overlays: {_activeOverlays.Count}");

            // Cleanup when control is unloaded
            container.Unloaded += (s, e) =>
            {
                _activeOverlays.Remove(overlay);
                if (webViewToHide != null)
                {
                    _webViewsToHide.Remove(webViewToHide);
                }
                System.Diagnostics.Debug.WriteLine($"SetupRegionSelection: cleaned up, overlays: {_activeOverlays.Count}, webviews: {_webViewsToHide.Count}");
            };

            // Create canvas for selection rectangle
            var canvas = new Canvas
            {
                Background = null,
                IsHitTestVisible = false
            };
            overlay.Child = canvas;

            // Selection state
            bool isSelecting = false;
            System.Windows.Point startPoint = new System.Windows.Point();
            System.Windows.Shapes.Rectangle selectionRect = null;

            // Store data in container Tag
            var selectionData = new RegionSelectionData
            {
                FilePath = filePath,
                PreviewType = previewType
            };
            container.Tag = selectionData;

            // Mouse down to start selection
            overlay.MouseLeftButtonDown += (s, e) =>
            {
                // Clear previous selection
                canvas.Children.Clear();

                isSelecting = true;
                startPoint = e.GetPosition(overlay);
                overlay.CaptureMouse();

                // Create selection rectangle
                selectionRect = new System.Windows.Shapes.Rectangle
                {
                    Stroke = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 122, 204)),
                    StrokeThickness = 2,
                    StrokeDashArray = new DoubleCollection { 4, 2 },
                    Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(50, 0, 122, 204))
                };
                Canvas.SetLeft(selectionRect, startPoint.X);
                Canvas.SetTop(selectionRect, startPoint.Y);
                canvas.Children.Add(selectionRect);

                e.Handled = true;
            };

            overlay.MouseMove += (s, e) =>
            {
                if (!isSelecting || selectionRect == null) return;

                var currentPoint = e.GetPosition(overlay);

                double x = Math.Min(startPoint.X, currentPoint.X);
                double y = Math.Min(startPoint.Y, currentPoint.Y);
                double width = Math.Abs(currentPoint.X - startPoint.X);
                double height = Math.Abs(currentPoint.Y - startPoint.Y);

                Canvas.SetLeft(selectionRect, x);
                Canvas.SetTop(selectionRect, y);
                selectionRect.Width = width;
                selectionRect.Height = height;

                e.Handled = true;
            };

            overlay.MouseLeftButtonUp += (s, e) =>
            {
                if (!isSelecting || selectionRect == null) return;

                isSelecting = false;
                overlay.ReleaseMouseCapture();

                var currentPoint = e.GetPosition(overlay);

                double x = Math.Min(startPoint.X, currentPoint.X);
                double y = Math.Min(startPoint.Y, currentPoint.Y);
                double width = Math.Abs(currentPoint.X - startPoint.X);
                double height = Math.Abs(currentPoint.Y - startPoint.Y);

                // Only process meaningful selections (at least 10x10 pixels)
                if (width < 10 || height < 10)
                {
                    canvas.Children.Clear();
                    return;
                }

                // Update selection data
                selectionData.RectX = x;
                selectionData.RectY = y;
                selectionData.RectWidth = width;
                selectionData.RectHeight = height;
                selectionData.SelectedText = "Region";

                // Make rectangle solid to indicate selection complete
                selectionRect.StrokeDashArray = null;
                selectionRect.Stroke = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 122, 204));

                // Add context menu to the selection rectangle
                var contextMenu = new ContextMenu();
                var addToTaskItem = new MenuItem
                {
                    Header = "📋 Add to Task"
                };
                addToTaskItem.Click += (menuSender, menuE) =>
                {
                    if (selectionData.RectWidth > 0 && selectionData.RectHeight > 0)
                    {
                        ProjectCommands.AddToTask.Execute(null, System.Windows.Application.Current.MainWindow);
                    }
                };
                contextMenu.Items.Add(addToTaskItem);
                selectionRect.ContextMenu = contextMenu;

                // Keep canvas active to show rectangle and allow context menu
                canvas.IsHitTestVisible = true;

                // KEEP SCREENSHOT VISIBLE: Don't show WebView2 yet
                // Rectangle needs to stay visible on top of screenshot
                // WebView2 will only be shown when right-clicking outside or after AddToTask

                // Keep overlay with screenshot background but make rectangle clickable
                // overlay.Background stays the same (screenshot)
                canvas.IsHitTestVisible = true; // Rectangle is clickable for context menu

                // Don't hide overlay, keep it visible with screenshot
                // Don't show WebView2 yet
                // Don't uncheck toggle button yet

                e.Handled = true;
            };

            // Allow clicking outside rectangle to clear selection and exit mode
            overlay.MouseRightButtonDown += (s, e) =>
            {
                // If right-clicking on the rectangle, let context menu handle it
                if (selectionRect != null && selectionData.RectWidth >= 10 && selectionData.RectHeight >= 10)
                {
                    var clickPoint = e.GetPosition(canvas);
                    if (clickPoint.X >= selectionData.RectX && clickPoint.X <= selectionData.RectX + selectionData.RectWidth &&
                        clickPoint.Y >= selectionData.RectY && clickPoint.Y <= selectionData.RectY + selectionData.RectHeight)
                    {
                        // Click is inside rectangle, let it handle context menu
                        return;
                    }
                }

                // Exit selection mode: clear rectangle and show WebView2
                canvas.Children.Clear();
                selectionData.RectWidth = 0;
                selectionData.RectHeight = 0;

                // Show WebView2 again
                if (webViewToHide != null)
                {
                    webViewToHide.Visibility = System.Windows.Visibility.Visible;
                }

                // Reset overlay
                overlay.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(1, 0, 0, 0));
                overlay.IsHitTestVisible = false;

                // Uncheck toggle button
                var mainWindow = System.Windows.Application.Current.MainWindow;
                if (mainWindow != null)
                {
                    var toggleButton = mainWindow.FindName("SelectRegionToggle") as System.Windows.Controls.Primitives.ToggleButton;
                    if (toggleButton != null)
                    {
                        toggleButton.IsChecked = false;
                    }
                }

                _isRegionSelectionActive = false;
            };

            container.Children.Add(overlay);
        }
    }
}
