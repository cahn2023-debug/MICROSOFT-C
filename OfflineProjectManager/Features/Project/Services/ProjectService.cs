using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OfflineProjectManager.Data;
using OfflineProjectManager.Models;
using OfflineProjectManager.Services;
using Task = System.Threading.Tasks.Task;

namespace OfflineProjectManager.Features.Project.Services
{
    public class ProjectService : IProjectService
    {
        private readonly DbContextPool _dbContextPool;
        private string _currentDbPath;

        public event Action ProjectChanged;
        public OfflineProjectManager.Models.Project CurrentProject { get; private set; }
        public bool IsProjectOpen => CurrentProject != null;

        public string GetDbPath() => _currentDbPath;

        public ProjectService(DbContextPool dbContextPool)
        {
            _dbContextPool = dbContextPool ?? throw new ArgumentNullException(nameof(dbContextPool));
        }

        public async System.Threading.Tasks.Task<OfflineProjectManager.Models.Project> CreateProjectAsync(string name, string folderPath)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Project name cannot be empty.");

            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            string pmpPath = Path.Combine(folderPath, $"{name}.pmp");
            if (File.Exists(pmpPath))
                throw new InvalidOperationException($"Project file {pmpPath} already exists.");

            _currentDbPath = pmpPath;
            _dbContextPool.Initialize(_currentDbPath);

            using (var pooledCtx = await _dbContextPool.GetContextAsync())
            {
                var ctx = pooledCtx.Context;
                await InitDatabaseSchemaAsync(ctx).ConfigureAwait(false);

                var project = new OfflineProjectManager.Models.Project
                {
                    Name = name,
                    RootPath = folderPath,
                    Description = $"Created at {DateTime.UtcNow}",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                ctx.Projects.Add(project);

                try
                {
                    await ctx.SaveChangesAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // Get the full exception details including inner exceptions
                    var fullMessage = ex.Message;
                    var innerEx = ex.InnerException;
                    while (innerEx != null)
                    {
                        fullMessage += $"\nInner Exception: {innerEx.Message}";
                        innerEx = innerEx.InnerException;
                    }
                    throw new Exception($"Failed to save project: {fullMessage}", ex);
                }

                CurrentProject = project;
            }

            using (var pooledCtx = await _dbContextPool.GetContextAsync())
            {
                var ctx = pooledCtx.Context;
                CurrentProject = await ctx.Projects
                    .Include(p => p.Folders)
                    .AsNoTracking()
                    .FirstOrDefaultAsync();
            }

            ProjectChanged?.Invoke();
            return CurrentProject;
        }

        public async System.Threading.Tasks.Task<OfflineProjectManager.Models.Project> LoadProjectAsync(string pmpPath)
        {
            if (!File.Exists(pmpPath))
                throw new FileNotFoundException("Project file not found.", pmpPath);

            CloseProject();

            // CRITICAL FIX: Run raw SQL migrations BEFORE DbContext is created
            // EF validates model schema on DbContext creation, so columns MUST exist first
            await EnsureSchemaColumnsExistRawAsync(pmpPath).ConfigureAwait(false);

            _currentDbPath = pmpPath;
            _dbContextPool.Initialize(_currentDbPath);

            using (var pooledCtx = await _dbContextPool.GetContextAsync())
            {
                var ctx = pooledCtx.Context;
                try
                {
                    // CRITICAL: Run ALL schema operations BEFORE any EF queries
                    await InitDatabaseSchemaAsync(ctx).ConfigureAwait(false);

                    // Phase 6: Initialize WAL mode
                    await ctx.InitializeWALModeAsync().ConfigureAwait(false);

                    // NOW safe to query - all migrations have completed
                    var project = await ctx.Projects
                        .Include(p => p.Folders)
                        .AsNoTracking()
                        .FirstOrDefaultAsync();

                    if (project == null)
                    {
                        CloseProject();
                        throw new Exception("Invalid project file: No metadata found.");
                    }

                    CurrentProject = project;
                    ProjectChanged?.Invoke();
                    return CurrentProject;
                }
                catch (Exception)
                {
                    CloseProject();
                    throw;
                }
            }
        }

