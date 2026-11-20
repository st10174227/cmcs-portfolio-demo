using System.ComponentModel.DataAnnotations;

namespace CMCS_POE.Models
{
    public class Notification
    {
        public int NotificationId { get; set; }

        [Required]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Message { get; set; } = string.Empty;

        public string Type { get; set; } = "info"; 

        public int? ClaimId { get; set; }

        public bool IsRead { get; set; } = false;

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public string Icon { get; set; } = "bi-info-circle";

        public string ActionUrl { get; set; } = string.Empty;
    }
}
//OpenAI.2025.Chat-GPT(Version 5).[Large language model]Available at: https://chatgpt.com/c/68b46a0b-99f8-8326-98b4-5245e5c93d35[Accessed: 20 November 2025].