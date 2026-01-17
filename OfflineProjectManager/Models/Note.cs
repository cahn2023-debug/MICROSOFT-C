using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OfflineProjectManager.Models
{
    [Table("notes")]
    public class Note
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("project_id")]
        public int ProjectId { get; set; }

        [Required]
        [Column("title")]
        public string Title { get; set; }

        [Column("content")]
        public string Content { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("target_file_path")]
        public string TargetFilePath { get; set; }

        [Column("anchor_data")]
        public string AnchorData { get; set; }

        public string GetCleanContent()
        {
            if (string.IsNullOrEmpty(Content)) return string.Empty;
            int markerIndex = Content.IndexOf("<!--CONTEXT:");
            if (markerIndex >= 0)
            {
                return Content.Substring(0, markerIndex).Trim();
            }
            return Content;
        }

        public string GetAnchorData()
        {
            if (!string.IsNullOrEmpty(AnchorData)) return AnchorData;
            
            if (string.IsNullOrEmpty(Content)) return null;
            int start = Content.IndexOf("<!--CONTEXT:");
            if (start >= 0)
            {
                int jsonStart = start + "<!--CONTEXT:".Length;
                int end = Content.IndexOf("-->", jsonStart);
                if (end >= 0)
                {
                    return Content.Substring(jsonStart, end - jsonStart);
                }
            }
            return null;
        }

        [ForeignKey("ProjectId")]
        public virtual Project Project { get; set; }
    }
}
