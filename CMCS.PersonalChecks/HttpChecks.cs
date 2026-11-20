using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using CMCS_POE.Data;
using CMCS_POE.Models;
using CMCS_POE.Security;
using CMCS_POE.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public static class HttpChecks
{
    // These credentials exist only inside a deleted, isolated test fixture.
    private const string TestPassword = "IsolatedFixtureOnly!82";
    private static readonly Uri BaseUri = new("https://localhost");

    public static async Task RunAsync(Action<string> passed)
    {
        var fixture = Path.Combine(Path.GetTempPath(), "cmcs-auth-http-" + Guid.NewGuid().ToString("N"));
        var content = Path.Combine(fixture, "Content");
        var data = Path.Combine(fixture, "Data");
        Directory.CreateDirectory(Path.Combine(content, "wwwroot", "uploads"));
        Directory.CreateDirectory(Path.Combine(content, "wwwroot", "css"));
        await File.WriteAllTextAsync(Path.Combine(content, "wwwroot", "uploads", "blocked-proof.txt"), "private fixture");
        await File.WriteAllTextAsync(Path.Combine(content, "wwwroot", "css", "site.css"), "body { color: #123; }");

        try
        {
            await using var app = CMCS_POE.Program.BuildApplication(new[] { "--environment=Testing", "--contentRoot=" + content }, builder =>
            {
                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["PersonalWorkspace:DataDirectory"] = data, ["Demo:Enabled"] = "false" });
                builder.Logging.ClearProviders();
                // Test fixtures only: expose server exceptions without verbose SQL/request logging.
                builder.Logging.AddConsole().SetMinimumLevel(LogLevel.Error);
                builder.WebHost.UseTestServer();
            });
            await app.StartAsync();
            using var anonymous = Client(app.GetTestServer());
            using var one = Client(app.GetTestServer());
            using var two = Client(app.GetTestServer());
            using var coordinator = Client(app.GetTestServer());
            using var manager = Client(app.GetTestServer());
            using var hr = Client(app.GetTestServer());
            int firstId, secondId;
            byte[] attachment;

            await using (var scope = app.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
                var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                foreach (var role in WorkspaceRoles.All) Need((await roles.CreateAsync(new IdentityRole(role))).Succeeded, "Fixture role creation failed.");
                foreach (var (email, role, id) in new[]
                {
                    ("one@checks.invalid", WorkspaceRoles.Lecturer, (int?)1),
                    ("two@checks.invalid", WorkspaceRoles.Lecturer, (int?)2),
                    ("coordinator@checks.invalid", WorkspaceRoles.Coordinator, (int?)null),
                    ("manager@checks.invalid", WorkspaceRoles.Manager, (int?)null),
                    ("hr@checks.invalid", WorkspaceRoles.HR, (int?)null),
                    ("lock@checks.invalid", WorkspaceRoles.Coordinator, (int?)null)
                })
                {
                    var user = new ApplicationUser { UserName = email, Email = email, DisplayName = email, LecturerId = id, EmailConfirmed = true, LockoutEnabled = true };
                    Need((await users.CreateAsync(user, TestPassword)).Succeeded, "Fixture user creation failed.");
                    Need((await users.AddToRoleAsync(user, role)).Succeeded, "Fixture role assignment failed.");
                }
                var first = new Claim { LecturerId = 1, LecturerName = "Dr. John Smith", Month = "2026-10", HoursWorked = 10, HourlyRate = 250, Status = ClaimStages.CoordinatorPending, Notes = "owner-one-private" };
                var second = new Claim { LecturerId = 2, LecturerName = "Prof. Jane Doe", Month = "2026-09", HoursWorked = 8, HourlyRate = 300, Status = ClaimStages.CoordinatorPending, Notes = "owner-two-private" };
                attachment = new PdfService().GenerateInvoice(first);
                using var stream = new MemoryStream(attachment);
                var file = new FormFile(stream, 0, stream.Length, "Document", "fixture.pdf");
                var saved = scope.ServiceProvider.GetRequiredService<ClaimDocuments>().Save(file);
                first.DocumentPath = saved.Path; first.DocumentName = saved.Name;
                db.Claims.AddRange(first, second);
                await db.SaveChangesAsync();
                firstId = first.ClaimId; secondId = second.ClaimId;
            }

            async Task Check(string name, Func<Task> test) { await test(); passed(name); }
            async Task<Claim> Read(int id)
            {
                await using var scope = app.Services.CreateAsyncScope();
                return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Claims.AsNoTracking().SingleAsync(c => c.ClaimId == id);
            }

            await Check("Anonymous users cannot read claim, review, HR or spreadsheet routes", async () =>
            {
                foreach (var path in new[] { "/Claim", $"/Claim/Details/{firstId}", "/Coordinator", "/Manager", "/HR", "/HR/ExportAllClaims", "/HR/ExportLecturers" })
                {
                    using var response = await anonymous.GetAsync(path);
                    Need(response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Unauthorized, "Anonymous route was not challenged: " + path);
                }
                using var health = await anonymous.GetAsync("/health");
                Need(health.IsSuccessStatusCode, "Health should remain available.");
            });

            await Check("Public demo role-entry endpoints are unavailable in personal mode", async () =>
            {
                using var get = await anonymous.GetAsync("/Demo");
                var token = await Token(anonymous, "/Account/Login");
                using var post = await anonymous.PostAsync("/Demo/Start", Form(("role", "HR"), ("__RequestVerificationToken", token)));
                Need(get.StatusCode == HttpStatusCode.NotFound && post.StatusCode == HttpStatusCode.NotFound,
                    "Demo endpoints must not grant roles in the personal workspace.");
            });

            await Check("Login requires a matching antiforgery token", async () =>
            {
                using var response = await anonymous.PostAsync("/Account/Login", Form(("Email", "one@checks.invalid"), ("Password", TestPassword)));
                Need(response.StatusCode == HttpStatusCode.BadRequest, "A tokenless login must be rejected.");
            });

            await Check("Each assigned role signs in and reaches its own workspace", async () =>
            {
                foreach (var (client, email, path) in new[]
                {
                    (one, "one@checks.invalid", "/Claim"), (two, "two@checks.invalid", "/Claim"),
                    (coordinator, "coordinator@checks.invalid", "/Coordinator"),
                    (manager, "manager@checks.invalid", "/Manager"), (hr, "hr@checks.invalid", "/HR")
                })
                {
                    await Login(client, email, TestPassword);
                    using var response = await client.GetAsync(path);
                    Need(response.IsSuccessStatusCode, "Assigned workspace failed for " + email);
                }
            });

            await Check("Untrusted cookies and external return URLs cannot bypass sign-in", async () =>
            {
                using var invalid = new HttpRequestMessage(HttpMethod.Get, "/Claim");
                invalid.Headers.TryAddWithoutValidation("Cookie", "cmcs-personal.auth=untrusted-cookie");
                using var rejected = await anonymous.SendAsync(invalid);
                Need(rejected.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Unauthorized, "An untrusted cookie was accepted.");
                using var client = Client(app.GetTestServer());
                var token = await Token(client, "/Account/Login");
                using var response = await client.PostAsync("/Account/Login", Form(("Email", "hr@checks.invalid"),
                    ("Password", TestPassword), ("ReturnUrl", "https://example.invalid/outside"), ("__RequestVerificationToken", token)));
                Need(response.StatusCode == HttpStatusCode.Redirect && response.Headers.Location?.ToString() == "/Account/Workspace",
                    "A sign-in redirect left the local workspace.");
            });

            await Check("A lecturer sees only their own saved claim rows", async () =>
            {
                var body = await one.GetStringAsync("/Claim");
                Need(body.Contains("Dr. John Smith", StringComparison.Ordinal) && !body.Contains("Prof. Jane Doe", StringComparison.Ordinal), "Another lecturer's row leaked into the list.");
            });

            await Check("Cross-lecturer detail, edit, delete and download requests do not expose or change a claim", async () =>
            {
                foreach (var path in new[] { $"/Claim/Details/{firstId}", $"/Claim/Edit/{firstId}", $"/Claim/DownloadFile/{firstId}" })
                {
                    using var response = await two.GetAsync(path);
                    Need(response.StatusCode == HttpStatusCode.NotFound, "Cross-owner read should be unavailable: " + path);
                }
                var token = await Token(two, "/Claim/Create");
                using var edit = await two.PostAsync("/Claim/Edit", Form(("ClaimId", firstId.ToString()), ("Month", "2026-12"), ("HoursWorked", "99"), ("__RequestVerificationToken", token)));
                using var delete = await two.PostAsync("/Claim/Delete", Form(("id", firstId.ToString()), ("__RequestVerificationToken", token)));
                Need(edit.StatusCode == HttpStatusCode.NotFound && delete.StatusCode == HttpStatusCode.NotFound, "Cross-owner writes were not rejected.");
                Need((await Read(firstId)).Month == "2026-10", "Cross-owner input changed saved data.");
            });

            await Check("Wrong-role users cannot execute coordinator, manager or HR actions", async () =>
            {
                var token = await Token(one, "/Claim/Create");
                foreach (var path in new[] { "/Coordinator/Verify", "/Manager/Verify", "/HR/MarkAsPaid", "/HR/BulkMarkAsPaid" })
                {
                    using var response = await one.PostAsync(path, Form(("id", firstId.ToString()), ("claimIds", firstId.ToString()), ("__RequestVerificationToken", token)));
                    Need(Denied(response), "Wrong-role action was not forbidden: " + path);
                }
                Need((await Read(firstId)).Status == ClaimStages.CoordinatorPending && !(await Read(firstId)).IsPaid, "Wrong-role input changed state.");
            });

            await Check("Missing antiforgery tokens cannot mutate a signed-in claim", async () =>
            {
                using var response = await one.PostAsync("/Claim/Delete", Form(("id", firstId.ToString())));
                Need(response.StatusCode == HttpStatusCode.BadRequest, "A tokenless delete must fail.");
                Need((await Read(firstId)).Status == ClaimStages.CoordinatorPending, "Tokenless request changed data.");
            });

            await Check("Forged owner, rate and payment fields cannot control a new claim", async () =>
            {
                var token = await Token(one, "/Claim/Create");
                using var response = await one.PostAsync("/Claim/Create", Form(("Month", "2026-12"), ("HoursWorked", "12"),
                    ("LecturerId", "2"), ("HourlyRate", "999"), ("Status", "Approved"), ("IsPaid", "true"), ("PaymentReference", "forged"),
                    ("DocumentPath", "/uploads/blocked-proof.txt"), ("__RequestVerificationToken", token)));
                Need(response.StatusCode == HttpStatusCode.Redirect, "A valid owned claim was not accepted.");
                await using var scope = app.Services.CreateAsyncScope();
                var saved = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Claims.SingleAsync(c => c.Month == "2026-12");
                Need(saved.LecturerId == 1 && saved.HourlyRate == 250 && saved.Status == ClaimStages.CoordinatorPending && !saved.IsPaid && saved.PaymentReference is null && saved.DocumentPath is null,
                    "Posted server-owned fields were trusted.");
            });

            await Check("Non-finite hours and invalid months produce form errors without persistence", async () =>
            {
                await using var scope = app.Services.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var before = await db.Claims.CountAsync();
                foreach (var (month, hours) in new[]
                {
                    ("2026-13", "10"), ("2026-11", "NaN"), ("2026-11", "Infinity"),
                    ("2026-11", "-Infinity"), ("2026-11", "999999999999999999"),
                    ("2026-11", "1e300"), ("2026-11", "0.99"), ("2026-11", "200.01")
                })
                {
                    foreach (var action in new[] { "Create", "Edit" })
                    {
                        var token = await Token(one, "/Claim/Create");
                        using var response = await one.PostAsync("/Claim/" + action, Form(("ClaimId", firstId.ToString()),
                            ("Month", month), ("HoursWorked", hours), ("__RequestVerificationToken", token)));
                        Need(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.BadRequest,
                            $"Invalid {action} input month={month}, hours={hours} returned HTTP {(int)response.StatusCode} ({response.StatusCode}); redirect={response.Headers.Location}. Expected a readable form error (200 or 400).");
                        if (response.StatusCode == HttpStatusCode.OK)
                        {
                            var html = await response.Content.ReadAsStringAsync();
                            Need(html.Contains("validation-summary-errors", StringComparison.Ordinal), "The returned form must show its validation errors.");
                        }
                    }
                }
                var unchanged = await Read(firstId);
                Need(await db.Claims.CountAsync() == before && unchanged.Month == "2026-10" && unchanged.HoursWorked == 10
                    && unchanged.Status == ClaimStages.CoordinatorPending && !unchanged.IsPaid,
                    "Invalid create/edit input changed the saved claims.");
            });

            await Check("Private attachment download works for its owner and static upload paths remain unavailable", async () =>
            {
                using var download = await one.GetAsync($"/Claim/DownloadFile/{firstId}");
                Need(download.IsSuccessStatusCode && (await download.Content.ReadAsByteArrayAsync()).SequenceEqual(attachment), "Owned attachment did not round-trip.");
                using var publicFile = await anonymous.GetAsync("/uploads/blocked-proof.txt");
                using var asset = await anonymous.GetAsync("/css/site.css");
                Need(publicFile.StatusCode == HttpStatusCode.NotFound && asset.IsSuccessStatusCode, "Private/static asset boundaries are incorrect.");
                var encrypted = await File.ReadAllBytesAsync(Directory.GetFiles(Path.Combine(data, "Attachments"), "*.bin").Single());
                Need(!encrypted.SequenceEqual(attachment), "Document bytes were stored as plaintext.");
            });

            await Check("A disguised PDF upload leaves no claim or private attachment behind", async () =>
            {
                await using var scope = app.Services.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var before = await db.Claims.CountAsync();
                var files = Directory.GetFiles(Path.Combine(data, "Attachments"), "*.bin").Length;
                var token = await Token(one, "/Claim/Create");
                using var body = new MultipartFormDataContent();
                body.Add(new StringContent(token), "__RequestVerificationToken");
                body.Add(new StringContent("2026-07"), "Month");
                body.Add(new StringContent("10"), "HoursWorked");
                body.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("This is not a PDF")), "Document", "fake.pdf");
                using var response = await one.PostAsync("/Claim/Create", body);
                Need(response.StatusCode == HttpStatusCode.OK && await db.Claims.CountAsync() == before
                    && Directory.GetFiles(Path.Combine(data, "Attachments"), "*.bin").Length == files,
                    "An invalid document created saved data or an orphan attachment.");
            });

            await Check("GET confirmation pages do not approve a claim", async () =>
            {
                using var response = await coordinator.GetAsync($"/Coordinator/Verify/{firstId}");
                Need(response.IsSuccessStatusCode && (await Read(firstId)).Status == ClaimStages.CoordinatorPending, "GET verification changed state.");
            });

            await Check("Manager approval and HR payment cannot skip coordinator review", async () =>
            {
                var managerToken = await Token(manager, "/Manager");
                using var managerResult = await manager.PostAsync("/Manager/Verify", Form(("id", firstId.ToString()), ("__RequestVerificationToken", managerToken)));
                var hrToken = await Token(hr, "/HR");
                using var hrResult = await hr.PostAsync("/HR/MarkAsPaid", Form(("id", firstId.ToString()), ("__RequestVerificationToken", hrToken)));
                using var bulk = await hr.PostAsync("/HR/BulkMarkAsPaid", Form(("claimIds", firstId.ToString()), ("__RequestVerificationToken", hrToken)));
                Need(managerResult.StatusCode == HttpStatusCode.Conflict && hrResult.StatusCode == HttpStatusCode.Conflict, "Review stages were bypassed.");
                Need((await Read(firstId)).Status == ClaimStages.CoordinatorPending && !(await Read(firstId)).IsPaid, "A pending claim was approved or paid.");
            });

            await Check("Coordinator, manager and HR complete the allowed workflow exactly once", async () =>
            {
                var token = await Token(coordinator, "/Coordinator");
                using var verify = await coordinator.PostAsync("/Coordinator/Verify", Form(("id", firstId.ToString()), ("__RequestVerificationToken", token)));
                Need(verify.StatusCode == HttpStatusCode.Redirect && (await Read(firstId)).Status == ClaimStages.ManagerPending, "Coordinator transition failed.");
                var managerToken = await Token(manager, "/Manager");
                using var approve = await manager.PostAsync("/Manager/Verify", Form(("id", firstId.ToString()), ("__RequestVerificationToken", managerToken)));
                Need(approve.StatusCode == HttpStatusCode.Redirect && (await Read(firstId)).Status == ClaimStages.Approved, "Manager transition failed.");
                var hrToken = await Token(hr, "/HR");
                using var pay = await hr.PostAsync("/HR/MarkAsPaid", Form(("id", firstId.ToString()), ("paymentReference", "FIXTURE-ONLY"), ("__RequestVerificationToken", hrToken)));
                Need(pay.StatusCode == HttpStatusCode.Redirect && (await Read(firstId)).IsPaid, "Payment status was not recorded.");
                var recorded = await Read(firstId);
                using var repeat = await hr.PostAsync("/HR/MarkAsPaid", Form(("id", firstId.ToString()), ("paymentReference", "CHANGED"), ("__RequestVerificationToken", hrToken)));
                var unchanged = await Read(firstId);
                Need(repeat.StatusCode == HttpStatusCode.Conflict && unchanged.PaidDate == recorded.PaidDate && unchanged.PaymentReference == "FIXTURE-ONLY", "Repeated recording replaced payment evidence.");
            });

            await Check("An approved or paid claim cannot be edited or deleted by its owner", async () =>
            {
                var token = await Token(one, "/Claim");
                using var edit = await one.PostAsync("/Claim/Edit", Form(("ClaimId", firstId.ToString()), ("Month", "2026-08"), ("HoursWorked", "99"), ("__RequestVerificationToken", token)));
                using var delete = await one.PostAsync("/Claim/Delete", Form(("id", firstId.ToString()), ("__RequestVerificationToken", token)));
                Need(edit.StatusCode == HttpStatusCode.Conflict && delete.StatusCode == HttpStatusCode.Conflict && (await Read(firstId)).IsPaid, "A completed claim was mutable.");
            });

            await Check("Spreadsheet and report access is restricted to HR", async () =>
            {
                using var denied = await one.GetAsync("/HR/ExportAllClaims");
                using var allowed = await hr.GetAsync("/HR/ExportAllClaims");
                using var invoice = await hr.GetAsync($"/HR/GenerateInvoice/{firstId}");
                Need(Denied(denied) && allowed.IsSuccessStatusCode && invoice.IsSuccessStatusCode, "Report access boundaries failed.");
            });

            await Check("SignalR negotiation requires a signed-in account", async () =>
            {
                using var denied = await anonymous.PostAsync("/claimHub/negotiate?negotiateVersion=1", new StringContent(string.Empty));
                using var allowed = await one.PostAsync("/claimHub/negotiate?negotiateVersion=1", new StringContent(string.Empty));
                Need(!denied.IsSuccessStatusCode && allowed.IsSuccessStatusCode, "SignalR access boundaries failed.");
            });

            await Check("A disabled account loses access on its next request", async () =>
            {
                await using (var scope = app.Services.CreateAsyncScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var user = await db.Users.SingleAsync(u => u.Email == "two@checks.invalid");
                    user.IsActive = false; await db.SaveChangesAsync();
                }
                using var response = await two.GetAsync("/Claim");
                Need(response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Unauthorized, "A disabled account retained access.");
            });

            await Check("Repeated failed passwords lock the account without granting access", async () =>
            {
                using var client = Client(app.GetTestServer());
                for (var attempt = 0; attempt < 5; attempt++)
                {
                    var token = await Token(client, "/Account/Login");
                    using var response = await client.PostAsync("/Account/Login", Form(("Email", "lock@checks.invalid"), ("Password", "WrongPassword!82"), ("__RequestVerificationToken", token)));
                    Need(response.StatusCode == HttpStatusCode.OK, "Failed password should redisplay the generic sign-in error.");
                }
                await using var scope = app.Services.CreateAsyncScope();
                var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var user = await users.FindByEmailAsync("lock@checks.invalid");
                Need(user is not null && await users.IsLockedOutAsync(user), "Failed attempts did not create the expected lockout.");
            });

            await Check("Logout rejects GET and signed-out clients cannot reuse the workspace", async () =>
            {
                using var get = await one.GetAsync("/Account/Logout");
                Need(!get.IsSuccessStatusCode, "GET logout must not mutate a session.");
                var token = await Token(one, "/Claim");
                using var logout = await one.PostAsync("/Account/Logout", Form(("__RequestVerificationToken", token)));
                using var after = await one.GetAsync("/Claim");
                Need(logout.StatusCode == HttpStatusCode.Redirect && after.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.Unauthorized, "Logout did not end the client session.");
            });

            await app.StopAsync();
        }
        finally
        {
            SqliteClearPools();
            if (Directory.Exists(fixture)) Directory.Delete(fixture, recursive: true);
        }
    }

    private static void SqliteClearPools() => Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
    private static void Need(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static bool Denied(HttpResponseMessage response) => response.StatusCode == HttpStatusCode.Forbidden ||
        (response.StatusCode == HttpStatusCode.Redirect && response.Headers.Location?.ToString().Contains("AccessDenied", StringComparison.Ordinal) == true);
    internal static FormUrlEncodedContent Form(params (string Key, string Value)[] values) => new(values.Select(v => new KeyValuePair<string, string>(v.Key, v.Value)));
    private static async Task Login(HttpClient client, string email, string password)
    {
        var token = await Token(client, "/Account/Login");
        using var response = await client.PostAsync("/Account/Login", Form(("Email", email), ("Password", password), ("__RequestVerificationToken", token)));
        Need(response.StatusCode == HttpStatusCode.Redirect, "Fixture sign-in failed for " + email);
        Need(response.Headers.TryGetValues("Set-Cookie", out var values) && values.Any(value =>
            value.StartsWith("cmcs-personal.auth=", StringComparison.Ordinal) &&
            value.Contains("httponly", StringComparison.OrdinalIgnoreCase) &&
            value.Contains("secure", StringComparison.OrdinalIgnoreCase)), "The sign-in cookie lacks its expected protections.");
    }
    internal static async Task<string> Token(HttpClient client, string path)
    {
        var body = await client.GetStringAsync(path);
        foreach (Match input in Regex.Matches(body, "<input\\b[^>]*>", RegexOptions.IgnoreCase))
        {
            if (!Regex.IsMatch(input.Value, "name=\"__RequestVerificationToken\"", RegexOptions.IgnoreCase)) continue;
            var value = Regex.Match(input.Value, "value=\"([^\"]+)\"", RegexOptions.IgnoreCase);
            if (value.Success) return WebUtility.HtmlDecode(value.Groups[1].Value);
        }
        throw new InvalidOperationException("No antiforgery token was rendered on " + path);
    }
    internal static HttpClient Client(TestServer server) => new(new CookieJar(server.CreateHandler())) { BaseAddress = BaseUri };
    private sealed class CookieJar : DelegatingHandler
    {
        private readonly CookieContainer _cookies = new();
        public CookieJar(HttpMessageHandler inner) : base(inner) { }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri ?? throw new InvalidOperationException("A request URI is required.");
            var cookies = _cookies.GetCookieHeader(uri);
            if (cookies.Length > 0) request.Headers.TryAddWithoutValidation("Cookie", cookies);
            var response = await base.SendAsync(request, cancellationToken);
            if (response.Headers.TryGetValues("Set-Cookie", out var values))
                foreach (var value in values) _cookies.SetCookies(uri, value);
            return response;
        }
    }
}
