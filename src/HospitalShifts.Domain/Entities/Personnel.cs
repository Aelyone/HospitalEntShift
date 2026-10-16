using HospitalShifts.Domain.Enums;

namespace HospitalShifts.Domain.Entities;

public class Personnel
{
    public int Id { get; set; }
    public PersonKind Type { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? Department { get; set; }

    /// <summary>سهم پایه ماهانه (واحد شیفت).</summary>
    public int BaseShare { get; set; }

    /// <summary>مانده اولیه هنگام شروع کار با سامانه (مثبت = طلبکار).</summary>
    public int InitialBalance { get; set; }

    public bool Active { get; set; } = true;
    public bool CanLeader { get; set; }
    public int LeaderPriority { get; set; }
    public string? Level { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public List<AllowedShift> Allowed { get; set; } = new();
    public List<RestRule> Rest { get; set; } = new();

    public string FullName => $"{FirstName} {LastName}";
}

/// <summary>شیفتی که برای یک فرد مجاز است.</summary>
public class AllowedShift
{
    public int PersonnelId { get; set; }
    public string ShiftCode { get; set; } = "";
}

/// <summary>حداقل ساعت استراحت اختصاصی فرد پس از یک نوع شیفت.</summary>
public class RestRule
{
    public int PersonnelId { get; set; }
    public string ShiftCode { get; set; } = "";
    public int RestHours { get; set; }
}
