using System.Security.Claims;
using HospitalShifts.Application.Abstractions;
using HospitalShifts.Application.Dtos;
using HospitalShifts.Domain.Common;
using HospitalShifts.Web.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalShifts.Web.Controllers;

public sealed class AccountController : BaseController
{
    readonly IAuthService _auth;

    public AccountController(IAuthService auth) => _auth = auth;

    async Task SignInAsync(AdminIdentity identity)
    {
        var claims = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, identity.Username),
            new Claim(ClaimTypes.NameIdentifier, identity.Id.ToString()),
            new Claim(ClaimTypes.Role, AppAuth.AdminRole),
            new Claim(AppAuth.MustChangeClaim, identity.MustChangePassword ? "1" : "0"),
        }, AppAuth.CookieScheme);
        await HttpContext.SignInAsync(AppAuth.CookieScheme, new ClaimsPrincipal(claims));
    }

    [AllowAnonymous, HttpGet]
    public IActionResult Login() => View();

    [AllowAnonymous, HttpPost]
    public async Task<IActionResult> Login(string username, string password, CancellationToken cancellationToken)
    {
        try
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "?";
            var identity = await _auth.LoginAsync(username, password, ip, cancellationToken);
            await SignInAsync(identity);
            return identity.MustChangePassword ? RedirectToAction(nameof(ChangePassword)) : Redirect("/");
        }
        catch (DomainException e)
        {
            ViewBag.Err = e.Message;
            return View();
        }
    }

    [HttpGet]
    public IActionResult ChangePassword() => View();

    [HttpPost]
    public async Task<IActionResult> ChangePassword(string current, string next, string repeat, CancellationToken cancellationToken)
    {
        if (next != repeat)
        {
            ViewBag.Err = "تکرار رمز با رمز جدید یکسان نیست";
            return View();
        }
        try
        {
            await SignInAsync(await _auth.ChangePasswordAsync(UserId, current, next, cancellationToken));
            TempData["ok"] = "رمز تغییر کرد";
            return Redirect("/");
        }
        catch (DomainException e)
        {
            ViewBag.Err = e.Message;
            return View();
        }
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(AppAuth.CookieScheme);
        return RedirectToAction(nameof(Login));
    }
}
