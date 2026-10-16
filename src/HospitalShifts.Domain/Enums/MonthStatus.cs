namespace HospitalShifts.Domain.Enums;

/// <summary>وضعیت چرخه عمر یک ماه برنامه‌ریزی.</summary>
public enum MonthStatus
{
    Draft,
    Approved,
    Settled
}

public static class MonthStatuses
{
    public static string ToCode(this MonthStatus status) => status switch
    {
        MonthStatus.Draft => "draft",
        MonthStatus.Approved => "approved",
        MonthStatus.Settled => "settled",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };

    public static string ToLabel(this MonthStatus status) => status switch
    {
        MonthStatus.Draft => "پیش‌نویس",
        MonthStatus.Approved => "تأییدشده",
        MonthStatus.Settled => "تسویه‌شده",
        _ => status.ToString()
    };

    public static bool TryParse(string? code, out MonthStatus status)
    {
        switch (code)
        {
            case "draft": status = MonthStatus.Draft; return true;
            case "approved": status = MonthStatus.Approved; return true;
            case "settled": status = MonthStatus.Settled; return true;
            default: status = default; return false;
        }
    }
}
