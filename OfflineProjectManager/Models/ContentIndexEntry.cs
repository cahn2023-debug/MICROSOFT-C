using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OfflineProjectManager.Models
{
    [Table("content_index")]
    public class ContentIndexEntry
    {
        [Key]
        [Column("file_path")]
        public string FilePath { get; set; }

        [Column("content")]
        public string Content { get; set; }

        [Column("last_modified")]
        public long LastModified { get; set; }

        /// <summary>
        /// SHA256 hash of file content for efficient change detection
        /// </summary>
        [Column("content_hash")]
        public string ContentHash { get; set; }

        /// <summary>
        /// File size in bytes for quick validation before hashing
        /// </summary>
        [Column("file_size")]
        public long FileSize { get; set; }
    }
}
