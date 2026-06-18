using GridTrack.Domain.Abstractions;
using GridTrack.Domain.Deliveries;
using GridTrack.Domain.DistrictGroups;
using GridTrack.Domain.Drivers;
using GridTrack.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GridTrack.Infrastructure.DbContext;

public class AppDbContext : Microsoft.EntityFrameworkCore.DbContext, IUnitOfWork
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Delivery> Deliveries => Set<Delivery>();
    public DbSet<Driver> Drivers => Set<Driver>();
    public DbSet<DeliveryRoute> DeliveryRoutes => Set<DeliveryRoute>();
    public DbSet<DistrictGroup> DistrictGroups => Set<DistrictGroup>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // builder.HasPostgresExtension("postgis");
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        builder.Entity<DeliveryRoute>(b =>
        {
            b.ToTable("delivery_routes");
            b.HasKey(r => new { r.DeliveryId, r.Sequence });
            b.Property(r => r.Lat).HasColumnType("double precision");
            b.Property(r => r.Lng).HasColumnType("double precision");
            b.HasIndex(r => r.DeliveryId);
        });
    }
}