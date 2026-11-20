using System.Security.Claims;

namespace CMCS_POE.Security;

public static class WorkspaceRoles
{
    public const string Lecturer = "Lecturer";
    public const string Coordinator = "Coordinator";
    public const string Manager = "Manager";
    public const string HR = "HR";
    public const string Staff = Coordinator + "," + Manager + "," + HR;
    public const string LecturerIdClaim = "cmcs:lecturer-id";
    public const string DisplayNameClaim = "cmcs:display-name";

    public static readonly string[] All = { Lecturer, Coordinator, Manager, HR };
    public static int? LecturerId(this ClaimsPrincipal principal) =>
        int.TryParse(principal.FindFirstValue(LecturerIdClaim), out var id) && id > 0 ? id : null;
    public static bool IsStaff(this ClaimsPrincipal principal) =>
        principal.IsInRole(Coordinator) || principal.IsInRole(Manager) || principal.IsInRole(HR);
    public static bool CanRead(this ClaimsPrincipal principal, Models.Claim claim) =>
        principal.IsStaff() || (principal.IsInRole(Lecturer) && principal.LecturerId() == claim.LecturerId);
}
