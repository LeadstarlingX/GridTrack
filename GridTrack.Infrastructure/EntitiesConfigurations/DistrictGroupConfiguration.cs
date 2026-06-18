using GridTrack.Domain.DistrictGroups;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GridTrack.Infrastructure.EntitiesConfigurations;

internal sealed class DistrictGroupConfiguration : IEntityTypeConfiguration<DistrictGroup>
{
    public void Configure(EntityTypeBuilder<DistrictGroup> builder)
    {
        builder.ToTable("district_groups");

        builder.HasKey(g => g.Id);

        builder.Property(g => g.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(g => g.DistrictIds)
            .HasColumnType("text[]")
            .IsRequired();
    }
}