        /// <summary>
        /// Run raw SQL to add missing columns BEFORE EF DbContext is created.
        /// This prevents "no such column" errors when opening old databases.
        /// </summary>
        private static async System.Threading.Tasks.Task EnsureSchemaColumnsExistRawAsync(string dbPath)
        {
            var connectionString = $"Data Source={dbPath}";
            using var connection = new Microsoft.Data.Sqlite.SqliteConnection(connectionString);
            await connection.OpenAsync().ConfigureAwait(false);

            // Add missing columns to tasks table
            await SafeAddColumnRawAsync(connection, "tasks", "target_file_path", "TEXT");
            await SafeAddColumnRawAsync(connection, "tasks", "anchor_data", "TEXT");
            await SafeAddColumnRawAsync(connection, "tasks", "progress", "REAL DEFAULT 0");
            await SafeAddColumnRawAsync(connection, "tasks", "dependencies", "TEXT");
            await SafeAddColumnRawAsync(connection, "tasks", "created_at", "TIMESTAMP DEFAULT CURRENT_TIMESTAMP");
            await SafeAddColumnRawAsync(connection, "tasks", "updated_at", "TIMESTAMP DEFAULT CURRENT_TIMESTAMP");

            // Add missing columns to notes table
            await SafeAddColumnRawAsync(connection, "notes", "target_file_path", "TEXT");
            await SafeAddColumnRawAsync(connection, "notes", "anchor_data", "TEXT");
            await SafeAddColumnRawAsync(connection, "notes", "project_id", "INTEGER"); // Critical Fix

            // Add missing columns to tasks table for Personnel and Contracts
            await SafeAddColumnRawAsync(connection, "tasks", "assignee_id", "INTEGER");
            await SafeAddColumnRawAsync(connection, "tasks", "contract_id", "INTEGER");

            // Re-run table creation logic for new tables if they don't exist
            // (EF usually handles this if database is new, but for existing DBs we need to be careful)
            // Raw SQL is safest for existing DBs to avoid EF migration complexity
            string createPersonnel = @"
                CREATE TABLE IF NOT EXISTS personnel (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    project_id INTEGER NOT NULL,
                    name TEXT NOT NULL,
                    phone TEXT,
                    region TEXT,
                    role TEXT,
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY(project_id) REFERENCES projects(id) ON DELETE CASCADE
                );";

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = createPersonnel;
                await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }

            string createContracts = @"
                CREATE TABLE IF NOT EXISTS contracts (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    project_id INTEGER NOT NULL,
                    contractor_name TEXT NOT NULL,
                    contract_code TEXT,
                    bidding_package TEXT,
                    content TEXT,
                    region TEXT,
                    volume REAL,
                    volume_unit TEXT,
                    status TEXT DEFAULT 'Active',
                    start_date TIMESTAMP,
                    end_date TIMESTAMP,
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY(project_id) REFERENCES projects(id) ON DELETE CASCADE
                );";

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = createContracts;
                await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        private static async System.Threading.Tasks.Task SafeAddColumnRawAsync(Microsoft.Data.Sqlite.SqliteConnection connection, string tableName, string columnName, string columnType)
        {
            try
            {
                // Check if table exists first
                using var checkTableCmd = connection.CreateCommand();
                checkTableCmd.CommandText = $"SELECT name FROM sqlite_master WHERE type='table' AND name='{tableName}'";
                var tableExists = await checkTableCmd.ExecuteScalarAsync().ConfigureAwait(false);
                if (tableExists == null) return; // Table doesn't exist yet, skip

                // Check if column exists
                using var cmd = connection.CreateCommand();
                cmd.CommandText = $"PRAGMA table_info({tableName})";
                bool columnExists = false;
                using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
                {
                    while (await reader.ReadAsync().ConfigureAwait(false))
                    {
                        if (reader.GetString(1).Equals(columnName, StringComparison.OrdinalIgnoreCase))
                        {
                            columnExists = true;
                            break;
                        }
                    }
                }

                if (!columnExists)
                {
                    using var alterCmd = connection.CreateCommand();
                    alterCmd.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnType}";
                    await alterCmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                    System.Diagnostics.Debug.WriteLine($"[RawMigration] Added {tableName}.{columnName}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RawMigration] Error adding {tableName}.{columnName}: {ex.Message}");
                // Swallow - column may already exist or table not created yet
            }
        }

