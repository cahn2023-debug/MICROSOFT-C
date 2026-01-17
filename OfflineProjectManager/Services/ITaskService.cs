using System;
using System.Collections.Generic;
using OfflineProjectManager.Models;

namespace OfflineProjectManager.Services
{
    public interface ITaskService
    {
        event Action TasksChanged;
        void NotifyTasksChanged();

        // Tasks
        System.Threading.Tasks.Task<ProjectTask> CreateTaskAsync(int projectId, string name, string description = null, DateTime? startDate = null, DateTime? endDate = null, int? relatedFileId = null);
        System.Threading.Tasks.Task UpdateTaskAsync(ProjectTask task);
        System.Threading.Tasks.Task DeleteTaskAsync(int taskId);
        System.Threading.Tasks.Task<ProjectTask> GetTaskByIdAsync(int taskId);
        System.Threading.Tasks.Task<List<ProjectTask>> GetTasksByProjectAsync(int projectId);
        System.Threading.Tasks.Task<List<ProjectTask>> GetTasksByFileAsync(int projectId, int fileId);
        System.Threading.Tasks.Task<List<ProjectTask>> GetTasksByPathAsync(int projectId, string filePath, bool includeParents = true, bool includeChildren = false);
        System.Threading.Tasks.Task<ProjectTask> CreateTaskFromFileAsync(int projectId, string filePath);

        // Notes
        System.Threading.Tasks.Task<Note> CreateNoteAsync(int projectId, string title, string content = null);
        System.Threading.Tasks.Task UpdateNoteAsync(Note note);
        System.Threading.Tasks.Task DeleteNoteAsync(int noteId);
        System.Threading.Tasks.Task<List<Note>> GetNotesByProjectAsync(int projectId);
        System.Threading.Tasks.Task<List<Note>> GetNotesByPathAsync(int projectId, string filePath, bool includeParents = true, bool includeChildren = false);

        // Utils
        System.Threading.Tasks.Task<int?> GetFileIdByPathAsync(int projectId, string filePath);
    }
}
