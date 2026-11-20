using CMCS_POE.Demo;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using CMCS_POE.Data;
using CMCS_POE.Models;
using CMCS_POE.Security;
using CMCS_POE.Services;
using CMCS_POE.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CMCS_Part2.Controllers;

[Authorize]
public sealed class ClaimController : Controller
{
    private readonly DemoRequestContext? _demo;
    private readonly ApplicationDbContext _db;
    private readonly IClaimRepository _claims;
    private readonly IClaimValidationService _validation;
    private readonly ClaimDocuments _documents;
    private readonly ClaimRealtime _realtime;
    private readonly ILogger<ClaimController> _logger;

    public ClaimController(ApplicationDbContext db, IClaimRepository claims, IClaimValidationService validation,
        ClaimDocuments documents, ClaimRealtime realtime, ILogger<ClaimController> logger, DemoRequestContext? demo = null)
    { _demo = demo; _db = db; _claims = claims; _validation = validation; _documents = documents; _realtime = realtime; _logger = logger; }

    [HttpGet]
    public IActionResult Index()
    {
        if (!User.IsStaff() && !User.IsInRole(WorkspaceRoles.Lecturer)) return Forbid();
        var claims = _claims.GetAll().Where(c => User.CanRead(c)).OrderByDescending(c => c.ClaimId).ToList();
        ViewBag.TotalClaims = claims.Count;
        ViewBag.SubmittedClaims = claims.Count(c => ClaimStages.CanEdit(c));
        ViewBag.VerifiedClaims = claims.Count(c => c.Status == ClaimStages.Approved);
        ViewBag.RejectedClaims = claims.Count(c => c.Status.StartsWith("Rejected", StringComparison.Ordinal));
        ViewBag.PendingClaims = claims.Count(c => c.Status.StartsWith("Pending", StringComparison.Ordinal));
        ViewBag.TotalAmount = claims.Where(c => c.Status == ClaimStages.Approved).Sum(c => c.TotalAmount);
        return View(claims);
    }

    [HttpGet, Authorize(Roles = WorkspaceRoles.Lecturer)]
    public IActionResult Create()
    {
        var lecturer = CurrentLecturer();
        if (lecturer is null) return Forbid();
        var claim = new Claim { Month = DateTime.UtcNow.ToString("yyyy-MM") };
        AssignLecturer(claim, lecturer);
        return View(new ClaimSubmissionViewModel { Claim = claim });
    }

