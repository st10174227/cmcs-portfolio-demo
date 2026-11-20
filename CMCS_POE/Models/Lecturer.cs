using System.ComponentModel.DataAnnotations;
namespace CMCS_POE.Models
{
    public class Lecturer
    {
        [Key]
        public int LecturerId { get; set; }
        [Required]
        [StringLength(100)]
        public string FullName { get; set; } = string.Empty;
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
        [Phone]
        [DisplayFormat(ConvertEmptyStringToNull = false)]
        public string PhoneNumber { get; set; } = string.Empty;
        [StringLength(100)]
        [DisplayFormat(ConvertEmptyStringToNull = false)]
        public string Department { get; set; } = string.Empty;
        [Range(typeof(decimal), "0", "1000")]
        public decimal StandardHourlyRate { get; set; }
        public bool IsActive { get; set; } = true;
        public virtual ICollection<Claim> Claims { get; set; } = new List<Claim>();
    }
}
//OpenAI.2025.Chat-GPT(Version 5).[Large language model]Available at: https://chatgpt.com/c/68b46a0b-99f8-8326-98b4-5245e5c93d35[Accessed: 20 November 2025].