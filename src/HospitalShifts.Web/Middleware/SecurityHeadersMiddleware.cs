namespace HospitalShifts.Web.Middleware;

/// <summary>افزودن هدرهای امنیتی به همه پاسخ‌ها.</summary>
public sealed class SecurityHeadersMiddleware
{
    readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["X-Frame-Options"] = "DENY";
        context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; style-src 'self'; script-src 'self'";
        await _next(context);
    }
}
