using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;

namespace CMCS_POE.Demo;

public sealed class DemoMiddleware
{
    private readonly RequestDelegate _next;
    public DemoMiddleware(RequestDelegate next) { _next = next; }
    public async Task InvokeAsync(HttpContext http, DemoSessions sessions, DemoRequestContext workspace)
    {
        using var lease = sessions.Acquire(http.Request.Cookies[DemoOptions.CookieName]);
        workspace.Session = lease?.Session;
        var gate = workspace.Session?.Mutations;
        var write = !HttpMethods.IsGet(http.Request.Method) && !HttpMethods.IsHead(http.Request.Method)
            && !http.Request.Path.StartsWithSegments("/claimHub");
        if (write && gate is not null) await gate.WaitAsync(http.RequestAborted);
        try
        {
            if (workspace.Session is null && http.Request.Cookies.ContainsKey(DemoOptions.CookieName))
            {
                http.Response.Cookies.Delete(DemoOptions.CookieName);
                await http.SignOutAsync(IdentityConstants.ApplicationScheme);
            }
            if (workspace.Session is not null && !sessions.IsLive(workspace.Session))
            {
                http.Response.StatusCode = 409;
                await http.Response.WriteAsync("This demo has expired or been reset. Open /Demo to start again.");
                return;
            }
            await _next(http);
        }
        finally { if (write && gate is not null) gate.Release(); }
    }
}
