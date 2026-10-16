using HospitalShifts.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalShifts.Web;

/// <summary>ثبت سرویس‌های لایه وب: MVC، احراز هویت کوکی و مجوز.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddWebServices(this IServiceCollection services)
    {
        services.AddControllersWithViews(options =>
            options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));

        services.AddAuthentication(AppAuth.CookieScheme).AddCookie(AppAuth.CookieScheme, options =>
        {
            options.LoginPath = "/Account/Login";
            options.Cookie.Name = AppAuth.CookieName;
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = false;
        });

        services.AddAuthorization(options =>
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build());

        return services;
    }
}
