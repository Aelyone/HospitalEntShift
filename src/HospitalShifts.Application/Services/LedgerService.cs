using HospitalShifts.Application.Abstractions;
using HospitalShifts.Application.Dtos;
using HospitalShifts.Application.Services.Common;
using HospitalShifts.Domain.Common;
using HospitalShifts.Domain.Entities;
using HospitalShifts.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HospitalShifts.Application.Services;

/// <summary>چرخه مالی ماه: پیش‌نمایش سهم، تأیید، بازگشایی، تسویه و بخشودگی.</summary>
public sealed class LedgerService : ILedgerService
{
    readonly IApplicationDbContext _db;
    readonly IAuditService _audit;
    readonly TimeProvider _time;

    public LedgerService(IApplicationDbContext db, IAuditService audit, TimeProvider time)
    {
        _db = db;
        _audit = audit;
        _time = time;
    }

    /// <summary>پیش‌نمایش سهم در ماه پیش‌نویس: ورودی + کارکرد + بخشودگی − سهم.</summary>
    async Task<List<LedgerRowDto>> BuildProjectionAsync(MonthDto month, CancellationToken ct)
    {
        var openings = await SchedulingQueries.ComputeOpeningBalancesAsync(_db, month, ct);
        var monthRows = await SchedulingQueries.LoadAssignmentsAsync(_db, month.Start, month.End, ct);
        var worked = monthRows.GroupBy(r => r.PersonnelId)
            .ToDictionary(g => g.Key, g => g.Sum(r => r.Slot.Units()));
        var adjustments = await _db.LedgerAdjustments.AsNoTracking().Where(x => x.MonthId == month.Id).ToListAsync(ct);
        var adjustmentSum = adjustments.GroupBy(x => x.PersonnelId).ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        var people = await _db.Personnel.AsNoTracking()
            .OrderBy(x => x.Type).ThenBy(x => x.LastName).ThenBy(x => x.FirstName)
            .ToListAsync(ct);
        var rows = new List<LedgerRowDto>();
        foreach (var p in people)
        {
            var w = worked.GetValueOrDefault(p.Id);
            if (!p.Active && w == 0) continue;
            var opening = openings.GetValueOrDefault(p.Id);
            rows.Add(new LedgerRowDto(p.Id, $"{p.FirstName} {p.LastName}", p.Type, p.BaseShare, opening,
                Math.Max(0, p.BaseShare - opening), w, adjustmentSum.GetValueOrDefault(p.Id), null, null));
        }
        return rows;
    }

    Task<MonthStatus?> PreviousMonthStatusAsync(MonthDto month, CancellationToken ct) =>
        _db.ScheduleMonths.AsNoTracking()
            .Where(x => x.EndDate < month.Start)
            .OrderByDescending(x => x.EndDate)
            .Select(x => (MonthStatus?)x.Status)
            .FirstOrDefaultAsync(ct);

    public Task<List<LedgerRowDto>> GetLedgerAsync(int jy, int jm, CancellationToken cancellationToken = default) =>
        _db.ExecuteInTransactionAsync(async ct =>
        {
            var month = await SchedulingQueries.EnsureMonthAsync(_db, jy, jm, ct);
            if (month.Status == MonthStatus.Draft) return await BuildProjectionAsync(month, ct);

            var stored = await _db.MonthlyLedgers.AsNoTracking()
                .Where(x => x.MonthId == month.Id)
                .Join(_db.Personnel.AsNoTracking(), l => l.PersonnelId, p => p.Id, (l, p) => new { l, p })
                .OrderBy(x => x.p.Type).ThenBy(x => x.p.LastName).ThenBy(x => x.p.FirstName)
                .Select(x => new LedgerRowDto(x.l.PersonnelId, $"{x.p.FirstName} {x.p.LastName}", x.p.Type,
                    x.l.Share, x.l.Opening, x.l.Target, x.l.Worked, x.l.Forgiveness, x.l.Closing, x.l.ForgivenessReason))
                .ToListAsync(ct);
            return stored;
        }, cancellationToken);

    public Task ApproveMonthAsync(int jy, int jm, string actor, CancellationToken cancellationToken = default) =>
        _db.ExecuteInTransactionAsync(async ct =>
        {
            var month = await SchedulingQueries.EnsureMonthAsync(_db, jy, jm, ct);
            if (month.Status != MonthStatus.Draft) throw new DomainException("ماه در وضعیت پیش‌نویس نیست");
            if (await PreviousMonthStatusAsync(month, ct) == MonthStatus.Draft)
                throw new DomainException("ابتدا ماه قبل را تأیید کنید");

            var ctx = await SchedulingQueries.LoadContextAsync(_db, ct);
            var violations = await SchedulingQueries.ComputeViolationsAsync(_db, ctx, month.Start, month.End, ct);
            if (violations.Count > 0)
                throw new DomainException($"{violations.Count} خطای قانونی وجود دارد؛ تأیید ممکن نیست");

            foreach (var row in await BuildProjectionAsync(month, ct))
                _db.MonthlyLedgers.Add(new MonthlyLedger
                {
                    MonthId = month.Id, PersonnelId = row.PersonnelId, Share = row.Share,
                    Opening = row.Opening, Target = row.Target, Worked = row.Worked,
                    Forgiveness = row.Forgiveness, Closing = row.Projected, Status = MonthStatus.Approved
                });
            await _db.ScheduleMonths.Where(x => x.Id == month.Id)
                .ExecuteUpdateAsync(u => u.SetProperty(x => x.Status, MonthStatus.Approved), ct);
            _audit.Log(actor, "approve", month.Title);
            await _db.SaveChangesAsync(ct);
        }, cancellationToken);

