using CMCS_POE.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace CMCS_POE.ViewModels
{
    public class ClaimSubmissionViewModel
    {
        public Claim Claim { get; set; } = new Claim();
        public List<string> ValidationWarnings { get; set; } = new List<string>();
        public decimal CalculatedTotal { get; set; }
    }

    public class CoordinatorDashboardViewModel
    {
        public List<ClaimWithValidation> PendingClaims { get; set; } = new List<ClaimWithValidation>();
        public int TotalPending { get; set; }
        public int ClaimsWithIssues { get; set; }
        public decimal TotalPendingAmount { get; set; }
    }

    public class ClaimWithValidation
    {
        public Claim Claim { get; set; } = new Claim();
        public List<string> ValidationWarnings { get; set; } = new List<string>();
        public bool HasIssues => ValidationWarnings.Any();
    }

    public class ManagerDashboardViewModel
    {
        public List<ClaimWithValidation> PendingApproval { get; set; } = new List<ClaimWithValidation>();
        public int TotalPendingApproval { get; set; }
        public decimal TotalAmountPendingApproval { get; set; }
        public List<Claim> RecentlyApproved { get; set; } = new List<Claim>();
    }


    public class UpdateUserViewModel
    {
        public int UserId { get; set; }

        [Required]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Role { get; set; } = string.Empty;

        public string? PhoneNumber { get; set; }
        public string? Department { get; set; }
        public decimal HourlyRate { get; set; }
        public bool IsActive { get; set; }
        public bool ResetPassword { get; set; }
    }
}

//OpenAI.2025.Chat-GPT(Version 5).[Large language model]Available at: https://chatgpt.com/c/68b46a0b-99f8-8326-98b4-5245e5c93d35[Accessed: 20 November 2025].