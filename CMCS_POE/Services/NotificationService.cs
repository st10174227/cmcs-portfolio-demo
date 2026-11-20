using CMCS_POE.Models;
using CMCS_POE.Data;

namespace CMCS_POE.Services
{
    public class NotificationService : INotificationService
    {
        private readonly INotificationRepository _notificationRepository;

        public NotificationService(INotificationRepository notificationRepository)
        {
            _notificationRepository = notificationRepository;
        }

        public void NotifyClaimSubmitted(Claim claim)
        {
            var notification = new Notification
            {
                Title = "Claim Submitted",
                Message = $"Claim #{claim.ClaimId} for {claim.LecturerName} ({claim.Month}) has been submitted for verification.",
                Type = "info",
                Icon = "bi-file-earmark-check",
                ClaimId = claim.ClaimId,
                ActionUrl = $"/Coordinator/Index"
            };
            _notificationRepository.Add(notification);
        }

        public void NotifyClaimVerified(Claim claim)
        {
            var notification = new Notification
            {
                Title = "Claim Verified",
                Message = $"Claim #{claim.ClaimId} for {claim.LecturerName} has been verified by the coordinator.",
                Type = "success",
                Icon = "bi-check-circle",
                ClaimId = claim.ClaimId,
                ActionUrl = $"/Manager/Index"
            };
            _notificationRepository.Add(notification);
        }

        public void NotifyClaimRejected(Claim claim, string reason)
        {
            var notification = new Notification
            {
                Title = "Claim Rejected",
                Message = $"Claim #{claim.ClaimId} for {claim.LecturerName} was rejected. Reason: {reason}",
                Type = "danger",
                Icon = "bi-x-circle",
                ClaimId = claim.ClaimId,
                ActionUrl = $"/Claim/Details/{claim.ClaimId}"
            };
            _notificationRepository.Add(notification);
        }

        public void NotifyClaimApproved(Claim claim)
        {
            var notification = new Notification
            {
                Title = "Claim Approved",
                Message = $"Claim #{claim.ClaimId} for {claim.LecturerName} (R{claim.TotalAmount:N2}) has been approved and is ready for payment.",
                Type = "success",
                Icon = "bi-check-circle-fill",
                ClaimId = claim.ClaimId,
                ActionUrl = $"/HR/ProcessPayments"
            };
            _notificationRepository.Add(notification);
        }

        public void NotifyClaimUpdated(Claim claim)
        {
            var notification = new Notification
            {
                Title = "Claim Updated",
                Message = $"Claim #{claim.ClaimId} for {claim.LecturerName} has been updated successfully.",
                Type = "info",
                Icon = "bi-pencil-square",
                ClaimId = claim.ClaimId,
                ActionUrl = $"/Claim/Details/{claim.ClaimId}"
            };
            _notificationRepository.Add(notification);
        }

        public void NotifyClaimDeleted(int claimId, string lecturerName)
        {
            var notification = new Notification
            {
                Title = "Claim Deleted",
                Message = $"Claim #{claimId} for {lecturerName} has been permanently deleted.",
                Type = "warning",
                Icon = "bi-trash",
                ClaimId = claimId,
                ActionUrl = "/Claim/Index"
            };
            _notificationRepository.Add(notification);
        }
        public void NotifyPaymentProcessed(Claim claim)
        {
            var notification = new Notification
            {
                Title = "Payment Processed",
                Message = $"Payment of R{claim.TotalAmount:N2} for Claim #{claim.ClaimId} ({claim.LecturerName}) has been processed. Ref: {claim.PaymentReference}",
                Type = "success",
                Icon = "bi-cash-coin",
                ClaimId = claim.ClaimId,
                ActionUrl = $"/HR/PaymentHistory"
            };
            _notificationRepository.Add(notification);
        }

        public void NotifyBulkPaymentProcessed(int claimCount, decimal totalAmount)
        {
            var notification = new Notification
            {
                Title = "Bulk Payment Processed",
                Message = $"Successfully processed {claimCount} payments totaling R{totalAmount:N2}.",
                Type = "success",
                Icon = "bi-cash-stack",
                ActionUrl = "/HR/PaymentHistory"
            };
            _notificationRepository.Add(notification);
        }

        public void NotifyLecturerAdded(Lecturer lecturer)
        {
            var notification = new Notification
            {
                Title = "Lecturer Added",
                Message = $"New lecturer {lecturer.FullName} has been added to the system.",
                Type = "success",
                Icon = "bi-person-plus",
                ActionUrl = "/HR/Lecturers"
            };
            _notificationRepository.Add(notification);
        }

        public void NotifyLecturerUpdated(Lecturer lecturer)
        {
            var notification = new Notification
            {
                Title = "Lecturer Updated",
                Message = $"Lecturer profile for {lecturer.FullName} has been updated.",
                Type = "info",
                Icon = "bi-person-check",
                ActionUrl = "/HR/Lecturers"
            };
            _notificationRepository.Add(notification);
        }

