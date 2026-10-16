namespace HospitalShifts.Application.Abstractions;

/// <summary>هش و راستی‌آزمایی رمز عبور (PBKDF2-SHA256).</summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string encodedHash);

    /// <summary>اگر رمز ضعیف باشد دلیل فارسی آن، وگرنه null.</summary>
    string? GetWeaknessReason(string password, string username);
}
