namespace HospitalShifts.Application.Forms;

/// <summary>فرم ورودی افزودن/ویرایش محل نگهبانی.</summary>
public sealed class LocationForm
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int Sort { get; set; }
    public bool Active { get; set; } = true;
    public List<string> NeedOfficial { get; set; } = new();
    public List<string> NeedSoldier { get; set; } = new();
}
