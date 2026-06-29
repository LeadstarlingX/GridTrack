using GridTrack.Application.Abstractions.Cache;
using GridTrack.Application.Abstractions.Telemetry;
using GridTrack.Application.CQRS.ReadServices;
using GridTrack.Application.Errors;
using GridTrack.Domain.Abstractions;
using GridTrack.Domain.Drivers;
using NetTopologySuite.Geometries;

namespace GridTrack.Application.UseCases.Drivers;

public sealed record UpdatePositionRequest(Guid DriverId, Point Location, DateTime Timestamp);

public sealed record UpdateDriverPositionCommand(UpdatePositionRequest Request);

public sealed class UpdateDriverPositionHandler
{
    // Metadata is stable between ticks (name, districtId, isActive rarely change).
    // Cache it for 10 minutes so the per-tick path never touches Postgres.
    private static string MetaKey(Guid id) => $"driver:meta:{id}";

    public async Task<(Result Result, IEnumerable<object> Events)> Handle(
        UpdateDriverPositionCommand command,
        IDriverReadService readService,
        ICacheService cache,
        IPositionWriteBuffer writeBuffer,
        IPositionStreamPublisher streamPublisher,
        CancellationToken ct)
    {
        var req = command.Request;

        var metadata = await cache.GetAsync<CachedDriverMetadata>(MetaKey(req.DriverId), ct);

        if (metadata is null)
        {
            var driver = await readService.GetAggregateByIdAsync(req.DriverId, ct);
            if (driver is null)
                return (Result.Failure(ApplicationErrors.DriverNotFound), Array.Empty<object>());

            metadata = new CachedDriverMetadata(driver.Name, driver.ShortName, driver.DistrictId, driver.IsActive);
            await cache.SetAsync(MetaKey(req.DriverId), metadata, TimeSpan.FromMinutes(10), ct);
        }

        writeBuffer.Write(req.DriverId, lat: req.Location.Y, lng: req.Location.X, metadata.DistrictId, req.Timestamp);

        await streamPublisher.PublishAsync(
            req.DriverId,
            lat: req.Location.Y, lng: req.Location.X,
            metadata.DistrictId,
            metadata.Name, metadata.ShortName, metadata.IsActive,
            req.Timestamp, ct);

        var positionEvent = new DriverPositionUpdatedDomainEvent(
            req.DriverId, req.Location, req.Timestamp,
            metadata.DistrictId, metadata.Name, metadata.ShortName, metadata.IsActive);

        // Throttle ETA recalculation to once every 30 s per driver to avoid hammering OSRM.
        var events = new List<object> { positionEvent };
        var etaThrottleKey = $"eta:recalc:{req.DriverId}";
        if (await cache.GetAsync<bool?>(etaThrottleKey, ct) is null)
        {
            await cache.SetAsync(etaThrottleKey, true, TimeSpan.FromSeconds(30), ct);
            events.Add(new RecalculateDeliveryEtaMessage(req.DriverId, req.Location.Y, req.Location.X));
        }

        return (Result.Success(), events);
    }
}
