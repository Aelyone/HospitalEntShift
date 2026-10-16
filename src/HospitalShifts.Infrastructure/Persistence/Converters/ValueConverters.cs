using System.Globalization;
using HospitalShifts.Domain.Enums;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace HospitalShifts.Infrastructure.Persistence.Converters;

/// <summary>نگاشت DateOnly به رشته ISO (yyyy-MM-dd) برای سازگاری با قالب ذخیره‌سازی موجود.</summary>
public sealed class DateOnlyToIsoStringConverter : ValueConverter<DateOnly, string>
{
    public DateOnlyToIsoStringConverter() : base(
        d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        s => Parse(s))
    { }

    static DateOnly Parse(string s) =>
        DateOnly.ParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None);
}

/// <summary>نگاشت DateTimeOffset به رشته ISO 8601 (قالب O) — سازگار با مقدارهای قبلی «Z»‌دار.</summary>
public sealed class DateTimeOffsetToIsoStringConverter : ValueConverter<DateTimeOffset, string>
{
    public DateTimeOffsetToIsoStringConverter() : base(
        v => v.ToString("O", CultureInfo.InvariantCulture),
        s => Parse(s))
    { }

    internal static DateTimeOffset Parse(string s) =>
        DateTimeOffset.TryParseExact(s, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var v)
            ? v
            : DateTimeOffset.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}

/// <inheritdoc cref="DateTimeOffsetToIsoStringConverter"/>
public sealed class NullableDateTimeOffsetToIsoStringConverter : ValueConverter<DateTimeOffset?, string?>
{
    public NullableDateTimeOffsetToIsoStringConverter() : base(
        v => v.HasValue ? v.Value.ToString("O", CultureInfo.InvariantCulture) : null,
        s => s == null ? (DateTimeOffset?)null : DateTimeOffsetToIsoStringConverter.Parse(s))
    { }
}

/// <summary>نگاشت enumها به کدهای رشته‌ای کوچک (official/morning/draft…) سازگار با داده‌های موجود.</summary>
public sealed class PersonKindToCodeConverter : ValueConverter<PersonKind, string>
{
    public PersonKindToCodeConverter() : base(k => k.ToCode(), s => Parse(s)) { }

    static PersonKind Parse(string code) =>
        PersonKinds.TryParse(code, out var kind) ? kind : throw new InvalidOperationException($"کد نامعتبر: {code}");
}

public sealed class SlotToCodeConverter : ValueConverter<Slot, string>
{
    public SlotToCodeConverter() : base(s => s.ToCode(), s => Parse(s)) { }

    static Slot Parse(string code) =>
        Domain.Enums.Slots.TryParse(code, out var slot) ? slot : throw new InvalidOperationException($"کد نامعتبر: {code}");
}

public sealed class MonthStatusToCodeConverter : ValueConverter<MonthStatus, string>
{
    public MonthStatusToCodeConverter() : base(s => s.ToCode(), s => Parse(s)) { }

    static MonthStatus Parse(string code) =>
        MonthStatuses.TryParse(code, out var status) ? status : throw new InvalidOperationException($"کد نامعتبر: {code}");
}
