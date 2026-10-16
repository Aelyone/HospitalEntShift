namespace HospitalShifts.Web.Security;

/// <summary>ثابت‌های احراز هویت کوکی.</summary>
public static class AppAuth
{
    public const string CookieScheme = "Cookies";
    public const string CookieName = "hs.auth";

    /// <summary>کلیم «باید رمز را عوض کند» (۱ = بله).</summary>
    public const string MustChangeClaim = "mc";
    public const string AdminRole = "admin";
}
