using System.Globalization;
using HospitalShifts.Application.Abstractions;
using HospitalShifts.Application.Services.Common;
using HospitalShifts.Domain.Common;
using HospitalShifts.Domain.Entities;

namespace HospitalShifts.Application.Services;

public sealed class SettingsService : ISettingsService
{
    readonly IApplicationDbContext _db;
    readonly IAuditService _audit;

    public SettingsService(IApplicationDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public Task<int> GetDefaultRestHoursAsync(CancellationToken cancellationToken = default) =>
        SchedulingQueries.GetDefaultRestHoursAsync(_db, cancellationToken);

    public Task SetDefaultRestHoursAsync(int hours, string actor, CancellationToken cancellationToken = default) =>
        _db.ExecuteInTransactionAsync(async ct =>
        {
            if (hours is < 0 or > 72) throw new DomainException("مقدار نامعتبر");
            var value = hours.ToString(CultureInfo.InvariantCulture);
            var setting = await _db.Settings.FindAsync(new object[] { SettingKeys.DefaultRestHours }, ct);
            if (setting is null) _db.Settings.Add(new Setting { Key = SettingKeys.DefaultRestHours, Value = value });
            else setting.Value = value;
            _audit.Log(actor, "setting", $"{SettingKeys.DefaultRestHours}={value}");
            await _db.SaveChangesAsync(ct);
        }, cancellationToken);
}
