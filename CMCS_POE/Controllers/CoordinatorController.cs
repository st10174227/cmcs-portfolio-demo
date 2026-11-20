using CMCS_POE.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using CMCS_POE.Data;
using CMCS_Part2.Hubs;
using CMCS_Part2.Models;
using CMCS_POE.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace CMCS_Part2.Controllers
{
    [Authorize(Roles = WorkspaceRoles.Coordinator)]
    public class CoordinatorController : Controller
    {
        private readonly ClaimRealtime _realtime;
        private readonly IClaimRepository _claimRepository;
        private readonly IClaimValidationService _validationService;

        public CoordinatorController(
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
                .Where(ClaimStages.CanCoordinate)
                .ToList();

            ViewBag.TotalPending = claims.Count;
            ViewBag.TotalAmount = claims.Sum(c => c.TotalAmount);

            var warnings = new Dictionary<int, List<string>>();
            foreach (var claim in claims)
            {
                warnings[claim.ClaimId] = _validationService.GetValidationWarnings(claim);
            }
            ViewBag.ValidationWarnings = warnings;

            return View(claims);
        }

        [HttpGet]
        public IActionResult Verify(int id)
        {
            var claim = _claimRepository.GetById(id);
            if (claim is null) return NotFound();
            if (!ClaimStages.CanCoordinate(claim)) return Conflict("This claim is not awaiting this review stage.");
            return View("Verify", claim);
        }

        [HttpPost]
        [ActionName("Verify")]
        public async Task<IActionResult> VerifyConfirmed(int id)
        {
            var claim = _claimRepository.GetById(id);
            if (claim != null)
            {
                if (!ClaimStages.CanCoordinate(claim)) return Conflict("This claim has already moved to another review stage.");
                var validationResult = _validationService.ValidateClaim(claim);

                if (!validationResult.IsValid)
                {
                    TempData["Error"] = "❌ Cannot approve: " + string.Join("; ", validationResult.Errors);
                    return RedirectToAction("Index"); 
                }

                claim.Status = "Pending Manager Approval";
                try { _claimRepository.Update(claim); }
                catch (DbUpdateConcurrencyException) { return Conflict("A claim changed during review. Refresh the list before retrying."); }
                await _realtime.ChangedAsync(claim);

                TempData["Success"] = $"✓ Claim #{id} approved and forwarded to Manager";
                return RedirectToAction("Index"); 
            }
            return NotFound();
        }

        [HttpPost]
        public async Task<IActionResult> Reject(int id, string reason)
        {
            if (string.IsNullOrWhiteSpace(reason) || reason.Length > 1000) return BadRequest("Enter a rejection reason of up to 1,000 characters.");
            var claim = _claimRepository.GetById(id);
            if (claim != null)
            {
                if (!ClaimStages.CanCoordinate(claim)) return Conflict("This claim has already moved to another review stage.");
                claim.Status = "RejectedByCoordinator";

                if (!string.IsNullOrEmpty(reason))
                {
                    claim.Notes = $"[Coordinator Rejection: {reason}] " + (claim.Notes ?? "");
                }

                try { _claimRepository.Update(claim); }
                catch (DbUpdateConcurrencyException) { return Conflict("A claim changed during review. Refresh the list before retrying."); }
                await _realtime.ChangedAsync(claim);

                TempData["Success"] = $"✓ Claim #{id} rejected";
                return RedirectToAction("Index"); 
            }
            return NotFound();
        }

        [HttpPost]
        public async Task<IActionResult> BulkVerify(int[] claimIds)
        {
            if (claimIds == null || claimIds.Length == 0 || claimIds.Length > 100 || !ModelState.IsValid)
            {
                TempData["Error"] = "No claims selected";
                return RedirectToAction("Index");
            }

            int verifiedCount = 0;
            int failedCount = 0;

            foreach (var id in claimIds.Distinct())
            {
                var claim = _claimRepository.GetById(id);
                if (claim != null && ClaimStages.CanCoordinate(claim) && _validationService.IsClaimWithinLimits(claim))
                {
                    claim.Status = "Pending Manager Approval";
                    try { _claimRepository.Update(claim); }
                catch (DbUpdateConcurrencyException) { return Conflict("A claim changed during review. Refresh the list before retrying."); }
                    await _realtime.ChangedAsync(claim);
                    verifiedCount++;
                }
                else
                {
                    failedCount++;
                }
            }

            TempData["Success"] = $"✓ Verified {verifiedCount} claims. {failedCount} failed validation.";
            return RedirectToAction("Index");
        }
    }
}
//OpenAI.2025.Chat-GPT(Version 5).[Large language model]Available at: https://chatgpt.com/c/68b46a0b-99f8-8326-98b4-5245e5c93d35[Accessed: 20 November 2025].