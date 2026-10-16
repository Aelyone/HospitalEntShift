using System.Security.Cryptography;
using HospitalShifts.Application.Abstractions;

namespace HospitalShifts.Application.Security;

/// <summary>پیاده‌سازی PBKDF2-SHA256 با نمک تصادفی و قالب pbkdf2$iterations$salt$hash.</summary>
public sealed class PasswordHasher : IPasswordHasher
{
    const int Iterations = 120000;
    const int SaltSize = 16;
    const int HashSize = 32;

    static readonly string[] CommonPasswords =
        { "12345678", "password", "admin123", "11111111", "123456789", "1234567890", "qwerty123" };

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        using var kdf = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256);
        var hash = kdf.GetBytes(HashSize);
        return $"pbkdf2${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public bool Verify(string password, string encodedHash)
    {
        var parts = encodedHash.Split('$');
        if (parts.Length != 4 || parts[0] != "pbkdf2" || !int.TryParse(parts[1], out var iterations)) return false;
        var salt = Convert.FromBase64String(parts[2]);
        var expected = Convert.FromBase64String(parts[3]);
        using var kdf = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
        return CryptographicOperations.FixedTimeEquals(kdf.GetBytes(expected.Length), expected);
    }

    public string? GetWeaknessReason(string password, string username)
    {
        if (password.Length < 8) return "رمز عبور باید حداقل ۸ کاراکتر باشد";
        if (CommonPasswords.Contains(password.ToLowerInvariant()) || password.Equals(username, StringComparison.OrdinalIgnoreCase))
            return "رمز عبور بسیار ساده است";
        if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit)) return "رمز عبور باید شامل حرف و عدد باشد";
        return null;
    }
}
