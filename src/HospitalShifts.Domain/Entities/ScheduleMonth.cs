using HospitalShifts.Domain.Enums;

namespace HospitalShifts.Domain.Entities;

/// <summary>یک ماه شمسی برنامه‌ریزی با چرخه عمر پیش‌نویس ← تأییدشده ← تسویه‌شده.</summary>
public class ScheduleMonth
{
    public int Id { get; set; }
    public int Jy { get; set; }
    public int Jm { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public MonthStatus Status { get; set; } = MonthStatus.Draft;
    public DateTimeOffset? SettledAt { get; set; }
}

/// <summary>تخصیص یک نفر به یک جایگاه در یک نوبت از یک روز.</summary>
public class Assignment
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public Slot Slot { get; set; }
    public int LocationId { get; set; }
    public PersonKind Role { get; set; }
    public int PersonnelId { get; set; }
    public bool IsShiftLeader { get; set; }
}
