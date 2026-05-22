
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
        return Host.CreateDefaultBuilder(args)
            .ConfigureWebHostDefaults(webBuilder => { webBuilder.UseStartup<Startup>(); })
            .UseWolverine((ctx, opts) =>
            {
                opts.Discovery.IncludeAssembly(typeof(Application.DependencyInjection).Assembly);

                var rabbit = ctx.Configuration.GetConnectionString("RabbitMq");

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