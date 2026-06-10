namespace GridTrack.Application.Dtos;

public sealed record DistrictVolumeItemResponse(string DistrictId, int Deliveries);

public sealed record GetDistrictVolumeResponse(IReadOnlyList<DistrictVolumeItemResponse> Items);
