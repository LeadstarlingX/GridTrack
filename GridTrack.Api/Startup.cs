using Asp.Versioning.ApiExplorer;
using GridTrack.Api.Extensions;
using GridTrack.Application;
using GridTrack.Infrastructure;
using GridTrack.Presentation;

namespace GridTrack.Api;

public class Startup
{
    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    private IConfiguration Configuration { get; }

    public void ConfigureServices(IServiceCollection services)
    {
        var conn = Configuration.GetConnectionString("DefaultConnection");
        Console.WriteLine($"\n******** Using connection string: {conn} ********\n");

        services.AddApi(Configuration)
            .AddPresentation()
            .AddInfrastructure(Configuration)
            .AddApplication();
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IApiVersionDescriptionProvider provider)
    {
        app.UseSwagger();
        
        
        app.UseSwaggerUI(options =>
        {
            foreach (ApiVersionDescription description in provider.ApiVersionDescriptions)
            {
                string url = $"/swagger/{description.GroupName}/swagger.json";
                string name = description.GroupName.ToUpperInvariant();
                options.SwaggerEndpoint(url, name);
            }
        });
        app.ApplyMigrations();
        
        // app.SeedData();

        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
            
        }

        // app.UseHttpsRedirection();

        app.UseStaticFiles();

        app.UseRouting();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
            
        });
    }
}