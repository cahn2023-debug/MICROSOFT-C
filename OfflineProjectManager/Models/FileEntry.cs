using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OfflineProjectManager.Models
{
    [Table("files")]
    public class FileEntry
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("project_id")]
        public int ProjectId { get; set; }

        [Required]
        [Column("path")]
        public string Path { get; set; } // Full absolute path

        [Column("path_noaccent")]
        public string PathNoAccent { get; set; }

        [Required]
        [Column("filename")]
        public string Filename { get; set; }

        [Column("filename_noaccent")]
        public string FilenameNoAccent { get; set; }

        [Column("extension")]
        public string Extension { get; set; }

        [Column("size")]
        public long Size { get; set; } = 0;

        [Column("file_type")]
        public string FileType { get; set; } // Text, Binary, Image, etc.

        [Column("created_at")]
        public DateTime? CreatedAt { get; set; }

        [Column("modified_at")]
        public DateTime? ModifiedAt { get; set; }

        // Metadata and Content Indexing
        [Column("content_summary")]
        public string ContentSummary { get; set; }

        [Column("content_index")]
        public string ContentIndex { get; set; } // Full text for search interaction

        [Column("metadata_json")]
        public string MetadataJson { get; set; } // JSON string for extra properties

        [ForeignKey("ProjectId")]
        public virtual Project Project { get; set; }

        public virtual ICollection<Task> Tasks { get; set; } = new HashSet<Task>();
    }
}
