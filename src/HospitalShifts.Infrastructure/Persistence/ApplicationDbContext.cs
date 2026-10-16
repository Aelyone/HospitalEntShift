using HospitalShifts.Application.Abstractions;
using HospitalShifts.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HospitalShifts.Infrastructure.Persistence;

/// <summary>
/// DbContext اصلی برنامه؛ نگاشت‌ها از کلاس‌های Configurations خوانده می‌شوند.
/// توجه: تراکنش‌ها سطح سرویس تعریف می‌شوند و این کلاس فقط هماهنگ‌کننده Commit/Rollback است؛
/// قفل سراسری نسخه قدیمی حذف شده چون SQL Server خود همروندی را مدیریت می‌کند.
/// </summary>
public sealed class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Admin> Admins => Set<Admin>();
    public DbSet<Setting> Settings => Set<Setting>();
    public DbSet<ShiftType> ShiftTypes => Set<ShiftType>();
    public DbSet<ShiftSegment> ShiftSegments => Set<ShiftSegment>();
    public DbSet<Personnel> Personnel => Set<Personnel>();
    public DbSet<AllowedShift> AllowedShifts => Set<AllowedShift>();
    public DbSet<RestRule> RestRules => Set<RestRule>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<LocationRequirement> LocationRequirements => Set<LocationRequirement>();
    public DbSet<LocationClosure> LocationClosures => Set<LocationClosure>();
    public DbSet<ScheduleMonth> ScheduleMonths => Set<ScheduleMonth>();
    public DbSet<Assignment> Assignments => Set<Assignment>();
    public DbSet<MonthlyLedger> MonthlyLedgers => Set<MonthlyLedger>();
    public DbSet<LedgerAdjustment> LedgerAdjustments => Set<LedgerAdjustment>();
    public DbSet<AuditEntry> AuditLog => Set<AuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

    public async Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default)
    {
        var strategy = Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var result = await action(cancellationToken);
                await SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                ChangeTracker.Clear();
                throw;
            }
        });
    }

    public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default) =>
        ExecuteInTransactionAsync(async ct => { await action(ct); return 0; }, cancellationToken);
}
