using HospitalShifts.Domain.Entities;
using HospitalShifts.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HospitalShifts.Infrastructure.Persistence.Configurations;

public sealed class ShiftTypeConfiguration : IEntityTypeConfiguration<ShiftType>
{
    public void Configure(EntityTypeBuilder<ShiftType> e)
    {
        e.ToTable("shift_types");
        e.HasKey(x => x.Code);
        e.Property(x => x.Code).HasColumnName("code").HasMaxLength(30);
        e.Property(x => x.Name).HasColumnName("name").HasMaxLength(100);
        e.Property(x => x.Units).HasColumnName("units");
        e.Property(x => x.AutoGenerate).HasColumnName("auto_generate");
        e.HasMany(x => x.Segments).WithOne().HasForeignKey(s => s.ShiftCode).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ShiftSegmentConfiguration : IEntityTypeConfiguration<ShiftSegment>
{
    public void Configure(EntityTypeBuilder<ShiftSegment> e)
    {
        e.ToTable("shift_segments");
        e.HasKey(x => new { x.ShiftCode, x.Position });
        e.Property(x => x.ShiftCode).HasColumnName("shift_code").HasMaxLength(30);
        e.Property(x => x.Position).HasColumnName("position");
        e.Property(x => x.Slot).HasColumnName("slot").HasMaxLength(20)
            .HasConversion<SlotToCodeConverter>();
    }
}
