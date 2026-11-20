using CMCS_POE.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using CMCS_POE.Models;
using CMCS_POE.Data;
using CMCS_Part2.Hubs;
using CMCS_POE.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace CMCS_Part2.Controllers
{
    [Authorize(Roles = WorkspaceRoles.Manager)]
    public class ManagerController : Controller
    {
        private readonly ClaimRealtime _realtime;
        private readonly IClaimRepository _claimRepository;
        private readonly IClaimValidationService _validationService;

        public ManagerController(
            ClaimRealtime realtime,
            IClaimRepository claimRepository,
            IClaimValidationService validationService)
        {
            _realtime = realtime;
            _claimRepository = claimRepository;
            _validationService = validationService;
        }

        public IActionResult Index()
        {
            var claims = _claimRepository.GetAll()
                .Where(ClaimStages.CanManage)
                .OrderByDescending(c => c.ClaimId)
                .ToList();

            ViewBag.TotalPending = claims.Count;
            ViewBag.TotalAmount = claims.Sum(c => c.TotalAmount);

            var warnings = new Dictionary<int, List<string>>();
            foreach (var claim in claims)
            {
                var validationResult = _validationService.ValidateClaim(claim);
                if (validationResult.Warnings.Any())
                {
                    warnings[claim.ClaimId] = validationResult.Warnings;
                }
            }
            ViewBag.ValidationWarnings = warnings;

            return View(claims);
        }

        [HttpGet]
        public IActionResult Verify(int id)
        {
            var claim = _claimRepository.GetById(id);
            if (claim is null) return NotFound();
            if (!ClaimStages.CanManage(claim)) return Conflict("This claim is not awaiting this review stage.");
            return View("Verify", claim);
        }

        [HttpPost]
        [ActionName("Verify")]
        public async Task<IActionResult> VerifyConfirmed(int id)
        {
            var claim = _claimRepository.GetById(id);
            if (claim == null)
            {
                TempData["Error"] = "Claim not found";
                return RedirectToAction(nameof(Index));
            }

            if (!ClaimStages.CanManage(claim)) return Conflict("This claim is not awaiting manager review.");

            var validationResult = _validationService.ValidateClaim(claim);
            if (!validationResult.IsValid)
            {
                TempData["Error"] = "❌ Cannot approve: " + string.Join("; ", validationResult.Errors);
                return RedirectToAction(nameof(Index));
            }

            claim.Status = "Approved";
            claim.ApprovedDate = DateTime.UtcNow;
            try { _claimRepository.Update(claim); }
                catch (DbUpdateConcurrencyException) { return Conflict("A claim changed during review. Refresh the list before retrying."); }

            await _realtime.ChangedAsync(claim);

            TempData["Success"] = $"✓ Claim #{id} approved successfully. Amount: R{claim.TotalAmount:N2}";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id, string reason)
        {
            var claim = _claimRepository.GetById(id);
            if (claim == null)
            {
                TempData["Error"] = "Claim not found";
                return RedirectToAction(nameof(Index));
            }

            if (!ClaimStages.CanManage(claim)) return Conflict("This claim is not awaiting manager review.");

            if (string.IsNullOrWhiteSpace(reason) || reason.Length > 1000)
            {
                TempData["Error"] = "Rejection reason is required";
                return RedirectToAction(nameof(Index));
            }

            claim.Status = ClaimStages.ManagerRejected;
            claim.Notes = $"[Manager rejection: {reason}] " + (claim.Notes ?? "");
            try { _claimRepository.Update(claim); }
                catch (DbUpdateConcurrencyException) { return Conflict("A claim changed during review. Refresh the list before retrying."); }

            await _realtime.ChangedAsync(claim);

            TempData["Success"] = $"✓ Claim #{id} rejected";
            return RedirectToAction(nameof(Index));
        }

        public IActionResult Details(int id)
        {
            var claim = _claimRepository.GetById(id);
            if (claim == null)
                return NotFound();

            return View("~/Views/Claim/Details.cshtml", claim);
        }
    }
}
//OpenAI.2025.Chat-GPT(Version 5).[Large language model]Available at: https://chatgpt.com/c/68b46a0b-99f8-8326-98b4-5245e5c93d35[Accessed: 20 November 2025].