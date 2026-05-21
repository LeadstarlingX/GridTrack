using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GridTrack.Infrastructure.DbContext;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

        var connectionString = Environment.GetEnvironmentVariable("DefaultConnection")
                               ?? "Host=localhost;Port=5433;Database=gridtrack_docker;User Id=postgres;Password=postgres Security=true;";

        optionsBuilder.UseNpgsql(connectionString, options =>
        {
            options.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
            options.UseNetTopologySuite();
        });

        return new AppDbContext(optionsBuilder.Options);
    }
}