        public void CloseProject()
        {
            CurrentProject = null;
            _currentDbPath = null;
            _dbContextPool.Clear();
            ProjectChanged?.Invoke();
        }

        public async System.Threading.Tasks.Task SaveProjectAsync()
        {
            if (CurrentProject != null && !string.IsNullOrEmpty(_currentDbPath))
            {
                using (var pooledCtx = await _dbContextPool.GetContextAsync())
                {
                    var ctx = pooledCtx.Context;
                    var project = await ctx.Projects.FindAsync(CurrentProject.Id).ConfigureAwait(false);
                    if (project != null)
                    {
                        project.UpdatedAt = DateTime.UtcNow;
                        await ctx.SaveChangesAsync().ConfigureAwait(false);
                        CurrentProject.UpdatedAt = project.UpdatedAt;
                    }
                }
            }
        }

        public async System.Threading.Tasks.Task AddFolderAsync(string folderPath)
        {
            if (CurrentProject == null) return;

            using (var pooledCtx = await _dbContextPool.GetContextAsync())
            {
                var ctx = pooledCtx.Context;
                var exists = await ctx.Folders.AnyAsync(f => f.ProjectId == CurrentProject.Id && f.FolderPath == folderPath).ConfigureAwait(false);
                if (!exists)
                {
                    var folder = new ProjectFolder
                    {
                        ProjectId = CurrentProject.Id,
                        FolderPath = folderPath
                    };
                    ctx.Folders.Add(folder);
                    await ctx.SaveChangesAsync().ConfigureAwait(false);

                    CurrentProject.Folders.Add(folder);
                }
            }
        }

        public async System.Threading.Tasks.Task RemoveFolderAsync(string folderPath)
        {
            if (CurrentProject == null) return;

            using (var pooledCtx = await _dbContextPool.GetContextAsync())
            {
                var ctx = pooledCtx.Context;
                var folder = await ctx.Folders
                    .FirstOrDefaultAsync(f => f.ProjectId == CurrentProject.Id && f.FolderPath == folderPath);

                if (folder != null)
                {
                    using (var transaction = await ctx.Database.BeginTransactionAsync())
                    {
                        try
                        {
                            var filesToDelete = await ctx.Files
                                .Where(f => f.Path.StartsWith(folderPath) && f.ProjectId == CurrentProject.Id)
                                .Select(f => f.Id)
                                .ToListAsync();

                            if (filesToDelete.Any())
                            {
                                await ctx.Tasks
                                    .Where(t => t.RelatedFileId.HasValue && filesToDelete.Contains(t.RelatedFileId.Value))
                                    .ExecuteDeleteAsync();

                                await ctx.Files
                                    .Where(f => filesToDelete.Contains(f.Id))
                                    .ExecuteDeleteAsync();

                                await ctx.ContentIndex
                                    .Where(ci => ci.FilePath.StartsWith(folderPath))
                                    .ExecuteDeleteAsync();
                            }

                            ctx.Folders.Remove(folder);
                            await ctx.SaveChangesAsync().ConfigureAwait(false);
                            await transaction.CommitAsync().ConfigureAwait(false);
                        }
                        catch
                        {
                            await transaction.RollbackAsync().ConfigureAwait(false);
                            throw;
                        }
                    }
                }
            }
        }

        public List<string> GetProjectFolders()
        {
            if (CurrentProject == null) return new List<string>();
            return CurrentProject.Folders.Select(f => f.FolderPath).ToList();
        }

        public async System.Threading.Tasks.Task<List<string>> GetDirectoriesAsync(string path)
        {
            return await System.Threading.Tasks.Task.Run(() => Directory.GetDirectories(path).OrderBy(d => d).ToList()).ConfigureAwait(false);
        }

