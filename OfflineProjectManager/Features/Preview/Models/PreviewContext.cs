using System;
using System.IO;

namespace OfflineProjectManager.Features.Preview.Models
{
    /// <summary>
    /// Context object passed to preview providers containing all information
    /// needed to render a file preview with optional search/anchor data.
    /// </summary>
    public class PreviewContext
    {
        /// <summary>
        /// Full path to the file to preview
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// File type category: "Word", "Excel", "PowerPoint", "Text", "PDF", "Image", "CAD", "Other"
        /// </summary>
        public string FileType { get; set; }

        /// <summary>
        /// File extension (lowercase, with dot)
        /// </summary>
        public string Extension { get; set; }

        /// <summary>
        /// Search keyword to highlight (null if no search)
        /// </summary>
        public string SearchKeyword { get; set; }

        /// <summary>
        /// Anchor data for position restoration (null if no anchor)
        /// </summary>
        public AnchorData Anchor { get; set; }

        /// <summary>
        /// Returns true if this context requires highlight/scroll support
        /// </summary>
        public bool HasSearchContext =>
            !string.IsNullOrEmpty(SearchKeyword) || Anchor != null;

        /// <summary>
        /// Create context from file path
        /// </summary>
        public static PreviewContext FromFile(string filePath, string searchKeyword = null, AnchorData anchor = null)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentNullException(nameof(filePath));

            var ext = Path.GetExtension(filePath)?.ToLowerInvariant() ?? "";

            return new PreviewContext
            {
                FilePath = filePath,
                Extension = ext,
                FileType = GetFileType(ext),
                SearchKeyword = searchKeyword,
                Anchor = anchor
            };
        }

        /// <summary>
        /// Determine file type category from extension
        /// </summary>
        public static string GetFileType(string extension)
        {
            return extension switch
            {
                ".doc" or ".docx" or ".docm" or ".rtf" => "Word",
                ".xls" or ".xlsx" or ".xlsm" or ".csv" => "Excel",
                ".ppt" or ".pptx" or ".pptm" => "PowerPoint",
                ".pdf" => "PDF",
                ".txt" or ".log" or ".md" or ".json" or ".xml" or ".yaml" or ".yml" => "Text",
                ".cs" or ".py" or ".js" or ".ts" or ".java" or ".cpp" or ".c" or ".h" or ".css" or ".html" or ".htm" => "Code",
                ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".tiff" or ".tif" or ".webp" or ".ico" => "Image",
                ".dwg" or ".dxf" => "CAD",
                ".mp3" or ".wav" or ".ogg" or ".flac" => "Audio",
                ".mp4" or ".avi" or ".mkv" or ".mov" or ".wmv" => "Video",
                _ => "Other"
            };
        }

        /// <summary>
        /// Check if file type is Office document
        /// </summary>
        public bool IsOfficeDocument =>
            FileType is "Word" or "Excel" or "PowerPoint";

        /// <summary>
        /// Check if file type is text/code (can use AvalonEdit)
        /// </summary>
        public bool IsTextBased =>
            FileType is "Text" or "Code";
    }
}
