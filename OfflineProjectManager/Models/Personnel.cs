using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace OfflineProjectManager.Models
{
    [Table("personnel")]
    public class Personnel
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("project_id")]
        public int ProjectId { get; set; }

        [Column("name")]
        public string Name { get; set; }

        [Column("phone")]
        public string Phone { get; set; }

        [Column("region")]
        public string Region { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("role")]
        public string Role { get; set; }
    }
}
