namespace CMCS_POE.Demo;

public sealed record DemoOptions(bool Enabled, string DataDirectory)
{
    public const int Minutes = 45;
    public const int MaximumSessions = 20;
    public const int MaximumClaims = 40;
    public const int MaximumLecturers = 12;
    public const long MaximumAttachmentBytes = 10 * 1024 * 1024;
    public const string WorkspaceClaim = "cmcs:demo-workspace";
    public const string CookieName = "cmcs-demo.workspace";
}

public sealed class DemoRequestContext
{
    public DemoSession? Session { get; set; }
    public string Directory => Session?.Directory ?? throw new InvalidOperationException("Open a demo workspace first.");
}

public sealed class DemoSession
{
    public required string Id { get; init; }
    public required string Directory { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
    public SemaphoreSlim Mutations { get; } = new(1, 1);
    internal object Sync { get; } = new();
    internal int Readers;
    internal bool Retired;
}
