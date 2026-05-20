using GridTrack.Presentation.Controllers.Shared;

namespace GridTrack.Presentation.Controllers.Analytics;

public sealed record H3DensityCell(
    string H3Index,
    Coordinate Center,
    int DeliveryCount);