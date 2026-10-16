using HospitalShifts.Application.Abstractions;
using HospitalShifts.Application.Dtos;
using HospitalShifts.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HospitalShifts.Application.Audit;

public sealed class AuditService : IAuditService
{
    readonly IApplicationDbContext _db;
    readonly TimeProvider _time;

    public AuditService(IApplicationDbContext db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    public void Log(string? actor, string action, string? detail = null) =>
        _db.AuditLog.Add(new AuditEntry { At = _time.GetUtcNow(), Actor = actor, Action = action, Detail = detail });

    public async Task<List<AuditEntryDto>> GetRecentAsync(int take, CancellationToken cancellationToken = default) =>
        await _db.AuditLog.AsNoTracking()
            .OrderByDescending(a => a.Id)
            .Take(take)
            .Select(a => new AuditEntryDto(a.At.ToString("O"), a.Actor, a.Action, a.Detail))
            .ToListAsync(cancellationToken);
}