    public Task ReopenMonthAsync(int jy, int jm, string actor, CancellationToken cancellationToken = default) =>
        _db.ExecuteInTransactionAsync(async ct =>
        {
            var month = await SchedulingQueries.EnsureMonthAsync(_db, jy, jm, ct);
            if (month.Status != MonthStatus.Approved)
                throw new DomainException("فقط ماه تأییدشده (تسویه‌نشده) قابل بازگشایی است");

            var nextStatus = await _db.ScheduleMonths.AsNoTracking()
                .Where(x => x.StartDate > month.End)
                .OrderBy(x => x.StartDate)
                .Select(x => (MonthStatus?)x.Status)
                .FirstOrDefaultAsync(ct);
            if (nextStatus is not null && nextStatus != MonthStatus.Draft)
                throw new DomainException("ماه بعد تأیید شده است؛ ابتدا آن را بازگشایی کنید");

            await _db.MonthlyLedgers.Where(x => x.MonthId == month.Id).ExecuteDeleteAsync(ct);
            await _db.ScheduleMonths.Where(x => x.Id == month.Id)
                .ExecuteUpdateAsync(u => u.SetProperty(x => x.Status, MonthStatus.Draft), ct);
            _audit.Log(actor, "reopen", month.Title);
            await _db.SaveChangesAsync(ct);
        }, cancellationToken);

    public Task SettleMonthAsync(int jy, int jm, string actor, CancellationToken cancellationToken = default) =>
        _db.ExecuteInTransactionAsync(async ct =>
        {
            var month = await SchedulingQueries.EnsureMonthAsync(_db, jy, jm, ct);
            if (month.Status != MonthStatus.Approved) throw new DomainException("فقط ماه تأییدشده قابل تسویه است");
            var prev = await PreviousMonthStatusAsync(month, ct);
            if (prev is not null && prev != MonthStatus.Settled)
                throw new DomainException("ابتدا ماه‌های قبل را تسویه کنید");

            var now = _time.GetUtcNow();
            await _db.MonthlyLedgers.Where(x => x.MonthId == month.Id)
                .ExecuteUpdateAsync(u => u.SetProperty(x => x.Status, MonthStatus.Settled), ct);
            await _db.ScheduleMonths.Where(x => x.Id == month.Id)
                .ExecuteUpdateAsync(u => u.SetProperty(x => x.Status, MonthStatus.Settled).SetProperty(x => x.SettledAt, (DateTimeOffset?)now), ct);
            _audit.Log(actor, "settle", month.Title);
            await _db.SaveChangesAsync(ct);
        }, cancellationToken);

    /// <summary>بخشودگی/اصلاح واحد برای یک نفر؛ فقط در ماه پیش‌نویس. عدد مثبت از بدهی می‌کاهد.</summary>
    public Task AddAdjustmentAsync(int jy, int jm, int personId, int amount, string reason, string actor, CancellationToken cancellationToken = default) =>
        _db.ExecuteInTransactionAsync(async ct =>
        {
            var month = await SchedulingQueries.EnsureMonthAsync(_db, jy, jm, ct);
            if (month.Status != MonthStatus.Draft) throw new DomainException("تعدیل فقط در ماه پیش‌نویس ممکن است");
            if (amount == 0 || Math.Abs(amount) > 100) throw new DomainException("مقدار نامعتبر است");
            if (string.IsNullOrWhiteSpace(reason)) throw new DomainException("ذکر دلیل الزامی است");
            if (!await _db.Personnel.AnyAsync(x => x.Id == personId, ct)) throw new DomainException("فرد یافت نشد");

            _db.LedgerAdjustments.Add(new LedgerAdjustment
            {
                MonthId = month.Id, PersonnelId = personId, Amount = amount,
                Reason = reason.Trim(), CreatedAt = _time.GetUtcNow()
            });
            _audit.Log(actor, "adjust", $"{month.Title} #{personId} {amount}: {reason.Trim()}");
            await _db.SaveChangesAsync(ct);
        }, cancellationToken);

    public Task<List<AdjustmentDto>> GetAdjustmentsAsync(int monthId, CancellationToken cancellationToken = default) =>
        _db.LedgerAdjustments.AsNoTracking()
            .Where(x => x.MonthId == monthId)
            .Join(_db.Personnel.AsNoTracking(), a => a.PersonnelId, p => p.Id,
                (a, p) => new AdjustmentDto(a.Id, $"{p.FirstName} {p.LastName}", a.Amount, a.Reason, a.CreatedAt.ToString("O")))
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);
}
