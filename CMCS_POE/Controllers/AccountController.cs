using CMCS_POE.Demo;
using Microsoft.AspNetCore.Authentication;
using System.Threading.RateLimiting;
using CMCS_POE.Data;
using CMCS_POE.Models;
using CMCS_POE.Security;
using CMCS_POE.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace CMCS_POE.Controllers;

public sealed class AccountController : Controller
{
    private readonly DemoOptions _demo;
    private SignInManager<ApplicationUser> SignInService => HttpContext.RequestServices.GetRequiredService<SignInManager<ApplicationUser>>();
    private UserManager<ApplicationUser> Users => HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
    private ApplicationDbContext Db => HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
    public AccountController(DemoOptions demo) { _demo = demo; }

    [AllowAnonymous, HttpGet]
    public IActionResult Login(string? returnUrl = null) => _demo.Enabled ? RedirectToAction("Index", "Demo") : User.Identity?.IsAuthenticated == true
        ? RedirectToAction(nameof(Workspace))
        : View(new LoginViewModel { ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null });

    [AllowAnonymous, HttpPost, EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (_demo.Enabled) return RedirectToAction("Index", "Demo");
        if (!ModelState.IsValid) return View(model);
        var user = await Users.FindByEmailAsync(model.Email.Trim());
        var eligible = user is { IsActive: true };
        if (eligible && user?.LecturerId is int id)
            eligible = await Db.Lecturers.AnyAsync(l => l.LecturerId == id && l.IsActive);
        if (eligible && user is not null)
        {
            var result = await SignInService.PasswordSignInAsync(user, model.Password, isPersistent: false, lockoutOnFailure: true);
            if (result.Succeeded)
                return Url.IsLocalUrl(model.ReturnUrl) ? LocalRedirect(model.ReturnUrl!) : RedirectToAction(nameof(Workspace));
        }
        ModelState.AddModelError(string.Empty, "Unable to sign in. Check your details or try again later.");
        model.Password = string.Empty;
        ModelState.Remove(nameof(LoginViewModel.Password));
        return View(model);
    }

    [Authorize, HttpGet]
    public IActionResult Workspace()
    {
        if (User.IsInRole(WorkspaceRoles.Lecturer)) return RedirectToAction("Index", "Claim");
        if (User.IsInRole(WorkspaceRoles.Coordinator)) return RedirectToAction("Index", "Coordinator");
        if (User.IsInRole(WorkspaceRoles.Manager)) return RedirectToAction("Index", "Manager");
        if (User.IsInRole(WorkspaceRoles.HR)) return RedirectToAction("Index", "HR");
        return Forbid();
    }

    [Authorize, HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        return RedirectToAction("Index", "Home");
    }

    [AllowAnonymous, HttpGet]
    public IActionResult AccessDenied()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View();
    }
}
