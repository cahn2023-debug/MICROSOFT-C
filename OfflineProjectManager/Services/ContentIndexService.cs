using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using Task = System.Threading.Tasks.Task;
using Microsoft.EntityFrameworkCore;
using OfflineProjectManager.Data;
using OfflineProjectManager.Models;
using OfflineProjectManager.Services.FileParsers;
using OfflineProjectManager.Utils;

namespace OfflineProjectManager.Services
{
    public class ContentIndexService : IContentIndexService
    {
        private const string NoAccentMarker = "\n__NOACCENT__\n";

        private readonly IProjectService _projectService;
        private readonly IFileParserRegistry _parserRegistry;
        private readonly DbContextPool _dbContextPool;
        private readonly SemaphoreSlim _dbGate = new SemaphoreSlim(1, 1);

        private static readonly HashSet<string> SupportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".txt", ".log", ".md", ".json", ".xml", ".py", ".cs", ".js",
            ".docx", ".xlsx", ".xls",
            ".pdf",
            ".dwg"
        };

        public ContentIndexService(IProjectService projectService, IFileParserRegistry parserRegistry, DbContextPool dbContextPool)
        {
            _projectService = projectService;
            _parserRegistry = parserRegistry;
            _dbContextPool = dbContextPool ?? throw new ArgumentNullException(nameof(dbContextPool));
        }

        public async Task EnsureFilesScannedAsync(int projectId, string scopePath = null, CancellationToken cancellationToken = default)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            if (!_projectService.IsProjectOpen) return;

            var roots = new List<string>();
            if (!string.IsNullOrWhiteSpace(scopePath))
            {
                roots.Add(scopePath);
            }
            else
            {
                roots.AddRange(_projectService.GetProjectFolders());
            }

