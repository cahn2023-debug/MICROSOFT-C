using System;
using System.IO;

namespace OfflineProjectManager.Utils
{
    public static class PdfJsHelper
    {
        public static string EnsurePdfJsAssets()
        {
            // e.g: AppData\OfflineProjectManager\pdfjs
            // Copy embedded pdf.js distribution if missing (Logic to be expanded if needed)
            // For now, ensures the path is resolved relative to the executable
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "pdfjs");
        }
    }
}
