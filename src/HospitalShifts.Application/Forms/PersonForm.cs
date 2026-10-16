namespace HospitalShifts.Application.Forms;

/// <summary>فرم ورودی افزودن/ویرایش فرد (مرز UI ← Application).</summary>
public sealed class PersonForm
{
    public int Id { get; set; }
    public string Type { get; set; } = "official";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? Department { get; set; }
    public string? Level { get; set; }
    public int BaseShare { get; set; }
    public int InitialBalance { get; set; }
    public bool CanLeader { get; set; }
    public int LeaderPriority { get; set; }
    public bool Active { get; set; } = true;
    public List<string> Allowed { get; set; } = new();
    public Dictionary<string, int?> Rest { get; set; } = new();
}
