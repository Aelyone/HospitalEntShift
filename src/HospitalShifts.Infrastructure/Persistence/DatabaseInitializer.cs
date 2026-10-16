using System.Globalization;
using HospitalShifts.Application.Abstractions;
using HospitalShifts.Application.Services.Common;
using HospitalShifts.Domain.Entities;
using HospitalShifts.Domain.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace HospitalShifts.Infrastructure.Persistence;

/// <summary>ساخت پایگاه‌داده (EnsureCreated) و درج داده‌های پایه در اولین اجرا.</summary>
public sealed class DatabaseInitializer
{
    readonly ApplicationDbContext _db;
    readonly IPasswordHasher _passwordHasher;
    readonly TimeProvider _time;

    public DatabaseInitializer(ApplicationDbContext db, IPasswordHasher passwordHasher, TimeProvider time)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _time = time;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _db.Database.EnsureCreatedAsync(cancellationToken);

        if (!await _db.ShiftTypes.AnyAsync(cancellationToken))
        {
            foreach (var def in ShiftDefinitions.All)
                _db.ShiftTypes.Add(new ShiftType
                {
                    Code = def.Code,
                    Name = def.Name,
                    Units = def.Units,
                    AutoGenerate = def.Auto,
                    Segments = def.Segments
                        .Select((slot, i) => new ShiftSegment { Position = i, Slot = slot })
                        .ToList()
                });
        }

        if (!await _db.Settings.AnyAsync(s => s.Key == SettingKeys.DefaultRestHours, cancellationToken))
            _db.Settings.Add(new Setting
            {
                Key = SettingKeys.DefaultRestHours,
                Value = SchedulingRules.DefaultRestHours.ToString(CultureInfo.InvariantCulture)
            });

        if (!await _db.Admins.AnyAsync(cancellationToken))
            _db.Admins.Add(new Admin
            {
                Username = "admin",
                PasswordHash = _passwordHasher.Hash("1234"),
                MustChange = true,
                CreatedAt = _time.GetUtcNow()
            });

        await _db.SaveChangesAsync(cancellationToken);
    }
}
