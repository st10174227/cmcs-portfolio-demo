using System.ComponentModel.DataAnnotations;
using System.Reflection;
using CMCS_POE.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.AspNetCore.SignalR;
using CMCS_Part2.Hubs;
using CMCS_POE.Controllers;
using CMCS_POE.Data;
using CMCS_POE.Models;
using CMCS_POE.Services;
using CMCS_Part2.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ClosedXML.Excel;
using iTextSharp.text.pdf;
using System.Text;

var passed = 0;

void Check(string name, Action test)
{
    test();
    passed++;
    Console.WriteLine($"PASS {name}");
}

void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

HRController CreateHr(RecordingLecturerRepository repository) => new(null!, repository, null!, null!)
{
    TempData = new TempDataDictionary(new DefaultHttpContext(), new MemoryTempDataProvider())
};

Lecturer ExampleLecturer() => new()
{
    FullName = "Demo Lecturer", Email = "lecturer@example.invalid",
    PhoneNumber = "0215550100", Department = "Demo", StandardHourlyRate = 250
};

Claim ReportClaim() => new()
{
    ClaimId = 42, LecturerId = 1, LecturerName = "Demo Lecturer", Month = "2026-11",
    HoursWorked = 10, HourlyRate = 250, Notes = null, Status = "Approved"
};

