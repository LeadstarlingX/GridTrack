using GridTrack.Domain.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace GridTrack.Infrastructure.DbContext;

public class AppDbContext : Microsoft.EntityFrameworkCore.DbContext, IUnitOfWork
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    


    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}