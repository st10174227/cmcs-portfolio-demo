using CMCS_Part2.Models;
using CMCS_POE.Models;

namespace CMCS_POE.Services
{
    public interface INotificationService
    {
        void NotifyClaimSubmitted(Claim claim);
        void NotifyClaimVerified(Claim claim);
        void NotifyClaimRejected(Claim claim, string reason);
        void NotifyClaimApproved(Claim claim);
        void NotifyClaimUpdated(Claim claim);
        void NotifyClaimDeleted(int claimId, string lecturerName);

        void NotifyPaymentProcessed(Claim claim);
        void NotifyBulkPaymentProcessed(int claimCount, decimal totalAmount);

        void NotifyLecturerAdded(Lecturer lecturer);
        void NotifyLecturerUpdated(Lecturer lecturer);
        void NotifyLecturerDeactivated(string lecturerName);

        void NotifyDocumentUploaded(int claimId, string fileName);
        void NotifyDocumentReplaced(int claimId, string oldFileName, string newFileName);

        void NotifyValidationWarning(Claim claim, string warning);
        void NotifyHighValueClaim(Claim claim);

        void NotifyReportGenerated(string reportType, string month);
        void NotifyExportCompleted(string exportType, int recordCount);

        void NotifySystemError(string errorMessage);
        void NotifyDeadlineApproaching(int claimId, string lecturerName, string deadline);
        void NotifyPendingApprovals(int pendingCount);

        void SendCustomNotification(string title, string message, string type, string icon, string actionUrl, int? claimId = null);
    }
}
//OpenAI.2025.Chat-GPT(Version 5).[Large language model]Available at: https://chatgpt.com/c/68b46a0b-99f8-8326-98b4-5245e5c93d35[Accessed: 20 November 2025].