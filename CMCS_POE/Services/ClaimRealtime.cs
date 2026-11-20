using CMCS_POE.Data;
using CMCS_POE.Models;
using CMCS_POE.Security;
using CMCS_Part2.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace CMCS_POE.Services;

public sealed class ClaimRealtime
{
    private readonly ApplicationDbContext _db;
    private readonly IHubContext<ClaimHub> _hub;
    public ClaimRealtime(ApplicationDbContext db, IHubContext<ClaimHub> hub) { _db = db; _hub = hub; }

    public async Task ChangedAsync(Claim claim, bool deleted = false)
    {
        var recipients = await (
            from user in _db.Users
            join membership in _db.UserRoles on user.Id equals membership.UserId
            join role in _db.Roles on membership.RoleId equals role.Id
            where user.IsActive &&
                (role.Name == WorkspaceRoles.Coordinator || role.Name == WorkspaceRoles.Manager || role.Name == WorkspaceRoles.HR ||
                 (role.Name == WorkspaceRoles.Lecturer && user.LecturerId == claim.LecturerId &&
                  _db.Lecturers.Any(l => l.LecturerId == claim.LecturerId && l.IsActive)))
            select user.Id).Distinct().ToListAsync();
        if (recipients.Count == 0) return;
        if (deleted) await _hub.Clients.Users(recipients).SendAsync("DeleteClaim", claim.ClaimId);
        else await _hub.Clients.Users(recipients).SendAsync("UpdateClaimStatus", claim.ClaimId, claim.Status);
    }
}
