using System;
using System.Text.Json.Serialization;

namespace OfflineProjectManager.Models
{
    /// <summary>
    /// Extended metadata for files including EXIF, Office properties, and media info.
    /// This is serialized to JSON and stored in FileEntry.MetadataJson.
    /// </summary>
    public class FileMetadata
    {
        // General properties
        [JsonPropertyName("fileType")]
        public string FileType { get; set; }

        [JsonPropertyName("mimeType")]
        public string MimeType { get; set; }

        // Image properties (EXIF)
        [JsonPropertyName("width")]
        public int? ImageWidth { get; set; }

        [JsonPropertyName("height")]
        public int? ImageHeight { get; set; }

        [JsonPropertyName("colorDepth")]
        public int? ColorDepth { get; set; }

        [JsonPropertyName("camera")]
        public string CameraModel { get; set; }

        [JsonPropertyName("dateTaken")]
        public DateTime? DateTaken { get; set; }

        [JsonPropertyName("gpsLatitude")]
        public double? GpsLatitude { get; set; }

        [JsonPropertyName("gpsLongitude")]
        public double? GpsLongitude { get; set; }

        // Office document properties
        [JsonPropertyName("author")]
        public string Author { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("subject")]
        public string Subject { get; set; }

        [JsonPropertyName("keywords")]
        public string Keywords { get; set; }

        [JsonPropertyName("pageCount")]
        public int? PageCount { get; set; }

        [JsonPropertyName("wordCount")]
        public int? WordCount { get; set; }

        [JsonPropertyName("createdDate")]
        public DateTime? DocumentCreated { get; set; }

        [JsonPropertyName("modifiedDate")]
        public DateTime? DocumentModified { get; set; }

        // PDF properties
        [JsonPropertyName("pdfVersion")]
        public string PdfVersion { get; set; }

        [JsonPropertyName("isEncrypted")]
        public bool? IsEncrypted { get; set; }

        // Media properties (audio/video)
        [JsonPropertyName("duration")]
        public TimeSpan? Duration { get; set; }

        [JsonPropertyName("bitrate")]
        public int? Bitrate { get; set; }

        // Thumbnail
        [JsonPropertyName("thumbnailPath")]
        public string ThumbnailPath { get; set; }

        // Extraction info
        [JsonPropertyName("extractedAt")]
        public DateTime? ExtractedAt { get; set; }

        [JsonPropertyName("extractionError")]
        public string ExtractionError { get; set; }
    }
}
