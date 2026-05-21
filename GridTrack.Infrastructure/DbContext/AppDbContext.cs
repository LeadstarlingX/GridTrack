using GridTrack.Domain.Abstractions;
using GridTrack.Domain.Deliveries;
using GridTrack.Domain.Drivers;
using Microsoft.EntityFrameworkCore;

namespace GridTrack.Infrastructure.DbContext;

public class AppDbContext : Microsoft.EntityFrameworkCore.DbContext, IUnitOfWork
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    
    public DbSet<Delivery> Deliveries => Set<Delivery>();
    public DbSet<Driver> Drivers => Set<Driver>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        
        // builder.HasPostgresExtension("postgis");
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}