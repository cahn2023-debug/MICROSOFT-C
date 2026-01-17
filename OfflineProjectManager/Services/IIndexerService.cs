using System.Threading.Tasks;

namespace OfflineProjectManager.Services
{
    public interface IIndexerService
    {
        string ExtractText(string filePath);
    }
}
