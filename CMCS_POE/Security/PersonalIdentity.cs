using CMCS_POE.Demo;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Security.Claims;
using CMCS_POE.Data;
using CMCS_POE.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CMCS_POE.Security;

public static class PersonalIdentity
{
    public static IdentityBuilder AddPersonalIdentity(this IServiceCollection services, bool development, bool demo = false)
    {
        services.TryAddScoped<DemoRequestContext>();
        var prefix = demo ? "cmcs-demo" : "cmcs-personal";
        var identity = services.AddIdentity<ApplicationUser, IdentityRole>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 12;
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
            options.SignIn.RequireConfirmedAccount = false;
            // Explicit schema options are also used by the design-time factory.
            options.Stores.SchemaVersion = IdentitySchemaVersions.Version1;
            options.Stores.MaxLengthForKeys = 0;
        }).AddEntityFrameworkStores<ApplicationDbContext>()
          .AddClaimsPrincipalFactory<WorkspacePrincipalFactory>()
          .AddDefaultTokenProviders();

        services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.Zero);
        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = prefix + ".auth";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = development ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            options.LoginPath = "/Account/Login";
            options.AccessDeniedPath = "/Account/AccessDenied";
            options.ExpireTimeSpan = demo ? TimeSpan.FromMinutes(DemoOptions.Minutes) : TimeSpan.FromHours(8);
            options.SlidingExpiration = false;
            options.Events.OnValidatePrincipal = async context =>
            {
                if (demo)
                {
                    var workspace = context.HttpContext.RequestServices.GetRequiredService<DemoRequestContext>();
                    if (workspace.Session is null || context.Principal?.FindFirst(DemoOptions.WorkspaceClaim)?.Value != workspace.Session.Id)
                    {
                        context.RejectPrincipal();
                        await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
                        return;
                    }
                }
                await SecurityStampValidator.ValidatePrincipalAsync(context);
                if (context.Principal?.Identity?.IsAuthenticated != true) return;
                var users = context.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
                var user = await users.GetUserAsync(context.Principal);
                var valid = user is { IsActive: true };
                if (valid && user?.LecturerId is int lecturerId)
                {
                    var db = context.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
                    valid = await db.Lecturers.AnyAsync(l => l.LecturerId == lecturerId && l.IsActive);
                }
                if (!valid)
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
                }
            };
        });
        foreach (var (scheme, name) in new[]
        {
            (IdentityConstants.ExternalScheme, prefix + ".external"),
            (IdentityConstants.TwoFactorRememberMeScheme, prefix + ".remember"),
            (IdentityConstants.TwoFactorUserIdScheme, prefix + ".two-factor")
        })
        {
            services.Configure<CookieAuthenticationOptions>(scheme, options =>
            {
                options.Cookie.Name = name;
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = development ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            });
        }
        return identity;
    }
}

public sealed class WorkspacePrincipalFactory : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>
{
    private readonly DemoRequestContext _workspace;
    public WorkspacePrincipalFactory(UserManager<ApplicationUser> users, RoleManager<IdentityRole> roles,
        IOptions<IdentityOptions> options, DemoRequestContext workspace) : base(users, roles, options) { _workspace = workspace; }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        if (_workspace.Session is not null)
            identity.AddClaim(new System.Security.Claims.Claim(DemoOptions.WorkspaceClaim, _workspace.Session.Id));
        identity.AddClaim(new System.Security.Claims.Claim(WorkspaceRoles.DisplayNameClaim, user.DisplayName));
        if (user.LecturerId is int id)
            identity.AddClaim(new System.Security.Claims.Claim(WorkspaceRoles.LecturerIdClaim,
                id.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        return identity;
    }
}
