using GridTrack.Domain.H3Districts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GridTrack.Infrastructure.EntitiesConfigurations;

public sealed class H3DistrictConfiguration : IEntityTypeConfiguration<H3District>
{
    public void Configure(EntityTypeBuilder<H3District> builder)
    {
        builder.HasKey(h => h.H3Index);

        builder.Property(h => h.H3Index)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(h => h.CenterPoint)
            .HasColumnType("geometry (point)")
            .IsRequired();

        // Polygon, not a point — the original "geometry (point)" typmod rejected polygon
        // inserts, which is why H3District was never seeded. Plain "geometry" accepts it.
        builder.Property(h => h.BoundaryPolygon)
            .HasColumnType("geometry")
            .IsRequired();

        builder.Property(h => h.Resolution)
            .IsRequired();

        builder.HasIndex(h => h.Resolution);
    }
}