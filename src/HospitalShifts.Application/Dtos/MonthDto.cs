using HospitalShifts.Domain.Calendar;
using HospitalShifts.Domain.Enums;

namespace HospitalShifts.Application.Dtos;

/// <summary>اطلاعات یک ماه برنامه‌ریزی برای نمایش و ناوبری.</summary>
public sealed record MonthDto(int Id, int Jy, int Jm, DateOnly Start, DateOnly End, MonthStatus Status)
{
    public string Title => $"{Jalali.MonthNames[Jm - 1]} {Jy}";
    public string StatusLabel => Status.ToLabel();
    public string StatusCode => Status.ToCode();

    public IEnumerable<DateOnly> Dates()
    {
        for (var d = Start; d <= End; d = d.AddDays(1)) yield return d;
    }

    public bool Contains(DateOnly date) => date >= Start && date <= End;

    public (int Y, int M) Prev() => Jm == 1 ? (Jy - 1, 12) : (Jy, Jm - 1);
    public (int Y, int M) Next() => Jm == 12 ? (Jy + 1, 1) : (Jy, Jm + 1);
}
