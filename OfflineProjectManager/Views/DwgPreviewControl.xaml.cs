using System;
using System.Runtime.InteropServices;
using System.Windows.Controls;
using OfflineProjectManager.Services.Interop;
using OfflineProjectManager.Services;

namespace OfflineProjectManager.Views
{
    public partial class DwgPreviewControl : System.Windows.Controls.UserControl, IDisposable
    {
        private object _currentHandler;
        private IPreviewHandler _previewHandler;

        public DwgPreviewControl()
        {
            InitializeComponent();
            // Update preview size when window Resizes
            this.SizeChanged += (s, e) => ResizePreview();
        }

        public void LoadFile(string filePath)
        {
            UnloadPreview(); // Clean up old handler first

            Guid clsid = PreviewHandlerService.GetPreviewHandlerGUID(filePath);
            if (clsid == Guid.Empty)
            {
                ErrorText.Text = "No Preview Handler found in registry for .dwg files.";
                ErrorText.Visibility = System.Windows.Visibility.Visible;
                Host.Visibility = System.Windows.Visibility.Collapsed;
                return;
            }

            try
            {
                ErrorText.Visibility = System.Windows.Visibility.Collapsed;
                Host.Visibility = System.Windows.Visibility.Visible;

                // 1. Create Handler instance
                _currentHandler = PreviewHandlerService.CreatePreviewHandler(clsid);
                if (_currentHandler == null)
                {
                    throw new Exception("Could not create COM instance of the Preview Handler.");
                }

                // 2. Initialize with file
                if (_currentHandler is IInitializeWithFile initFile)
                {
                    initFile.Initialize(filePath, 0);
                }

                // 3. Attach to Panel
                if (_currentHandler is IPreviewHandler prevHandler)
                {
                    _previewHandler = prevHandler;

                    // Get WinForms Panel Handle
                    IntPtr hwnd = WinFormsPanel.Handle;

                    RECT rect = new(0, 0, (int)WinFormsPanel.Width, (int)WinFormsPanel.Height);

                    // Attach Preview Handler to our Panel
                    _previewHandler.SetWindow(hwnd, ref rect);
                    _previewHandler.SetRect(ref rect);
                    _previewHandler.DoPreview(); // Start rendering
                }
                else
                {
                    throw new Exception("The registered handler does not implement IPreviewHandler.");
                }
            }
            catch (Exception ex)
            {
                UnloadPreview();
                ErrorText.Text = $"Error with handler {clsid}:\n{ex.Message}";
                ErrorText.Visibility = System.Windows.Visibility.Visible;
                Host.Visibility = System.Windows.Visibility.Collapsed;
                System.Diagnostics.Debug.WriteLine($"DWG Preview Error: {ex}");
            }
        }

        private void ResizePreview()
        {
            if (_previewHandler != null)
            {
                RECT rect = new(0, 0, (int)WinFormsPanel.Width, (int)WinFormsPanel.Height);
                _previewHandler.SetRect(ref rect);
            }
        }

        public void UnloadPreview()
        {
            if (_previewHandler != null)
            {
                try { _previewHandler.Unload(); } catch { }
                Marshal.FinalReleaseComObject(_previewHandler);
                _previewHandler = null;
            }
            if (_currentHandler != null)
            {
                if (Marshal.IsComObject(_currentHandler))
                {
                    Marshal.FinalReleaseComObject(_currentHandler);
                }
                _currentHandler = null;
                // Force collection to release file locks
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        public void Dispose()
        {
            UnloadPreview();
            GC.SuppressFinalize(this);
        }
    }
}
