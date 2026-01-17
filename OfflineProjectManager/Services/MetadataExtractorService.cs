using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using OfflineProjectManager.Models;
using WpfImage = System.Windows.Media.Imaging.BitmapImage;
using WpfBitmapMetadata = System.Windows.Media.Imaging.BitmapMetadata;
using WpfBitmapFrame = System.Windows.Media.Imaging.BitmapFrame;

namespace OfflineProjectManager.Services
{
    /// <summary>
    /// Interface for metadata extraction service.
    /// </summary>
    public interface IMetadataExtractorService
    {
        Task<FileMetadata> ExtractMetadataAsync(string filePath, CancellationToken cancellationToken = default);
        string SerializeMetadata(FileMetadata metadata);
        FileMetadata DeserializeMetadata(string json);
    }

    /// <summary>
    /// Service for extracting extended metadata from files.
    /// Supports images (EXIF), Office documents, and PDFs.
    /// </summary>
    public class MetadataExtractorService : IMetadataExtractorService
    {
        private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".tif", ".webp"
        };

        private static readonly HashSet<string> OfficeExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".docx", ".xlsx", ".pptx", ".doc", ".xls", ".ppt"
        };

        private static readonly HashSet<string> PdfExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf"
        };

        public async Task<FileMetadata> ExtractMetadataAsync(string filePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return null;

            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            var metadata = new FileMetadata
            {
                ExtractedAt = DateTime.UtcNow
            };

            try
            {
                // Get MIME type
                metadata.MimeType = GetMimeType(ext);
                metadata.FileType = GetFileTypeCategory(ext);

                if (ImageExtensions.Contains(ext))
                {
                    await ExtractImageMetadataAsync(filePath, metadata, cancellationToken);
                }
                else if (OfficeExtensions.Contains(ext))
                {
                    await ExtractOfficeMetadataAsync(filePath, metadata, cancellationToken);
                }
                else if (PdfExtensions.Contains(ext))
                {
                    await ExtractPdfMetadataAsync(filePath, metadata, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                metadata.ExtractionError = ex.Message;
                System.Diagnostics.Debug.WriteLine($"[MetadataExtractor] Error extracting {filePath}: {ex.Message}");
            }

            return metadata;
        }

        private Task ExtractImageMetadataAsync(string filePath, FileMetadata metadata, CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                try
                {
                    using var stream = File.OpenRead(filePath);
                    var frame = WpfBitmapFrame.Create(stream, System.Windows.Media.Imaging.BitmapCreateOptions.DelayCreation, System.Windows.Media.Imaging.BitmapCacheOption.None);

                    metadata.ImageWidth = frame.PixelWidth;
                    metadata.ImageHeight = frame.PixelHeight;

                    if (frame.Metadata is WpfBitmapMetadata bitmapMeta)
                    {
                        metadata.CameraModel = bitmapMeta.CameraModel;
                        metadata.DateTaken = ParseDateTime(bitmapMeta.DateTaken);
                        metadata.Title = bitmapMeta.Title;
                        metadata.Author = bitmapMeta.Author != null ? string.Join(", ", bitmapMeta.Author) : null;
                        metadata.Subject = bitmapMeta.Subject;
                        metadata.Keywords = bitmapMeta.Keywords != null ? string.Join(", ", bitmapMeta.Keywords) : null;

                        // Try to get GPS coordinates
                        try
                        {
                            var latQuery = bitmapMeta.GetQuery("/app1/ifd/gps/{ushort=2}");
                            var lonQuery = bitmapMeta.GetQuery("/app1/ifd/gps/{ushort=4}");
                            if (latQuery != null && lonQuery != null)
                            {
                                // GPS coordinates exist - full parsing would need more code
                                // For now just note they exist
                            }
                        }
                        catch { }
                    }
                }
                catch (Exception ex)
                {
                    metadata.ExtractionError = $"Image: {ex.Message}";
                }
            }, cancellationToken);
        }

        private Task ExtractOfficeMetadataAsync(string filePath, FileMetadata metadata, CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                try
                {
                    var ext = Path.GetExtension(filePath).ToLowerInvariant();

                    // For modern Office formats (.docx, .xlsx, .pptx), use Open XML
                    if (ext == ".docx" || ext == ".xlsx" || ext == ".pptx")
                    {
                        ExtractOpenXmlMetadata(filePath, metadata);
                    }
                    else
                    {
                        // For older formats, use Shell property reading
                        ExtractShellProperties(filePath, metadata);
                    }
                }
                catch (Exception ex)
                {
                    metadata.ExtractionError = $"Office: {ex.Message}";
                }
            }, cancellationToken);
        }

        private void ExtractOpenXmlMetadata(string filePath, FileMetadata metadata)
        {
            // Use System.IO.Packaging for Open XML formats
            try
            {
                using var package = System.IO.Packaging.Package.Open(filePath, FileMode.Open, FileAccess.Read);
                var props = package.PackageProperties;

                metadata.Author = props.Creator;
                metadata.Title = props.Title;
                metadata.Subject = props.Subject;
                metadata.Keywords = props.Keywords;
                metadata.DocumentCreated = props.Created;
                metadata.DocumentModified = props.Modified;
            }
            catch (Exception ex)
            {
                metadata.ExtractionError = $"OpenXml: {ex.Message}";
            }
        }

        private void ExtractShellProperties(string filePath, FileMetadata metadata)
        {
            // Fallback for older Office formats - use FileInfo for basic info
            try
            {
                var fileInfo = new FileInfo(filePath);
                metadata.DocumentCreated = fileInfo.CreationTimeUtc;
                metadata.DocumentModified = fileInfo.LastWriteTimeUtc;
            }
            catch { }
        }

        private Task ExtractPdfMetadataAsync(string filePath, FileMetadata metadata, CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                try
                {
                    // Basic PDF metadata extraction
                    // For full PDF support, would need a PDF library like iTextSharp or PdfSharp
                    using var stream = File.OpenRead(filePath);
                    using var reader = new StreamReader(stream);
                    var header = reader.ReadLine();

                    if (header != null && header.StartsWith("%PDF-"))
                    {
                        metadata.PdfVersion = header.Substring(5);
                    }

                    // Check for encryption (basic check)
                    stream.Position = 0;
                    var bytes = new byte[Math.Min(1024, stream.Length)];
                    stream.Read(bytes, 0, bytes.Length);
                    var content = System.Text.Encoding.ASCII.GetString(bytes);
                    metadata.IsEncrypted = content.Contains("/Encrypt");
                }
                catch (Exception ex)
                {
                    metadata.ExtractionError = $"PDF: {ex.Message}";
                }
            }, cancellationToken);
        }

        private static DateTime? ParseDateTime(string dateString)
        {
            if (string.IsNullOrEmpty(dateString)) return null;
            if (DateTime.TryParse(dateString, out var dt)) return dt;
            return null;
        }

        private static string GetMimeType(string extension)
        {
            return extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                ".tiff" or ".tif" => "image/tiff",
                ".webp" => "image/webp",
                ".pdf" => "application/pdf",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                ".doc" => "application/msword",
                ".xls" => "application/vnd.ms-excel",
                ".ppt" => "application/vnd.ms-powerpoint",
                ".txt" => "text/plain",
                ".json" => "application/json",
                ".xml" => "application/xml",
                ".html" or ".htm" => "text/html",
                ".css" => "text/css",
                ".js" => "application/javascript",
                ".cs" => "text/x-csharp",
                ".py" => "text/x-python",
                _ => "application/octet-stream"
            };
        }

        private static string GetFileTypeCategory(string extension)
        {
            if (ImageExtensions.Contains(extension)) return "Image";
            if (OfficeExtensions.Contains(extension)) return "Document";
            if (PdfExtensions.Contains(extension)) return "PDF";
            if (extension is ".txt" or ".log" or ".md" or ".json" or ".xml" or ".cs" or ".py" or ".js") return "Text";
            return "Binary";
        }

        public string SerializeMetadata(FileMetadata metadata)
        {
            if (metadata == null) return null;
            return JsonSerializer.Serialize(metadata, new JsonSerializerOptions
            {
                WriteIndented = false,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });
        }

        public FileMetadata DeserializeMetadata(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                return JsonSerializer.Deserialize<FileMetadata>(json);
            }
            catch
            {
                return null;
            }
        }
    }
}
