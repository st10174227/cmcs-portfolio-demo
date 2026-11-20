using System.ComponentModel.DataAnnotations;

namespace CMCS_POE.Models
{
    public class AuditLog
    {
        [Key]
        public int AuditLogId { get; set; }

        public int? UserId { get; set; }

        [StringLength(255)]
        public string? UserEmail { get; set; }

        [Required]
        [StringLength(100)]
        public string Action { get; set; } = string.Empty; 

        [StringLength(100)]
        public string? EntityType { get; set; } 

        public int? EntityId { get; set; }

        [StringLength(500)]
        public string? Details { get; set; }

        [StringLength(50)]
        public string? IpAddress { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}

//OpenAI.2025.Chat-GPT(Version 5).[Large language model]Available at: https://chatgpt.com/c/68b46a0b-99f8-8326-98b4-5245e5c93d35[Accessed: 20 November 2025].