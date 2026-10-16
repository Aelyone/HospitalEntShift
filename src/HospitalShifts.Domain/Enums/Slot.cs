namespace HospitalShifts.Domain.Enums;

/// <summary>نوبت کاری در یک روز.</summary>
public enum Slot
{
    Morning,
    Afternoon,
    Night
}

public static class Slots
{
    public static readonly Slot[] All = { Slot.Morning, Slot.Afternoon, Slot.Night };

    public static string ToCode(this Slot slot) => slot switch
    {
        Slot.Morning => "morning",
        Slot.Afternoon => "afternoon",
        Slot.Night => "night",
        _ => throw new ArgumentOutOfRangeException(nameof(slot))
    };

    public static string ToLabel(this Slot slot) => slot switch
    {
        Slot.Morning => "صبح",
        Slot.Afternoon => "عصر",
        Slot.Night => "شب",
        _ => slot.ToString()
    };

    public static bool TryParse(string? code, out Slot slot)
    {
        switch (code)
        {
            case "morning": slot = Slot.Morning; return true;
            case "afternoon": slot = Slot.Afternoon; return true;
            case "night": slot = Slot.Night; return true;
            default: slot = default; return false;
        }
    }

    /// <summary>اندیس نوبت در روز (برای مرتب‌سازی زنجیره‌ها).</summary>
    public static int Index(this Slot slot) => slot switch
    {
        Slot.Morning => 0,
        Slot.Afternoon => 1,
        Slot.Night => 2,
        _ => throw new ArgumentOutOfRangeException(nameof(slot))
    };

    /// <summary>واحد شیفت هر نوبت: صبح و عصر ۱ واحد، شب ۲ واحد.</summary>
    public static int Units(this Slot slot) => slot == Slot.Night ? 2 : 1;

    /// <summary>دقیقه شروع/پایان از ابتدای روز تقویمی؛ نوبت شب تا ۰۷:۰۰ روز بعد ادامه دارد (۱۸۶۰).</summary>
    public static (int Start, int End) Minutes(this Slot slot) => slot switch
    {
        Slot.Morning => (450, 840),
        Slot.Afternoon => (840, 1140),
        Slot.Night => (1140, 1860),
        _ => throw new ArgumentOutOfRangeException(nameof(slot))
    };
}
