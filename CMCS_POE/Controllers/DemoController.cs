using CMCS_POE.Data;
using Microsoft.EntityFrameworkCore;
using CMCS_POE.Demo;
using CMCS_POE.Models;
using CMCS_POE.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CMCS_POE.Controllers;

[AllowAnonymous]
public sealed class DemoController : Controller
{
    private readonly DemoOptions _options;
    private readonly DemoSessions _sessions;
    private readonly DemoRequestContext _workspace;
    private readonly IServiceScopeFactory _scopes;
    public DemoController(DemoOptions options, DemoSessions sessions, DemoRequestContext workspace, IServiceScopeFactory scopes)
    { _options = options; _sessions = sessions; _workspace = workspace; _scopes = scopes; }

    [HttpGet]
    public IActionResult Index() => _options.Enabled ? View("~/Views/Home/Index.cshtml") : NotFound();

    [HttpPost, EnableRateLimiting("demo-start")]
    public async Task<IActionResult> Start(string role)
    {
        if (!_options.Enabled) return NotFound();
        if (!WorkspaceRoles.All.Contains(role, StringComparer.Ordinal)) return BadRequest("Choose one of the four demo roles.");
        var session = _workspace.Session;
        var created = session is null;
        session ??= await _sessions.CreateAsync(HttpContext.RequestAborted);
        if (session is null) return StatusCode(503, "All demo workspaces are in use. Please try again later.");
        try
        {
            await using var scope = _scopes.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<DemoRequestContext>().Session = session;
            if (created) await DemoSeed.InitializeAsync(scope.ServiceProvider, session);
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await users.FindByIdAsync(session.Id + "-" + role.ToLowerInvariant());
            if (user is null || !user.IsActive || !await users.IsInRoleAsync(user, role)) return StatusCode(409, "That demo role is unavailable. Reset the demo to restore its sample accounts.");
            if (user.LecturerId is int lecturerId && !await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
                .Lecturers.AnyAsync(l => l.LecturerId == lecturerId && l.IsActive))
                return StatusCode(409, "The sample lecturer is inactive. Reset the demo to restore the sample profile.");
            await scope.ServiceProvider.GetRequiredService<SignInManager<ApplicationUser>>().SignInAsync(user,
                new AuthenticationProperties { IsPersistent = false, ExpiresUtc = session.ExpiresAt, AllowRefresh = false });
            _sessions.WriteCookie(HttpContext, session);
            return RedirectToAction("Workspace", "Account");
        }
        catch
        {
            if (created) _sessions.Retire(session);
            throw;
        }
    }

    [HttpPost, EnableRateLimiting("demo-start")]
    public async Task<IActionResult> Reset()
    {
        if (!_options.Enabled) return NotFound();
        _sessions.Retire(_workspace.Session);
        await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        Response.Cookies.Delete(DemoOptions.CookieName);
        TempData["Success"] = "Your demo has been reset. Choose a role to load fresh sample claims.";
        return RedirectToAction(nameof(Index));
    }
}
