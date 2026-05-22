using GridTrack.Domain.Drivers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GridTrack.Infrastructure.EntitiesConfigurations;

public sealed class DriverConfiguration : IEntityTypeConfiguration<Driver>
{
    public void Configure(EntityTypeBuilder<Driver> builder)
    {
        builder.HasKey(d => d.DriverId);

        builder.Property(d => d.DriverId)
            .IsRequired();

        builder.Property(d => d.Location)
            .HasColumnType("geometry (point)")
            .IsRequired();

        builder.Property(d => d.IsActive)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(d => d.LastSeen)
            .IsRequired();

        builder.Property(d => d.DistrictId)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(d => d.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(d => d.ShortName)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(d => d.IsActive);

        builder.HasIndex(d => d.DistrictId);
    }
}