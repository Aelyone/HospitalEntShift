using HospitalShifts.Application.Abstractions;
using HospitalShifts.Application.Dtos;
using HospitalShifts.Application.Services.Common;
using HospitalShifts.Domain.Calendar;
using HospitalShifts.Domain.Common;
using HospitalShifts.Domain.Entities;
using HospitalShifts.Domain.Enums;
using HospitalShifts.Domain.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace HospitalShifts.Application.Services;

/// <summary>منطق برنامه ماهانه: تابلو، چیدن خودکار، پاک‌سازی و ویرایش دستی نوبت.</summary>
public sealed class ScheduleService : IScheduleService
{
    readonly IApplicationDbContext _db;
    readonly IAuditService _audit;

    public ScheduleService(IApplicationDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    // ---------- خواندن‌ها ----------

    public Task<MonthDto> GetMonthAsync(int jy, int jm, CancellationToken cancellationToken = default) =>
        _db.ExecuteInTransactionAsync(ct => SchedulingQueries.EnsureMonthAsync(_db, jy, jm, ct), cancellationToken);

    public Task<SchedulingContext> GetContextAsync(CancellationToken cancellationToken = default) =>
        SchedulingQueries.LoadContextAsync(_db, cancellationToken);

    public async Task<List<AssignmentEntry>> GetSlotAssignmentsAsync(DateOnly date, Slot slot, CancellationToken cancellationToken = default)
    {
        var rows = await _db.Assignments.AsNoTracking()
            .Where(x => x.Date == date && x.Slot == slot)
            .ToListAsync(cancellationToken);
        return rows.Select(r => new AssignmentEntry
        {
            Id = r.Id, Date = r.Date, Slot = r.Slot, LocationId = r.LocationId,
            Role = r.Role, PersonnelId = r.PersonnelId, IsLeader = r.IsShiftLeader
        }).ToList();
    }

    public Task<ScheduleBoardDto> GetBoardAsync(int jy, int jm, CancellationToken cancellationToken = default) =>
        _db.ExecuteInTransactionAsync(async ct =>
        {
            var month = await SchedulingQueries.EnsureMonthAsync(_db, jy, jm, ct);
            var ctx = await SchedulingQueries.LoadContextAsync(_db, ct);
            var rows = await SchedulingQueries.LoadAssignmentsAsync(_db, month.Start, month.End, ct);
            var (gaps, leaderMissing) = SchedulingRules.Coverage(rows, month.Dates(), ctx);
            var violations = await SchedulingQueries.ComputeViolationsAsync(_db, ctx, month.Start, month.End, ct);
            return new ScheduleBoardDto(month, ctx, rows, violations, gaps, leaderMissing);
        }, cancellationToken);

    // ---------- چیدن خودکار ----------

    static string BusyKey(int personId, DateOnly date, Slot slot) => $"{personId}|{Jalali.Iso(date)}|{slot.ToCode()}";

    static (DateOnly Date, Slot Slot) PreviousCell(DateOnly date, Slot slot) => slot switch
    {
        Slot.Morning => (date.AddDays(-1), Slot.Night),
        Slot.Afternoon => (date, Slot.Morning),
        _ => (date, Slot.Afternoon)
    };

    static (DateOnly Date, Slot Slot) NextCell(DateOnly date, Slot slot) => slot switch
    {
        Slot.Morning => (date, Slot.Afternoon),
        Slot.Afternoon => (date, Slot.Night),
        _ => (date.AddDays(1), Slot.Morning)
    };

    /// <summary>اولویت سرشیفت: عدد اولویت بزرگ‌تر، سپس تعداد سرشیفتی کمتر، سپس شناسه.</summary>
    static AssignmentEntry? PickLeader(IEnumerable<AssignmentEntry> slotRows, SchedulingContext ctx, Dictionary<int, int> leaderCount) =>
        slotRows
            .Where(r => r.Role == PersonKind.Official
                        && ctx.People.TryGetValue(r.PersonnelId, out var p) && p.Active && p.CanLeader && p.Type == PersonKind.Official)
            .OrderByDescending(r => ctx.People[r.PersonnelId].Priority)
            .ThenBy(r => leaderCount.GetValueOrDefault(r.PersonnelId))
            .ThenBy(r => r.PersonnelId)
            .FirstOrDefault();

    public Task<GenerateReportDto> GenerateAsync(int jy, int jm, string actor, CancellationToken cancellationToken = default) =>
        _db.ExecuteInTransactionAsync(async ct =>
        {
            var month = await SchedulingQueries.EnsureMonthAsync(_db, jy, jm, ct);
            if (month.Status != MonthStatus.Draft) throw new DomainException("فقط ماه پیش‌نویس قابل تولید است");

            var ctx = await SchedulingQueries.LoadContextAsync(_db, ct);
            // بافر ±۳ روز بارگذاری می‌شود ولی هرگز حذف/تغییر نمی‌شود
            var rows = await SchedulingQueries.LoadAssignmentsAsync(_db, month.Start.AddDays(-3), month.End.AddDays(3), ct);
            var busy = rows.Select(r => BusyKey(r.PersonnelId, r.Date, r.Slot)).ToHashSet();
            var filled = rows.Select(r => CellKeys.Of(r.Date, r.Slot, r.LocationId, r.Role)).ToHashSet();
            var openings = await SchedulingQueries.ComputeOpeningBalancesAsync(_db, month, ct);
            var shares = await _db.Personnel.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.BaseShare, ct);
            var units = new Dictionary<int, int>();
            foreach (var r in rows.Where(r => month.Contains(r.Date)))
                units[r.PersonnelId] = units.GetValueOrDefault(r.PersonnelId) + r.Slot.Units();

            int Deficit(int personId) =>
                Math.Max(0, shares.GetValueOrDefault(personId) - openings.GetValueOrDefault(personId))
                - units.GetValueOrDefault(personId);

            var added = 0;
            var inserted = new List<(AssignmentEntry Entry, Assignment Entity)>();
            foreach (var date in month.Dates())
            foreach (var slot in Slots.All)
            foreach (var loc in ctx.Locations.Values.Where(l => l.Active && !ctx.IsClosed(l.Id, date)))
            foreach (var role in PersonKinds.All)
            {
                if (loc.Need(slot, role) == 0 || filled.Contains(CellKeys.Of(date, slot, loc.Id, role))) continue;
                var (prevDate, prevSlot) = PreviousCell(date, slot);
                var (nextDate, nextSlot) = NextCell(date, slot);
                var candidates = ctx.People.Values
                    .Where(p => p.Active && p.Type == role && p.Allowed.Contains(slot.ToCode()))
                    .Where(p => !(role == PersonKind.Soldier && slot == Slot.Morning && !date.IsFriday()))
                    .Where(p => !busy.Contains(BusyKey(p.Id, date, slot))
                                && !busy.Contains(BusyKey(p.Id, prevDate, prevSlot))
                                && !busy.Contains(BusyKey(p.Id, nextDate, nextSlot)))
                    .OrderByDescending(p => Deficit(p.Id))
                    .ThenBy(p => units.GetValueOrDefault(p.Id))
                    .ThenBy(p => rows.Count(r => r.PersonnelId == p.Id && r.LocationId == loc.Id && month.Contains(r.Date)))
                    .ThenBy(p => p.Id)
                    .ToList();
                foreach (var p in candidates)
                {
                    var lo = date.AddDays(-2);
                    var hi = date.AddDays(2);
                    var mine = rows.Where(r => r.PersonnelId == p.Id && r.Date >= lo && r.Date <= hi).ToList();
                    var errorCountBefore = SchedulingRules.ValidatePerson(mine, p, ctx.DefaultRestHours).Count;
                    var entry = new AssignmentEntry { Date = date, Slot = slot, LocationId = loc.Id, Role = role, PersonnelId = p.Id };
                    mine.Add(entry);
                    if (SchedulingRules.ValidatePerson(mine, p, ctx.DefaultRestHours).Count > errorCountBefore) continue;

                    var entity = new Assignment { Date = date, Slot = slot, LocationId = loc.Id, Role = role, PersonnelId = p.Id };
                    _db.Assignments.Add(entity);
                    inserted.Add((entry, entity));
                    rows.Add(entry);
                    busy.Add(BusyKey(p.Id, date, slot));
                    filled.Add(CellKeys.Of(date, slot, loc.Id, role));
                    units[p.Id] = units.GetValueOrDefault(p.Id) + slot.Units();
                    added++;
                    break;
                }
            }
            await _db.SaveChangesAsync(ct);
            foreach (var (entry, entity) in inserted) entry.Id = entity.Id;

            // سرشیفت فقط برای نوبت‌هایی که سرشیفت ندارند
            var monthRows = rows.Where(r => month.Contains(r.Date)).ToList();
            var leaderCount = monthRows.Where(r => r.IsLeader).GroupBy(r => r.PersonnelId)
                .ToDictionary(g => g.Key, g => g.Count());
            var leadersSet = 0;
            foreach (var group in monthRows.GroupBy(r => r.CellKey))
            {
                if (group.Any(r => r.IsLeader)) continue;
                var pick = PickLeader(group, ctx, leaderCount);
                if (pick is null) continue;
                await _db.Assignments.Where(x => x.Id == pick.Id)
                    .ExecuteUpdateAsync(u => u.SetProperty(x => x.IsShiftLeader, true), ct);
                pick.IsLeader = true;
                leaderCount[pick.PersonnelId] = leaderCount.GetValueOrDefault(pick.PersonnelId) + 1;
                leadersSet++;
            }

            _audit.Log(actor, "generate", $"{month.Title}: {added} تخصیص، {leadersSet} سرشیفت");
            await _db.SaveChangesAsync(ct);
            var (gaps, leaderMissing) = SchedulingRules.Coverage(monthRows, month.Dates(), ctx);
            return new GenerateReportDto(added, leadersSet, gaps, leaderMissing);
        }, cancellationToken);

