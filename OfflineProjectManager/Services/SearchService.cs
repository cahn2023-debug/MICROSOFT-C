using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OfflineProjectManager.Data;
using OfflineProjectManager.Models;
using OfflineProjectManager.Utils;
using Task = System.Threading.Tasks.Task;

namespace OfflineProjectManager.Services
{
    public class SearchService(IProjectService projectService, IContentIndexService contentIndexService, DbContextPool dbContextPool) : ISearchService
    {
        private readonly IProjectService _projectService = projectService;
        private readonly IContentIndexService _contentIndexService = contentIndexService;
        private readonly DbContextPool _dbContextPool = dbContextPool ?? throw new ArgumentNullException(nameof(dbContextPool));

        public async Task<List<string>> SearchFilesAsync(
            string query,
            int projectId,
            string scopePath = null,
            CancellationToken cancellationToken = default,
            Action<FileEntry, List<SearchMatch>> foundCallback = null,
            int maxResults = 1000)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            if (string.IsNullOrWhiteSpace(query)) return [];

            // Pre-process Query
            string queryNorm = VietnameseTextHelper.NormalizeText(query);
            string queryNoAccent = VietnameseTextHelper.RemoveAccents(queryNorm);

            var emittedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string dbPath = _projectService.GetDbPath();
            if (string.IsNullOrEmpty(dbPath)) return [];

            // Ensure files are scanned (best-effort, non-blocking)
            if (_contentIndexService != null)
            {
                try
                {
                    await _contentIndexService.EnsureFilesScannedAsync(projectId, scopePath, cancellationToken).ConfigureAwait(false);
                }
                catch { /* Ignore scanning errors */ }
            }

