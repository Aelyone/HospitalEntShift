using System.Globalization;

namespace HospitalShifts.Domain.Calendar;

/// <summary>
/// ابزار تبدیل تاریخ شمسی/میلادی.
/// تاریخ‌ها در دامنه <see cref="DateOnly"/> هستند و در پایگاه‌داده به‌صورت رشته ISO میلادی (yyyy-MM-dd) ذخیره می‌شوند.
/// </summary>
public static class Jalali
{
    static readonly PersianCalendar Pc = new();

    public static readonly string[] MonthNames =
        { "فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور", "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند" };

    static readonly string[] DayNames =
        { "یکشنبه", "دوشنبه", "سه‌شنبه", "چهارشنبه", "پنجشنبه", "جمعه", "شنبه" };

    /// <summary>قالب ذخیره‌سازی در پایگاه‌داده: yyyy-MM-dd</summary>
    public static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static DateOnly ParseIso(string iso) => DateOnly.ParseExact(iso, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static bool TryParseIso(string? iso, out DateOnly date) =>
        DateOnly.TryParseExact(iso, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    /// <summary>تجزیه تاریخ میلادی به اجزای شمسی.</summary>
    public static (int Year, int Month, int Day) ToJalali(this DateOnly date)
    {
        var t = date.ToDateTime(TimeOnly.MinValue);
        return (Pc.GetYear(t), Pc.GetMonth(t), Pc.GetDayOfMonth(t));
    }

    /// <summary>نمایش کوتاه شمسی مانند 1404/01/15</summary>
    public static string ToJalaliLabel(this DateOnly date)
    {
        var (y, m, d) = date.ToJalali();
        return $"{y}/{m:00}/{d:00}";
    }

    /// <summary>نمایش کامل مانند «۱۵ فروردین ۱۴۰۴، شنبه» (ارقام لاتین برای خوانایی در جدول).</summary>
    public static string ToLongLabel(this DateOnly date)
    {
        var (y, m, d) = date.ToJalali();
        return $"{d} {MonthNames[m - 1]} {y}، {DayNames[(int)date.DayOfWeek]}";
    }

    public static bool IsFriday(this DateOnly date) => date.DayOfWeek == DayOfWeek.Friday;

    /// <summary>ساخت تاریخ میلادی از اجزای شمسی؛ در صورت نامعتبر بودن null.</summary>
    public static DateOnly? FromJalali(int year, int month, int day)
    {
        try { return DateOnly.FromDateTime(Pc.ToDateTime(year, month, day, 0, 0, 0, 0)); }
        catch { return null; }
    }

    /// <summary>ورودی کاربر مانند 1404/01/15 یا با ارقام فارسی ۱۴۰۴/۰۱/۱۵</summary>
    public static DateOnly? ParseJalaliText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var latin = new string(text
            .Select(c => c >= '۰' && c <= '۹' ? (char)('0' + c - '۰') : c)
            .ToArray());
        var parts = latin.Split('/', '-', '.');
        if (parts.Length != 3
            || !int.TryParse(parts[0], out var y)
            || !int.TryParse(parts[1], out var m)
            || !int.TryParse(parts[2], out var d))
            return null;
        return FromJalali(y, m, d);
    }

    /// <summary>بازه میلادیِ یک ماه شمسی (روز اول تا روز آخر).</summary>
    public static (DateOnly Start, DateOnly End) MonthRange(int year, int month)
    {
        var first = DateOnly.FromDateTime(Pc.ToDateTime(year, month, 1, 0, 0, 0, 0));
        return (first, first.AddDays(Pc.GetDaysInMonth(year, month) - 1));
    }

    /// <summary>سال و ماه شمسیِ امروز (به‌وقت محلی سرور) برای ناوبری پیش‌فرض صفحات.</summary>
    public static (int Year, int Month) CurrentYearMonth(TimeProvider time)
    {
        var today = DateOnly.FromDateTime(time.GetLocalNow().Date);
        var (y, m, _) = today.ToJalali();
        return (y, m);
    }
}
