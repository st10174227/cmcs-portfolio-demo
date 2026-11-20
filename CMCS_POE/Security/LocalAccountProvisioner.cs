using System.Security.Cryptography;
using System.Text;
using CMCS_POE.Data;
using CMCS_POE.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CMCS_POE.Security;

public static class LocalAccountProvisioner
{
    public static async Task InitializeAsync(IServiceProvider services, string directory)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        if (await db.Users.AnyAsync()) return; // Existing passwords are never reset by startup.
        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = services.GetRequiredService<RoleManager<IdentityRole>>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        foreach (var role in WorkspaceRoles.All)
            if (!await roles.RoleExistsAsync(role)) Require(await roles.CreateAsync(new IdentityRole(role)));

        var lecturers = new[]
        {
            new Lecturer { FullName = "Demo Lecturer One", Email = "lecturer.one@demo.invalid", Department = "Demo", StandardHourlyRate = 250 },
            new Lecturer { FullName = "Demo Lecturer Two", Email = "lecturer.two@demo.invalid", Department = "Demo", StandardHourlyRate = 300 }
        };
        db.Lecturers.AddRange(lecturers);
        await db.SaveChangesAsync();
        var accounts = new[]
        {
            ("lecturer.one@demo.invalid", "Demo Lecturer One", WorkspaceRoles.Lecturer, (int?)lecturers[0].LecturerId),
            ("lecturer.two@demo.invalid", "Demo Lecturer Two", WorkspaceRoles.Lecturer, (int?)lecturers[1].LecturerId),
            ("coordinator@demo.invalid", "Demo Coordinator", WorkspaceRoles.Coordinator, (int?)null),
            ("manager@demo.invalid", "Demo Manager", WorkspaceRoles.Manager, (int?)null),
            ("hr@demo.invalid", "Demo HR", WorkspaceRoles.HR, (int?)null)
        };
        var text = new StringBuilder("CMCS personal local accounts\nGenerated on this PC. Keep this file private; do not add it to Git or paste it in chat.\n\n");
        foreach (var (email, name, role, lecturerId) in accounts)
        {
            var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)) + "aA1!";
            var user = new ApplicationUser
            {
                UserName = email, Email = email, EmailConfirmed = true, DisplayName = name,
                LecturerId = lecturerId, IsActive = true, LockoutEnabled = true
            };
            Require(await users.CreateAsync(user, password));
            Require(await users.AddToRoleAsync(user, role));
            text.AppendLine($"Role: {role}\nEmail: {email}\nPassword: {password}\n");
        }

        var credentials = Path.Combine(directory, "LOCAL-ACCOUNTS-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..8] + ".txt");
        var committed = false;
        try
        {
            await File.WriteAllTextAsync(credentials, text.ToString(), new UTF8Encoding(false));
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(credentials, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            await transaction.CommitAsync();
            committed = true;
            Console.WriteLine("Local accounts created. Credentials file: " + credentials);
        }
        finally
        {
            if (!committed && File.Exists(credentials)) File.Delete(credentials);
        }
    }

    private static void Require(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException("Local account setup failed: " + string.Join("; ", result.Errors.Select(e => e.Description)));
    }
}
