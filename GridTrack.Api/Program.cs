using GridTrack.Application.IntegrationEvents;
using Wolverine;
using Wolverine.RabbitMQ;

namespace GridTrack.Api;

using GridTrack.Api;

public class Program
{
    public static void Main(string[] args)
    {
        CreateHostBuilder(args).Build().Run();
    }

    public static IHostBuilder CreateHostBuilder(string[] args)
    {
        string? rabbit = null;

        return Host.CreateDefaultBuilder(args)
            .ConfigureServices((ctx, _) =>
            {
                rabbit = ctx.Configuration.GetConnectionString("Queue");
            })
            .ConfigureWebHostDefaults(webBuilder => { webBuilder.UseStartup<Startup>(); })
            .UseWolverine(opts =>
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
                    // Python publishes with the "message-type" AMQP header set to
                    // the lowercased full .NET type name. DefaultIncomingMessage<T>()
                    // guarantees routing even when Wolverine can't resolve the header
                    // from its type registry (cross-language header format mismatch).
                    // This is the recommended Wolverine API for non-Wolverine senders.
                    opts.ListenToRabbitQueue("gridtrack.urgency-results")
                        .DefaultIncomingMessage<UrgencyResultMessage>();
                    opts.ListenToRabbitQueue("gridtrack.forecast-results")
                        .DefaultIncomingMessage<ForecastResultMessage>();
                }
            });
    }
}
