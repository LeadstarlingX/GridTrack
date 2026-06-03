using GridTrack.Application.Dtos;
using GridTrack.Application.Interfaces;
using GridTrack.Domain.Drivers;

namespace GridTrack.Application.CQRS.Handlers;

public static class DriverPositionBroadcastHandler
{
    public static Task Handle(
        DriverPositionUpdatedDomainEvent e,
        IDashboardPushService push,
        CancellationToken ct)
        => push.BroadcastDriverPositionAsync(
            e.DistrictId,
            new DriverDto
            {
                DriverId   = e.DriverId,
                Location   = e.Location,
                IsActive   = e.IsActive,
                LastSeen   = e.Timestamp,
                DistrictId = e.DistrictId,
                Name       = e.Name,
                ShortName  = e.ShortName,
            },
            ct);
}
