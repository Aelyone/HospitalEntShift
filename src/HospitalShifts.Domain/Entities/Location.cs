using HospitalShifts.Domain.Enums;

namespace HospitalShifts.Domain.Entities;

/// <summary>محل نگهبانی.</summary>
public class Location
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public bool Active { get; set; } = true;
    public int Sort { get; set; }

    public List<LocationRequirement> Requirements { get; set; } = new();
    public List<LocationClosure> Closures { get; set; } = new();
}

/// <summary>نیازمندی یک محل در یک نوبت (تعداد افسر و سرباز).</summary>
public class LocationRequirement
{
    public int LocationId { get; set; }
    public Slot Slot { get; set; }
    public int NeedsOfficial { get; set; }
    public int NeedsSoldier { get; set; }
}

/// <summary>تعطیلی یک محل در یک روز مشخص.</summary>
public class LocationClosure
{
    public int LocationId { get; set; }
    public DateOnly Date { get; set; }
}
