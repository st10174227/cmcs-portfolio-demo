using CMCS_POE.Demo;
using CMCS_POE.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using CMCS_Part2.Models;
using CMCS_POE.Data;
using CMCS_POE.Models;
using CMCS_POE.Services;
using CMCS_POE.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace CMCS_POE.Controllers
{
    [Authorize(Roles = WorkspaceRoles.HR)]
    public class HRController : Controller
    {
        private readonly DemoRequestContext? _demo;
        private readonly IClaimRepository _claimRepository;
        private readonly ILecturerRepository _lecturerRepository;
        private readonly IPdfService _pdfService;
        private readonly IExcelService _excelService;

        public HRController(
            IClaimRepository claimRepository,
            ILecturerRepository lecturerRepository,
            IPdfService pdfService,
            IExcelService excelService, DemoRequestContext? demo = null)
        {
            _demo = demo;
            _claimRepository = claimRepository;
            _lecturerRepository = lecturerRepository;
            _pdfService = pdfService;
            _excelService = excelService;
        }

        public IActionResult Index()
        {
            var allClaims = _claimRepository.GetAll();

            var approvedClaims = allClaims
                .Where(c => c.Status == ClaimStages.Approved)
                .ToList();

            var paidClaims = approvedClaims.Where(c => c.IsPaid).ToList();
            var unpaidClaims = approvedClaims.Where(c => !c.IsPaid).ToList();

            var monthlyBreakdown = allClaims
                .Where(c => c.Status == ClaimStages.Approved)
                .GroupBy(c => c.Month)
                .Select(g => new MonthlyStats
                {
                    Month = g.Key,
                    ClaimCount = g.Count(),
                    TotalAmount = g.Sum(c => c.TotalAmount),
                    PaidCount = g.Count(c => c.IsPaid),
                    PaidAmount = g.Where(c => c.IsPaid).Sum(c => c.TotalAmount)
                })
                .OrderBy(m => m.Month)
                .ToList();

            var viewModel = new HRDashboardViewModel
            {
                TotalApprovedClaims = approvedClaims.Count,
                TotalUnpaidClaims = unpaidClaims.Count,
                TotalPaidClaims = paidClaims.Count,
                TotalAmountApproved = approvedClaims.Sum(c => c.TotalAmount),
                TotalAmountPaid = paidClaims.Sum(c => c.TotalAmount),
                TotalAmountOutstanding = unpaidClaims.Sum(c => c.TotalAmount),
                RecentApprovedClaims = approvedClaims.OrderByDescending(c => c.ClaimId).Take(10).ToList(),
                MonthlyBreakdown = monthlyBreakdown
            };

            return View(viewModel);
        }

        [HttpGet]
        public IActionResult ProcessPayments()
        {
            var unpaidClaims = _claimRepository.GetUnpaidApprovedClaims();

            var viewModel = new PaymentBatchViewModel
            {
                UnpaidClaims = unpaidClaims,
                TotalAmount = unpaidClaims.Sum(c => c.TotalAmount)
            };

            return View(viewModel);
        }

        [HttpPost]
        public IActionResult MarkAsPaid(int id, string? paymentReference)
        {
            var claim = _claimRepository.GetById(id);

            if (claim is null) return NotFound();
            if (!ClaimStages.CanRecordPayment(claim)) return Conflict("Only an approved, unpaid claim can be recorded as paid.");
            if (paymentReference?.Length > 100) return BadRequest("Use a payment reference of up to 100 characters.");

            if (ClaimStages.CanRecordPayment(claim))
            {
                claim.IsPaid = true;
                claim.PaidDate = DateTime.UtcNow;
                claim.PaymentReference = string.IsNullOrWhiteSpace(paymentReference) ? $"PAY-{DateTime.UtcNow:yyyyMMdd}-{claim.ClaimId}" : paymentReference.Trim();
                try { _claimRepository.Update(claim); }
                catch (DbUpdateConcurrencyException) { return Conflict("A claim changed while recording payment. Refresh before retrying."); }
                TempData["Success"] = $"✓ Claim #{id} marked as paid";
            }
            else
            {
                TempData["Error"] = $"Claim #{id} cannot be marked as paid. Status: {claim?.Status ?? "Not Found"}";
            }

            return RedirectToAction(nameof(ProcessPayments));
        }

        [HttpPost]
        public IActionResult BulkMarkAsPaid(int[] claimIds)
        {
            if (claimIds == null || claimIds.Length == 0 || claimIds.Length > 100 || !ModelState.IsValid)
            {
                TempData["Error"] = "No claims selected";
                return RedirectToAction(nameof(ProcessPayments));
            }

            int paidCount = 0;
            var batchReference = $"BATCH-{DateTime.Now:yyyyMMddHHmmss}";

            foreach (var id in claimIds.Distinct())
            {
                var claim = _claimRepository.GetById(id);

                if (claim != null && ClaimStages.CanRecordPayment(claim))
                {
                    claim.IsPaid = true;
                    claim.PaidDate = DateTime.UtcNow;
                    claim.PaymentReference = $"{batchReference}-{claim.ClaimId}";
                    try { _claimRepository.Update(claim); }
                catch (DbUpdateConcurrencyException) { return Conflict("A claim changed while recording payment. Refresh before retrying."); }
                    paidCount++;
                }
            }

            TempData["Success"] = $"✓ Successfully processed {paidCount} payments";
            return RedirectToAction(nameof(ProcessPayments));
        }

        public IActionResult GenerateInvoice(int id)
        {
            var claim = _claimRepository.GetById(id);

            if (claim == null || claim.Status != ClaimStages.Approved)
            {
                TempData["Error"] = "Claim not found or not approved";
                return RedirectToAction(nameof(Index));
            }

            var pdf = _pdfService.GenerateInvoice(claim);
            return File(pdf, "application/pdf", $"Invoice-{claim.ClaimId}.pdf");
        }

        [HttpGet]
        public IActionResult GenerateMonthlyReport()
        {
            var months = _claimRepository.GetAll()
                .Where(c => c.Status == ClaimStages.Approved)
                .Select(c => c.Month)
                .Distinct()
                .OrderBy(m => m)
                .ToList();

            ViewBag.Months = months;
            return View();
        }

        [HttpPost]
        public IActionResult GenerateMonthlyReport(string month)
        {
            if (string.IsNullOrEmpty(month))
            {
                TempData["Error"] = "Please select a month";
                return RedirectToAction(nameof(GenerateMonthlyReport));
            }

            var claims = _claimRepository.GetClaimsByMonth(month)
                .Where(c => c.Status == ClaimStages.Approved)
                .ToList();

            if (!claims.Any())
            {
                TempData["Error"] = $"No approved claims found for {month}";
                return RedirectToAction(nameof(GenerateMonthlyReport));
            }

            var pdf = _pdfService.GenerateMonthlyReport(claims, month);
            return File(pdf, "application/pdf", $"MonthlyReport-{month}.pdf");
        }

        public IActionResult ExportAllClaims()
        {
            var claims = _claimRepository.GetAll();
            var excel = _excelService.ExportClaims(claims);
            return File(excel, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"AllClaims-{DateTime.Now:yyyyMMdd}.xlsx");
        }

        public IActionResult ExportApprovedClaims()
        {
            var claims = _claimRepository.GetApprovedClaims();
            var excel = _excelService.ExportClaims(claims);
            return File(excel, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"ApprovedClaims-{DateTime.Now:yyyyMMdd}.xlsx");
        }

        public IActionResult ExportPaymentBatch(int[] claimIds)
        {
            if (claimIds == null || claimIds.Length == 0 || claimIds.Length > 100 || !ModelState.IsValid)
            {
                TempData["Error"] = "No claims selected";
                return RedirectToAction(nameof(ProcessPayments));
            }

            var claims = claimIds.Distinct().Select(id => _claimRepository.GetById(id))
                .OfType<Claim>().ToList();

            var excel = _excelService.ExportClaims(claims);
            return File(excel, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"PaymentBatch-{DateTime.Now:yyyyMMdd}.xlsx");
        }

        public IActionResult Lecturers()
        {
            var lecturers = _lecturerRepository.GetAll();
            return View(lecturers);
        }

        [HttpGet]
        public IActionResult CreateLecturer()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateLecturer([Bind("FullName,Email,PhoneNumber,Department,StandardHourlyRate")] Lecturer lecturer)
        {
            if (_demo?.Session is not null && _lecturerRepository.GetAll().Count >= DemoOptions.MaximumLecturers)
                ModelState.AddModelError(string.Empty, "This demo has reached its lecturer limit. Reset it to load fresh sample data.");
            ValidateLecturerText(lecturer);
            if (ModelState.IsValid)
            {
                lecturer.LecturerId = 0;
                lecturer.IsActive = true;
                _lecturerRepository.Add(lecturer);
                TempData["Success"] = $"✓ Lecturer {lecturer.FullName} added successfully";
                return RedirectToAction(nameof(Lecturers));
            }
            return View(lecturer);
        }

        [HttpGet]
        public IActionResult EditLecturer(int id)
        {
            var lecturer = _lecturerRepository.GetById(id);
            if (lecturer == null)
                return NotFound();
            return View(lecturer);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditLecturer([Bind("LecturerId,FullName,Email,PhoneNumber,Department,StandardHourlyRate,IsActive")] Lecturer lecturer)
        {
            ValidateLecturerText(lecturer);
            if (ModelState.IsValid)
            {
                _lecturerRepository.Update(lecturer);
                TempData["Success"] = $"✓ Lecturer {lecturer.FullName} updated successfully";
                return RedirectToAction(nameof(Lecturers));
            }
            return View(lecturer);
        }

        [HttpPost]
        public IActionResult DeleteLecturer(int id)
        {
            _lecturerRepository.Delete(id);
            TempData["Success"] = "✓ Lecturer deactivated successfully";
            return RedirectToAction(nameof(Lecturers));
        }

        public IActionResult ExportLecturers()
        {
            var lecturers = _lecturerRepository.GetAll();
            var excel = _excelService.ExportLecturers(lecturers);
            return File(excel, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Lecturers-{DateTime.Now:yyyyMMdd}.xlsx");
        }

        public IActionResult PaymentHistory()
        {
            var paidClaims = _claimRepository.GetPaidClaims()
                .OrderByDescending(c => c.PaidDate)
                .ToList();
            return View(paidClaims);
        }
        private void ValidateLecturerText(Lecturer lecturer)
        {
            if (lecturer.Email?.Length > 254) ModelState.AddModelError(nameof(Lecturer.Email), "Use an email address of up to 254 characters.");
            if (lecturer.PhoneNumber?.Length > 50) ModelState.AddModelError(nameof(Lecturer.PhoneNumber), "Use a phone number of up to 50 characters.");
        }
    }
}
//OpenAI.2025.Chat-GPT(Version 5).[Large language model]Available at: https://chatgpt.com/c/68b46a0b-99f8-8326-98b4-5245e5c93d35[Accessed: 20 November 2025].