using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace OfflineProjectManager.Services
{
    /// <summary>
    /// Service for generating and caching file thumbnails.
    /// </summary>
    public interface IThumbnailCacheService
    {
        Task<string> GetOrCreateThumbnailAsync(string filePath, CancellationToken cancellationToken = default);
        string GetThumbnailCacheDir();
        void ClearCache();
    }

    /// <summary>
    /// Implementation of thumbnail caching service.
    /// Generates thumbnails for images and caches them in a dedicated folder.
    /// </summary>
    public class ThumbnailCacheService : IThumbnailCacheService
    {
        private readonly string _cacheDir;
        private const int ThumbnailSize = 128;

        private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".tif", ".webp", ".ico"
        };

        public ThumbnailCacheService(IProjectService projectService)
        {
            // Store thumbnails in a .thumbnails folder alongside the database
            var dbPath = projectService?.GetDbPath();
            if (!string.IsNullOrEmpty(dbPath))
            {
                _cacheDir = Path.Combine(Path.GetDirectoryName(dbPath), ".thumbnails");
            }
            else
            {
                _cacheDir = Path.Combine(Path.GetTempPath(), "OfflineProjectManager", "thumbnails");
            }

            if (!Directory.Exists(_cacheDir))
            {
                Directory.CreateDirectory(_cacheDir);
            }
        }

        public string GetThumbnailCacheDir() => _cacheDir;

        public async Task<string> GetOrCreateThumbnailAsync(string filePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return null;

            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            if (!SupportedExtensions.Contains(ext))
                return null; // Not a supported image format

            // Generate unique filename based on file path hash
            var hash = ComputeFilePathHash(filePath);
            var thumbPath = Path.Combine(_cacheDir, $"{hash}.jpg");

            // If thumbnail exists and is newer than source, return it
            if (File.Exists(thumbPath))
            {
                var thumbInfo = new FileInfo(thumbPath);
                var sourceInfo = new FileInfo(filePath);
                if (thumbInfo.LastWriteTimeUtc > sourceInfo.LastWriteTimeUtc)
                {
                    return thumbPath;
                }
            }

            // Generate thumbnail
            try
            {
                return await Task.Run(() => GenerateThumbnail(filePath, thumbPath), cancellationToken);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ThumbnailCache] Error generating thumbnail for {filePath}: {ex.Message}");
                return null;
            }
        }

        private string GenerateThumbnail(string sourcePath, string thumbPath)
        {
            try
            {
                using var stream = File.OpenRead(sourcePath);
                var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.None);
                var frame = decoder.Frames[0];

                // Calculate scaled dimensions maintaining aspect ratio
                double scale = Math.Min((double)ThumbnailSize / frame.PixelWidth, (double)ThumbnailSize / frame.PixelHeight);
                int newWidth = (int)(frame.PixelWidth * scale);
                int newHeight = (int)(frame.PixelHeight * scale);

                var resized = new TransformedBitmap(frame, new System.Windows.Media.ScaleTransform(scale, scale));

                // Save as JPEG
                using var outStream = File.Create(thumbPath);
                var encoder = new JpegBitmapEncoder { QualityLevel = 85 };
                encoder.Frames.Add(BitmapFrame.Create(resized));
                encoder.Save(outStream);

                return thumbPath;
            }
            catch
            {
                return null;
            }
        }

        private static string ComputeFilePathHash(string filePath)
        {
            using var md5 = System.Security.Cryptography.MD5.Create();
            var bytes = System.Text.Encoding.UTF8.GetBytes(filePath.ToLowerInvariant());
            var hashBytes = md5.ComputeHash(bytes);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
        }

        public void ClearCache()
        {
            if (Directory.Exists(_cacheDir))
            {
                try
                {
                    foreach (var file in Directory.GetFiles(_cacheDir, "*.jpg"))
                    {
                        File.Delete(file);
                    }
                }
                catch { }
            }
        }
    }
}
