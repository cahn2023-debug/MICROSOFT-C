using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OfflineProjectManager.Models
{
    [Table("projects")]
    public class Project
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("name")]
        public string Name { get; set; }

        [Required]
        [Column("root_path")]
        public string RootPath { get; set; }

        [Column("description")]
        public string Description { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public virtual ICollection<FileEntry> Files { get; set; } = new HashSet<FileEntry>();
        public virtual ICollection<ProjectTask> Tasks { get; set; } = new HashSet<ProjectTask>();
        public virtual ICollection<Note> Notes { get; set; } = new HashSet<Note>();
        public virtual ICollection<ProjectFolder> Folders { get; set; } = new HashSet<ProjectFolder>();
    }
}
