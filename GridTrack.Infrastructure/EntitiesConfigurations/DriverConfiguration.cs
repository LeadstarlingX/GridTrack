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

        builder.Property(d => d.CarType)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(d => d.LicensePlate)
            .HasMaxLength(20);

        builder.Property(d => d.PhoneNumber)
            .HasMaxLength(20);

        builder.Property(d => d.VehicleCapacityKg)
            .HasColumnType("numeric(10,2)");

        builder.Property(d => d.ShiftStartedAt);

        builder.Property(d => d.ShiftEndsAt);

        builder.HasIndex(d => d.IsActive);

        builder.HasIndex(d => d.DistrictId);

        builder.HasIndex(d => d.LicensePlate)
            .IsUnique()
            .HasFilter("\"LicensePlate\" IS NOT NULL");
    }
}