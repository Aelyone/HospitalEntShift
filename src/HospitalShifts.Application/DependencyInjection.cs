using HospitalShifts.Application.Abstractions;
using HospitalShifts.Application.Audit;
using HospitalShifts.Application.Security;
using HospitalShifts.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace HospitalShifts.Application;

/// <summary>ثبت همه سرویس‌های لایه Application در کانتینر DI.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddSingleton(TimeProvider.System);

        services.AddSingleton<IPasswordHasher, PasswordHasher>();

        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<ISettingsService, SettingsService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IPersonnelService, PersonnelService>();
        services.AddScoped<ILocationService, LocationService>();
        services.AddScoped<IScheduleService, ScheduleService>();
        services.AddScoped<ILedgerService, LedgerService>();

        return services;
    }
}
