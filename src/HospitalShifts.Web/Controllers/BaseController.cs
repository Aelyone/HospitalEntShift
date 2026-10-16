using System.Security.Claims;
using HospitalShifts.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace HospitalShifts.Web.Controllers;

public abstract class BaseController : Controller
{
    protected string Actor => User.Identity?.Name ?? "?";
    protected int UserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    /// <summary>اجرای عمل و تبدیل خطاهای دامنه به پیام TempData؛ الگوی یکدست برای اکشن‌های فرم.</summary>
    protected async Task<IActionResult> TryAsync(Func<Task<string?>> action, Func<IActionResult> next)
    {
        try
        {
            var message = await action();
            if (message is not null) TempData["ok"] = message;
        }
        catch (DomainException e)
        {
            TempData["err"] = e.Message;
        }
        return next();
    }
}
