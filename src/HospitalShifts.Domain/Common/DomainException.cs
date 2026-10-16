namespace HospitalShifts.Domain.Common;

/// <summary>خطای قاعده کسب‌وکار؛ پیام آن مستقیم به کاربر نشان داده می‌شود.</summary>
public sealed class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}
