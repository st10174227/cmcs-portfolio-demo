using CMCS_POE.Models;

namespace CMCS_POE.ViewModels
{
    public class HRDashboardViewModel
    {
        public int TotalApprovedClaims { get; set; }
        public int TotalUnpaidClaims { get; set; }
        public int TotalPaidClaims { get; set; }
        public decimal TotalAmountApproved { get; set; }
        public decimal TotalAmountPaid { get; set; }
        public decimal TotalAmountOutstanding { get; set; }
        public List<Claim> RecentApprovedClaims { get; set; } = new List<Claim>();
        public List<MonthlyStats> MonthlyBreakdown { get; set; } = new List<MonthlyStats>();
    }

    public class MonthlyStats
    {
        public string Month { get; set; } = string.Empty;
        public int ClaimCount { get; set; }
        public decimal TotalAmount { get; set; }
        public int PaidCount { get; set; }
        public decimal PaidAmount { get; set; }
    }

    public class PaymentBatchViewModel
    {
        public List<Claim> UnpaidClaims { get; set; } = new List<Claim>();
        public decimal TotalAmount { get; set; }
        public int SelectedCount { get; set; }
    }
}
//OpenAI.2025.Chat-GPT(Version 5).[Large language model]Available at: https://chatgpt.com/c/68b46a0b-99f8-8326-98b4-5245e5c93d35[Accessed: 20 November 2025].