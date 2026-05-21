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
            .HasColumnType("geometry(Point,4326)")
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
        

        builder.Property(d => d.AnomalyReason)
            .HasMaxLength(500);

        builder.HasIndex(d => d.Status);

        builder.HasIndex(d => d.DistrictId);

        builder.HasIndex(d => d.AssignedDriverId);
        
    }
}