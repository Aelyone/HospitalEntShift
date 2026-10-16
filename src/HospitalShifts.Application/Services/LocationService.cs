using HospitalShifts.Application.Abstractions;
using HospitalShifts.Application.Forms;
using HospitalShifts.Application.Services.Common;
using HospitalShifts.Domain.Common;
using HospitalShifts.Domain.Entities;
using HospitalShifts.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HospitalShifts.Application.Services;

public sealed class LocationService : ILocationService
{
    readonly IApplicationDbContext _db;
    readonly IAuditService _audit;

    public LocationService(IApplicationDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public Task<List<Location>> GetAllAsync(CancellationToken cancellationToken = default) =>
        _db.Locations.AsNoTracking().OrderBy(l => l.Sort).ThenBy(l => l.Id).ToListAsync(cancellationToken);

    public async Task<LocationForm> GetForEditAsync(int id, CancellationToken cancellationToken = default)
    {
        var l = await _db.Locations.AsNoTracking().Include(x => x.Requirements)
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new DomainException("محل یافت نشد");
        return new LocationForm
        {
            Id = l.Id, Name = l.Name, Sort = l.Sort, Active = l.Active,
            NeedOfficial = l.Requirements.Where(q => q.NeedsOfficial > 0).Select(q => q.Slot.ToCode()).ToList(),
            NeedSoldier = l.Requirements.Where(q => q.NeedsSoldier > 0).Select(q => q.Slot.ToCode()).ToList()
        };
    }

    public Task<List<DateOnly>> GetClosureDatesAsync(int locationId, CancellationToken cancellationToken = default) =>
        _db.LocationClosures.AsNoTracking()
            .Where(c => c.LocationId == locationId)
            .OrderBy(c => c.Date)
            .Select(c => c.Date)
            .ToListAsync(cancellationToken);

    public Task<int> SaveAsync(LocationForm form, string actor, CancellationToken cancellationToken = default) =>
        _db.ExecuteInTransactionAsync(async ct =>
        {
            var name = (form.Name ?? "").Trim();
            if (name == "") throw new DomainException("نام محل الزامی است");
            if (await _db.Locations.AnyAsync(l => l.Name == name && l.Id != form.Id, ct))
                throw new DomainException("نام محل تکراری است");

            Location l;
            if (form.Id == 0)
            {
                l = new Location { Active = true };
                _db.Locations.Add(l);
            }
            else
            {
                l = await _db.Locations.Include(x => x.Requirements).FirstOrDefaultAsync(x => x.Id == form.Id, ct)
                    ?? throw new DomainException("محل یافت نشد");
            }

            l.Name = name;
            l.Sort = form.Sort;
            l.Requirements.Clear();
            foreach (var slot in Slots.All)
                l.Requirements.Add(new LocationRequirement
                {
                    Slot = slot,
                    NeedsOfficial = form.NeedOfficial.Contains(slot.ToCode()) ? 1 : 0,
                    NeedsSoldier = form.NeedSoldier.Contains(slot.ToCode()) ? 1 : 0
                });

            _audit.Log(actor, form.Id == 0 ? "location_add" : "location_edit", name);
            await _db.SaveChangesAsync(ct);
            return l.Id;
        }, cancellationToken);

    public Task SetActiveAsync(int id, bool active, string actor, CancellationToken cancellationToken = default) =>
        _db.ExecuteInTransactionAsync(async ct =>
        {
            var l = await _db.Locations.FindAsync(new object[] { id }, ct) ?? throw new DomainException("محل یافت نشد");
            l.Active = active;
            _audit.Log(actor, "location_active", $"#{id}={active}");
            await _db.SaveChangesAsync(ct);
        }, cancellationToken);

    public Task<int> AddClosureAsync(int id, DateOnly date, string actor, CancellationToken cancellationToken = default) =>
        _db.ExecuteInTransactionAsync(async ct =>
        {
            var month = await SchedulingQueries.FindMonthContainingAsync(_db, date, ct);
            if (month is not null && month.Status != MonthStatus.Draft)
                throw new DomainException($"ماه {month.Title} پیش‌نویس نیست");

            if (!await _db.LocationClosures.AnyAsync(c => c.LocationId == id && c.Date == date, ct))
                _db.LocationClosures.Add(new LocationClosure { LocationId = id, Date = date });

            var removed = await _db.Assignments.Where(a => a.LocationId == id && a.Date == date).ExecuteDeleteAsync(ct);
            _audit.Log(actor, "closure_add", $"#{id} {Domain.Calendar.Jalali.Iso(date)} ({removed} تخصیص حذف شد)");
            await _db.SaveChangesAsync(ct);
            return removed;
        }, cancellationToken);

    public Task RemoveClosureAsync(int id, DateOnly date, string actor, CancellationToken cancellationToken = default) =>
        _db.ExecuteInTransactionAsync(async ct =>
        {
            await _db.LocationClosures.Where(c => c.LocationId == id && c.Date == date).ExecuteDeleteAsync(ct);
            _audit.Log(actor, "closure_remove", $"#{id} {Domain.Calendar.Jalali.Iso(date)}");
            await _db.SaveChangesAsync(ct);
        }, cancellationToken);
}
