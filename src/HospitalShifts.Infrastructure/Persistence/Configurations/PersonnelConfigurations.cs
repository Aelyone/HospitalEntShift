using HospitalShifts.Domain.Entities;
using HospitalShifts.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HospitalShifts.Infrastructure.Persistence.Configurations;

public sealed class PersonnelConfiguration : IEntityTypeConfiguration<Personnel>
{
    public void Configure(EntityTypeBuilder<Personnel> e)
    {
        e.ToTable("personnel", t => t.HasCheckConstraint("CK_personnel_type", "type IN ('official','soldier')"));
        e.Ignore(x => x.FullName);
        e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.Type).HasColumnName("type").HasMaxLength(10)
            .HasConversion<PersonKindToCodeConverter>();
        e.Property(x => x.FirstName).HasColumnName("first_name").HasMaxLength(100);
        e.Property(x => x.LastName).HasColumnName("last_name").HasMaxLength(100);
        e.Property(x => x.Phone).HasColumnName("phone").HasMaxLength(30);
        e.Property(x => x.Address).HasColumnName("address").HasMaxLength(300);
        e.Property(x => x.Department).HasColumnName("department").HasMaxLength(100);
        e.Property(x => x.BaseShare).HasColumnName("base_share");
        e.Property(x => x.InitialBalance).HasColumnName("initial_balance");
        e.Property(x => x.Active).HasColumnName("active").HasDefaultValue(true);
        e.Property(x => x.CanLeader).HasColumnName("can_leader");
        e.Property(x => x.LeaderPriority).HasColumnName("leader_priority");
        e.Property(x => x.Level).HasColumnName("level").HasMaxLength(50);
        e.Property(x => x.CreatedAt).HasColumnName("created_at").HasMaxLength(40)
            .HasConversion<DateTimeOffsetToIsoStringConverter>();
        e.HasMany(x => x.Allowed).WithOne().HasForeignKey(a => a.PersonnelId).OnDelete(DeleteBehavior.Cascade);
        e.HasMany(x => x.Rest).WithOne().HasForeignKey(r => r.PersonnelId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class AllowedShiftConfiguration : IEntityTypeConfiguration<AllowedShift>
{
    public void Configure(EntityTypeBuilder<AllowedShift> e)
    {
        e.ToTable("personnel_allowed_shifts");
        e.HasKey(x => new { x.PersonnelId, x.ShiftCode });
        e.Property(x => x.PersonnelId).HasColumnName("personnel_id");
        e.Property(x => x.ShiftCode).HasColumnName("shift_code").HasMaxLength(30);
        e.HasOne<ShiftType>().WithMany().HasForeignKey(x => x.ShiftCode).OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class RestRuleConfiguration : IEntityTypeConfiguration<RestRule>
{
    public void Configure(EntityTypeBuilder<RestRule> e)
    {
        e.ToTable("rest_rules");
        e.HasKey(x => new { x.PersonnelId, x.ShiftCode });
        e.Property(x => x.PersonnelId).HasColumnName("personnel_id");
        e.Property(x => x.ShiftCode).HasColumnName("shift_code").HasMaxLength(30);
        e.Property(x => x.RestHours).HasColumnName("rest_hours");
        e.HasOne<ShiftType>().WithMany().HasForeignKey(x => x.ShiftCode).OnDelete(DeleteBehavior.NoAction);
    }
}
