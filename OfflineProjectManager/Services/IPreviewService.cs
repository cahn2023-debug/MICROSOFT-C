using System;
using System.Threading.Tasks;
using System.Windows;

namespace OfflineProjectManager.Services
{
    /// <summary>
    /// Simple interface for preview service.
    /// </summary>
    public interface IPreviewService : IDisposable
    {
        /// <summary>
        /// Creates preview for the given file with optional search keyword for highlighting.
        /// </summary>
        /// <param name="filePath">Path to the file to preview.</param>
        /// <param name="searchKeyword">Optional keyword to highlight and scroll to in preview.</param>
        Task<UIElement> CreatePreviewAsync(string filePath, string searchKeyword = null);

        /// <summary>
        /// Checks if file type is supported.
        /// </summary>
        bool IsSupportedFileType(string extension);
    }
}
