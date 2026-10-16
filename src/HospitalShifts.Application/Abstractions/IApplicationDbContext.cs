using HospitalShifts.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HospitalShifts.Application.Abstractions;

/// <summary>
/// قرارداد دسترسی به داده برای لایه Application؛ پیاده‌سازی در Infrastructure است.
/// واحد کار (Unit of Work) از طریق <see cref="SaveChangesAsync"/> و تراکنش‌ها مدیریت می‌شود.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<Admin> Admins { get; }
    DbSet<Setting> Settings { get; }
    DbSet<ShiftType> ShiftTypes { get; }
    DbSet<ShiftSegment> ShiftSegments { get; }
    DbSet<Personnel> Personnel { get; }
    DbSet<AllowedShift> AllowedShifts { get; }
    DbSet<RestRule> RestRules { get; }
    DbSet<Location> Locations { get; }
    DbSet<LocationRequirement> LocationRequirements { get; }
    DbSet<LocationClosure> LocationClosures { get; }
    DbSet<ScheduleMonth> ScheduleMonths { get; }
    DbSet<Assignment> Assignments { get; }
    DbSet<MonthlyLedger> MonthlyLedgers { get; }
    DbSet<LedgerAdjustment> LedgerAdjustments { get; }
    DbSet<AuditEntry> AuditLog { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>اجرای کار داخل یک تراکنش؛ در صورت استثنا همه تغییرات برمی‌گردد و پیش از Commit یک SaveChanges خودکار انجام می‌شود.</summary>
    Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="ExecuteInTransactionAsync{T}"/>
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default);
}
