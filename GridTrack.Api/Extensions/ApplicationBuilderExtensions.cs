using GridTrack.Infrastructure.DbContext;
using Microsoft.EntityFrameworkCore;

namespace GridTrack.Api.Extensions;

public static class ApplicationBuilderExtensions
{
    public static void ApplyMigrations(this IApplicationBuilder app)
    {
        using IServiceScope scope = app.ApplicationServices.CreateScope();

        using var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        dbContext.Database.Migrate();
    }
}