            using (var pooledCtx = await _dbContextPool.GetContextAsync(cancellationToken))
            {
                var ctx = pooledCtx.Context;
                try
                {
                    // PHASE 3 OPTIMIZATION: FTS5-First Search Strategy
                    // 1. FTS5 Content Search (lightning fast)
                    await SearchWithFTS5Async(ctx, queryNorm, projectId, scopePath, maxResults, emittedPaths, foundCallback, cancellationToken).ConfigureAwait(false);

                    // 2. Filename Search (indexed, very fast)
                    if (emittedPaths.Count < maxResults)
                    {
                        await SearchFilenamesAsync(ctx, queryNorm, queryNoAccent, projectId, scopePath, maxResults, emittedPaths, foundCallback, cancellationToken).ConfigureAwait(false);
                    }

                    // 3. On-Demand Index Fallback (for files not yet in FTS)
                    if (_contentIndexService != null && emittedPaths.Count < maxResults)
                    {
                        await SearchWithOnDemandIndexAsync(ctx, queryNorm, queryNoAccent, projectId, scopePath, maxResults, emittedPaths, foundCallback, cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Search Error: {ex.Message}");
                }
            }

            // MONITORING: Log execution time
            sw.Stop();
            System.Diagnostics.Debug.WriteLine($"[Perf] Search '{query}' took {sw.ElapsedMilliseconds}ms. Found {emittedPaths.Count} items.");
            return [.. emittedPaths];
        }

        /// <summary>
        /// Phase 3: Search using FTS5 virtual table (fastest method)
        /// </summary>
        private static async Task SearchWithFTS5Async(
            AppDbContext ctx,
            string queryNorm,
            int projectId,
            string scopePath,
            int maxResults,
            HashSet<string> emittedPaths,
            Action<FileEntry, List<SearchMatch>> foundCallback,
            CancellationToken cancellationToken)
        {
            try
            {
                // Phase 3: Improved FTS5 query syntax
                // Escape special FTS5 characters for safe query
                var queryFts = queryNorm
                    .Replace("\"", "\"\"")
                    .Replace("'", "''")
                    .Replace("*", "")
                    .Replace("?", "")
                    .Trim();

                if (string.IsNullOrEmpty(queryFts))
                {
                    System.Diagnostics.Debug.WriteLine("[FTS5] Query is empty after escaping, skipping FTS search");
                    return;
                }

                // FIX: Use prefix search with trailing * for better accent/partial matching
                var matchQuery = $"\"{queryFts}\"*";

                System.Diagnostics.Debug.WriteLine($"[FTS5] Searching for: {matchQuery}");

                // Execute FTS5 search with proper table reference
                // FIX: Use files_fts in the MATCH clause, not ffts alias
                var ftsPaths = await ctx.Database
                    .SqlQueryRaw<string>(
                        "SELECT f.path FROM files_fts JOIN files f ON f.id = files_fts.rowid WHERE files_fts MATCH {0} LIMIT {1}",
                        matchQuery,
                        maxResults)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                System.Diagnostics.Debug.WriteLine($"[FTS5] Query returned {ftsPaths.Count} paths");

                if (ftsPaths.Count > 0)
                {
                    // Get file entries for matched paths
                    var filesQuery = ctx.Files.AsNoTracking()
                        .Where(f => f.ProjectId == projectId && ftsPaths.Contains(f.Path));

                    if (!string.IsNullOrEmpty(scopePath))
                    {
                        filesQuery = filesQuery.Where(f => f.Path.StartsWith(scopePath));
                    }

                    var files = await filesQuery.ToListAsync(cancellationToken).ConfigureAwait(false);

                    foreach (var file in files)
                    {
                        if (cancellationToken.IsCancellationRequested) break;
                        if (emittedPaths.Contains(file.Path)) continue;

                        // Get content for snippet generation
                        var contentEntry = await ctx.ContentIndex.AsNoTracking()
                            .FirstOrDefaultAsync(x => x.FilePath == file.Path, cancellationToken)
                            .ConfigureAwait(false);

                        if (contentEntry != null)
                        {
                            var queryNoAccent = VietnameseTextHelper.RemoveAccents(queryNorm);
                            var matchDetails = FindMatchesInContent(contentEntry.Content, queryNorm, queryNoAccent);
                            foundCallback?.Invoke(file, matchDetails);
                            emittedPaths.Add(file.Path);

                            if (emittedPaths.Count >= maxResults) break;
                        }
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[FTS5] No results found, will use fallback search");
                }
            }
            catch (Exception ex)
            {
                // FTS5 might not be available or query syntax error
                System.Diagnostics.Debug.WriteLine($"[FTS5] Search failed (will fallback): {ex.GetType().Name} - {ex.Message}");
                if (ex.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[FTS5] Inner: {ex.InnerException.Message}");
                }
            }
        }

        /// <summary>
        /// Search by filename (indexed field, very fast)
        /// </summary>
        private static async Task SearchFilenamesAsync(
            AppDbContext ctx,
            string queryNorm,
            string queryNoAccent,
            int projectId,
            string scopePath,
            int maxResults,
            HashSet<string> emittedPaths,
            Action<FileEntry, List<SearchMatch>> foundCallback,
            CancellationToken cancellationToken)
        {
            var nameQuery = ctx.Files.AsNoTracking().Where(f => f.ProjectId == projectId);

            if (!string.IsNullOrEmpty(scopePath))
            {
                nameQuery = nameQuery.Where(f => f.Path.StartsWith(scopePath));
            }

            var nameMatches = await nameQuery
                .Where(f => (f.FilenameNoAccent != null && f.FilenameNoAccent.Contains(queryNoAccent, StringComparison.OrdinalIgnoreCase))
                         || f.Filename.Contains(queryNorm, StringComparison.OrdinalIgnoreCase))
                .Take(maxResults)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            foreach (var file in nameMatches)
            {
                if (cancellationToken.IsCancellationRequested) break;
                if (emittedPaths.Contains(file.Path)) continue;

                var match = new SearchMatch { MatchType = "filename", Text = file.Filename };
                foundCallback?.Invoke(file, [match]);
                emittedPaths.Add(file.Path);

                if (emittedPaths.Count >= maxResults) break;
            }
        }

        /// <summary>
        /// Fallback: Search files not yet indexed in FTS using on-demand indexing
        /// </summary>
        private async Task SearchWithOnDemandIndexAsync(
            AppDbContext ctx,
            string queryNorm,
            string queryNoAccent,
            int projectId,
            string scopePath,
            int maxResults,
            HashSet<string> emittedPaths,
            Action<FileEntry, List<SearchMatch>> foundCallback,
            CancellationToken cancellationToken)
        {
            var toConsider = ctx.Files.AsNoTracking().Where(f => f.ProjectId == projectId);

            if (!string.IsNullOrEmpty(scopePath))
            {
                toConsider = toConsider.Where(f => f.Path.StartsWith(scopePath));
            }

            var fileList = await toConsider
                .OrderBy(f => f.Path)
                .Take(maxResults * 5)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            // Batch load existing content from DB
            var filePaths = fileList.Select(f => f.Path).ToList();
            var existingContent = await ctx.ContentIndex.AsNoTracking()
                .Where(ci => filePaths.Contains(ci.FilePath))
                .ToDictionaryAsync(ci => ci.FilePath, ci => ci.Content, cancellationToken)
                .ConfigureAwait(false);

            foreach (var file in fileList)
            {
                if (cancellationToken.IsCancellationRequested) break;
                if (emittedPaths.Contains(file.Path)) continue;
                if (emittedPaths.Count >= maxResults) break;

                string content = null;

                // Check if we have cached content
                if (existingContent.TryGetValue(file.Path, out var cached))
                {
                    content = cached;
                }
                else
                {
                    // Build index on-demand for files not yet indexed
                    content = await _contentIndexService.GetOrBuildIndexedContentAsync(file.Path, cancellationToken).ConfigureAwait(false);
                }

                if (string.IsNullOrEmpty(content)) continue;
                if (!content.Contains(queryNorm) && !content.Contains(queryNoAccent)) continue;

                var matchDetails = FindMatchesInContent(content, queryNorm, queryNoAccent);
                if (matchDetails.Count > 0)
                {
                    foundCallback?.Invoke(file, matchDetails);
                    emittedPaths.Add(file.Path);
                }
            }
        }

        private static List<SearchMatch> FindMatchesInContent(string content, string queryNorm, string queryNoAccent)
        {
            var matches = new List<SearchMatch>();
            if (string.IsNullOrEmpty(content)) return matches;

            using (var reader = new System.IO.StringReader(content))
            {
                string line;
                int lineNum = 0;
                while ((line = reader.ReadLine()) != null)
                {
                    lineNum++;
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    if (line.Trim() == "__NOACCENT__") break; // STOP! Only search the first half (NFC) for correct line numbering.

                    string lineNorm = VietnameseTextHelper.NormalizeText(line);

                    if (lineNorm.Contains(queryNorm))
                    {
                        // FIX: Build index map to correctly map normalized offsets to original text
                        var (lineNoAccentForMap, map) = VietnameseTextHelper.BuildNoAccentAndMap(line.Normalize(System.Text.NormalizationForm.FormC));
                        int idx = lineNorm.IndexOf(queryNorm, StringComparison.Ordinal);
                        while (idx != -1)
                        {
                            // Map normalized index back to original text index
                            int start = (idx < map.Count) ? map[idx] : idx;
                            int endIdxRaw = idx + queryNorm.Length - 1;
                            int end = (endIdxRaw < map.Count) ? map[endIdxRaw] + 1 : start + queryNorm.Length;

                            matches.Add(new SearchMatch
                            {
                                Line = lineNum,
                                Text = line.Trim(),
                                MatchType = "text",
                                Start = start,
                                End = end
                            });
                            idx = lineNorm.IndexOf(queryNorm, idx + 1, StringComparison.Ordinal);
                        }
                    }
                    else if (!string.IsNullOrEmpty(queryNoAccent))
                    {
                        var (lineNoAccent, map) = VietnameseTextHelper.BuildNoAccentAndMap(line.Normalize(System.Text.NormalizationForm.FormC));
                        if (lineNoAccent.Contains(queryNoAccent))
                        {
                            int idx = lineNoAccent.IndexOf(queryNoAccent, StringComparison.Ordinal);
                            while (idx != -1)
                            {
                                int start = (idx < map.Count) ? map[idx] : idx;
                                int endIdxRaw = idx + queryNoAccent.Length - 1;
                                int end = (endIdxRaw < map.Count) ? map[endIdxRaw] + 1 : start + queryNoAccent.Length;

                                matches.Add(new SearchMatch
                                {
                                    Line = lineNum,
                                    Text = line.Trim(),
                                    MatchType = "text",
                                    Start = start,
                                    End = end
                                });
                                idx = lineNoAccent.IndexOf(queryNoAccent, idx + 1, StringComparison.Ordinal);
                            }
                        }
                    }
                }
            }
            return matches;
        }
    }
}
