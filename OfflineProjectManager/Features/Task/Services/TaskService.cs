using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using OfflineProjectManager.Data;
using OfflineProjectManager.Models;
using OfflineProjectManager.Services;

namespace OfflineProjectManager.Features.Task.Services
{
    public class TaskService : ITaskService
    {
        private readonly DbContextPool _dbContextPool;
        private readonly IIndexerService _indexerService;
        public event Action TasksChanged;

        public TaskService(DbContextPool dbContextPool, IIndexerService indexerService)
        {
            _dbContextPool = dbContextPool;
            _indexerService = indexerService;
        }

        public void NotifyTasksChanged() => TasksChanged?.Invoke();

        public async System.Threading.Tasks.Task<ProjectTask> CreateTaskAsync(int projectId, string name, string description = null, DateTime? startDate = null, DateTime? endDate = null, int? relatedFileId = null)
        {
            using var pooledCtx = await _dbContextPool.GetContextAsync();
            var task = new ProjectTask
            {
                ProjectId = projectId,
                Name = name,
                Description = description,
                Status = "Todo",
                Priority = "Normal",
                StartDate = startDate,
                EndDate = endDate,
                RelatedFileId = relatedFileId
            };
            pooledCtx.Context.Tasks.Add(task);
            await pooledCtx.Context.SaveChangesAsync().ConfigureAwait(false);
            NotifyTasksChanged();
            return task;
        }

        public async System.Threading.Tasks.Task UpdateTaskAsync(ProjectTask task)
        {
            using var pooledCtx = await _dbContextPool.GetContextAsync();
            var existing = await pooledCtx.Context.Tasks.FindAsync(task.Id).ConfigureAwait(false);
            if (existing != null)
            {
                // Force clean state from DB to ensure we aren't working with stale cached data
                await pooledCtx.Context.Entry(existing).ReloadAsync().ConfigureAwait(false);

                existing.Name = task.Name;
                existing.Description = task.Description;
                existing.Status = task.Status;
                existing.Priority = task.Priority;
                existing.StartDate = task.StartDate;
                existing.EndDate = task.EndDate;

                // Force Modified state to guaranteed SQL UPDATE even if values appear same to tracker
                pooledCtx.Context.Entry(existing).State = EntityState.Modified;

                await pooledCtx.Context.SaveChangesAsync().ConfigureAwait(false);
                NotifyTasksChanged();
            }
        }

        public async System.Threading.Tasks.Task DeleteTaskAsync(int taskId)
        {
            using var pooledCtx = await _dbContextPool.GetContextAsync();
            var task = await pooledCtx.Context.Tasks.FindAsync(taskId).ConfigureAwait(false);
            if (task != null)
            {
                pooledCtx.Context.Tasks.Remove(task);
                await pooledCtx.Context.SaveChangesAsync().ConfigureAwait(false);
                NotifyTasksChanged();
            }
        }

        public async System.Threading.Tasks.Task<ProjectTask> GetTaskByIdAsync(int taskId)
        {
            using var pooledCtx = await _dbContextPool.GetContextAsync();
            return await pooledCtx.Context.Tasks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == taskId).ConfigureAwait(false);
        }

        public async System.Threading.Tasks.Task<List<ProjectTask>> GetTasksByProjectAsync(int projectId)
        {
            using var pooledCtx = await _dbContextPool.GetContextAsync();
            return await pooledCtx.Context.Tasks.AsNoTracking()
                .Where(t => t.ProjectId == projectId)
                .OrderByDescending(t => t.Id)
                .ToListAsync();
        }

        public async System.Threading.Tasks.Task<List<ProjectTask>> GetTasksByFileAsync(int projectId, int fileId)
        {
            using (var pooledCtx = await _dbContextPool.GetContextAsync())
            {
                return await pooledCtx.Context.Tasks.AsNoTracking()
                    .Where(t => t.ProjectId == projectId && t.RelatedFileId == fileId)
                    .ToListAsync();
            }
        }

        public async System.Threading.Tasks.Task<List<ProjectTask>> GetTasksByPathAsync(int projectId, string filePath, bool includeParents = true, bool includeChildren = false)
        {
            using (var pooledCtx = await _dbContextPool.GetContextAsync())
            {
                var query = pooledCtx.Context.Tasks.AsNoTracking()
                    .Where(t => t.ProjectId == projectId);

                if (includeParents && includeChildren)
                {
                    query = query.Where(t => t.TargetFilePath == filePath || filePath.StartsWith(t.TargetFilePath) || t.TargetFilePath.StartsWith(filePath));
                }
                else if (includeParents)
                {
                    query = query.Where(t => t.TargetFilePath == filePath || filePath.StartsWith(t.TargetFilePath));
                }
                else if (includeChildren)
                {
                    query = query.Where(t => t.TargetFilePath == filePath || t.TargetFilePath.StartsWith(filePath));
                }
                else
                {
                    query = query.Where(t => t.TargetFilePath == filePath);
                }

                return await query.OrderByDescending(t => t.Id).ToListAsync().ConfigureAwait(false);
            }
        }

