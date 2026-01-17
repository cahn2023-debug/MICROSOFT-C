using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OfflineProjectManager.Services
{
    /// <summary>
    /// LRU (Least Recently Used) cache for preview controls
    /// Reduces memory usage by reusing controls and evicting old ones
    /// </summary>
    public class PreviewCache : IDisposable
    {
        private readonly int _maxCacheSize;
        private readonly LinkedList<CacheEntry> _lruList = new();
        private readonly Dictionary<string, LinkedListNode<CacheEntry>> _cache = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _lock = new();

        public class CacheEntry
        {
            public string FilePath { get; set; }
            public object Control { get; set; }  // Strong reference - rely on LRU eviction, not GC
            public DateTime LastAccessed { get; set; }
            public long EstimatedMemoryBytes { get; set; }
            public long FileLastModified { get; set; }
        }

        public PreviewCache(int maxCacheSize = 10)
        {
            _maxCacheSize = maxCacheSize;
        }

        /// <summary>
        /// Get cached control or create new one
        /// </summary>
        public object GetOrCreate(string filePath, Func<object> factory)
        {
            lock (_lock)
            {
                // Check if file was modified since cached
                long currentModified = 0;
                try
                {
                    if (File.Exists(filePath))
                    {
                        currentModified = File.GetLastWriteTimeUtc(filePath).Ticks;
                    }
                }
                catch { /* Ignore file access errors */ }

                // 1. Check cache
                if (_cache.TryGetValue(filePath, out var node))
                {
                    // Validate not stale
                    if (node.Value.FileLastModified == currentModified && node.Value.Control != null)
                    {
                        // Cache HIT! Move to front (most recently used)
                        _lruList.Remove(node);
                        _lruList.AddFirst(node);
                        node.Value.LastAccessed = DateTime.UtcNow;

                        System.Diagnostics.Debug.WriteLine($"[PreviewCache] HIT: {Path.GetFileName(filePath)}");
                        return node.Value.Control;
                    }
                    else
                    {
                        // Stale - remove
                        _lruList.Remove(node);
                        _cache.Remove(filePath);
                        System.Diagnostics.Debug.WriteLine($"[PreviewCache] STALE: {Path.GetFileName(filePath)}");
                    }
                }

                // 2. Cache MISS - create new
                System.Diagnostics.Debug.WriteLine($"[PreviewCache] MISS: {Path.GetFileName(filePath)}");
                var newControl = factory();

                var entry = new CacheEntry
                {
                    FilePath = filePath,
                    Control = newControl,  // Strong reference
                    LastAccessed = DateTime.UtcNow,
                    EstimatedMemoryBytes = EstimateMemory(newControl),
                    FileLastModified = currentModified
                };

                // 3. Add to cache (at front)
                var newNode = _lruList.AddFirst(entry);
                _cache[filePath] = newNode;

                // 4. Evict if necessary
                Trim();

                return newControl;
            }
        }

        /// <summary>
        /// Remove least recently used items if cache too large
        /// </summary>
        private void Trim()
        {
            while (_lruList.Count > _maxCacheSize)
            {
                var last = _lruList.Last;
                _lruList.RemoveLast();
                _cache.Remove(last.Value.FilePath);

                // Dispose if IDisposable
                if (last.Value.Control is IDisposable disposable)
                {
                    try
                    {
                        disposable.Dispose();
                    }
                    catch { /* Ignore disposal errors */ }
                }
                System.Diagnostics.Debug.WriteLine($"[PreviewCache] EVICTED: {Path.GetFileName(last.Value.FilePath)}");
            }
        }

        /// <summary>
        /// Estimate memory usage of control
        /// </summary>
        private static long EstimateMemory(object control)
        {
            return control switch
            {
                Microsoft.Web.WebView2.Wpf.WebView2 => 100_000_000, // 100MB for WebView2
                ICSharpCode.AvalonEdit.TextEditor => 10_000_000,     // 10MB for text editor
                System.Windows.Controls.Image img => EstimateImageMemory(img),
                _ => 1_000_000 // 1MB default
            };
        }

        private static long EstimateImageMemory(System.Windows.Controls.Image img)
        {
            try
            {
                if (img.Source is System.Windows.Media.Imaging.BitmapSource bmp)
                {
                    return (long)(bmp.PixelWidth * bmp.PixelHeight * 4); // 4 bytes per pixel (RGBA)
                }
            }
            catch { }
            return 5_000_000; // 5MB default for images
        }

        /// <summary>
        /// Get cache statistics
        /// </summary>
        public (int Count, long TotalMemoryBytes) GetStats()
        {
            lock (_lock)
            {
                long total = 0;
                int count = 0;
                foreach (var node in _lruList)
                {
                    if (node.Control != null)
                    {
                        total += node.EstimatedMemoryBytes;
                        count++;
                    }
                }
                return (count, total);
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                foreach (var node in _lruList)
                {
                    if (node.Control is IDisposable disposable)
                    {
                        try
                        {
                            disposable.Dispose();
                        }
                        catch { /* Ignore disposal errors */ }
                    }
                }
                _lruList.Clear();
                _cache.Clear();
            }
        }

        public void Dispose()
        {
            Clear();
        }
    }
}
