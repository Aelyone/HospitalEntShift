using HospitalShifts.Domain.Enums;

namespace HospitalShifts.Domain.Entities;

/// <summary>سند قطعی سهم/کارکرد هر نفر در یک ماه (پس از تأیید ماه ثبت می‌شود).</summary>
public class MonthlyLedger
{
    public int MonthId { get; set; }
    public int PersonnelId { get; set; }
    public int Share { get; set; }
    public int Opening { get; set; }
    public int Target { get; set; }
    public int Worked { get; set; }
    public int Forgiveness { get; set; }
    public int? Closing { get; set; }
    public MonthStatus Status { get; set; } = MonthStatus.Approved;
    public string? ForgivenessReason { get; set; }
}

/// <summary>بخشودگی/اصلاح دستی واحد برای یک نفر در یک ماه پیش‌نویس.</summary>
public class LedgerAdjustment
{
    public int Id { get; set; }
    public int MonthId { get; set; }
    public int PersonnelId { get; set; }
    public int Amount { get; set; }
    public string Reason { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }

    public Personnel? Personnel { get; set; }
}

/// <summary>رویداد ممیزی (چه کسی، چه زمانی، چه کاری).</summary>
public class AuditEntry
{
    public int Id { get; set; }
    public DateTimeOffset At { get; set; }
    public string? Actor { get; set; }
    public string Action { get; set; } = "";
    public string? Detail { get; set; }
}
