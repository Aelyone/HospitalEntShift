using HospitalShifts.Domain.Entities;
using HospitalShifts.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HospitalShifts.Infrastructure.Persistence.Configurations;

public sealed class AdminConfiguration : IEntityTypeConfiguration<Admin>
{
    public void Configure(EntityTypeBuilder<Admin> e)
    {
        e.ToTable("admins");
        e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.Username).HasColumnName("username").HasMaxLength(50);
        e.Property(x => x.PasswordHash).HasColumnName("password_hash").HasMaxLength(400);
        e.Property(x => x.MustChange).HasColumnName("must_change");
        e.Property(x => x.FailedCount).HasColumnName("failed_count");
        e.Property(x => x.LockedUntil).HasColumnName("locked_until");
        e.Property(x => x.CreatedAt).HasColumnName("created_at").HasMaxLength(40)
            .HasConversion<DateTimeOffsetToIsoStringConverter>();
        e.HasIndex(x => x.Username).IsUnique();
    }
}

public sealed class SettingConfiguration : IEntityTypeConfiguration<Setting>
{
    public void Configure(EntityTypeBuilder<Setting> e)
    {
        e.ToTable("settings");
        e.HasKey(x => x.Key);
        e.Property(x => x.Key).HasColumnName("key").HasMaxLength(100);
        e.Property(x => x.Value).HasColumnName("value").HasMaxLength(400);
    }
}

public sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> e)
    {
        e.ToTable("audit_log");
        e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.At).HasColumnName("at").HasMaxLength(40)
            .HasConversion<DateTimeOffsetToIsoStringConverter>();
        e.Property(x => x.Actor).HasColumnName("actor").HasMaxLength(50);
        e.Property(x => x.Action).HasColumnName("action").HasMaxLength(50);
        e.Property(x => x.Detail).HasColumnName("detail");
    }
}
