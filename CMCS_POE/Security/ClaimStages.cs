namespace CMCS_POE.Security;

public static class ClaimStages
{
    public const string CoordinatorPending = "Pending Coordinator Verification";
    public const string ManagerPending = "Pending Manager Approval";
    public const string Approved = "Approved";
    public const string CoordinatorRejected = "RejectedByCoordinator";
    public const string ManagerRejected = "RejectedByManager";

    public static bool CanEdit(Models.Claim claim) => !claim.IsPaid &&
        (claim.Status == "Submitted" || claim.Status == CoordinatorPending);
    public static bool CanCoordinate(Models.Claim claim) => CanEdit(claim);
    public static bool CanManage(Models.Claim claim) => !claim.IsPaid &&
        (claim.Status == "Verified" || claim.Status == ManagerPending);
    public static bool CanRecordPayment(Models.Claim claim) => !claim.IsPaid && claim.Status == Approved;
}
