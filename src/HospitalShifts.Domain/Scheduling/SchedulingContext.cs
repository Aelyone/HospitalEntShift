using HospitalShifts.Domain.Calendar;
using HospitalShifts.Domain.Enums;

namespace HospitalShifts.Domain.Scheduling;

/// <summary>کلید یکتای «سلول» جدول برنامه برای نمایش و جست‌وجو.</summary>
public static class CellKeys
{
    public static string Of(DateOnly date, Slot slot) => $"{Jalali.Iso(date)}|{slot.ToCode()}";

    public static string Of(DateOnly date, Slot slot, int locationId, PersonKind role) =>
        $"{Jalali.Iso(date)}|{slot.ToCode()}|{locationId}|{role.ToCode()}";
}

/// <summary>مدل خواندنیِ تخصیص که قواعد دامنه روی آن کار می‌کنند.</summary>
public sealed class AssignmentEntry
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public Slot Slot { get; set; }
    public int LocationId { get; set; }
    public PersonKind Role { get; set; }
    public int PersonnelId { get; set; }
    public bool IsLeader { get; set; }

    public string CellKey => CellKeys.Of(Date, Slot);
}

/// <summary>مدل خواندنیِ پرسنل برای اعتبارسنجی.</summary>
public sealed class PersonProfile
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public PersonKind Type { get; set; }
    public bool Active { get; set; }
    public bool CanLeader { get; set; }
    public int Priority { get; set; }

    /// <summary>کدهای شیفت مجاز برای این فرد.</summary>
    public HashSet<string> Allowed { get; set; } = new();

    /// <summary>کد شیفت ← حداقل ساعت استراحت اختصاصی.</summary>
    public Dictionary<string, int> Rest { get; set; } = new();
}

/// <summary>مدل خواندنیِ محل برای اعتبارسنجی و چیدن خودکار.</summary>
public sealed class LocationProfile
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public bool Active { get; set; }

    /// <summary>نوبت ← (تعداد افسر، تعداد سرباز) موردنیاز.</summary>
    public Dictionary<Slot, (int Official, int Soldier)> Requirements { get; set; } = new();

    public int Need(Slot slot, PersonKind role) =>
        Requirements.TryGetValue(slot, out var r) ? (role == PersonKind.Official ? r.Official : r.Soldier) : 0;
}

/// <summary>تصویر لحظه‌ای از داده‌های مرجع که قواعد به آن نیاز دارند.</summary>
public sealed class SchedulingContext
{
    public Dictionary<int, PersonProfile> People { get; set; } = new();
    public Dictionary<int, LocationProfile> Locations { get; set; } = new();

    /// <summary>تعطیلی‌ها به شکل «locationId|yyyy-MM-dd».</summary>
    public HashSet<string> Closures { get; set; } = new();

    public int DefaultRestHours { get; set; } = SchedulingRules.DefaultRestHours;

    public bool IsClosed(int locationId, DateOnly date) => Closures.Contains($"{locationId}|{Jalali.Iso(date)}");
}