            var allFiles = new List<string>();
            foreach (var root in roots)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) continue;

                IEnumerable<string> files;
                try
                {
                    files = Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories);
                }
                catch
                {
                    continue;
                }

                foreach (var file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var ext = Path.GetExtension(file);
                    if (string.IsNullOrWhiteSpace(ext)) continue;
                    if (!SupportedExtensions.Contains(ext)) continue;
                    allFiles.Add(file);
                }
            }

            if (allFiles.Count == 0) return;

            string dbPath = _projectService.GetDbPath();
            if (string.IsNullOrEmpty(dbPath)) return;

            await _dbGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                using (var pooledCtx = await _dbContextPool.GetContextAsync(cancellationToken))
                {
                    var ctx = pooledCtx.Context;
                    var existing = await ctx.Files.AsNoTracking()
                        .Where(f => f.ProjectId == projectId)
                        .Select(f => f.Path)
                        .ToListAsync(cancellationToken);

                    var existingSet = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);

                    var filesToAdd = new List<FileEntry>();
                    var filesToUpdate = new List<(string path, FileInfo info)>();

                    // Parallelize metadata gathering
                    await Task.Run(() =>
                    {
                        Parallel.ForEach(allFiles, filePath =>
                        {
                            var fi = new FileInfo(filePath);
                            if (!fi.Exists) return;

                            lock (filesToAdd)
                            {
                                if (!existingSet.Contains(filePath))
                                {
                                    var filename = Path.GetFileName(filePath);
                                    var ext = Path.GetExtension(filePath);
                                    var filenameNoAccent = VietnameseTextHelper.RemoveAccents(VietnameseTextHelper.NormalizeText(filename));
                                    var pathNoAccent = VietnameseTextHelper.RemoveAccents(VietnameseTextHelper.NormalizeText(filePath));

                                    filesToAdd.Add(new FileEntry
                                    {
                                        ProjectId = projectId,
                                        Path = filePath,
                                        PathNoAccent = pathNoAccent,
                                        Filename = filename,
                                        FilenameNoAccent = filenameNoAccent,
                                        Extension = ext,
                                        Size = fi.Length,
                                        FileType = "File",
                                        CreatedAt = fi.CreationTimeUtc,
                                        ModifiedAt = fi.LastWriteTimeUtc,
                                        MetadataJson = JsonSerializer.Serialize(new { attributes = fi.Attributes.ToString() })
                                    });
                                }
                                else
                                {
                                    filesToUpdate.Add((filePath, fi));
                                }
                            }
                        });
                    });

                    // OPTIMIZATION: Batch add new files
                    if (filesToAdd.Count > 0)
                    {
                        ctx.Files.AddRange(filesToAdd);
                    }

                    if (filesToUpdate.Count > 0)
                    {
                        // OPTIMIZATION: Instead of ExecuteUpdateAsync in a loop (which is N round-trips),
                        // we load the records to be updated in batches and update them via tracked changes.
                        // Or for simplicity and speed in SQLite, we use a single transaction.
                        var pathsToUpdate = filesToUpdate.Select(x => x.path).ToList();
                        var entitiesToUpdate = await ctx.Files
                            .Where(f => f.ProjectId == projectId && pathsToUpdate.Contains(f.Path))
                            .ToListAsync(cancellationToken);

                        var fileInfoMap = filesToUpdate.ToDictionary(x => x.path, x => x.info);

                        foreach (var entity in entitiesToUpdate)
                        {
                            if (fileInfoMap.TryGetValue(entity.Path, out var fi))
                            {
                                entity.ModifiedAt = fi.LastWriteTimeUtc;
                                entity.Size = fi.Length;
                            }
                        }
                    }

                    await ctx.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                _dbGate.Release();
            }

            // MONITORING: Log execution time
            sw.Stop();
            System.Diagnostics.Debug.WriteLine($"[Perf] EnsureFilesScannedAsync took {sw.ElapsedMilliseconds}ms for {allFiles.Count} files.");
        }

        public async Task<string> GetOrBuildIndexedContentAsync(string filePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return string.Empty;
            if (!File.Exists(filePath)) return string.Empty;

            string dbPath = _projectService.GetDbPath();
            if (string.IsNullOrEmpty(dbPath)) return string.Empty;

            var fileInfo = new FileInfo(filePath);
            var lastModifiedTicks = fileInfo.LastWriteTimeUtc.Ticks;
            var fileSize = fileInfo.Length;

            // Fast path: fetch existing index if valid
            // CRITICAL FIX: Add gate protection to prevent duplicate indexing
            await _dbGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                using (var pooledCtx = await _dbContextPool.GetContextAsync(cancellationToken))
                {
                    var ctx = pooledCtx.Context;
                    var existing = await ctx.ContentIndex.AsNoTracking()
                        .FirstOrDefaultAsync(x => x.FilePath == filePath, cancellationToken).ConfigureAwait(false);

                    // Phase 3 Optimization: Check file size first (fastest), then hash if available
                    if (existing != null && !string.IsNullOrEmpty(existing.Content))
                    {
                        // Quick size check
                        if (existing.FileSize == fileSize && existing.LastModified == lastModifiedTicks)
                        {
                            return existing.Content; // No changes detected
                        }

                        // If hash exists, compute and compare (more accurate than timestamp)
                        if (!string.IsNullOrEmpty(existing.ContentHash))
                        {
                            var currentHash = await ComputeFileHashAsync(filePath, cancellationToken).ConfigureAwait(false);
                            if (existing.ContentHash == currentHash)
                            {
                                // Content unchanged, update timestamp only
                                existing.LastModified = lastModifiedTicks;
                                return existing.Content;
                            }
                        }
                    }
                }
            }
            finally
            {
                _dbGate.Release();
            }

            // Build index (CPU/IO heavy) outside db gate
            var parser = _parserRegistry.GetParserForPath(filePath);
            if (parser == null) return string.Empty;

            ParsedDocument parsed;
            try
            {
                parsed = await parser.ParseAsync(filePath, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                return string.Empty;
            }

            var text = parsed?.Text ?? string.Empty;
            var textNorm = VietnameseTextHelper.NormalizeText(text);
            var textNoAccent = VietnameseTextHelper.RemoveAccents(textNorm);
            var merged = textNorm + NoAccentMarker + textNoAccent;

            // Phase 3: Compute content hash for change detection
            var contentHash = await ComputeFileHashAsync(filePath, cancellationToken).ConfigureAwait(false);

            await _dbGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                using (var pooledCtx = await _dbContextPool.GetContextAsync(cancellationToken))
                {
                    var ctx = pooledCtx.Context;
                    var entity = await ctx.ContentIndex.FirstOrDefaultAsync(x => x.FilePath == filePath, cancellationToken).ConfigureAwait(false);
                    if (entity == null)
                    {
                        ctx.ContentIndex.Add(new ContentIndexEntry
                        {
                            FilePath = filePath,
                            Content = merged,
                            LastModified = lastModifiedTicks,
                            ContentHash = contentHash,
                            FileSize = fileSize
                        });
                    }
                    else
                    {
                        entity.Content = merged;
                        entity.LastModified = lastModifiedTicks;
                        entity.ContentHash = contentHash;
                        entity.FileSize = fileSize;
                    }

                    await ctx.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                _dbGate.Release();
            }

            return merged;
        }

        public async Task<Dictionary<string, int>> GetMatchCountByFileAsync(IEnumerable<string> filePaths, string queryNorm, string queryNoAccent, CancellationToken cancellationToken = default)
        {
            var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (filePaths == null) return dict;

            var pathList = filePaths.ToList();
            if (pathList.Count == 0) return dict;

            // Step 1: Initialize all to 0
            foreach (var path in pathList) dict[path] = 0;

            // Step 2: Batch load existing indexed content from DB
            Dictionary<string, string> cachedContent;
            await _dbGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                using (var pooledCtx = await _dbContextPool.GetContextAsync(cancellationToken))
                {
                    var ctx = pooledCtx.Context;
                    cachedContent = await ctx.ContentIndex.AsNoTracking()
                        .Where(ci => pathList.Contains(ci.FilePath))
                        .ToDictionaryAsync(ci => ci.FilePath, ci => ci.Content, cancellationToken);
                }
            }
            finally
            {
                _dbGate.Release();
            }

            // Step 3: Count matches (CPU intensive, outside gate)
            // Step 3: Count matches (CPU intensive, outside gate)
            // Use a semaphore to limit concurrency and avoid overwhelming IO/CPU
            var semaphore = new SemaphoreSlim(Environment.ProcessorCount);
            var tasks = pathList.Select(async path =>
            {
                await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    string content = null;
                    if (!cachedContent.TryGetValue(path, out content))
                    {
                        content = await GetOrBuildIndexedContentAsync(path, cancellationToken).ConfigureAwait(false);
                    }
                    if (string.IsNullOrEmpty(content)) return;

                    var count = 0;
                    if (!string.IsNullOrEmpty(queryNorm)) count += CountOccurrences(content, queryNorm);
                    if (!string.IsNullOrEmpty(queryNoAccent) && !string.Equals(queryNoAccent, queryNorm, StringComparison.Ordinal))
                        count += CountOccurrences(content, queryNoAccent);

                    if (count > 0)
                    {
                        lock (dict) dict[path] = count;
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks).ConfigureAwait(false);

            return dict;
        }

        private static int CountOccurrences(string haystack, string needle)
        {
            if (string.IsNullOrEmpty(haystack) || string.IsNullOrEmpty(needle)) return 0;

            int count = 0;
            ReadOnlySpan<char> hSpan = haystack.AsSpan();
            ReadOnlySpan<char> nSpan = needle.AsSpan();

            int index;
            while ((index = hSpan.IndexOf(nSpan, StringComparison.Ordinal)) != -1)
            {
                count++;
                hSpan = hSpan.Slice(index + nSpan.Length);
            }
            return count;
        }

        /// <summary>
        /// Compute SHA256 hash of file content for change detection
        /// </summary>
        private static async Task<string> ComputeFileHashAsync(string filePath, CancellationToken cancellationToken)
        {
            try
            {
                using var stream = File.OpenRead(filePath);
                using var sha256 = System.Security.Cryptography.SHA256.Create();
                var hashBytes = await sha256.ComputeHashAsync(stream, cancellationToken).ConfigureAwait(false);
                return Convert.ToBase64String(hashBytes);
            }
            catch
            {
                // If hashing fails, return empty string (will fallback to timestamp check)
                return string.Empty;
            }
        }
    }
}