try
{
    Check("Valid lecturer form creates a record", () =>
    {
        var repository = new RecordingLecturerRepository();
        var result = CreateHr(repository).CreateLecturer(ExampleLecturer());
        Require(repository.AddCalls == 1 && result is RedirectToActionResult,
            "A valid lecturer must be saved and redirect to the lecturer list.");
    });

    Check("Invalid lecturer form preserves errors without creating a record", () =>
    {
        var repository = new RecordingLecturerRepository();
        var controller = CreateHr(repository);
        var lecturer = ExampleLecturer();
        controller.ModelState.AddModelError(nameof(Lecturer.Email), "Invalid email");
        var result = controller.CreateLecturer(lecturer) as ViewResult;
        Require(repository.AddCalls == 0 && result?.Model == lecturer && !controller.ModelState.IsValid,
            "Invalid lecturer input must be redisplayed without a repository write.");
    });

    Check("Valid lecturer edit updates a record", () =>
    {
        var repository = new RecordingLecturerRepository();
        var result = CreateHr(repository).EditLecturer(ExampleLecturer());
        Require(repository.UpdateCalls == 1 && result is RedirectToActionResult,
            "A valid lecturer edit must be saved.");
    });

    Check("Invalid lecturer edit does not update a record", () =>
    {
        var repository = new RecordingLecturerRepository();
        var controller = CreateHr(repository);
        controller.ModelState.AddModelError(nameof(Lecturer.FullName), "Required");
        var result = controller.EditLecturer(ExampleLecturer());
        Require(repository.UpdateCalls == 0 && result is ViewResult,
            "An invalid lecturer edit must not write to the repository.");
    });

    Check("New lecturers have an empty claim collection", () =>
    {
        Require(ExampleLecturer().Claims is { Count: 0 }, "Claims must be initialized.");
    });

    Check("Hours validation rejects non-finite and excessive values without integer conversion", () =>
    {
        var range = typeof(Claim).GetProperty(nameof(Claim.HoursWorked))!.GetCustomAttribute<RangeAttribute>()!;
        foreach (var value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity,
            double.MaxValue, 999999999999999999d, -1d, 0d, 0.99d, 200.01d })
            Require(!range.IsValid(value), $"Invalid hours {value} must be rejected without throwing.");
        foreach (var value in new[] { 1d, 1.5d, 199.5d, 200d })
            Require(range.IsValid(value), $"Valid hours {value} must remain accepted.");
    });

    Check("Decimal rate validation keeps exact boundaries without integer rounding or overflow", () =>
    {
        var lecturerRate = typeof(Lecturer).GetProperty(nameof(Lecturer.StandardHourlyRate))!.GetCustomAttribute<RangeAttribute>()!;
        var claimRate = typeof(Claim).GetProperty(nameof(Claim.HourlyRate))!.GetCustomAttribute<RangeAttribute>()!;
        foreach (var value in new[] { -0.01m, 1000.01m, decimal.MaxValue, decimal.MinValue })
        {
            Require(!lecturerRate.IsValid(value), $"Invalid lecturer rate {value} must be rejected without throwing.");
            Require(!claimRate.IsValid(value), $"Invalid claim rate {value} must be rejected without throwing.");
        }
        foreach (var value in new[] { 1m, 250.50m, 1000m })
            Require(lecturerRate.IsValid(value) && claimRate.IsValid(value), "Valid fractional rates must remain accepted.");
        Require(lecturerRate.IsValid(0m) && !claimRate.IsValid(0m), "An unassigned lecturer rate can be zero, but a submitted claim rate cannot.");
    });

    Check("Claim submission after deletion uses a generated, non-colliding ID", () =>
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        using var context = new ApplicationDbContext(options);
        context.Database.EnsureCreated();

        Claim ExampleClaim() => new()
        {
            LecturerId = 1, LecturerName = "Demo Lecturer", Month = "2026-11",
            HoursWorked = 10, HourlyRate = 250, Notes = "Personal checks fixture"
        };

        var repository = new EFClaimRepository(context);
        var first = ExampleClaim();
        var second = ExampleClaim();
        repository.Add(first);
        repository.Add(second);
        repository.Delete(first.ClaimId);

        // Count + 1 would now collide with the retained second claim.
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        serviceCollection.AddSignalR();
        using var provider = serviceCollection.BuildServiceProvider();
        var hub = provider.GetRequiredService<IHubContext<ClaimHub>>();
        var directory = Path.Combine(Path.GetTempPath(), "cmcs-controller-check-" + Guid.NewGuid().ToString("N"));
        var documents = new ClaimDocuments(directory, directory, new EphemeralDataProtectionProvider(), NullLogger<ClaimDocuments>.Instance);
        var controller = new ClaimController(context, repository, new ClaimValidationService(), documents,
            new ClaimRealtime(context, hub), NullLogger<ClaimController>.Instance)
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), new MemoryTempDataProvider()),
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext
            {
                User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(new[]
                {
                    new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, "fixture"),
                    new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, WorkspaceRoles.Lecturer),
                    new System.Security.Claims.Claim(WorkspaceRoles.LecturerIdClaim, "1")
                }, "Fixture"))
            } }
        };
        var submitted = ExampleClaim();
        submitted.ClaimId = 9999; // Posted identities must not control the inserted primary key.
        var result = controller.Create(submitted, null).GetAwaiter().GetResult();
        Require(result is RedirectToActionResult, "A new claim must succeed after an earlier claim is removed.");
        Require(submitted.ClaimId > second.ClaimId && submitted.ClaimId != 9999,
            "SQLite must generate the identity rather than using a count or posted value.");
        Require(context.Claims.Count() == 2 && context.Claims.Any(c => c.ClaimId == second.ClaimId),
            "Creating the new claim must preserve the existing claim.");
        Directory.Delete(directory, recursive: true);
    });

    Check("Fresh personal schema migrates and matches the current model", () =>
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var context = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        Require(!context.Database.HasPendingModelChanges(), "Migration snapshot differs from the current model.");
        context.Database.Migrate();
        Require(context.Lecturers.Count() == 3 && context.Claims.Count() == 0,
            "Fresh migrations must create the demo lecturers and an empty claims table.");
    });

    Check("Adding accounts preserves claims saved under the previous schema", () =>
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        int savedId;
        using (var old = new ApplicationDbContext(options))
        {
            old.GetService<IMigrator>().Migrate("20251027175911_MakeNavigationPropertyNullable");
            var claim = ReportClaim();
            claim.ClaimId = 0;
            claim.DocumentPath = "/uploads/retained-fixture.pdf";
            claim.IsPaid = true;
            claim.PaymentReference = "RETAINED-REFERENCE";
            old.Claims.Add(claim);
            old.SaveChanges();
            savedId = claim.ClaimId;
        }
        using var current = new ApplicationDbContext(options);
        current.Database.Migrate();
        var saved = current.Claims.Single(c => c.ClaimId == savedId);
        Require(saved.TotalAmount == 2500 && saved.IsPaid && saved.PaymentReference == "RETAINED-REFERENCE"
            && saved.DocumentPath == "/uploads/retained-fixture.pdf" && current.Lecturers.Count() == 3,
            "Adding accounts must preserve the previous personal claim and lecturer data.");
        Require(!current.Users.Any(), "An ordinary migration must not create demo credentials.");
    });

    Check("A stale claim edit cannot overwrite a concurrent review decision", () =>
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        using var first = new ApplicationDbContext(options);
        first.Database.EnsureCreated();
        var claim = ReportClaim();
        claim.ClaimId = 0;
        claim.Status = ClaimStages.CoordinatorPending;
        first.Claims.Add(claim);
        first.SaveChanges();
        using (var second = new ApplicationDbContext(options))
        {
            var concurrent = second.Claims.Single(c => c.ClaimId == claim.ClaimId);
            concurrent.Status = ClaimStages.ManagerPending;
            second.SaveChanges();
        }
        claim.HoursWorked = 99;
        var rejected = false;
        try { first.SaveChanges(); }
        catch (DbUpdateConcurrencyException) { rejected = true; }
        Require(rejected, "The stale write must raise a concurrency conflict.");
        first.ChangeTracker.Clear();
        var saved = first.Claims.Single();
        Require(saved.Status == ClaimStages.ManagerPending && saved.HoursWorked == 10,
            "The accepted review decision must remain intact.");
    });

    Check("Personal claims survive reopening a file-backed fixture", () =>
    {
        var fixtureDirectory = Path.Combine(Path.GetTempPath(), "cmcs-personal-checks-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixtureDirectory);
        try
        {
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = Path.Combine(fixtureDirectory, "fixture.db"), Pooling = false
            }.ToString();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connectionString).Options;
            int savedId;
            using (var context = new ApplicationDbContext(options))
            {
                context.Database.Migrate();
                var claim = ReportClaim();
                claim.ClaimId = 0;
                context.Claims.Add(claim);
                context.SaveChanges();
                savedId = claim.ClaimId;
            }
            using (var context = new ApplicationDbContext(options))
            {
                context.Database.Migrate();
                var saved = context.Claims.Single(c => c.ClaimId == savedId);
                Require(saved.TotalAmount == 2500 && saved.Month == "2026-11", "Saved claim was not preserved.");
            }
        }
        finally { Directory.Delete(fixtureDirectory, recursive: true); }
    });

    Check("Invoice PDF opens and contains the saved claim reference", () =>
    {
        var bytes = new PdfService().GenerateInvoice(ReportClaim());
        using var reader = new PdfReader(bytes);
        var content = Encoding.Latin1.GetString(reader.GetPageContent(1));
        Require(reader.NumberOfPages >= 1 && content.Contains("INV-000042", StringComparison.Ordinal)
            && content.Contains("UNPAID", StringComparison.Ordinal) && !content.Contains("PENDING", StringComparison.Ordinal),
            "Invoice PDF must be readable and identify its claim.");
    });

    Check("Empty monthly PDF remains readable", () =>
    {
        using var reader = new PdfReader(new PdfService().GenerateMonthlyReport(new List<Claim>(), "2026-11"));
        Require(reader.NumberOfPages >= 1, "An empty report must still be a valid PDF.");
    });

    Check("Monthly PDF handles missing names and multiple pages", () =>
    {
        var claims = Enumerable.Range(1, 160).Select(id =>
        {
            var claim = ReportClaim();
            claim.ClaimId = id;
            claim.LecturerName = id == 1 ? null : "Demo Lecturer " + id;
            return claim;
        }).ToList();
        using var reader = new PdfReader(new PdfService().GenerateMonthlyReport(claims, "2026-11"));
        Require(reader.NumberOfPages >= 2, "A long monthly report must span readable pages.");
    });

    Check("Claims spreadsheet preserves numeric hours, rate and total", () =>
    {
        using var stream = new MemoryStream(new ExcelService().ExportClaims(new List<Claim> { ReportClaim() }));
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheet("Claims");
        Require(sheet.Cell(2, 1).GetValue<int>() == 42
            && sheet.Cell(2, 4).GetValue<double>() == 10
            && sheet.Cell(2, 5).GetValue<decimal>() == 250
            && sheet.Cell(2, 6).GetValue<decimal>() == 2500,
            "Spreadsheet export must retain saved values as numeric cells.");
    });

    Check("Lecturer spreadsheet preserves identity and email", () =>
    {
        using var stream = new MemoryStream(new ExcelService().ExportLecturers(new List<Lecturer> { ExampleLecturer() }));
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheet("Lecturers");
        Require(sheet.Cell(2, 2).GetString() == "Demo Lecturer"
            && sheet.Cell(2, 3).GetString() == "lecturer@example.invalid",
            "Lecturer export must preserve the supplied details.");
    });

    await HttpChecks.RunAsync(name =>
    {
        passed++;
        Console.WriteLine("PASS " + name);
    });

    await DemoHttpChecks.RunAsync(name =>
    {
        passed++;
        Console.WriteLine("PASS " + name);
    });

    Console.WriteLine($"Personal regression checks passed: {passed}. Website acceptance remains separate.");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"FAIL after {passed} checks: {exception}");
    return 1;
}

sealed class RecordingLecturerRepository : ILecturerRepository
{
    public int AddCalls { get; private set; }
    public int UpdateCalls { get; private set; }
    public List<Lecturer> GetAll() => new();
    public Lecturer? GetById(int id) => null;
    public Lecturer? GetByEmail(string email) => null;
    public void Add(Lecturer lecturer) => AddCalls++;
    public void Update(Lecturer lecturer) => UpdateCalls++;
    public void Delete(int id) => throw new NotSupportedException();
}

sealed class MemoryTempDataProvider : ITempDataProvider
{
    public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
    public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
}
