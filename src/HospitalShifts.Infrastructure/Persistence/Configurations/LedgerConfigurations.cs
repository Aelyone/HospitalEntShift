using HospitalShifts.Domain.Entities;
using HospitalShifts.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HospitalShifts.Infrastructure.Persistence.Configurations;

public sealed class MonthlyLedgerConfiguration : IEntityTypeConfiguration<MonthlyLedger>
{
    public void Configure(EntityTypeBuilder<MonthlyLedger> e)
    {
        e.ToTable("monthly_ledgers");
        e.HasKey(x => new { x.MonthId, x.PersonnelId });
        e.Property(x => x.MonthId).HasColumnName("month_id");
        e.Property(x => x.PersonnelId).HasColumnName("personnel_id");
        e.Property(x => x.Share).HasColumnName("share");
        e.Property(x => x.Opening).HasColumnName("opening");
        e.Property(x => x.Target).HasColumnName("target");
        e.Property(x => x.Worked).HasColumnName("worked");
        e.Property(x => x.Forgiveness).HasColumnName("forgiveness");
        e.Property(x => x.Closing).HasColumnName("closing");
        e.Property(x => x.Status).HasColumnName("status").HasMaxLength(20)
            .HasConversion<MonthStatusToCodeConverter>();
        e.Property(x => x.ForgivenessReason).HasColumnName("forgiveness_reason").HasMaxLength(300);
        e.HasOne<ScheduleMonth>().WithMany().HasForeignKey(x => x.MonthId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne<Personnel>().WithMany().HasForeignKey(x => x.PersonnelId).OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class LedgerAdjustmentConfiguration : IEntityTypeConfiguration<LedgerAdjustment>
{
    public void Configure(EntityTypeBuilder<LedgerAdjustment> e)
    {
        e.ToTable("ledger_adjustments");
        e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.MonthId).HasColumnName("month_id");
        e.Property(x => x.PersonnelId).HasColumnName("personnel_id");
        e.Property(x => x.Amount).HasColumnName("amount");
        e.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(300);
        e.Property(x => x.CreatedAt).HasColumnName("created_at").HasMaxLength(40)
            .HasConversion<DateTimeOffsetToIsoStringConverter>();
        e.HasOne(x => x.Personnel).WithMany().HasForeignKey(x => x.PersonnelId).OnDelete(DeleteBehavior.NoAction);
        e.HasOne<ScheduleMonth>().WithMany().HasForeignKey(x => x.MonthId).OnDelete(DeleteBehavior.NoAction);
    }
}
