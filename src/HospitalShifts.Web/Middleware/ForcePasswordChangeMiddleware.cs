using HospitalShifts.Web.Security;

namespace HospitalShifts.Web.Middleware;

/// <summary>تا تغییر رمز اولیه، هیچ صفحه‌ای جز بخش Account باز نمی‌شود.</summary>
public sealed class ForcePasswordChangeMiddleware
{
    readonly RequestDelegate _next;

    public ForcePasswordChangeMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";
        var mustChange = context.User.Identity?.IsAuthenticated == true
                         && context.User.HasClaim(AppAuth.MustChangeClaim, "1");
        if (mustChange && !path.StartsWith("/Account", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.Redirect("/Account/ChangePassword");
            return;
        }
        await _next(context);
    }
}
