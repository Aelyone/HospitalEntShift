using HospitalShifts.Domain.Enums;

namespace HospitalShifts.Domain.Scheduling;

/// <summary>نقض یک قاعده؛ پیام آن به فارسی و قابل نمایش به کاربر است.</summary>
public sealed record RuleViolation(string Message, DateOnly Date, Slot Slot, int PersonnelId = 0, int LocationId = 0)
{
    public string CellKey => CellKeys.Of(Date, Slot);
}

/// <summary>کمبود نیرو در یک جایگاه (هشدار نرم؛ مانع تأیید ماه نیست).</summary>
public sealed record CoverageGap(DateOnly Date, Slot Slot, int LocationId, string LocationName, PersonKind Role, int Needed, int Filled)
{
    public string CellKey => CellKeys.Of(Date, Slot);
}

/// <summary>یک نوبت از یک روز (برای گزارش نوبت‌های بدون سرشیفت).</summary>
public sealed record SlotKey(DateOnly Date, Slot Slot)
{
    public string CellKey => CellKeys.Of(Date, Slot);
}
