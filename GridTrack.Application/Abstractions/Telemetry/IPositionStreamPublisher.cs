namespace GridTrack.Application.Abstractions.Telemetry;

public interface IPositionStreamPublisher
{
    ValueTask PublishAsync(
        Guid driverId,
        double lat, double lng,
        string districtId,
        string name, string shortName, bool isActive,
        DateTime ts,
        CancellationToken ct);
}
