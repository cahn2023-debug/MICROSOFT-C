using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace OfflineProjectManager.Models
{
    [Table("contracts")]
    public class Contract
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("project_id")]
        public int ProjectId { get; set; }

        [Column("contractor_name")]
        public string ContractorName { get; set; }

        [Column("contract_code")]
        public string ContractCode { get; set; }

        [Column("bidding_package")]
        public string BiddingPackage { get; set; }

        [Column("content")]
        public string Content { get; set; }

        [Column("region")]
        public string Region { get; set; }

        [Column("volume")]
        public double Volume { get; set; }

        [Column("volume_unit")]
        public string VolumeUnit { get; set; }

        [Column("status")]
        public string Status { get; set; } = "Active";

        [Column("start_date")]
        public DateTime? StartDate { get; set; }

        [Column("end_date")]
        public DateTime? EndDate { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
