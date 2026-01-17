using System.Collections.Generic;
using System.Threading.Tasks;
using OfflineProjectManager.Models;
using Task = System.Threading.Tasks.Task;

namespace OfflineProjectManager.Services
{
    public interface IProjectService
    {
        event Action ProjectChanged;
        Project CurrentProject { get; }
        bool IsProjectOpen { get; }

        // Core Actions
        Task<Project> CreateProjectAsync(string name, string folderPath);
        Task<Project> LoadProjectAsync(string pmpPath);
        void CloseProject();
        System.Threading.Tasks.Task SaveProjectAsync();

        // Folder Management
        System.Threading.Tasks.Task AddFolderAsync(string folderPath);
        System.Threading.Tasks.Task RemoveFolderAsync(string folderPath);
        List<string> GetProjectFolders();

        // New: Encapsulated File IO for UI
        System.Threading.Tasks.Task<List<string>> GetDirectoriesAsync(string path);
        System.Threading.Tasks.Task<List<string>> GetFilesAsync(string path);

        // Resource Management (Personnel)
        Task<List<Personnel>> GetPersonnelAsync();
        Task<Personnel> AddPersonnelAsync(Personnel personnel);
        Task<Personnel> UpdatePersonnelAsync(Personnel personnel);
        System.Threading.Tasks.Task DeletePersonnelAsync(int id);

        // Resource Management (Contracts)
        Task<List<Contract>> GetContractsAsync();
        Task<Contract> AddContractAsync(Contract contract);
        Task<Contract> UpdateContractAsync(Contract contract);
        System.Threading.Tasks.Task DeleteContractAsync(int id);

        // Utils
        string GetDbPath();
    }
}
