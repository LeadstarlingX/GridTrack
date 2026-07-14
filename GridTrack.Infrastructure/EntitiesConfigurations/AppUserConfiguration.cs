using GridTrack.Domain.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GridTrack.Infrastructure.EntitiesConfigurations;

internal sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.ToTable("app_users");
        builder.HasKey(u => u.UserId);
        builder.Property(u => u.Username).HasMaxLength(100).IsRequired();
        builder.Property(u => u.PasswordHash).IsRequired();
        builder.Property(u => u.Role).HasMaxLength(50).IsRequired();
        builder.Property(u => u.SectorIds).HasColumnType("uuid[]").IsRequired();
        builder.HasIndex(u => u.Username).IsUnique();
    }
}