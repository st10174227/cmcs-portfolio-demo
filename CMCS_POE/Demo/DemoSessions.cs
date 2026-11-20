using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace CMCS_POE.Demo;

public sealed class DemoSessions
{
    private readonly ConcurrentDictionary<string, DemoSession> _sessions = new();
    private readonly SemaphoreSlim _allocation = new(1, 1);
    private readonly IDataProtector _protector;
    private readonly TimeProvider _clock;
    private readonly ILogger<DemoSessions> _logger;
    private readonly string _sandboxesDirectory;
    private readonly string _instanceDirectory;

    public DemoSessions(DemoOptions options, IDataProtectionProvider protection, TimeProvider clock, ILogger<DemoSessions> logger)
    {
        _clock = clock; _logger = logger;
        _protector = protection.CreateProtector("cmcs-demo.workspace.v1");
        _sandboxesDirectory = Path.Combine(options.DataDirectory, "Sandboxes");
        _instanceDirectory = Path.Combine(_sandboxesDirectory, Guid.NewGuid().ToString("N"));
        if (options.Enabled) Directory.CreateDirectory(_instanceDirectory);
    }

    public async Task<DemoSession?> CreateAsync(CancellationToken cancellationToken)
    {
        await _allocation.WaitAsync(cancellationToken);
        try
        {
            Cleanup();
            if (_sessions.Values.Count(s => !s.Retired && s.ExpiresAt > _clock.GetUtcNow()) >= DemoOptions.MaximumSessions) return null;
            var id = Guid.NewGuid().ToString("N");
            var session = new DemoSession { Id = id, Directory = Path.Combine(_instanceDirectory, id),
                ExpiresAt = _clock.GetUtcNow().AddMinutes(DemoOptions.Minutes) };
            Directory.CreateDirectory(session.Directory);
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(session.Directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            _sessions[id] = session;
            return session;
        }
        finally { _allocation.Release(); }
    }

    public Lease? Acquire(string? token)
    {
        if (string.IsNullOrEmpty(token) || token.Length > 2048) return null;
        string id;
        try { id = _protector.Unprotect(token); }
        catch (CryptographicException) { return null; }
        if (!_sessions.TryGetValue(id, out var session)) return null;
        lock (session.Sync)
        {
            if (session.Retired || session.ExpiresAt <= _clock.GetUtcNow()) return null;
            session.Readers++;
            return new Lease(session);
        }
    }

    public void WriteCookie(HttpContext http, DemoSession session)
    {
        http.Response.Cookies.Append(DemoOptions.CookieName, _protector.Protect(session.Id), new CookieOptions
        {
            HttpOnly = true, Secure = http.Request.IsHttps, SameSite = SameSiteMode.Lax,
            IsEssential = true, Path = "/", Expires = session.ExpiresAt,
            MaxAge = session.ExpiresAt - _clock.GetUtcNow()
        });
    }

    public bool IsLive(DemoSession session)
    {
        lock (session.Sync) return !session.Retired && session.ExpiresAt > _clock.GetUtcNow();
    }

    public void Retire(DemoSession? session)
    {
        if (session is not null) lock (session.Sync) session.Retired = true;
    }

    public void Cleanup()
    {
        CleanupPreviousHosts();
        foreach (var session in _sessions.Values)
        {
            lock (session.Sync)
            {
                if ((!session.Retired && session.ExpiresAt > _clock.GetUtcNow()) || session.Readers != 0) continue;
                session.Retired = true;
                try
                {
                    // The path is allocated by this host; no client path is used for deletion.
                    if (Directory.Exists(session.Directory)) Directory.Delete(session.Directory, recursive: true);
                    _sessions.TryRemove(session.Id, out _);
                }
                catch (IOException exception) { _logger.LogWarning(exception, "An expired demo workspace will be cleaned up later."); }
                catch (UnauthorizedAccessException exception) { _logger.LogWarning(exception, "Demo workspace cleanup was denied."); }
            }
        }
    }

    private void CleanupPreviousHosts()
    {
        if (!Directory.Exists(_sandboxesDirectory)) return;
        foreach (var directory in Directory.EnumerateDirectories(_sandboxesDirectory))
        {
            if (directory == _instanceDirectory || !Guid.TryParseExact(Path.GetFileName(directory), "N", out _)) continue;
            if (Directory.GetLastWriteTimeUtc(directory) >= _clock.GetUtcNow().AddHours(-2).UtcDateTime) continue;
            try { Directory.Delete(directory, recursive: true); }
            catch (IOException exception) { _logger.LogWarning(exception, "An older demo host folder will be cleaned up later."); }
            catch (UnauthorizedAccessException exception) { _logger.LogWarning(exception, "Older demo host cleanup was denied."); }
        }
    }

    public sealed class Lease : IDisposable
    {
        public DemoSession Session { get; }
        public Lease(DemoSession session) { Session = session; }
        public void Dispose() { lock (Session.Sync) Session.Readers--; }
    }
}

public sealed class DemoCleanup : BackgroundService
{
    private readonly DemoSessions _sessions;
    public DemoCleanup(DemoSessions sessions) { _sessions = sessions; }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        try { while (await timer.WaitForNextTickAsync(stoppingToken)) _sessions.Cleanup(); }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