        public async System.Threading.Tasks.Task<List<string>> GetFilesAsync(string path)
        {
            return await System.Threading.Tasks.Task.Run(() => Directory.GetFiles(path).OrderBy(f => f).ToList()).ConfigureAwait(false);
        }

        private async System.Threading.Tasks.Task InitDatabaseSchemaAsync(AppDbContext ctx)
        {
            // 1.1 Mandatory Configuration
            string tuneSql = @"
                PRAGMA journal_mode = WAL;
                PRAGMA synchronous = NORMAL;
                PRAGMA foreign_keys = ON;
            ";
            await ctx.Database.ExecuteSqlRawAsync(tuneSql).ConfigureAwait(false);

            // 1.2 Projects Table
            string createProjectTable = @"
                CREATE TABLE IF NOT EXISTS projects (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    name TEXT NOT NULL,
                    root_path TEXT NOT NULL UNIQUE,
                    description TEXT,
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                );";

            // 1.3 Project Folders Table
            string createFoldersTable = @"
                CREATE TABLE IF NOT EXISTS project_folders (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    project_id INTEGER NOT NULL,
                    folder_path TEXT NOT NULL UNIQUE,
                    FOREIGN KEY(project_id) REFERENCES projects(id) ON DELETE CASCADE
                );";

            // 1.4 Files Table
            string createFilesTable = @"
                CREATE TABLE IF NOT EXISTS files (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    project_id INTEGER NOT NULL,
                    path TEXT NOT NULL UNIQUE,
                    path_noaccent TEXT,
                    filename TEXT NOT NULL,
                    filename_noaccent TEXT,
                    extension TEXT,
                    size INTEGER DEFAULT 0,
                    file_type TEXT,
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    modified_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    content_summary TEXT,
                    content_index TEXT,
                    metadata_json TEXT CHECK (metadata_json IS NULL OR json_valid(metadata_json)),
                    FOREIGN KEY(project_id) REFERENCES projects(id) ON DELETE CASCADE
                );";

            // Content Index (Keep existing for compatibility, but user suggests strict FTS later)
            string createContentIndexTable = @"
                CREATE TABLE IF NOT EXISTS content_index (
                    file_path TEXT PRIMARY KEY,
                    content TEXT,
                    last_modified INTEGER
                );";

            // 1.7 Tasks Table (Crucial)
            string createTasksTable = @"
                CREATE TABLE IF NOT EXISTS tasks (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    project_id INTEGER NOT NULL,
                    related_file_id INTEGER,
                    assignee_id INTEGER,
                    contract_id INTEGER,
                    name TEXT NOT NULL,
                    description TEXT,
                    status TEXT DEFAULT 'Todo',
                    priority TEXT DEFAULT 'Normal',
                    start_date TIMESTAMP,
                    end_date TIMESTAMP,
                    target_file_path TEXT,
                    anchor_data TEXT CHECK (anchor_data IS NULL OR json_valid(anchor_data)),
                    progress REAL DEFAULT 0,
                    dependencies TEXT CHECK (dependencies IS NULL OR json_valid(dependencies)),
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY(project_id) REFERENCES projects(id) ON DELETE CASCADE,
                    FOREIGN KEY(related_file_id) REFERENCES files(id) ON DELETE SET NULL,
                    FOREIGN KEY(assignee_id) REFERENCES personnel(id) ON DELETE SET NULL,
                    FOREIGN KEY(contract_id) REFERENCES contracts(id) ON DELETE SET NULL
                );";

            // 1.8 Notes Table (Fixed Design)
            string createNotesTable = @"
                CREATE TABLE IF NOT EXISTS notes (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    project_id INTEGER NOT NULL,
                    title TEXT NOT NULL,
                    content TEXT,
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    target_file_path TEXT,
                    anchor_data TEXT CHECK (anchor_data IS NULL OR json_valid(anchor_data)),
                    FOREIGN KEY(project_id) REFERENCES projects(id) ON DELETE CASCADE
                );";

            // 1.5 Personnel Table
            string createPersonnelTable = @"
                CREATE TABLE IF NOT EXISTS personnel (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    project_id INTEGER NOT NULL,
                    name TEXT NOT NULL,
                    phone TEXT,
                    region TEXT,
                    role TEXT,
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY(project_id) REFERENCES projects(id) ON DELETE CASCADE
                );";

            // 1.6 Contracts Table
            string createContractsTable = @"
                CREATE TABLE IF NOT EXISTS contracts (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    project_id INTEGER NOT NULL,
                    contractor_name TEXT NOT NULL,
                    contract_code TEXT,
                    bidding_package TEXT,
                    content TEXT,
                    region TEXT,
                    volume REAL,
                    volume_unit TEXT,
                    status TEXT DEFAULT 'Active',
                    start_date TIMESTAMP,
                    end_date TIMESTAMP,
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY(project_id) REFERENCES projects(id) ON DELETE CASCADE
                );";

            // Optimized Indices (Part 4.2)
            string indices = @"
                CREATE INDEX IF NOT EXISTS idx_file_project ON files (project_id);
                CREATE INDEX IF NOT EXISTS idx_file_name_project ON files (filename, project_id);
                CREATE INDEX IF NOT EXISTS idx_file_name_noaccent_project ON files (filename_noaccent, project_id);
                CREATE INDEX IF NOT EXISTS idx_file_path_noaccent_project ON files (path_noaccent, project_id);
                CREATE INDEX IF NOT EXISTS idx_files_project_path ON files(project_id, path);
                
                -- Task Indices
                CREATE INDEX IF NOT EXISTS idx_tasks_project_status ON tasks(project_id, status);
                CREATE INDEX IF NOT EXISTS idx_tasks_assignee ON tasks(assignee_id);
                CREATE INDEX IF NOT EXISTS idx_tasks_contract ON tasks(contract_id);
                CREATE INDEX IF NOT EXISTS idx_tasks_dates ON tasks(project_id, start_date, end_date);
                CREATE INDEX IF NOT EXISTS idx_tasks_target_file_path ON tasks(target_file_path);
                
                -- Note Indices
                CREATE INDEX IF NOT EXISTS idx_notes_project ON notes(project_id);
                CREATE INDEX IF NOT EXISTS idx_notes_path ON notes(target_file_path);
                
                -- Resource Indices
                CREATE INDEX IF NOT EXISTS idx_personnel_project ON personnel (project_id);
                CREATE INDEX IF NOT EXISTS idx_contracts_project ON contracts (project_id);
            ";

            await ctx.Database.ExecuteSqlRawAsync(createProjectTable).ConfigureAwait(false);
            await ctx.Database.ExecuteSqlRawAsync(createFoldersTable).ConfigureAwait(false);
            await ctx.Database.ExecuteSqlRawAsync(createFilesTable).ConfigureAwait(false);
            await ctx.Database.ExecuteSqlRawAsync(createContentIndexTable).ConfigureAwait(false);
            await ctx.Database.ExecuteSqlRawAsync(createTasksTable).ConfigureAwait(false);
            await ctx.Database.ExecuteSqlRawAsync(createNotesTable).ConfigureAwait(false);
            await ctx.Database.ExecuteSqlRawAsync(createPersonnelTable).ConfigureAwait(false);
            await ctx.Database.ExecuteSqlRawAsync(createContractsTable).ConfigureAwait(false);
            await ctx.Database.ExecuteSqlRawAsync(indices).ConfigureAwait(false);

            // CRITICAL: Run ALL migrations IMMEDIATELY AFTER table creation
            // This ensures old databases get all schema updates before Entity Framework validates models

            // 1. Add missing columns for backward compatibility
            await OfflineProjectManager.Data.Migrations.EnsureSchemaMigration.ApplyAsync(ctx).ConfigureAwait(false);

            // 2. Add FTS5 for search optimization
            await OfflineProjectManager.Migrations.AddFTS5Support.ApplyAsync(ctx).ConfigureAwait(false);

            // 3. Add task timestamps
            await OfflineProjectManager.Data.Migrations.AddTaskTimestamps.ApplyAsync(ctx).ConfigureAwait(false);
        }

