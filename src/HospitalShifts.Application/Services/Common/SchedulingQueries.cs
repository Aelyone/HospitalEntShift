using HospitalShifts.Application.Abstractions;
using HospitalShifts.Application.Dtos;
using HospitalShifts.Domain.Calendar;
using HospitalShifts.Domain.Common;
using HospitalShifts.Domain.Entities;
using HospitalShifts.Domain.Enums;
using HospitalShifts.Domain.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace HospitalShifts.Application.Services.Common;

/// <summary>کلیدهای شناخته‌شده جدول تنظیمات.</summary>
public static class SettingKeys
{
    public const string DefaultRestHours = "default_rest_hours";
}

/// <summary>
/// پرس‌وجوهای مشترک برنامه‌ریزی که بین چند سرویس استفاده می‌شوند
/// (پیش‌تر به‌صورت متدهای static با پارامتر DbContext در سرویس خدا پخش شده بودند).
/// </summary>
internal static class SchedulingQueries
{
    /// <summary>تصویر لحظه‌ای از افراد، محل‌ها و تعطیلی‌ها برای اجرای قواعد دامنه.</summary>
    public static async Task<SchedulingContext> LoadContextAsync(IApplicationDbContext db, CancellationToken ct)
    {
        var ctx = new SchedulingContext();

        var people = await db.Personnel.AsNoTracking()
            .Include(x => x.Allowed)
            .Include(x => x.Rest)
            .ToListAsync(ct);
        foreach (var r in people)
        {
            var p = new PersonProfile
            {
                Id = r.Id, Name = r.FullName, Type = r.Type,
                Active = r.Active, CanLeader = r.CanLeader, Priority = r.LeaderPriority
            };
            foreach (var a in r.Allowed) p.Allowed.Add(a.ShiftCode);
            foreach (var x in r.Rest) p.Rest[x.ShiftCode] = x.RestHours;
            ctx.People[r.Id] = p;
        }

        var locations = await db.Locations.AsNoTracking()
            .Include(x => x.Requirements)
            .OrderBy(x => x.Sort).ThenBy(x => x.Id)
            .ToListAsync(ct);
        foreach (var r in locations)
        {
            var l = new LocationProfile { Id = r.Id, Name = r.Name, Active = r.Active };
            foreach (var q in r.Requirements) l.Requirements[q.Slot] = (q.NeedsOfficial, q.NeedsSoldier);
            ctx.Locations[r.Id] = l;
        }

        var closures = await db.LocationClosures.AsNoTracking().ToListAsync(ct);
        foreach (var c in closures) ctx.Closures.Add($"{c.LocationId}|{Jalali.Iso(c.Date)}");

        ctx.DefaultRestHours = await GetDefaultRestHoursAsync(db, ct);
        return ctx;
    }

    public static async Task<int> GetDefaultRestHoursAsync(IApplicationDbContext db, CancellationToken ct)
    {
        var value = await db.Settings.AsNoTracking()
            .Where(s => s.Key == SettingKeys.DefaultRestHours)
            .Select(s => s.Value)
            .FirstOrDefaultAsync(ct);
        return int.TryParse(value, out var hours) ? hours : SchedulingRules.DefaultRestHours;
    }

    static AssignmentEntry ToEntry(Assignment r) => new()
    {
        Id = r.Id, Date = r.Date, Slot = r.Slot, LocationId = r.LocationId,
        Role = r.Role, PersonnelId = r.PersonnelId, IsLeader = r.IsShiftLeader
    };

    /// <summary>تخصیص‌های بازه [from,to] به‌صورت خواندنی.</summary>
    public static async Task<List<AssignmentEntry>> LoadAssignmentsAsync(IApplicationDbContext db, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var rows = await db.Assignments.AsNoTracking()
            .Where(a => a.Date >= from && a.Date <= to)
            .ToListAsync(ct);
        return rows.Select(ToEntry).ToList();
    }

    public static MonthDto ToDto(ScheduleMonth row) => new(row.Id, row.Jy, row.Jm, row.StartDate, row.EndDate, row.Status);

    /// <summary>یافتن (و در صورت نبود، ساخت) ماه شمسی؛ ماه جدید پیش‌نویس است.</summary>
    public static async Task<MonthDto> EnsureMonthAsync(IApplicationDbContext db, int jy, int jm, CancellationToken ct)
    {
        if (jm is < 1 or > 12 || jy is < 1300 or > 1500) throw new DomainException("ماه نامعتبر است");
        var row = await db.ScheduleMonths.FirstOrDefaultAsync(m => m.Jy == jy && m.Jm == jm, ct);
        if (row is null)
        {
            var (start, end) = Jalali.MonthRange(jy, jm);
            row = new ScheduleMonth { Jy = jy, Jm = jm, StartDate = start, EndDate = end, Status = MonthStatus.Draft };
            db.ScheduleMonths.Add(row);
            await db.SaveChangesAsync(ct);
        }
        return ToDto(row);
    }

    /// <summary>ماه شامل یک تاریخ میلادی (در صورت ثبت‌شده‌بودن).</summary>
    public static async Task<MonthDto?> FindMonthContainingAsync(IApplicationDbContext db, DateOnly date, CancellationToken ct)
    {
        var row = await db.ScheduleMonths.AsNoTracking()
            .FirstOrDefaultAsync(m => m.StartDate <= date && m.EndDate >= date, ct);
        return row is null ? null : ToDto(row);
    }

    /// <summary>تاریخ باید در ماه پیش‌نویس (یا ماه ثبت‌نشده) باشد؛ وگرنه ویرایش ممنوع است.</summary>
    public static async Task RequireDraftMonthAsync(IApplicationDbContext db, DateOnly date, CancellationToken ct)
    {
        var month = await FindMonthContainingAsync(db, date, ct);
        if (month is not null && month.Status != MonthStatus.Draft)
            throw new DomainException($"ماه {month.Title} {month.StatusLabel} است و قابل ویرایش نیست؛ ابتدا آن را بازگشایی کنید");
    }

    /// <summary>خطاهای سخت بازه با بافر ±۳ روز تا زنجیره‌های مرزی درست ارزیابی شوند.</summary>
    public static async Task<List<RuleViolation>> ComputeViolationsAsync(IApplicationDbContext db, SchedulingContext ctx, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var rows = await LoadAssignmentsAsync(db, from.AddDays(-3), to.AddDays(3), ct);
        return SchedulingRules.ValidateRows(rows, ctx)
            .Where(v => v.Date >= from && v.Date <= to)
            .Distinct()
            .ToList();
    }

    /// <summary>مانده ورودی هر نفر = مانده خروجی ماه قبل (اگر ثبت شده) وگرنه مانده اولیه.</summary>
    public static async Task<Dictionary<int, int>> ComputeOpeningBalancesAsync(IApplicationDbContext db, MonthDto month, CancellationToken ct)
    {
        var result = await db.Personnel.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.InitialBalance, ct);
        var prev = await db.ScheduleMonths.AsNoTracking()
            .Where(x => x.EndDate < month.Start)
            .OrderByDescending(x => x.EndDate)
            .FirstOrDefaultAsync(ct);
        if (prev is null) return result;
        var ledgers = await db.MonthlyLedgers.AsNoTracking().Where(x => x.MonthId == prev.Id).ToListAsync(ct);
        foreach (var r in ledgers)
            result[r.PersonnelId] = r.Closing ?? (r.Opening + r.Worked + r.Forgiveness - r.Share);
        return result;
    }
}
