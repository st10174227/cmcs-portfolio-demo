using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace CMCS_POE.Models
{
    public class Claim
    {
        public int ClaimId { get; set; }

        [Required(ErrorMessage = "Please select a lecturer.")]
        public int LecturerId { get; set; }

        [ForeignKey("LecturerId")]
        public virtual Lecturer? Lecturer { get; set; }

        public string? LecturerName { get; set; }

        [Required(ErrorMessage = "Month is required.")]
        public string Month { get; set; } = string.Empty;

        [Range(1d, 200d, ErrorMessage = "Enter between 1 and 200 hours.")]
        public double HoursWorked { get; set; }

        [Range(typeof(decimal), "1", "1000", ErrorMessage = "Hourly rate must be between R1 and R1,000.")]
        public decimal HourlyRate { get; set; }

        public string? Notes { get; set; }

        public string Status { get; set; } = "Submitted";

        [NotMapped, ValidateNever]
        public decimal TotalAmount => (decimal)HoursWorked * HourlyRate;

        public string? DocumentName { get; set; }
        public string? DocumentPath { get; set; }

        // HR fields
        public bool IsPaid { get; set; } = false;
        public DateTime? PaidDate { get; set; }
        public string? PaymentReference { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime? ApprovedDate { get; set; }
    }
}
//OpenAI.2025.Chat-GPT(Version 5).[Large language model]Available at: https://chatgpt.com/c/68b46a0b-99f8-8326-98b4-5245e5c93d35[Accessed: 20 November 2025].