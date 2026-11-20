using CMCS_POE.Models;

namespace CMCS_POE.Services
{
    public interface IAuthenticationService
    {
        Task<Lecturer?> ValidateUserAsync(string email, string password);
        bool VerifyPassword(Lecturer user, string password);
        string HashPassword(Lecturer user, string password);
        Task LogAuditAsync(int? userId, string action, string? entityType = null, int? entityId = null, string? details = null);
    }
}
//OpenAI.2025.Chat-GPT(Version 5).[Large language model]Available at: https://chatgpt.com/c/68b46a0b-99f8-8326-98b4-5245e5c93d35[Accessed: 20 November 2025].