    public Task ClearMonthAsync(int jy, int jm, string actor, CancellationToken cancellationToken = default) =>
        _db.ExecuteInTransactionAsync(async ct =>
        {
            var month = await SchedulingQueries.EnsureMonthAsync(_db, jy, jm, ct);
            if (month.Status != MonthStatus.Draft) throw new DomainException("فقط ماه پیش‌نویس قابل پاک‌سازی است");
            var count = await _db.Assignments
                .Where(x => x.Date >= month.Start && x.Date <= month.End)
                .ExecuteDeleteAsync(ct);
            _audit.Log(actor, "clear_month", $"{month.Title}: {count} ردیف");
            await _db.SaveChangesAsync(ct);
        }, cancellationToken);

    // ---------- ویرایش دستی یک نوبت ----------

    static Dictionary<string, int> CountByMessage(IEnumerable<RuleViolation> list)
    {
        var counts = new Dictionary<string, int>();
        foreach (var v in list)
        {
            var key = $"{v.Message}|{Jalali.Iso(v.Date)}|{v.Slot.ToCode()}|{v.PersonnelId}";
            counts[key] = counts.GetValueOrDefault(key) + 1;
        }
        return counts;
    }

    /// <summary>خطاهایی که پس از تغییر تازه ظاهر شده‌اند (مقایسه چندتایی).</summary>
    static List<RuleViolation> NewViolations(List<RuleViolation> before, List<RuleViolation> after)
    {
        var remaining = CountByMessage(before);
        var fresh = new List<RuleViolation>();
        foreach (var v in after)
        {
            var key = $"{v.Message}|{Jalali.Iso(v.Date)}|{v.Slot.ToCode()}|{v.PersonnelId}";
            if (remaining.GetValueOrDefault(key) > 0) remaining[key]--;
            else fresh.Add(v);
        }
        return fresh;
    }

