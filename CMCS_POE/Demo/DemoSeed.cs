using CMCS_POE.Data;
using CMCS_POE.Models;
using CMCS_POE.Security;
using CMCS_POE.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CMCS_POE.Demo;

public static class DemoSeed
{
    public static async Task InitializeAsync(IServiceProvider services, DemoSession session)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        if (await db.Users.AnyAsync()) return;
        var roles = services.GetRequiredService<RoleManager<IdentityRole>>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        foreach (var role in WorkspaceRoles.All) Require(await roles.CreateAsync(new IdentityRole(role)));
        var one = new Lecturer { FullName = "Demo Lecturer One", Email = "lecturer.one@demo.invalid", Department = "Computing", StandardHourlyRate = 250 };
        var two = new Lecturer { FullName = "Demo Lecturer Two", Email = "lecturer.two@demo.invalid", Department = "Mathematics", StandardHourlyRate = 300 };
        db.Lecturers.AddRange(one, two); await db.SaveChangesAsync();
        foreach (var role in WorkspaceRoles.All)
        {
            var user = new ApplicationUser
            {
                Id = session.Id + "-" + role.ToLowerInvariant(), UserName = role.ToLowerInvariant() + "@demo.invalid",
                Email = role.ToLowerInvariant() + "@demo.invalid", DisplayName = "Demo " + role,
                EmailConfirmed = true, LecturerId = role == WorkspaceRoles.Lecturer ? one.LecturerId : null
            };
            // Demo-only accounts have no password. The controlled demo endpoint signs them in.
            Require(await users.CreateAsync(user)); Require(await users.AddToRoleAsync(user, role));
        }
        var month = DateTime.UtcNow.ToString("yyyy-MM");
        Claim Sample(string status, double hours, string note) => new()
        {
            LecturerId = one.LecturerId, LecturerName = one.FullName, HourlyRate = one.StandardHourlyRate,
            Month = month, HoursWorked = hours, Status = status, Notes = note,
            CreatedDate = DateTime.UtcNow, ApprovedDate = status == ClaimStages.Approved ? DateTime.UtcNow : null
        };
        var submitted = Sample(ClaimStages.CoordinatorPending, 20, "Sample claim: start with coordinator review, then switch to manager and HR.");
        var manager = Sample(ClaimStages.ManagerPending, 12, "Sample ready for a manager decision.");
        var approved = Sample(ClaimStages.Approved, 8, "Approved sample: record a simulated payment in HR.");
        var paid = Sample(ClaimStages.Approved, 6, "Paid sample: download an invoice or report.");
        paid.IsPaid = true; paid.PaidDate = DateTime.UtcNow; paid.PaymentReference = "DEMO-PAID";
        var other = new Claim { LecturerId = two.LecturerId, LecturerName = two.FullName, HourlyRate = two.StandardHourlyRate,
            Month = month, HoursWorked = 10, Status = ClaimStages.CoordinatorPending, Notes = "Second lecturer sample, visible to staff only." };
        using var stream = new MemoryStream(new PdfService().GenerateInvoice(submitted));
        var document = services.GetRequiredService<ClaimDocuments>().Save(new FormFile(stream, 0, stream.Length, "Document", "sample-supporting-document.pdf"));
        submitted.DocumentPath = document.Path; submitted.DocumentName = document.Name;
        db.Claims.AddRange(submitted, manager, approved, paid, other);
        await db.SaveChangesAsync(); await transaction.CommitAsync();
    }
    private static void Require(IdentityResult result)
    {
        if (!result.Succeeded) throw new InvalidOperationException("Demo setup failed: " + string.Join("; ", result.Errors.Select(e => e.Description)));
    }
}
