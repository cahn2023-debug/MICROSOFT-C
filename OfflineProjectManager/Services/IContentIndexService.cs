using System.Collections.Generic;
using System.Threading;
using Task = System.Threading.Tasks.Task;

namespace OfflineProjectManager.Services
{
    public interface IContentIndexService
    {
        Task EnsureFilesScannedAsync(int projectId, string scopePath = null, CancellationToken cancellationToken = default);
        Task<string> GetOrBuildIndexedContentAsync(string filePath, CancellationToken cancellationToken = default);
        Task<Dictionary<string, int>> GetMatchCountByFileAsync(IEnumerable<string> filePaths, string queryNorm, string queryNoAccent, CancellationToken cancellationToken = default);
    }
}