    public Task<IReadOnlyList<string>> SaveSlotAsync(DateOnly date, Slot slot, IReadOnlyList<SlotAssignmentInput> cells, string? leaderKey, string actor, CancellationToken cancellationToken = default) =>
        _db.ExecuteInTransactionAsync(async ct =>
        {
            await SchedulingQueries.RequireDraftMonthAsync(_db, date, ct);
            // پایان نوبت شب به ماه روز بعد گره خورده است
            if (slot == Slot.Night) await SchedulingQueries.RequireDraftMonthAsync(_db, date.AddDays(1), ct);

            var ctx = await SchedulingQueries.LoadContextAsync(_db, ct);
            var from = date.AddDays(-3);
            var to = date.AddDays(3);
            var before = SchedulingRules.ValidateRows(
                await SchedulingQueries.LoadAssignmentsAsync(_db, from, to, ct), ctx);

            await _db.Assignments.Where(x => x.Date == date && x.Slot == slot).ExecuteDeleteAsync(ct);

            var used = new HashSet<int>();
            var warnings = new List<string>();
            var leaderSet = false;
            foreach (var cell in cells.Where(x => x.PersonnelId is > 0))
            {
                var personId = cell.PersonnelId!.Value;
                if (!ctx.People.TryGetValue(personId, out var person)) throw new DomainException("فرد نامعتبر");
                if (!person.Active) throw new DomainException($"{person.Name} غیرفعال است");

                var isLeader = leaderKey == $"{cell.LocationId}|{cell.Role.ToCode()}";
                if (isLeader)
                {
                    if (person.Type != PersonKind.Official || !person.CanLeader)
                        throw new DomainException($"{person.Name} صلاحیت سرشیفتی ندارد");
                    leaderSet = true;
                }
                if (!used.Add(person.Id)) throw new DomainException($"{person.Name} در این نوبت بیش از یک بار انتخاب شده است");

                _db.Assignments.Add(new Assignment
                {
                    Date = date, Slot = slot, LocationId = cell.LocationId,
                    Role = cell.Role, PersonnelId = personId, IsShiftLeader = isLeader
                });
            }
            await _db.SaveChangesAsync(ct);

            if (!string.IsNullOrEmpty(leaderKey) && !leaderSet)
                throw new DomainException("سرشیفت باید روی جایگاه افسرِ پرشده انتخاب شود");

            var after = SchedulingRules.ValidateRows(
                await SchedulingQueries.LoadAssignmentsAsync(_db, from, to, ct), ctx);
            var fresh = NewViolations(before, after);
            if (fresh.Count > 0)
                throw new DomainException("ذخیره نشد: " + string.Join(" | ", fresh.Select(v => v.Message).Distinct()));

            if (!leaderSet && cells.Any(x => x.PersonnelId is > 0)) warnings.Add("این نوبت سرشیفت ندارد");

            _audit.Log(actor, "save_slot", $"{Jalali.Iso(date)} {slot.ToCode()}");
            await _db.SaveChangesAsync(ct);
            return (IReadOnlyList<string>)warnings.AsReadOnly();
        }, cancellationToken);
}