        public void NotifyLecturerDeactivated(string lecturerName)
        {
            var notification = new Notification
            {
                Title = "Lecturer Deactivated",
                Message = $"Lecturer {lecturerName} has been deactivated.",
                Type = "warning",
                Icon = "bi-person-x",
                ActionUrl = "/HR/Lecturers"
            };
            _notificationRepository.Add(notification);
        }
        public void NotifyDocumentUploaded(int claimId, string fileName)
        {
            var notification = new Notification
            {
                Title = "Document Uploaded",
                Message = $"Document '{fileName}' has been uploaded for Claim #{claimId}.",
                Type = "info",
                Icon = "bi-file-earmark-arrow-up",
                ClaimId = claimId,
                ActionUrl = $"/Claim/Details/{claimId}"
            };
            _notificationRepository.Add(notification);
        }

        public void NotifyDocumentReplaced(int claimId, string oldFileName, string newFileName)
        {
            var notification = new Notification
            {
                Title = "Document Replaced",
                Message = $"Document for Claim #{claimId} has been replaced: '{oldFileName}' → '{newFileName}'",
                Type = "info",
                Icon = "bi-file-earmark-arrow-up",
                ClaimId = claimId,
                ActionUrl = $"/Claim/Details/{claimId}"
            };
            _notificationRepository.Add(notification);
        }

        public void NotifyValidationWarning(Claim claim, string warning)
        {
            var notification = new Notification
            {
                Title = "Validation Warning",
                Message = $"Claim #{claim.ClaimId} for {claim.LecturerName}: {warning}",
                Type = "warning",
                Icon = "bi-exclamation-triangle",
                ClaimId = claim.ClaimId,
                ActionUrl = $"/Claim/Details/{claim.ClaimId}"
            };
            _notificationRepository.Add(notification);
        }

        public void NotifyHighValueClaim(Claim claim)
        {
            var notification = new Notification
            {
                Title = "High Value Claim",
                Message = $"⚠️ High value claim detected: Claim #{claim.ClaimId} for R{claim.TotalAmount:N2} requires additional review.",
                Type = "warning",
                Icon = "bi-exclamation-diamond",
                ClaimId = claim.ClaimId,
                ActionUrl = $"/Coordinator/Details/{claim.ClaimId}"
            };
            _notificationRepository.Add(notification);
        }

        public void NotifyReportGenerated(string reportType, string month)
        {
            var notification = new Notification
            {
                Title = "Report Generated",
                Message = $"{reportType} report for {month} has been generated successfully.",
                Type = "success",
                Icon = "bi-file-earmark-pdf",
                ActionUrl = "/HR/GenerateMonthlyReport"
            };
            _notificationRepository.Add(notification);
        }

        public void NotifyExportCompleted(string exportType, int recordCount)
        {
            var notification = new Notification
            {
                Title = "Export Completed",
                Message = $"{exportType} export completed: {recordCount} records exported successfully.",
                Type = "success",
                Icon = "bi-file-earmark-excel",
                ActionUrl = "/HR/Index"
            };
            _notificationRepository.Add(notification);
        }

        public void NotifySystemError(string errorMessage)
        {
            var notification = new Notification
            {
                Title = "System Error",
                Message = $"⚠️ An error occurred: {errorMessage}",
                Type = "danger",
                Icon = "bi-exclamation-octagon",
                ActionUrl = "/Home/Index"
            };
            _notificationRepository.Add(notification);
        }

        public void NotifyDeadlineApproaching(int claimId, string lecturerName, string deadline)
        {
            var notification = new Notification
            {
                Title = "Deadline Approaching",
                Message = $"⏰ Claim #{claimId} for {lecturerName} is approaching deadline: {deadline}",
                Type = "warning",
                Icon = "bi-clock-history",
                ClaimId = claimId,
                ActionUrl = $"/Claim/Details/{claimId}"
            };
            _notificationRepository.Add(notification);
        }

        public void NotifyPendingApprovals(int pendingCount)
        {
            var notification = new Notification
            {
                Title = "Pending Approvals",
                Message = $"You have {pendingCount} claim{(pendingCount > 1 ? "s" : "")} waiting for approval.",
                Type = "info",
                Icon = "bi-hourglass-split",
                ActionUrl = "/Coordinator/Index"
            };
            _notificationRepository.Add(notification);
        }

        public void SendCustomNotification(string title, string message, string type, string icon, string actionUrl, int? claimId = null)
        {
            var notification = new Notification
            {
                Title = title,
                Message = message,
                Type = type,
                Icon = icon,
                ClaimId = claimId,
                ActionUrl = actionUrl
            };
            _notificationRepository.Add(notification);
        }
    }
}
//OpenAI.2025.Chat-GPT(Version 5).[Large language model]Available at: https://chatgpt.com/c/68b46a0b-99f8-8326-98b4-5245e5c93d35[Accessed: 20 November 2025].