        public async System.Threading.Tasks.Task<ProjectTask> CreateTaskFromFileAsync(int projectId, string filePath)
        {
            try
            {
                string fileName = System.IO.Path.GetFileName(filePath);
                string content = _indexerService.ExtractText(filePath);

                // Use content as Name (Summary), truncate to 100 chars
                string name = content.Length > 100 ? content.Substring(0, 97) + "..." : content;
                if (string.IsNullOrWhiteSpace(name)) name = fileName; // Fallback to filename if empty

                using (var pooledCtx = await _dbContextPool.GetContextAsync())
                {
                    var file = await pooledCtx.Context.Files.FirstOrDefaultAsync(f => f.Path == filePath).ConfigureAwait(false);

                    var task = new ProjectTask
                    {
                        ProjectId = projectId,
                        Name = name,
                        Description = "", // Clean description
                        Status = "Todo",
                        Priority = "Normal",
                        RelatedFileId = file?.Id,
                        TargetFilePath = filePath
                    };

                    pooledCtx.Context.Tasks.Add(task);
                    await pooledCtx.Context.SaveChangesAsync().ConfigureAwait(false);
                    NotifyTasksChanged();
                    return task;
                }
            }
            catch { return null; }
        }

        public async System.Threading.Tasks.Task<Note> CreateNoteAsync(int projectId, string title, string content = null)
        {
            using (var pooledCtx = await _dbContextPool.GetContextAsync())
            {
                var note = new Note
                {
                    ProjectId = projectId,
                    Title = title,
                    Content = content,
                    CreatedAt = DateTime.UtcNow
                };
                pooledCtx.Context.Notes.Add(note);
                await pooledCtx.Context.SaveChangesAsync().ConfigureAwait(false);
                NotifyTasksChanged();
                return note;
            }
        }

        public async System.Threading.Tasks.Task UpdateNoteAsync(Note note)
        {
            using (var pooledCtx = await _dbContextPool.GetContextAsync())
            {
                var existing = await pooledCtx.Context.Notes.FindAsync(note.Id).ConfigureAwait(false);
                if (existing != null)
                {
                    // Force refresh from DB
                    await pooledCtx.Context.Entry(existing).ReloadAsync().ConfigureAwait(false);

                    existing.Title = note.Title;
                    existing.Content = note.Content;

                    // Force Modified state to guarantee UPDATE
                    pooledCtx.Context.Entry(existing).State = EntityState.Modified;

                    await pooledCtx.Context.SaveChangesAsync().ConfigureAwait(false);
                    NotifyTasksChanged();
                }
            }
        }

        public async System.Threading.Tasks.Task DeleteNoteAsync(int noteId)
        {
            using (var pooledCtx = await _dbContextPool.GetContextAsync())
            {
                var note = await pooledCtx.Context.Notes.FindAsync(noteId).ConfigureAwait(false);
                if (note != null)
                {
                    pooledCtx.Context.Notes.Remove(note);
                    await pooledCtx.Context.SaveChangesAsync().ConfigureAwait(false);
                    NotifyTasksChanged();
                }
            }
        }

        public async System.Threading.Tasks.Task<List<Note>> GetNotesByProjectAsync(int projectId)
        {
            using (var pooledCtx = await _dbContextPool.GetContextAsync())
            {
                return await pooledCtx.Context.Notes.AsNoTracking()
                   .Where(n => n.ProjectId == projectId)
                   .OrderByDescending(n => n.Id)
                   .ToListAsync();
            }
        }

        public async System.Threading.Tasks.Task<List<Note>> GetNotesByPathAsync(int projectId, string filePath, bool includeParents = true, bool includeChildren = false)
        {
            using (var pooledCtx = await _dbContextPool.GetContextAsync())
            {
                var query = pooledCtx.Context.Notes.AsNoTracking()
                   .Where(n => n.ProjectId == projectId);

                if (includeParents && includeChildren)
                {
                    query = query.Where(n => n.TargetFilePath == filePath || filePath.StartsWith(n.TargetFilePath) || n.TargetFilePath.StartsWith(filePath));
                }
                else if (includeParents)
                {
                    query = query.Where(n => n.TargetFilePath == filePath || filePath.StartsWith(n.TargetFilePath));
                }
                else if (includeChildren)
                {
                    query = query.Where(n => n.TargetFilePath == filePath || n.TargetFilePath.StartsWith(filePath));
                }
                else
                {
                    query = query.Where(n => n.TargetFilePath == filePath);
                }

                return await query.OrderByDescending(n => n.Id).ToListAsync().ConfigureAwait(false);
            }
        }

        public async System.Threading.Tasks.Task<int?> GetFileIdByPathAsync(int projectId, string filePath)
        {
            using (var pooledCtx = await _dbContextPool.GetContextAsync())
            {
                var file = await pooledCtx.Context.Files.AsNoTracking()
                    .FirstOrDefaultAsync(f => f.ProjectId == projectId && f.Path == filePath);
                return file?.Id;
            }
        }
    }
}
