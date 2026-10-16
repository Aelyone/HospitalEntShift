using HospitalShifts.Domain.Enums;

namespace HospitalShifts.Application.Dtos;

/// <summary>یک ردیف از جدول سهم و تسویه.</summary>
public sealed record LedgerRowDto(
    int PersonnelId, string Name, PersonKind Type,
    int Share, int Opening, int Target, int Worked, int Forgiveness,
    int? Closing, string? Reason)
{
    /// <summary>مانده خروجی پیش‌بینی‌شده در ماه پیش‌نویس.</summary>
    public int Projected => Opening + Worked + Forgiveness - Share;
}

/// <summary>ردیف سابقه بخشودگی/اصلاح.</summary>
public sealed record AdjustmentDto(int Id, string Name, int Amount, string? Reason, string CreatedAt);

/// <summary>ردیف گزارش رویدادها.</summary>
public sealed record AuditEntryDto(string At, string? Actor, string Action, string? Detail);

/// <summary>نتیجه ذخیره فرد: شناسه و هشدارهای ناسازگاری تخصیص‌های موجود.</summary>
public sealed record SavePersonResult(int Id, IReadOnlyList<string> Warnings);
