using CMCS_POE.Models;

    namespace CMCS_POE.Services
    {
        public class ClaimValidationService : IClaimValidationService
        {
            private const double MAX_HOURS_PER_MONTH = 200;
            private const decimal MAX_HOURLY_RATE = 1000;
            private const decimal MAX_TOTAL_AMOUNT = 100000;
            private const double WARNING_HOURS_THRESHOLD = 160;
            private const decimal WARNING_AMOUNT_THRESHOLD = 50000;

            public ClaimValidationResult ValidateClaim(Claim claim)
            {
                var result = new ClaimValidationResult { IsValid = true };

                if (claim.HoursWorked <= 0)
                {
                    result.IsValid = false;
                    result.Errors.Add("Hours worked must be greater than 0");
                }

                if (claim.HoursWorked > MAX_HOURS_PER_MONTH)
                {
                    result.IsValid = false;
                    result.Errors.Add($"Hours worked cannot exceed {MAX_HOURS_PER_MONTH} per month");
                }

                if (claim.HourlyRate <= 0)
                {
                    result.IsValid = false;
                    result.Errors.Add("Hourly rate must be greater than 0");
                }

                if (claim.HourlyRate > MAX_HOURLY_RATE)
                {
                    result.IsValid = false;
                    result.Errors.Add($"Hourly rate cannot exceed R{MAX_HOURLY_RATE}");
                }

                if (claim.TotalAmount > MAX_TOTAL_AMOUNT)
                {
                    result.IsValid = false;
                    result.Errors.Add($"Total claim cannot exceed R{MAX_TOTAL_AMOUNT:N2}");
                }

                result.Warnings = GetValidationWarnings(claim);

                return result;
            }

            public bool IsClaimWithinLimits(Claim claim)
            {
                return claim.HoursWorked > 0 &&
                       claim.HoursWorked <= MAX_HOURS_PER_MONTH &&
                       claim.HourlyRate > 0 &&
                       claim.HourlyRate <= MAX_HOURLY_RATE &&
                       claim.TotalAmount <= MAX_TOTAL_AMOUNT;
            }

            public List<string> GetValidationWarnings(Claim claim)
            {
                var warnings = new List<string>();

                if (claim.HoursWorked > WARNING_HOURS_THRESHOLD)
                {
                    warnings.Add($"⚠️ High hours: {claim.HoursWorked} hours (threshold: {WARNING_HOURS_THRESHOLD})");
                }

                if (claim.TotalAmount > WARNING_AMOUNT_THRESHOLD)
                {
                    warnings.Add($"⚠️ High amount: R{claim.TotalAmount:N2} (threshold: R{WARNING_AMOUNT_THRESHOLD:N2})");
                }

                if (string.IsNullOrEmpty(claim.DocumentPath))
                {
                    warnings.Add("⚠️ No supporting document attached");
                }

                return warnings;
            }
        }
    }
//OpenAI.2025.Chat-GPT(Version 5).[Large language model]Available at: https://chatgpt.com/c/68b46a0b-99f8-8326-98b4-5245e5c93d35[Accessed: 20 November 2025].