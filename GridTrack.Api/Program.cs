
using GridTrack.Application.IntegrationEvents;
using Wolverine;
using Wolverine.RabbitMQ;

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
        string? rabbit = null;
        
        return Host.CreateDefaultBuilder(args)
            .ConfigureServices((ctx, _) =>
            {
                rabbit = ctx.Configuration.GetConnectionString("RabbitMq");
            })
            .ConfigureWebHostDefaults(webBuilder => { webBuilder.UseStartup<Startup>(); })
            .UseWolverine((opts) =>
            {
                opts.Discovery.IncludeAssembly(typeof(Application.DependencyInjection).Assembly);

                if (!string.IsNullOrWhiteSpace(rabbit))
                {
                    opts.UseRabbitMq(new Uri(rabbit))
                        .AutoProvision();

                    // ── Outbound: .NET → Python ─────────────────────────────
                    opts.PublishMessage<DeliveryAnomalyIntegrationEvent>()
                        .ToRabbitExchange("gridtrack.anomaly",
                            e => e.ExchangeType = ExchangeType.Fanout);

                    opts.PublishMessage<DriverPositionIntegrationEvent>()
                        .ToRabbitExchange("gridtrack.positions",
                            e => e.ExchangeType = ExchangeType.Fanout);

                    opts.PublishMessage<DeliveryCompletedIntegrationEvent>()
                        .ToRabbitExchange("gridtrack.completions",
                            e => e.ExchangeType = ExchangeType.Fanout);

                    // ── Inbound: Python → .NET ───────────────────────────────
                    opts.ListenToRabbitQueue("gridtrack.urgency-results");
                    opts.ListenToRabbitQueue("gridtrack.forecast-results");
                }
            });
    }
}