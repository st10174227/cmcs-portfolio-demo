using System.Net;
using CMCS_POE.Data;
using CMCS_POE.Demo;
using CMCS_POE.Models;
using CMCS_POE.Security;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public static class DemoHttpChecks
{
    public static async Task RunAsync(Action<string> passed)
    {
        var fixture = Path.Combine(Path.GetTempPath(), "cmcs-demo-http-" + Guid.NewGuid().ToString("N"));
        var content = Path.Combine(fixture, "Content");
        var personal = Path.Combine(fixture, "Personal");
        var data = Path.Combine(fixture, "Demo");
        Directory.CreateDirectory(Path.Combine(content, "wwwroot")); Directory.CreateDirectory(personal);
        var personalFile = Path.Combine(personal, "cmcs-personal.db");
        var sentinel = "Unrelated personal data must never be opened by demo mode.";
        await File.WriteAllTextAsync(personalFile, sentinel);
        var clock = new DemoClock();
        try
        {
            await using var app = CMCS_POE.Program.BuildApplication(new[] { "--environment=Testing", "--contentRoot=" + content }, builder =>
            {
                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Demo:Enabled"] = "true", ["Demo:DataDirectory"] = data, ["PersonalWorkspace:DataDirectory"] = personal
                });
                builder.Services.AddSingleton<TimeProvider>(clock);
                builder.Logging.ClearProviders(); builder.Logging.AddConsole().SetMinimumLevel(LogLevel.Error);
                builder.WebHost.UseTestServer();
            });
            await app.StartAsync();
            var server = app.GetTestServer();
            var sessions = app.Services.GetRequiredService<DemoSessions>();
            using var anonymous = HttpChecks.Client(server);
            using var one = HttpChecks.Client(server);
            using var two = HttpChecks.Client(server);
            string oneToken = "", twoToken = "", oneAuth = "", oneWorkspaceCookie = "", twoWorkspaceCookie = "";

            async Task Check(string name, Func<Task> check) { await check(); passed(name); }
            async Task<HttpResponseMessage> Start(HttpClient client, string role)
            {
                var token = await HttpChecks.Token(client, "/Demo");
                return await client.PostAsync("/Demo/Start", HttpChecks.Form(("role", role), ("__RequestVerificationToken", token)));
            }
            async Task<Claim> Read(string token, int id)
            {
                using var lease = sessions.Acquire(token) ?? throw new InvalidOperationException("Expected a live demo workspace.");
                await using var scope = app.Services.CreateAsyncScope();
                scope.ServiceProvider.GetRequiredService<DemoRequestContext>().Session = lease.Session;
                return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Claims.AsNoTracking().SingleAsync(c => c.ClaimId == id);
            }

            await Check("Public demo landing explains role access without requiring a password", async () =>
            {
                var html = await anonymous.GetStringAsync("/Demo");
                foreach (var role in WorkspaceRoles.All) Need(html.Contains("Try as " + role, StringComparison.Ordinal), "Demo role button is missing.");
                var home = await anonymous.GetStringAsync("/");
                foreach (var body in new[] { html, home })
                    Need(body.Contains("hero-section", StringComparison.Ordinal) && body.Contains("Choose Your Workspace", StringComparison.Ordinal)
                        && body.Contains("Everything You Need to Review a Claim", StringComparison.Ordinal)
                        && !body.Contains("<aside class=\"demo-banner\"", StringComparison.Ordinal),
                        "Demo and root entry must use the normal homepage without a separate demo banner.");
                Need(!html.Contains("type=\"password\"", StringComparison.OrdinalIgnoreCase), "Public demo should not ask for a private password.");
                using var redirect = await anonymous.GetAsync("/Account/Login");
                Need(redirect.StatusCode == HttpStatusCode.Redirect && redirect.Headers.Location?.ToString() == "/Demo", "Demo sign-in should lead to its role picker.");
                Need(!Directory.EnumerateFiles(data, "demo.db", SearchOption.AllDirectories).Any(), "Opening a public page should not allocate a visitor database.");
            });

            await Check("Demo role entry requires POST, a valid form token and an allowed role", async () =>
            {
                using var get = await anonymous.GetAsync("/Demo/Start?role=HR");
                using var missing = await anonymous.PostAsync("/Demo/Start", HttpChecks.Form(("role", "HR")));
                var token = await HttpChecks.Token(anonymous, "/Demo");
                using var forged = await anonymous.PostAsync("/Demo/Start", HttpChecks.Form(("role", "Administrator"), ("__RequestVerificationToken", token)));
                Need(!get.IsSuccessStatusCode && missing.StatusCode == HttpStatusCode.BadRequest && forged.StatusCode == HttpStatusCode.BadRequest,
                    "Demo role-entry validation failed.");
                Need(!Directory.EnumerateFiles(data, "demo.db", SearchOption.AllDirectories).Any(), "Invalid entry allocated data.");
            });

            await Check("Two browsers receive separate sample databases and private account identities", async () =>
            {
                using var a = await Start(one, WorkspaceRoles.Lecturer);
                using var b = await Start(two, WorkspaceRoles.Lecturer);
                Need(a.StatusCode == HttpStatusCode.Redirect && b.StatusCode == HttpStatusCode.Redirect, "Demo role sign-in failed.");
                oneAuth = Cookie(a, "cmcs-demo.auth");
                oneWorkspaceCookie = Cookie(a, DemoOptions.CookieName); twoWorkspaceCookie = Cookie(b, DemoOptions.CookieName);
                oneToken = Value(oneWorkspaceCookie); twoToken = Value(twoWorkspaceCookie);
                using var first = sessions.Acquire(oneToken); using var second = sessions.Acquire(twoToken);
                Need(first is not null && second is not null && first.Session.Id != second.Session.Id && first.Session.Directory != second.Session.Directory,
                    "Browsers shared a demo workspace.");
                Need(Directory.EnumerateFiles(data, "demo.db", SearchOption.AllDirectories).Count() == 2, "Expected two separate visitor databases.");
                Need(!Directory.EnumerateFiles(data, "LOCAL-ACCOUNTS*.txt", SearchOption.AllDirectories).Any(), "Public demo wrote a credential file.");
            });

            await Check("Claim tables show Rand, readable statuses and only the demo lecturer's own rows", async () =>
            {
                var html = await one.GetStringAsync("/Claim");
                Need(html.Contains("R 5,000.00", StringComparison.Ordinal) && !html.Contains("$", StringComparison.Ordinal)
                    && html.Contains("claim-table-shell", StringComparison.Ordinal) && html.Contains("Review status", StringComparison.Ordinal)
                    && !html.Contains("Demo Lecturer Two", StringComparison.Ordinal), "Claim presentation or lecturer isolation is incorrect.");
            });

            await Check("Editing a claim in one visitor workspace cannot change another visitor's claim", async () =>
            {
                var token = await HttpChecks.Token(one, "/Claim/Edit/1");
                using var update = await one.PostAsync("/Claim/Edit", HttpChecks.Form(("ClaimId", "1"), ("Month", "2026-12"),
                    ("HoursWorked", "17.5"), ("Notes", "Visitor-one-private-proof"), ("__RequestVerificationToken", token)));
                Need(update.StatusCode == HttpStatusCode.Redirect, "Valid fractional demo edit failed.");
                var a = await Read(oneToken, 1); var b = await Read(twoToken, 1);
                Need(a.HoursWorked == 17.5 && a.Notes == "Visitor-one-private-proof" && b.HoursWorked == 20 && b.Notes != a.Notes,
                    "Visitor edits crossed workspace boundaries.");
                var body = await two.GetStringAsync("/Claim/Details/1");
                Need(!body.Contains("Visitor-one-private-proof", StringComparison.Ordinal), "Another browser received private sample notes.");
            });

            await Check("Mixing one visitor's auth ticket with another workspace cookie is rejected", async () =>
            {
                using var attacker = new HttpClient(server.CreateHandler()) { BaseAddress = new Uri("https://localhost") };
                using var request = new HttpRequestMessage(HttpMethod.Get, "/Claim");
                request.Headers.TryAddWithoutValidation("Cookie", oneAuth + "; " + twoWorkspaceCookie);
                using var response = await attacker.SendAsync(request);
                Need(response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Unauthorized, "A ticket was accepted in a different workspace.");
            });

            await Check("Choosing a lecturer demo role does not grant HR payment permissions", async () =>
            {
                var token = await HttpChecks.Token(one, "/Claim");
                using var denied = await one.PostAsync("/HR/MarkAsPaid", HttpChecks.Form(("id", "3"), ("__RequestVerificationToken", token)));
                Need(denied.StatusCode == HttpStatusCode.Forbidden || (denied.StatusCode == HttpStatusCode.Redirect &&
                    denied.Headers.Location?.ToString().Contains("AccessDenied", StringComparison.Ordinal) == true), "Lecturer received HR permissions.");
                Need(!(await Read(oneToken, 3)).IsPaid, "Wrong-role request recorded a payment.");
            });

            await Check("Switching demo roles retains the visitor's claims and completes the real review workflow", async () =>
            {
                using var coordinator = await Start(one, WorkspaceRoles.Coordinator);
                var token = await HttpChecks.Token(one, "/Coordinator");
                using var verify = await one.PostAsync("/Coordinator/Verify", HttpChecks.Form(("id", "1"), ("__RequestVerificationToken", token)));
                Need(verify.StatusCode == HttpStatusCode.Redirect && (await Read(oneToken, 1)).Status == ClaimStages.ManagerPending, "Coordinator demo stage failed.");
                using var manager = await Start(one, WorkspaceRoles.Manager);
                token = await HttpChecks.Token(one, "/Manager");
                using var approve = await one.PostAsync("/Manager/Verify", HttpChecks.Form(("id", "1"), ("__RequestVerificationToken", token)));
                Need(approve.StatusCode == HttpStatusCode.Redirect && (await Read(oneToken, 1)).Status == ClaimStages.Approved, "Manager demo stage failed.");
                using var hr = await Start(one, WorkspaceRoles.HR);
                token = await HttpChecks.Token(one, "/HR");
                using var payment = await one.PostAsync("/HR/MarkAsPaid", HttpChecks.Form(("id", "1"), ("paymentReference", "VISITOR-DEMO"), ("__RequestVerificationToken", token)));
                var saved = await Read(oneToken, 1);
                Need(payment.StatusCode == HttpStatusCode.Redirect && saved.IsPaid && saved.PaymentReference == "VISITOR-DEMO"
                    && saved.HoursWorked == 17.5 && !(await Read(twoToken, 1)).IsPaid, "Demo workflow or workspace separation failed.");
            });

            await Check("Demo invoices, spreadsheets and owned sample attachments download successfully", async () =>
            {
                using var invoice = await one.GetAsync("/HR/GenerateInvoice/1");
                using var sheet = await one.GetAsync("/HR/ExportAllClaims");
                using var document = await one.GetAsync("/Claim/DownloadFile/1");
                Need(invoice.IsSuccessStatusCode && invoice.Content.Headers.ContentType?.MediaType == "application/pdf"
                    && sheet.IsSuccessStatusCode && document.IsSuccessStatusCode, "Demo downloads failed.");
                var bytes = await invoice.Content.ReadAsByteArrayAsync();
                using var reader = new iTextSharp.text.pdf.PdfReader(bytes);
                var text = System.Text.Encoding.Latin1.GetString(reader.GetPageContent(1));
                Need(text.Contains("DEMO CLAIM INVOICE", StringComparison.Ordinal) && text.Contains("PAID", StringComparison.Ordinal) && !text.Contains("UNPAID", StringComparison.Ordinal), "Demo invoice lacks its sample/payment label.");
            });

            await Check("The demo claim cap produces a form error instead of unbounded saved rows", async () =>
            {
                using (var lease = sessions.Acquire(twoToken))
                {
                    Need(lease is not null, "Second visitor workspace is unavailable.");
                    await using var scope = app.Services.CreateAsyncScope();
                    scope.ServiceProvider.GetRequiredService<DemoRequestContext>().Session = lease!.Session;
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var source = await db.Claims.FirstAsync(c => c.ClaimId == 1);
                    var required = DemoOptions.MaximumClaims - await db.Claims.CountAsync();
                    for (var i = 0; i < required; i++) db.Claims.Add(new Claim { LecturerId = source.LecturerId,
                        LecturerName = source.LecturerName, HourlyRate = 250, HoursWorked = 1, Month = "2026-11" });
                    await db.SaveChangesAsync();
                }
                var token = await HttpChecks.Token(two, "/Claim/Create");
                using var response = await two.PostAsync("/Claim/Create", HttpChecks.Form(("Month", "2026-11"), ("HoursWorked", "1"), ("__RequestVerificationToken", token)));
                Need(response.StatusCode == HttpStatusCode.OK && (await response.Content.ReadAsStringAsync()).Contains("claim limit", StringComparison.Ordinal), "Demo claim quota was bypassed.");
            });

            await Check("Demo lecturer and attachment limits reject extra writes with readable errors", async () =>
            {
                string attachmentDirectory;
                int before;
                using (var lease = sessions.Acquire(oneToken))
                {
                    Need(lease is not null, "First visitor workspace is unavailable.");
                    attachmentDirectory = Path.Combine(lease!.Session.Directory, "Attachments");
                    await using var scope = app.Services.CreateAsyncScope();
                    scope.ServiceProvider.GetRequiredService<DemoRequestContext>().Session = lease.Session;
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var count = await db.Lecturers.CountAsync();
                    for (var i = count; i < DemoOptions.MaximumLecturers; i++)
                        db.Lecturers.Add(new Lecturer { FullName = "Sample profile " + i, Email = $"profile{i}@demo.invalid", StandardHourlyRate = 250 });
                    await db.SaveChangesAsync();
                    before = await db.Claims.CountAsync();
                }
                var token = await HttpChecks.Token(one, "/HR/CreateLecturer");
                using var profile = await one.PostAsync("/HR/CreateLecturer", HttpChecks.Form(("FullName", "Extra sample"),
                    ("Email", "extra@demo.invalid"), ("PhoneNumber", "0215550000"), ("Department", "Demo"),
                    ("StandardHourlyRate", "250"), ("__RequestVerificationToken", token)));
                Need(profile.StatusCode == HttpStatusCode.OK && (await profile.Content.ReadAsStringAsync()).Contains("lecturer limit", StringComparison.Ordinal), "Demo lecturer cap was bypassed.");
                var used = Directory.GetFiles(attachmentDirectory, "*.bin").Sum(path => new FileInfo(path).Length);
                using (var padding = new FileStream(Path.Combine(attachmentDirectory, "quota-check.bin"), FileMode.CreateNew))
                    padding.SetLength(DemoOptions.MaximumAttachmentBytes - used);
                using var switchRole = await Start(one, WorkspaceRoles.Lecturer);
                token = await HttpChecks.Token(one, "/Claim/Create");
                var pdf = new CMCS_POE.Services.PdfService().GenerateInvoice(new Claim { ClaimId = 99, LecturerName = "Sample only", Month = "2026-11", HoursWorked = 1, HourlyRate = 250 });
                using var body = new MultipartFormDataContent();
                body.Add(new StringContent(token), "__RequestVerificationToken"); body.Add(new StringContent("2026-11"), "Month");
                body.Add(new StringContent("1"), "HoursWorked"); body.Add(new ByteArrayContent(pdf), "Document", "quota.pdf");
                using var upload = await one.PostAsync("/Claim/Create", body);
                Need(upload.StatusCode == HttpStatusCode.OK && (await upload.Content.ReadAsStringAsync()).Contains("storage is full", StringComparison.Ordinal), "Demo attachment quota was bypassed.");
                using var afterLease = sessions.Acquire(oneToken);
                await using var afterScope = app.Services.CreateAsyncScope();
                afterScope.ServiceProvider.GetRequiredService<DemoRequestContext>().Session = afterLease!.Session;
                var afterDb = afterScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                Need(await afterDb.Claims.CountAsync() == before && await afterDb.Lecturers.CountAsync() == DemoOptions.MaximumLecturers,
                    "Quota rejection changed saved records.");
            });

            await Check("GET and tokenless reset cannot clear a visitor workspace", async () =>
            {
                using var get = await one.GetAsync("/Demo/Reset");
                using var missing = await one.PostAsync("/Demo/Reset", HttpChecks.Form());
                Need(!get.IsSuccessStatusCode && missing.StatusCode == HttpStatusCode.BadRequest && (await Read(oneToken, 1)).IsPaid,
                    "An unsafe reset cleared sample state.");
            });

            await Check("Reset retires only the requesting workspace and rejects its old ticket", async () =>
            {
                var token = await HttpChecks.Token(one, "/Demo");
                using var reset = await one.PostAsync("/Demo/Reset", HttpChecks.Form(("__RequestVerificationToken", token)));
                Need(reset.StatusCode == HttpStatusCode.Redirect && sessions.Acquire(oneToken) is null && (await Read(twoToken, 1)).HoursWorked == 20,
                    "Reset changed another visitor or retained the old workspace.");
                using var replay = new HttpClient(server.CreateHandler()) { BaseAddress = new Uri("https://localhost") };
                using var request = new HttpRequestMessage(HttpMethod.Get, "/Claim");
                request.Headers.TryAddWithoutValidation("Cookie", oneAuth + "; " + oneWorkspaceCookie);
                using var rejected = await replay.SendAsync(request);
                Need(rejected.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Unauthorized, "A retired ticket was reused.");
                using var fresh = await Start(one, WorkspaceRoles.Lecturer);
                oneToken = Value(Cookie(fresh, DemoOptions.CookieName));
                Need((await Read(oneToken, 1)).HoursWorked == 20 && !(await Read(oneToken, 1)).IsPaid, "Reset did not load fresh samples.");
            });

            await Check("New demo allocation is bounded and full capacity returns a readable response", async () =>
            {
                var created = 0;
                while (await sessions.CreateAsync(CancellationToken.None) is not null)
                {
                    created++; Need(created <= DemoOptions.MaximumSessions, "Workspace allocation is unbounded.");
                }
                using var visitor = HttpChecks.Client(server);
                using var response = await Start(visitor, WorkspaceRoles.Lecturer);
                Need(response.StatusCode == HttpStatusCode.ServiceUnavailable, "Full capacity should refuse another workspace.");
            });

            await Check("Expired demo sessions lose access and their temporary files are cleaned up", async () =>
            {
                var staleHost = Path.Combine(data, "Sandboxes", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(staleHost);
                await File.WriteAllTextAsync(Path.Combine(staleHost, "sample.tmp"), "Expired sample fixture");
                Directory.SetLastWriteTimeUtc(staleHost, clock.GetUtcNow().AddHours(-3).UtcDateTime);
                clock.Advance(TimeSpan.FromMinutes(DemoOptions.Minutes + 1));
                using var response = await two.GetAsync("/Claim");
                Need(response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Unauthorized, "Expired demo retained access.");
                sessions.Cleanup();
                Need(!Directory.Exists(staleHost), "An expired previous-host folder was not removed.");
                Need(!Directory.EnumerateFiles(data, "demo.db", SearchOption.AllDirectories).Any(), "Expired visitor files were not removed.");
                using var fresh = await Start(one, WorkspaceRoles.HR);
                Need(fresh.StatusCode == HttpStatusCode.Redirect, "Expired capacity was not reclaimed.");
            });

            await Check("Demo activity leaves personal data untouched and blocks overlapping data roots", async () =>
            {
                Need(await File.ReadAllTextAsync(personalFile) == sentinel && Directory.GetFiles(personal).Length == 1, "The demo changed personal data.");
                foreach (var directory in new[] { personal, Path.Combine(content, "wwwroot", "demo") })
                {
                    var refused = false;
                    try
                    {
                        await using var invalid = CMCS_POE.Program.BuildApplication(new[] { "--environment=Testing", "--contentRoot=" + content }, builder =>
                        {
                            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                            { ["Demo:Enabled"] = "true", ["Demo:DataDirectory"] = directory, ["PersonalWorkspace:DataDirectory"] = personal });
                            builder.WebHost.UseTestServer();
                        });
                    }
                    catch (InvalidOperationException exception) when (exception.Message.Contains("separate private folder", StringComparison.Ordinal)) { refused = true; }
                    Need(refused, "Unsafe demo data root was accepted.");
                }
            });
            await app.StopAsync();
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(fixture)) Directory.Delete(fixture, recursive: true);
        }
    }

    private static string Cookie(HttpResponseMessage response, string name) => response.Headers.GetValues("Set-Cookie")
        .First(value => value.StartsWith(name + "=", StringComparison.Ordinal)).Split(';')[0];
    private static string Value(string pair) => pair[(pair.IndexOf('=') + 1)..];
    private static void Need(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private sealed class DemoClock : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan amount) { _now += amount; }
    }
}
