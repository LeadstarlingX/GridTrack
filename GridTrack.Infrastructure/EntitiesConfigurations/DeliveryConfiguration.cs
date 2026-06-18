using GridTrack.Domain.Deliveries;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GridTrack.Infrastructure.EntitiesConfigurations;

public sealed class DeliveryConfiguration : IEntityTypeConfiguration<Delivery>
{
    public void Configure(EntityTypeBuilder<Delivery> builder)
    {

        builder.HasKey(d => d.DeliveryId);

        builder.Property(d => d.DeliveryId)
            .IsRequired();

        builder.Property(d => d.CurrentLocation)
            .HasColumnType("geometry (point)")
            .IsRequired();

        builder.Property(d => d.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(d => d.DistrictId)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(d => d.AnomalyFlag)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(d => d.CreatedAt)
            .IsRequired();
        

        builder.Property(d => d.AnomalyTypeValue)
            .HasConversion<int>()
            .IsRequired(false);

        builder.Property(d => d.AnomalyReason)
            .HasMaxLength(500);

        builder.Property(d => d.UrgencyScore)
            .IsRequired(false);

        builder.Property(d => d.UrgencyScoreAt)
            .IsRequired(false);

        builder.Property(d => d.RouteDistanceMeters)
            .IsRequired(false);

        builder.Property(d => d.RouteDurationSeconds)
            .IsRequired(false);

        builder.Property(d => d.RouteCost)
            .HasColumnType("numeric(12,2)")
            .IsRequired(false);

        builder.HasIndex(d => d.Status);

        builder.HasIndex(d => d.DistrictId);

        builder.HasIndex(d => d.AssignedDriverId);

    }
}