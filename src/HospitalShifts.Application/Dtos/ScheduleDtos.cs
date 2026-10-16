using HospitalShifts.Domain.Enums;
using HospitalShifts.Domain.Scheduling;

namespace HospitalShifts.Application.Dtos;

/// <summary>تصویر کامل تابلوی یک ماه برای صفحه اصلی برنامه.</summary>
public sealed record ScheduleBoardDto(
    MonthDto Month,
    SchedulingContext Context,
    List<AssignmentEntry> Rows,
    List<RuleViolation> Violations,
    List<CoverageGap> Gaps,
    List<SlotKey> LeaderMissing);

/// <summary>گزارش عملیات چیدن خودکار.</summary>
public sealed record GenerateReportDto(int Added, int LeadersSet, List<CoverageGap> Gaps, List<SlotKey> LeaderMissing);

/// <summary>ورودی یک سلول جایگاه هنگام ویرایش دستی نوبت.</summary>
public sealed record SlotAssignmentInput(int LocationId, PersonKind Role, int? PersonnelId);

/// <summary>هویت مدیر پس از ورود موفق.</summary>
public sealed record AdminIdentity(int Id, string Username, bool MustChangePassword);
