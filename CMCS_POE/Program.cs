using CMCS_POE.Demo;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Threading.RateLimiting;
using CMCS_POE.Data;
using CMCS_POE.Security;
using CMCS_POE.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CMCS_POE;

public class Program
{
    public static void Main(string[] args)
    {
        if (args.Length == 1 && args[0] == "--backup-personal-data")
        {
            BackupPersonalData();
            return;
        }
        var app = BuildApplication(args);
        app.Run();
    }

    public static WebApplication BuildApplication(string[] args, Action<WebApplicationBuilder>? configure = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args, ApplicationName = typeof(Program).Assembly.GetName().Name
        });
        configure?.Invoke(builder);
        var personalDirectory = Path.GetFullPath(builder.Configuration["PersonalWorkspace:DataDirectory"]
            ?? Environment.GetEnvironmentVariable("CMCS_PERSONAL_DATA_DIR")
            ?? Path.Combine(builder.Environment.ContentRootPath, "App_Data", "Personal"));
        var demoEnabled = string.Equals(builder.Configuration["Demo:Enabled"]
            ?? Environment.GetEnvironmentVariable("CMCS_DEMO_MODE"), "true", StringComparison.OrdinalIgnoreCase);
        var directory = demoEnabled ? Path.GetFullPath(builder.Configuration["Demo:DataDirectory"]
            ?? Environment.GetEnvironmentVariable("CMCS_DEMO_DATA_DIR")
            ?? Path.Combine(builder.Environment.ContentRootPath, "App_Data", "Demo")) : personalDirectory;
        if (demoEnabled)
        {
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            static string Prefix(string path) => path.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var webRoot = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "wwwroot"));
            if (Prefix(directory).StartsWith(Prefix(personalDirectory), comparison)
                || Prefix(personalDirectory).StartsWith(Prefix(directory), comparison)
                || Prefix(directory).StartsWith(Prefix(webRoot), comparison)
                || File.Exists(Path.Combine(directory, "cmcs-personal.db")))
                throw new InvalidOperationException("The demo must use a separate private folder outside personal data and public assets.");
        }
        var demoOptions = new DemoOptions(demoEnabled, directory);
        builder.Services.AddSingleton(demoOptions);
        builder.Services.AddScoped<DemoRequestContext>();
        builder.Services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        builder.Services.AddSingleton<DemoSessions>();
        if (demoEnabled) builder.Services.AddHostedService<DemoCleanup>();
        if (builder.Environment.IsEnvironment("Testing") &&
            !directory.StartsWith(Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new InvalidOperationException("HTTP checks require an isolated temporary data directory.");
        Directory.CreateDirectory(directory);
        var keys = Path.Combine(directory, "Keys");
        Directory.CreateDirectory(keys);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            File.SetUnixFileMode(keys, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
        var protection = builder.Services.AddDataProtection().SetApplicationName(demoEnabled ? "cmcs-demo" : "cmcs-personal")
            .PersistKeysToFileSystem(new DirectoryInfo(keys));
        if (OperatingSystem.IsWindows()) protection.ProtectKeysWithDpapi();

        builder.Services.AddAntiforgery(options =>
        {
            options.Cookie.Name = demoEnabled ? "cmcs-demo.antiforgery" : "cmcs-personal.antiforgery";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        });
        builder.Services.AddControllersWithViews(options =>
        {
            options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
            options.Filters.Add(new ResponseCacheAttribute { NoStore = true, Location = ResponseCacheLocation.None });
        });
        builder.Services.Configure<Microsoft.AspNetCore.Mvc.CookieTempDataProviderOptions>(options =>
        {
            options.Cookie.Name = demoEnabled ? "cmcs-demo.messages" : "cmcs-personal.messages";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        });
        builder.Services.AddSignalR();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddDbContext<ApplicationDbContext>((provider, options) => options.UseSqlite(
            new SqliteConnectionStringBuilder
            {
                DataSource = demoEnabled
                    ? Path.Combine(provider.GetRequiredService<DemoRequestContext>().Directory, "demo.db")
                    : Path.Combine(directory, "cmcs-personal.db"),
                ForeignKeys = true, Pooling = !demoEnabled
            }.ToString()));
        builder.Services.AddPersonalIdentity(builder.Environment.IsDevelopment(), demoEnabled);
        builder.Services.AddAuthorization(options => options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("demo-start", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "local",
                _ => new FixedWindowRateLimiterOptions
                { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
            if (demoEnabled)
                options.GlobalLimiter = PartitionedRateLimiter.CreateChained(
                    PartitionedRateLimiter.Create<HttpContext, string>(context =>
                        RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "local",
                            _ => new FixedWindowRateLimiterOptions
                            { PermitLimit = 180, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true })),
                    PartitionedRateLimiter.Create<HttpContext, string>(context => context.Request.Path.StartsWithSegments("/claimHub")
                        ? RateLimitPartition.GetNoLimiter("hub")
                        : RateLimitPartition.GetConcurrencyLimiter("demo-host",
                            _ => new ConcurrencyLimiterOptions { PermitLimit = 4, QueueLimit = 0 })));
            options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "local",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true
                }));
        });
        builder.Services.AddScoped<IClaimRepository, EFClaimRepository>();
        builder.Services.AddScoped<ILecturerRepository, EFLecturerRepository>();
        builder.Services.AddSingleton<IClaimValidationService, ClaimValidationService>();
        builder.Services.AddScoped<IPdfService, PdfService>();
        builder.Services.AddScoped<IExcelService, ExcelService>();
        builder.Services.AddScoped<ClaimRealtime>();
        builder.Services.AddScoped(provider => new ClaimDocuments(
            demoEnabled ? provider.GetRequiredService<DemoRequestContext>().Directory : directory,
            builder.Environment.ContentRootPath, provider.GetRequiredService<IDataProtectionProvider>(),
            provider.GetRequiredService<ILogger<ClaimDocuments>>(),
            demoEnabled ? DemoOptions.MaximumAttachmentBytes : long.MaxValue));

        if (demoEnabled) builder.WebHost.ConfigureKestrel(options =>
        {
            options.Limits.MaxConcurrentConnections = 100;
            options.Limits.MaxConcurrentUpgradedConnections = 50;
            options.Limits.MaxRequestBodySize = 6 * 1024 * 1024;
        });
        var app = builder.Build();
        if (!demoEnabled) using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Database.Migrate();
            if (app.Environment.IsDevelopment() && Environment.GetEnvironmentVariable("CMCS_ENABLE_LOCAL_ACCOUNTS") == "1")
                LocalAccountProvisioner.InitializeAsync(scope.ServiceProvider, directory).GetAwaiter().GetResult();
        }
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Home/Error");
            app.UseHsts();
            app.UseHttpsRedirection();
        }
        app.Use(async (context, next) =>
        {
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["Referrer-Policy"] = "same-origin";
            // Old personal uploads are downloadable only through the ownership-checked action.
            if (context.Request.Path.StartsWithSegments("/uploads", StringComparison.OrdinalIgnoreCase))
            { context.Response.StatusCode = 404; return; }
            await next(context);
        });
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PublicAssetProvider(app.Environment.WebRootFileProvider,
                app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot"))
        });
        app.UseRouting();
        app.UseRateLimiter();
        if (demoEnabled) app.UseMiddleware<DemoMiddleware>();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapGet("/health", () => Results.Ok(new { status = "ok", workspace = demoEnabled ? "cmcs-demo" : "cmcs-personal" })).AllowAnonymous();
        app.MapHub<CMCS_Part2.Hubs.ClaimHub>("/claimHub", options => options.CloseOnAuthenticationExpiration = true).RequireAuthorization();
        app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");
        return app;
    }

    private static void BackupPersonalData()
    {
        var directory = Environment.GetEnvironmentVariable("CMCS_PERSONAL_DATA_DIR")
            ?? throw new InvalidOperationException("CMCS_PERSONAL_DATA_DIR must identify the personal folder for backup.");
        var source = Path.Combine(directory, "cmcs-personal.db");
        if (!File.Exists(source)) { Console.WriteLine("No personal data file exists yet; no data backup is needed."); return; }
        var backups = Path.Combine(directory, "Backups");
        Directory.CreateDirectory(backups);
        var destination = Path.Combine(backups, "before-accounts-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..8] + ".db");
        using var input = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = source, Mode = SqliteOpenMode.ReadOnly }.ToString());
        using var output = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = destination }.ToString());
        input.Open(); output.Open(); input.BackupDatabase(output);
        Console.WriteLine("Personal data backup created: " + destination);
    }
}

//OpenAI.2025.Chat-GPT(Version 5).[Large language model]Available at: https://chatgpt.com/c/68b46a0b-99f8-8326-98b4-5245e5c93d35[Accessed: 20 November 2025].