    [HttpPost, Authorize(Roles = WorkspaceRoles.Lecturer), RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Create([Bind("Month,HoursWorked,Notes")] Claim claim, IFormFile? Document)
    {
        var lecturer = CurrentLecturer();
        if (lecturer is null) return Forbid();
        AssignLecturer(claim, lecturer);
        claim.ClaimId = 0;
        claim.Status = ClaimStages.CoordinatorPending;
        claim.IsPaid = false;
        claim.PaidDate = null;
        claim.PaymentReference = null;
        claim.ApprovedDate = null;
        claim.CreatedDate = DateTime.UtcNow;
        claim.DocumentPath = null;
        claim.DocumentName = null;
        if (_demo?.Session is not null && _db.Claims.Count() >= DemoOptions.MaximumClaims)
            ModelState.AddModelError(string.Empty, "This demo has reached its claim limit. Reset it to load fresh sample data.");
        if (!ValidateSubmission(claim)) return View(new ClaimSubmissionViewModel { Claim = claim });
        string? createdPath = null;
        try
        {
            if (Document is { Length: > 0 })
            {
                var saved = _documents.Save(Document);
                createdPath = claim.DocumentPath = saved.Path;
                claim.DocumentName = saved.Name;
            }
            _claims.Add(claim);
        }
        catch (InvalidDataException exception)
        {
            _documents.Remove(createdPath);
            ModelState.AddModelError("Document", exception.Message);
            return View(new ClaimSubmissionViewModel { Claim = claim });
        }
        catch (Exception exception) when (exception is DbUpdateException or IOException or UnauthorizedAccessException)
        {
            _documents.Remove(createdPath);
            _logger.LogError(exception, "Claim submission could not be saved.");
            ModelState.AddModelError(string.Empty, "Your claim could not be saved. Please try again.");
            return View(new ClaimSubmissionViewModel { Claim = claim });
        }
        await NotifyAsync(claim);
        TempData["Success"] = $"Claim #{claim.ClaimId} submitted for coordinator review.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet, Authorize(Roles = WorkspaceRoles.Lecturer)]
    public IActionResult Edit(int id)
    {
        var claim = _claims.GetById(id);
        if (claim is null || claim.LecturerId != User.LecturerId()) return NotFound();
        if (!ClaimStages.CanEdit(claim)) return Conflict("This claim has already moved to another stage and cannot be edited.");
        return View(claim);
    }

    [HttpPost, Authorize(Roles = WorkspaceRoles.Lecturer), RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Edit([Bind("ClaimId,Month,HoursWorked,Notes")] Claim claim, IFormFile? Document)
    {
        var existing = _claims.GetById(claim.ClaimId);
        if (existing is null || existing.LecturerId != User.LecturerId()) return NotFound();
        if (!ClaimStages.CanEdit(existing)) return Conflict("This claim has already moved to another stage and cannot be edited.");
        var lecturer = CurrentLecturer();
        if (lecturer is null) return Forbid();
        AssignLecturer(claim, lecturer);
        claim.Status = existing.Status;
        claim.DocumentPath = existing.DocumentPath;
        claim.DocumentName = existing.DocumentName;
        if (!ValidateSubmission(claim)) return View(claim);
        string? createdPath = null;
        var oldPath = existing.DocumentPath;
        try
        {
            if (Document is { Length: > 0 })
            {
                var saved = _documents.Save(Document);
                createdPath = saved.Path;
                existing.DocumentPath = saved.Path;
                existing.DocumentName = saved.Name;
            }
            existing.Month = claim.Month;
            existing.HoursWorked = claim.HoursWorked;
            existing.HourlyRate = lecturer.StandardHourlyRate;
            existing.LecturerName = lecturer.FullName;
            existing.Notes = claim.Notes;
            _claims.Update(existing);
        }
        catch (DbUpdateConcurrencyException)
        {
            _documents.Remove(createdPath);
            return Conflict("This claim changed during your edit. Refresh the claim before trying again.");
        }
        catch (InvalidDataException exception)
        {
            _documents.Remove(createdPath);
            ModelState.AddModelError("Document", exception.Message);
            return View(claim);
        }
        catch (Exception exception) when (exception is DbUpdateException or IOException or UnauthorizedAccessException)
        {
            _documents.Remove(createdPath);
            _logger.LogError(exception, "Claim edit could not be saved.");
            ModelState.AddModelError(string.Empty, "Your changes could not be saved. Please try again.");
            return View(claim);
        }
        if (createdPath is not null) _documents.Remove(oldPath);
        await NotifyAsync(existing);
        TempData["Success"] = "Claim updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, Authorize(Roles = WorkspaceRoles.Lecturer)]
    public async Task<IActionResult> Delete(int id)
    {
        var claim = _claims.GetById(id);
        if (claim is null || claim.LecturerId != User.LecturerId()) return NotFound();
        if (!ClaimStages.CanEdit(claim)) return Conflict("Only a claim awaiting coordinator review can be deleted.");
        try { _claims.Delete(id); }
        catch (DbUpdateConcurrencyException) { return Conflict("The claim changed. Refresh before trying again."); }
        _documents.Remove(claim.DocumentPath);
        await NotifyAsync(claim, deleted: true);
        TempData["Success"] = "Claim deleted.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult Details(int id)
    {
        var claim = _claims.GetById(id);
        return claim is null || !User.CanRead(claim) ? NotFound() : View(claim);
    }

    [HttpGet]
    public IActionResult DownloadFile(int id)
    {
        var claim = _claims.GetById(id);
        if (claim is null || !User.CanRead(claim)) return NotFound();
        try
        {
            var document = _documents.Read(claim);
            return document is null ? NotFound() : File(document.Bytes, document.ContentType, document.Name);
        }
        catch (Exception exception) when (exception is IOException or CryptographicException or UnauthorizedAccessException)
        {
            _logger.LogWarning(exception, "A saved claim document could not be read.");
            return NotFound();
        }
    }

    private Lecturer? CurrentLecturer() => _db.Lecturers.FirstOrDefault(l => l.LecturerId == User.LecturerId() && l.IsActive);
    private void AssignLecturer(Claim claim, Lecturer lecturer)
    {
        claim.LecturerId = lecturer.LecturerId;
        claim.LecturerName = lecturer.FullName;
        claim.HourlyRate = lecturer.StandardHourlyRate;
        ModelState.Remove("HourlyRate");
        ModelState.Remove("Claim.HourlyRate");
    }

    private bool ValidateSubmission(Claim claim)
    {
        if (!Regex.IsMatch(claim.Month ?? string.Empty, @"^\d{4}-(0[1-9]|1[0-2])$", RegexOptions.CultureInvariant))
            ModelState.AddModelError("Month", "Choose a month and year.");
        if (!double.IsFinite(claim.HoursWorked) || claim.HoursWorked < 1 || claim.HoursWorked > 200)
            ModelState.AddModelError("HoursWorked", "Enter between 1 and 200 hours.");
        if (claim.HourlyRate <= 0 || claim.HourlyRate > 1000)
            ModelState.AddModelError(string.Empty, "Your assigned rate is outside the claim limits. Ask HR to review your record.");
        if (claim.Notes?.Length > 2000) ModelState.AddModelError("Notes", "Use no more than 2,000 characters.");
        if (!ModelState.IsValid) return false;
        foreach (var error in _validation.ValidateClaim(claim).Errors) ModelState.AddModelError(string.Empty, error);
        return ModelState.IsValid;
    }

    private async Task NotifyAsync(Claim claim, bool deleted = false)
    {
        try { await _realtime.ChangedAsync(claim, deleted); }
        catch (Exception exception) { _logger.LogWarning(exception, "A saved claim update could not be broadcast."); }
    }
}

//OpenAI.2025.Chat-GPT(Version 5).[Large language model]Available at: https://chatgpt.com/c/68b46a0b-99f8-8326-98b4-5245e5c93d35[Accessed: 20 November 2025].
