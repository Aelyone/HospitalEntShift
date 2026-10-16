namespace HospitalShifts.Domain.Entities;

/// <summary>کاربر مدیر سیستم.</summary>
public class Admin
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";

    /// <summary>الزام به تغییر رمز پیش از ادامه کار.</summary>
    public bool MustChange { get; set; }

    /// <summary>تعداد تلاش‌های ناموفق پیاپی ورود.</summary>
    public int FailedCount { get; set; }

    /// <summary>پایان قفل موقت حساب (ثانیه Unix؛ صفر یعنی باز).</summary>
    public long LockedUntil { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>تنظیمات کلید-مقدار سیستم.</summary>
public class Setting
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}
