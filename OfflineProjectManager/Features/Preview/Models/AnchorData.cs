using System;
using System.Text.Json.Serialization;

namespace OfflineProjectManager.Features.Preview.Models
{
    /// <summary>
    /// Anchor data for restoring preview position within a file.
    /// Supports different file types with type-specific anchoring.
    /// </summary>
    public class AnchorData
    {
        /// <summary>
        /// File type: "Word", "Excel", "PowerPoint", "Text", "PDF"
        /// </summary>
        [JsonPropertyName("fileType")]
        public string FileType { get; set; }

        /// <summary>
        /// For Word documents: paragraph index (0-based)
        /// </summary>
        [JsonPropertyName("paragraphIndex")]
        public int? ParagraphIndex { get; set; }

        /// <summary>
        /// For PowerPoint: slide number (1-based)
        /// </summary>
        [JsonPropertyName("slideNumber")]
        public int? SlideNumber { get; set; }

        /// <summary>
        /// For Excel: sheet name
        /// </summary>
        [JsonPropertyName("sheetName")]
        public string SheetName { get; set; }

        /// <summary>
        /// For Excel: cell row (1-based)
        /// </summary>
        [JsonPropertyName("cellRow")]
        public int? CellRow { get; set; }

        /// <summary>
        /// For Excel: cell column (1-based)
        /// </summary>
        [JsonPropertyName("cellColumn")]
        public int? CellColumn { get; set; }

        /// <summary>
        /// SHA1 hash of anchor text for validation after file changes
        /// </summary>
        [JsonPropertyName("textHash")]
        public string TextHash { get; set; }

        /// <summary>
        /// Search keyword that led to this anchor
        /// </summary>
        [JsonPropertyName("searchKeyword")]
        public string SearchKeyword { get; set; }

        /// <summary>
        /// Character offset within the text block
        /// </summary>
        [JsonPropertyName("charOffset")]
        public int CharOffset { get; set; }

        /// <summary>
        /// Length of selected/highlighted text
        /// </summary>
        [JsonPropertyName("charLength")]
        public int CharLength { get; set; }

        /// <summary>
        /// For text files: line number (1-based)
        /// </summary>
        [JsonPropertyName("lineNumber")]
        public int? LineNumber { get; set; }

        /// <summary>
        /// For PDF: page number (1-based)
        /// </summary>
        [JsonPropertyName("pageNumber")]
        public int? PageNumber { get; set; }

        /// <summary>
        /// Timestamp when anchor was created
        /// </summary>
        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Serialize anchor to JSON string
        /// </summary>
        public string ToJson()
        {
            return System.Text.Json.JsonSerializer.Serialize(this, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = false,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
        }

        /// <summary>
        /// Deserialize anchor from JSON string
        /// </summary>
        public static AnchorData FromJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<AnchorData>(json);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Compute hash of text for validation
        /// </summary>
        public static string ComputeTextHash(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            using var sha1 = System.Security.Cryptography.SHA1.Create();
            var bytes = System.Text.Encoding.UTF8.GetBytes(text);
            var hash = sha1.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }
    }
}
