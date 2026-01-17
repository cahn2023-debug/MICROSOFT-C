using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OfflineProjectManager.Models
{
    [Table("tasks")]
    public class ProjectTask
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("project_id")]
        public int ProjectId { get; set; }

        [Column("related_file_id")]
        public int? RelatedFileId { get; set; }

        [Required]
        [Column("name")]
        public string Name { get; set; }

        [Column("description")]
        public string Description { get; set; }

        [Column("status")]
        public string Status { get; set; } = "Todo"; // Todo, InProgress, Done

        [Column("priority")]
        public string Priority { get; set; } = "Normal";

        [Column("start_date")]
        public DateTime? StartDate { get; set; }

        [Column("end_date")]
        public DateTime? EndDate { get; set; }

        [Column("target_file_path")]
        public string TargetFilePath { get; set; }

        [Column("anchor_data")]
        public string AnchorData { get; set; }

        [Column("progress")]
        public double Progress { get; set; } = 0;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [Column("dependencies")]
        public string Dependencies { get; set; } // JSON string for predecessors

        public string GetCleanDescription()
        {
            if (string.IsNullOrEmpty(Description)) return string.Empty;
            int markerIndex = Description.IndexOf("<!--CONTEXT:");
            if (markerIndex >= 0)
            {
                return Description.Substring(0, markerIndex).Trim();
            }
            return Description;
        }

        public string GetAnchorData()
        {
            if (!string.IsNullOrEmpty(AnchorData)) return AnchorData;

            // Fallback for backward compatibility
            if (string.IsNullOrEmpty(Description)) return null;
            int start = Description.IndexOf("<!--CONTEXT:");
            if (start >= 0)
            {
                int jsonStart = start + "<!--CONTEXT:".Length;
                int end = Description.IndexOf("-->", jsonStart);
                if (end >= 0)
                {
                    return Description.Substring(jsonStart, end - jsonStart);
                }
            }
            return null;
        }

        [ForeignKey("ProjectId")]
        public virtual Project Project { get; set; }

        [ForeignKey("RelatedFileId")]
        public virtual FileEntry RelatedFile { get; set; }

        [Column("assignee_id")]
        public int? AssigneeId { get; set; }

        [ForeignKey("AssigneeId")]
        public virtual Personnel Assignee { get; set; }

        [Column("contract_id")]
        public int? ContractId { get; set; }

        [ForeignKey("ContractId")]
        public virtual Contract Contract { get; set; }
    }
}