        // Personnel CRUD
        public async System.Threading.Tasks.Task<List<Personnel>> GetPersonnelAsync()
        {
            if (CurrentProject == null) return new List<Personnel>();
            using var pooledCtx = await _dbContextPool.GetContextAsync();
            return await pooledCtx.Context.Personnel
                .Where(p => p.ProjectId == CurrentProject.Id)
                .OrderBy(p => p.Name)
                .ToListAsync();
        }

        public async System.Threading.Tasks.Task<Personnel> AddPersonnelAsync(Personnel personnel)
        {
            if (CurrentProject == null) return null;
            using var pooledCtx = await _dbContextPool.GetContextAsync();
            var ctx = pooledCtx.Context;

            personnel.ProjectId = CurrentProject.Id;
            ctx.Personnel.Add(personnel);
            await ctx.SaveChangesAsync().ConfigureAwait(false);
            return personnel;
        }

        public async System.Threading.Tasks.Task<Personnel> UpdatePersonnelAsync(Personnel personnel)
        {
            using var pooledCtx = await _dbContextPool.GetContextAsync();
            var ctx = pooledCtx.Context;

            var existing = await ctx.Personnel.FindAsync(personnel.Id);
            if (existing != null)
            {
                existing.Name = personnel.Name;
                existing.Phone = personnel.Phone;
                existing.Region = personnel.Region;
                existing.Role = personnel.Role;
                await ctx.SaveChangesAsync().ConfigureAwait(false);
                return existing;
            }
            return null;
        }

