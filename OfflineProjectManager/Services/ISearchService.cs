using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OfflineProjectManager.Models;

namespace OfflineProjectManager.Services
{
    public class SearchMatch
    {
        public int Line { get; set; }        // 1-indexed
        public int Col { get; set; }         // 1-indexed
        public string Text { get; set; }     // Content preview
        public string MatchType { get; set; } // 'text', 'filename'
        public int Start { get; set; }
        public int End { get; set; }
    }

    public interface ISearchService
    {
        Task<List<string>> SearchFilesAsync(
            string query, 
            int projectId, 
            string scopePath = null, 
            CancellationToken cancellationToken = default,
            System.Action<FileEntry, List<SearchMatch>> foundCallback = null,
            int maxResults = 1000);
    }
}
