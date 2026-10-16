namespace HospitalShifts.Domain.Enums;

/// <summary>نوع پرسنل؛ در تخصیص، «نقش» جایگاه با همین مقدار مشخص می‌شود.</summary>
public enum PersonKind
{
    Official,
    Soldier
}

public static class PersonKinds
{
    public static readonly PersonKind[] All = { PersonKind.Official, PersonKind.Soldier };

    /// <summary>کد ذخیره‌شده در پایگاه‌داده (سازگار با داده‌های قبلی).</summary>
    public static string ToCode(this PersonKind kind) => kind switch
    {
        PersonKind.Official => "official",
        PersonKind.Soldier => "soldier",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    /// <summary>برچسب فارسی برای نمایش.</summary>
    public static string ToLabel(this PersonKind kind) => kind switch
    {
        PersonKind.Official => "افسر",
        PersonKind.Soldier => "سرباز",
        _ => kind.ToString()
    };

    public static bool TryParse(string? code, out PersonKind kind)
    {
        switch (code)
        {
            case "official": kind = PersonKind.Official; return true;
            case "soldier": kind = PersonKind.Soldier; return true;
            default: kind = default; return false;
        }
    }
}
