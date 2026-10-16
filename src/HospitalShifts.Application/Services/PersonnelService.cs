using HospitalShifts.Application.Abstractions;
using HospitalShifts.Application.Dtos;
using HospitalShifts.Application.Forms;
using HospitalShifts.Application.Services.Common;
using HospitalShifts.Domain.Calendar;
using HospitalShifts.Domain.Common;
using HospitalShifts.Domain.Entities;
using HospitalShifts.Domain.Enums;
using HospitalShifts.Domain.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace HospitalShifts.Application.Services;

public sealed class PersonnelService : IPersonnelService
{
    readonly IApplicationDbContext _db;
    readonly IAuditService _audit;
    readonly TimeProvider _time;

    public PersonnelService(IApplicationDbContext db, IAuditService audit, TimeProvider time)
    {
        _db = db;
        _audit = audit;
        _time = time;
    }

    DateOnly Today => DateOnly.FromDateTime(_time.GetLocalNow().Date);

    public Task<List<Personnel>> GetAllAsync(CancellationToken cancellationToken = default) =>
        _db.Personnel.AsNoTracking()
            .OrderByDescending(p => p.Active).ThenBy(p => p.Type).ThenBy(p => p.LastName).ThenBy(p => p.FirstName)
            .ToListAsync(cancellationToken);

    public async Task<PersonForm> GetForEditAsync(int id, CancellationToken cancellationToken = default)
    {
        var p = await _db.Personnel.AsNoTracking()
                .Include(x => x.Allowed).Include(x => x.Rest)
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new DomainException("فرد یافت نشد");
        return new PersonForm
        {
            Id = p.Id, Type = p.Type.ToCode(), FirstName = p.FirstName, LastName = p.LastName,
            Phone = p.Phone, Address = p.Address, Department = p.Department, Level = p.Level,
            BaseShare = p.BaseShare, InitialBalance = p.InitialBalance,
            CanLeader = p.CanLeader, LeaderPriority = p.LeaderPriority, Active = p.Active,
            Allowed = p.Allowed.Select(a => a.ShiftCode).ToList(),
            Rest = p.Rest.ToDictionary(r => r.ShiftCode, r => (int?)r.RestHours)
        };
    }

    /// <summary>ماه‌های پیش‌نویس که روز «from» یا بعد از آن را پوشش می‌دهند.</summary>
    Task<List<ScheduleMonth>> DraftMonthsCoveringAsync(DateOnly from, CancellationToken ct) =>
        _db.ScheduleMonths.Where(m => m.Status == MonthStatus.Draft && m.EndDate >= from).ToListAsync(ct);

    public Task<SavePersonResult> SaveAsync(PersonForm form, string actor, CancellationToken cancellationToken = default) =>
        _db.ExecuteInTransactionAsync(async ct =>
        {
            form.FirstName = (form.FirstName ?? "").Trim();
            form.LastName = (form.LastName ?? "").Trim();
            if (form.FirstName == "" || form.LastName == "") throw new DomainException("نام و نام خانوادگی الزامی است");
            if (!PersonKinds.TryParse(form.Type, out var type)) throw new DomainException("نوع نامعتبر");
            if (form.BaseShare is < 0 or > 200) throw new DomainException("سهم پایه نامعتبر است");
            if (type == PersonKind.Soldier) form.CanLeader = false;

            Personnel p;
            if (form.Id == 0)
            {
                p = new Personnel { Active = true, CreatedAt = _time.GetUtcNow() };
                _db.Personnel.Add(p);
            }
            else
            {
                p = await _db.Personnel.Include(x => x.Allowed).Include(x => x.Rest)
                        .FirstOrDefaultAsync(x => x.Id == form.Id, ct)
                    ?? throw new DomainException("فرد یافت نشد");
                if (p.Type != type && await _db.Assignments.AnyAsync(a => a.PersonnelId == p.Id, ct))
                    throw new DomainException("نوع فردی که سابقه تخصیص دارد قابل تغییر نیست");
            }

            p.Type = type;
            p.FirstName = form.FirstName; p.LastName = form.LastName;
            p.Phone = form.Phone; p.Address = form.Address; p.Department = form.Department; p.Level = form.Level;
            p.BaseShare = form.BaseShare; p.InitialBalance = form.InitialBalance;
            p.CanLeader = form.CanLeader; p.LeaderPriority = form.LeaderPriority;

            p.Allowed.Clear();
            foreach (var code in form.Allowed.Where(ShiftDefinitions.Exists).Distinct())
                p.Allowed.Add(new AllowedShift { ShiftCode = code });

            p.Rest.Clear();
            foreach (var (code, hours) in form.Rest)
                if (hours is >= 0 and <= 72 && ShiftDefinitions.Exists(code))
                    p.Rest.Add(new RestRule { ShiftCode = code, RestHours = hours.Value });
            await _db.SaveChangesAsync(ct);

            // اگر صلاحیت سرشیفتی گرفته شده، پرچم سرشیفتش در تخصیص‌های آینده ماه‌های پیش‌نویس برداشته شود
            if (!form.CanLeader)
            {
                foreach (var m in await DraftMonthsCoveringAsync(Today, ct))
                {
                    var from = m.StartDate > Today ? m.StartDate : Today;
                    await _db.Assignments
                        .Where(a => a.PersonnelId == p.Id && a.IsShiftLeader && a.Date >= from && a.Date <= m.EndDate)
                        .ExecuteUpdateAsync(s => s.SetProperty(a => a.IsShiftLeader, false), ct);
                }
            }

            var warnings = new List<string>();
            var recent = Today.AddDays(-3);
            if (await _db.Assignments.AnyAsync(a => a.PersonnelId == p.Id && a.Date >= recent, ct))
            {
                var ctx = await SchedulingQueries.LoadContextAsync(_db, ct);
                var violations = await SchedulingQueries.ComputeViolationsAsync(_db, ctx, Today, Today.AddDays(400), ct);
                var count = violations.Count(v => v.PersonnelId == p.Id);
                if (count > 0) warnings.Add($"{count} تخصیص فعلی با تنظیمات جدید سازگار نیست؛ برنامه را بازبینی کنید");
            }

            _audit.Log(actor, form.Id == 0 ? "person_add" : "person_edit", $"#{p.Id} {p.FullName}");
            await _db.SaveChangesAsync(ct);
            return new SavePersonResult(p.Id, warnings);
        }, cancellationToken);

    public Task<int> SetActiveAsync(int id, bool active, string actor, CancellationToken cancellationToken = default) =>
        _db.ExecuteInTransactionAsync(async ct =>
        {
            var p = await _db.Personnel.FindAsync(new object[] { id }, ct) ?? throw new DomainException("فرد یافت نشد");
            p.Active = active;
            var removed = 0;
            if (!active)
            {
                foreach (var m in await DraftMonthsCoveringAsync(Today, ct))
                {
                    var from = m.StartDate > Today ? m.StartDate : Today;
                    removed += await _db.Assignments
                        .Where(a => a.PersonnelId == id && a.Date >= from && a.Date <= m.EndDate)
                        .ExecuteDeleteAsync(ct);
                }
            }
            _audit.Log(actor, active ? "person_activate" : "person_deactivate", $"#{id} ({removed} تخصیص آینده حذف شد)");
            await _db.SaveChangesAsync(ct);
            return removed;
        }, cancellationToken);
}