        public async System.Threading.Tasks.Task DeletePersonnelAsync(int id)
        {
            using var pooledCtx = await _dbContextPool.GetContextAsync();
            var ctx = pooledCtx.Context;
            await ctx.Personnel.Where(p => p.Id == id).ExecuteDeleteAsync();
        }

        // Contract CRUD
        public async System.Threading.Tasks.Task<List<Contract>> GetContractsAsync()
        {
            if (CurrentProject == null) return new List<Contract>();
            using var pooledCtx = await _dbContextPool.GetContextAsync();
            return await pooledCtx.Context.Contracts
                .Where(c => c.ProjectId == CurrentProject.Id)
                .OrderBy(c => c.ContractCode)
                .ToListAsync();
        }

        public async System.Threading.Tasks.Task<Contract> AddContractAsync(Contract contract)
        {
            if (CurrentProject == null) return null;
            using var pooledCtx = await _dbContextPool.GetContextAsync();
            var ctx = pooledCtx.Context;

            contract.ProjectId = CurrentProject.Id;
            ctx.Contracts.Add(contract);
            await ctx.SaveChangesAsync().ConfigureAwait(false);
            return contract;
        }

        public async System.Threading.Tasks.Task<Contract> UpdateContractAsync(Contract contract)
        {
            using var pooledCtx = await _dbContextPool.GetContextAsync();
            var ctx = pooledCtx.Context;

            var existing = await ctx.Contracts.FindAsync(contract.Id);
            if (existing != null)
            {
                existing.ContractorName = contract.ContractorName;
                existing.ContractCode = contract.ContractCode;
                existing.BiddingPackage = contract.BiddingPackage;
                existing.Content = contract.Content;
                existing.Region = contract.Region;
                existing.Volume = contract.Volume;
                existing.VolumeUnit = contract.VolumeUnit;
                existing.Status = contract.Status;
                existing.StartDate = contract.StartDate;
                existing.EndDate = contract.EndDate;

                await ctx.SaveChangesAsync().ConfigureAwait(false);
                return existing;
            }
            return null;
        }

        public async System.Threading.Tasks.Task DeleteContractAsync(int id)
        {
            using var pooledCtx = await _dbContextPool.GetContextAsync();
            var ctx = pooledCtx.Context;
            await ctx.Contracts.Where(c => c.Id == id).ExecuteDeleteAsync();
        }

        private async System.Threading.Tasks.Task SafeAddColumn(AppDbContext ctx, string tableName, string columnName, string type)
        {
            try
            {
                string sql = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {type}";
                await ctx.Database.ExecuteSqlRawAsync(sql).ConfigureAwait(false);
            }
            catch { }
        }
    }
}
