using HospitalShifts.Domain.Entities;
using HospitalShifts.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HospitalShifts.Infrastructure.Persistence.Configurations;

public sealed class ScheduleMonthConfiguration : IEntityTypeConfiguration<ScheduleMonth>
{
    public void Configure(EntityTypeBuilder<ScheduleMonth> e)
    {
        e.ToTable("schedule_months");
        e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.Jy).HasColumnName("jy");
        e.Property(x => x.Jm).HasColumnName("jm");
        e.Property(x => x.StartDate).HasColumnName("start_date").HasMaxLength(10)
            .HasConversion<DateOnlyToIsoStringConverter>();
        e.Property(x => x.EndDate).HasColumnName("end_date").HasMaxLength(10)
            .HasConversion<DateOnlyToIsoStringConverter>();
        e.Property(x => x.Status).HasColumnName("status").HasMaxLength(10)
            .HasConversion<MonthStatusToCodeConverter>();
        e.Property(x => x.SettledAt).HasColumnName("settled_at").HasMaxLength(40)
            .HasConversion<NullableDateTimeOffsetToIsoStringConverter>();
        e.HasIndex(x => x.StartDate).IsUnique();
        e.HasIndex(x => new { x.Jy, x.Jm }).IsUnique();
    }
}

public sealed class AssignmentConfiguration : IEntityTypeConfiguration<Assignment>
{
    public void Configure(EntityTypeBuilder<Assignment> e)
    {
        e.ToTable("assignments");
        e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.Date).HasColumnName("date").HasMaxLength(10)
            .HasConversion<DateOnlyToIsoStringConverter>();
        e.Property(x => x.Slot).HasColumnName("slot").HasMaxLength(20)
            .HasConversion<SlotToCodeConverter>();
        e.Property(x => x.LocationId).HasColumnName("location_id");
        e.Property(x => x.Role).HasColumnName("role").HasMaxLength(10)
            .HasConversion<PersonKindToCodeConverter>();
        e.Property(x => x.PersonnelId).HasColumnName("personnel_id");
        e.Property(x => x.IsShiftLeader).HasColumnName("is_shift_leader");
        e.HasIndex(x => x.Date);
        e.HasIndex(x => new { x.PersonnelId, x.Date, x.Slot }).IsUnique();
        e.HasIndex(x => new { x.Date, x.Slot, x.LocationId, x.Role }).IsUnique();
        e.HasOne<Location>().WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.NoAction);
        e.HasOne<Personnel>().WithMany().HasForeignKey(x => x.PersonnelId).OnDelete(DeleteBehavior.NoAction);
    }
}
