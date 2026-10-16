using HospitalShifts.Application.Abstractions;
using HospitalShifts.Application.Dtos;
using HospitalShifts.Application.Security;
using HospitalShifts.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace HospitalShifts.Application.Services;

public sealed class AuthService : IAuthService
{
    const int MaxAttemptsPerWindow = 30;
    static readonly TimeSpan AttemptWindow = TimeSpan.FromMinutes(15);
    const int LockoutThreshold = 5;
    const long LockoutSeconds = 900;

    readonly IApplicationDbContext _db;
    readonly IPasswordHasher _hasher;
    readonly IAuditService _audit;
    readonly IMemoryCache _rateLimit;
    readonly TimeProvider _time;

    // برای یکسان‌سازی زمان پاسخ هنگام نبود کاربر (ضد حدس نام کاربری).
    static readonly string DummyHash = new PasswordHasher().Hash("dummy-password-1");

    public AuthService(IApplicationDbContext db, IPasswordHasher hasher, IAuditService audit, IMemoryCache rateLimit, TimeProvider time)
    {
        _db = db;
        _hasher = hasher;
        _audit = audit;
        _rateLimit = rateLimit;
        _time = time;
    }

    bool IsRateLimited(string ip)
    {
        var attempts = _rateLimit.GetOrCreate($"login:{ip}", entry =>
        {
            entry.SlidingExpiration = AttemptWindow;
            return new List<DateTimeOffset>();
        })!;
        lock (attempts)
        {
            var now = _time.GetUtcNow();
            attempts.RemoveAll(t => now - t > AttemptWindow);
            if (attempts.Count >= MaxAttemptsPerWindow) return true;
            attempts.Add(now);
            return false;
        }
    }

    public async Task<AdminIdentity> LoginAsync(string username, string password, string ip, CancellationToken cancellationToken = default)
    {
        if (IsRateLimited(ip)) throw new DomainException("تعداد تلاش‌ها زیاد است؛ ۱۵ دقیقه بعد تلاش کنید");
        var normalized = (username ?? "").Trim();

        var identity = await _db.ExecuteInTransactionAsync(async ct =>
        {
            var user = await _db.Admins.FirstOrDefaultAsync(a => a.Username == normalized, ct);
            var now = _time.GetUtcNow().ToUnixTimeSeconds();

            if (user is null)
            {
                _hasher.Verify(password ?? "", DummyHash);
                return null;
            }

            // بازیابی اضطراری از سمت پایگاه‌داده: password_hash='reset' ← رمز 1234 با تغییر اجباری
            if (user.PasswordHash == "reset")
            {
                user.PasswordHash = _hasher.Hash("1234");
                user.MustChange = true;
                user.FailedCount = 0;
                user.LockedUntil = 0;
                _audit.Log(user.Username, "password_reset_db", ip);
            }
            if (user.LockedUntil > now) throw new DomainException("حساب موقتاً قفل است؛ ۱۵ دقیقه بعد تلاش کنید");

            if (!_hasher.Verify(password ?? "", user.PasswordHash))
            {
                user.FailedCount++;
                if (user.FailedCount >= LockoutThreshold) { user.FailedCount = 0; user.LockedUntil = now + LockoutSeconds; }
                _audit.Log(user.Username, "login_failed", ip);
                return null; // شمارنده و رویداد با SaveChanges تراکنش ثبت می‌شوند
            }

            user.FailedCount = 0;
            user.LockedUntil = 0;
            _audit.Log(user.Username, "login", ip);
            return new AdminIdentity(user.Id, user.Username, user.MustChange);
        }, cancellationToken);

        return identity ?? throw new DomainException("نام کاربری یا رمز عبور نادرست است");
    }

    public Task<AdminIdentity> ChangePasswordAsync(int adminId, string currentPassword, string newPassword, CancellationToken cancellationToken = default) =>
        _db.ExecuteInTransactionAsync(async ct =>
        {
            var user = await _db.Admins.FindAsync(new object[] { adminId }, ct)
                ?? throw new DomainException("کاربر یافت نشد");
            if (!_hasher.Verify(currentPassword ?? "", user.PasswordHash)) throw new DomainException("رمز فعلی نادرست است");
            if (_hasher.GetWeaknessReason(newPassword ?? "", user.Username) is { } weakness) throw new DomainException(weakness);
            if (currentPassword == newPassword) throw new DomainException("رمز جدید باید با رمز فعلی متفاوت باشد");

            user.PasswordHash = _hasher.Hash(newPassword!);
            user.MustChange = false;
            _audit.Log(user.Username, "password_changed", "");
            await _db.SaveChangesAsync(ct);
            return new AdminIdentity(user.Id, user.Username, false);
        }, cancellationToken);
}
