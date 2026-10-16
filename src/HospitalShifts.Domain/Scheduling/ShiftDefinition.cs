using HospitalShifts.Domain.Enums;

namespace HospitalShifts.Domain.Scheduling;

/// <summary>تعریف ثابت یک نوع شیفت: نوبت‌های تشکیل‌دهنده، واحد و امکان چیدن خودکار.</summary>
public sealed record ShiftDefinition(string Code, string Name, Slot[] Segments, int Units, bool Auto);

/// <summary>کاتالوگ ثابت انواع شیفت؛ در اولین اجرا در جدول shift_types بذر می‌شود.</summary>
public static class ShiftDefinitions
{
    public static readonly ShiftDefinition[] All =
    {
        new("morning", "صبح", new[] { Slot.Morning }, 1, true),
        new("afternoon", "عصر", new[] { Slot.Afternoon }, 1, true),
        new("night", "شب", new[] { Slot.Night }, 2, true),
        new("morning_afternoon", "صبح+عصر", new[] { Slot.Morning, Slot.Afternoon }, 2, true),
        new("afternoon_night", "عصر+شب", new[] { Slot.Afternoon, Slot.Night }, 3, true),
        new("night_morning", "شب+صبح (روز بعد)", new[] { Slot.Night, Slot.Morning }, 3, false),
        new("afternoon_night_morning", "عصر+شب+صبح (روز بعد)", new[] { Slot.Afternoon, Slot.Night, Slot.Morning }, 4, false),
    };

    public static readonly IReadOnlyDictionary<string, ShiftDefinition> ByCode = All.ToDictionary(t => t.Code);

    static readonly IReadOnlyDictionary<string, string> CodeBySegments =
        All.ToDictionary(t => SegmentKey(t.Segments), t => t.Code);

    static string SegmentKey(IEnumerable<Slot> segments) => string.Join(',', segments.Select(s => s.ToCode()));

    /// <summary>کد شیفت معادل یک دنباله از نوبت‌ها؛ اگر ترکیب معتبر نباشد null.</summary>
    public static string? CodeFor(IEnumerable<Slot> segments) =>
        CodeBySegments.TryGetValue(SegmentKey(segments), out var code) ? code : null;

    public static bool Exists(string code) => ByCode.ContainsKey(code);
}
