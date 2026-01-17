using Microsoft.EntityFrameworkCore;
using OfflineProjectManager.Models;
using System.IO;

namespace OfflineProjectManager.Data
{
    public class AppDbContext : DbContext
    {
        private readonly string _connectionString;

        public AppDbContext(string connectionString)
        {
            _connectionString = connectionString;
        }

        public DbSet<Project> Projects { get; set; }
        public DbSet<ProjectFolder> Folders { get; set; }
        public DbSet<FileEntry> Files { get; set; }
        public DbSet<ContentIndexEntry> ContentIndex { get; set; }
        public DbSet<ProjectTask> Tasks { get; set; }
        public DbSet<Note> Notes { get; set; }
        public DbSet<Personnel> Personnel { get; set; }
        public DbSet<Contract> Contracts { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                // Phase 6: WAL mode + optimizations
                optionsBuilder.UseSqlite(_connectionString, options =>
                {
                    options.CommandTimeout(30);
                    options.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                });

                // Disable tracking by default for better performance
                optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
            }
        }

        /// <summary>
        /// Phase 6: Initialize WAL mode and optimize SQLite settings
        /// </summary>
        public async System.Threading.Tasks.Task InitializeWALModeAsync()
        {
            await Database.OpenConnectionAsync().ConfigureAwait(false);

            try
            {
                // Enable WAL (Write-Ahead Logging)
                await Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;").ConfigureAwait(false);

                // Optimize settings
                await Database.ExecuteSqlRawAsync("PRAGMA synchronous=NORMAL;").ConfigureAwait(false);  // Faster than FULL, still safe
                await Database.ExecuteSqlRawAsync("PRAGMA cache_size=-64000;").ConfigureAwait(false);   // 64MB cache
                await Database.ExecuteSqlRawAsync("PRAGMA temp_store=MEMORY;").ConfigureAwait(false);   // Temp tables in RAM
                await Database.ExecuteSqlRawAsync("PRAGMA mmap_size=30000000000;").ConfigureAwait(false); // 30GB memory-mapped I/O
                await Database.ExecuteSqlRawAsync("PRAGMA wal_autocheckpoint=1000;").ConfigureAwait(false); // Checkpoint every 1000 pages

                System.Diagnostics.Debug.WriteLine("[Database] WAL mode enabled with optimizations");
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Database] WAL initialization failed: {ex.Message}");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Phase 6: CRITICAL - Ignore System.Threading.Tasks.Task to prevent EF Core confusion
            modelBuilder.Ignore<System.Threading.Tasks.Task>();

            // Explicitly configure ProjectTask entity
            modelBuilder.Entity<ProjectTask>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.ToTable("tasks");

                // Explicit column mappings to prevent EF Core from generating incorrect column names
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            });

            // Configure relationships
            modelBuilder.Entity<Project>()
                .HasMany(p => p.Files)
                .WithOne(f => f.Project)
                .HasForeignKey(f => f.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Project>()
                .HasMany(p => p.Tasks)
                .WithOne(t => t.Project)
                .HasForeignKey(t => t.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Project>()
                .HasMany(p => p.Notes)
                .WithOne(n => n.Project)
                .HasForeignKey(n => n.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Project>()
                .HasMany(p => p.Folders)
                .WithOne(f => f.Project)
                .HasForeignKey(f => f.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
