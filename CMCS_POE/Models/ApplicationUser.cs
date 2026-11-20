using Microsoft.AspNetCore.Identity;

namespace CMCS_POE.Models;

public sealed class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
    public int? LecturerId { get; set; }
    public bool IsActive { get; set; } = true;
}
