using HospitalShifts.Application.Dtos;
using HospitalShifts.Application.Forms;
using HospitalShifts.Domain.Entities;
using HospitalShifts.Domain.Enums;
using HospitalShifts.Domain.Scheduling;

namespace HospitalShifts.Application.Abstractions;

public interface IAuditService
{
    /// <summary>ثبت رویداد در همان تراکنش جاری؛ با SaveChanges بعدی نگهداشت می‌شود.</summary>
    void Log(string? actor, string action, string? detail = null);

    Task<List<AuditEntryDto>> GetRecentAsync(int take, CancellationToken cancellationToken = default);
}

public interface ISettingsService
{
    Task<int> GetDefaultRestHoursAsync(CancellationToken cancellationToken = default);
    Task SetDefaultRestHoursAsync(int hours, string actor, CancellationToken cancellationToken = default);
}

public interface IAuthService
{
    Task<AdminIdentity> LoginAsync(string username, string password, string ip, CancellationToken cancellationToken = default);
    Task<AdminIdentity> ChangePasswordAsync(int adminId, string currentPassword, string newPassword, CancellationToken cancellationToken = default);
}

public interface IPersonnelService
{
    Task<List<Personnel>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<PersonForm> GetForEditAsync(int id, CancellationToken cancellationToken = default);
    Task<SavePersonResult> SaveAsync(PersonForm form, string actor, CancellationToken cancellationToken = default);
    /// <summary>فعال/غیرفعال‌سازی؛ خروجی: تعداد تخصیص‌های آینده‌ای که حذف شدند.</summary>
    Task<int> SetActiveAsync(int id, bool active, string actor, CancellationToken cancellationToken = default);
}

public interface ILocationService
{
    Task<List<Location>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<LocationForm> GetForEditAsync(int id, CancellationToken cancellationToken = default);
    Task<List<DateOnly>> GetClosureDatesAsync(int locationId, CancellationToken cancellationToken = default);
    Task<int> SaveAsync(LocationForm form, string actor, CancellationToken cancellationToken = default);
    Task SetActiveAsync(int id, bool active, string actor, CancellationToken cancellationToken = default);
    /// <summary>ثبت تعطیلی؛ خروجی: تعداد تخصیص‌های آن روز که حذف شدند.</summary>
    Task<int> AddClosureAsync(int id, DateOnly date, string actor, CancellationToken cancellationToken = default);
    Task RemoveClosureAsync(int id, DateOnly date, string actor, CancellationToken cancellationToken = default);
}

public interface IScheduleService
{
    Task<MonthDto> GetMonthAsync(int jy, int jm, CancellationToken cancellationToken = default);
    Task<ScheduleBoardDto> GetBoardAsync(int jy, int jm, CancellationToken cancellationToken = default);
    Task<SchedulingContext> GetContextAsync(CancellationToken cancellationToken = default);
    Task<List<AssignmentEntry>> GetSlotAssignmentsAsync(DateOnly date, Slot slot, CancellationToken cancellationToken = default);
    Task<GenerateReportDto> GenerateAsync(int jy, int jm, string actor, CancellationToken cancellationToken = default);
    Task ClearMonthAsync(int jy, int jm, string actor, CancellationToken cancellationToken = default);
    /// <summary>ذخیره همه جایگاه‌های یک نوبت؛ خروجی: هشدارهای نرم. در صورت خطای جدید، کل تغییر برمی‌گردد.</summary>
    Task<IReadOnlyList<string>> SaveSlotAsync(DateOnly date, Slot slot, IReadOnlyList<SlotAssignmentInput> cells, string? leaderKey, string actor, CancellationToken cancellationToken = default);
}

public interface ILedgerService
{
    Task<List<LedgerRowDto>> GetLedgerAsync(int jy, int jm, CancellationToken cancellationToken = default);
    Task ApproveMonthAsync(int jy, int jm, string actor, CancellationToken cancellationToken = default);
    Task ReopenMonthAsync(int jy, int jm, string actor, CancellationToken cancellationToken = default);
    Task SettleMonthAsync(int jy, int jm, string actor, CancellationToken cancellationToken = default);
    Task AddAdjustmentAsync(int jy, int jm, int personId, int amount, string reason, string actor, CancellationToken cancellationToken = default);
    Task<List<AdjustmentDto>> GetAdjustmentsAsync(int monthId, CancellationToken cancellationToken = default);
}
