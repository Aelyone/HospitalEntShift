using HospitalShifts.Domain.Enums;

namespace HospitalShifts.Domain.Entities;

/// <summary>نوع شیفت (ترکیب مجاز نوبت‌ها) — از کاتالوگ ثابت <c>ShiftDefinitions</c> بذر می‌شود.</summary>
public class ShiftType
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public int Units { get; set; }
    public bool AutoGenerate { get; set; }
    public List<ShiftSegment> Segments { get; set; } = new();
}

/// <summary>یک بخش (نوبت) از یک شیفت ترکیبی، به ترتیب Position.</summary>
public class ShiftSegment
{
    public string ShiftCode { get; set; } = "";
    public int Position { get; set; }
    public Slot Slot { get; set; }
}
