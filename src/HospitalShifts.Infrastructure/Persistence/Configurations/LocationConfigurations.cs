using HospitalShifts.Domain.Entities;
using HospitalShifts.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HospitalShifts.Infrastructure.Persistence.Configurations;

public sealed class LocationConfiguration : IEntityTypeConfiguration<Location>
{
    public void Configure(EntityTypeBuilder<Location> e)
    {
        e.ToTable("locations");
        e.Property(x => x.Id).HasColumnName("id");
        e.Property(x => x.Name).HasColumnName("name").HasMaxLength(150);
        e.Property(x => x.Active).HasColumnName("active").HasDefaultValue(true);
        e.Property(x => x.Sort).HasColumnName("sort");
        e.HasMany(x => x.Requirements).WithOne().HasForeignKey(r => r.LocationId).OnDelete(DeleteBehavior.Cascade);
        e.HasMany(x => x.Closures).WithOne().HasForeignKey(c => c.LocationId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class LocationRequirementConfiguration : IEntityTypeConfiguration<LocationRequirement>
{
    public void Configure(EntityTypeBuilder<LocationRequirement> e)
    {
        e.ToTable("location_requirements");
        e.HasKey(x => new { x.LocationId, x.Slot });
        e.Property(x => x.LocationId).HasColumnName("location_id");
        e.Property(x => x.Slot).HasColumnName("slot").HasMaxLength(20)
            .HasConversion<SlotToCodeConverter>();
        e.Property(x => x.NeedsOfficial).HasColumnName("needs_official");
        e.Property(x => x.NeedsSoldier).HasColumnName("needs_soldier");
    }
}

public sealed class LocationClosureConfiguration : IEntityTypeConfiguration<LocationClosure>
{
    public void Configure(EntityTypeBuilder<LocationClosure> e)
    {
        e.ToTable("location_closures");
        e.HasKey(x => new { x.LocationId, x.Date });
        e.Property(x => x.LocationId).HasColumnName("location_id");
        e.Property(x => x.Date).HasColumnName("date").HasMaxLength(10)
            .HasConversion<DateOnlyToIsoStringConverter>();
    }
}
