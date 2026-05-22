
using Wolverine;

namespace GridTrack.Api;

using GridTrack.Api;

public partial class Program
{
    public static void Main(string[] args)
    {
        CreateHostBuilder(args).Build().Run();
    }

    private static IHostBuilder CreateHostBuilder(string[] args)
    {
        return Host.CreateDefaultBuilder(args)
            .ConfigureWebHostDefaults(webBuilder => { webBuilder.UseStartup<Startup>(); })
            .UseWolverine(options =>
            {
                options.Discovery.IncludeAssembly(typeof(Application.DependencyInjection).Assembly);
            });
    }
}