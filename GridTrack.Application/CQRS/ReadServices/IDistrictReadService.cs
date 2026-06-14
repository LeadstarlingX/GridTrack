using GridTrack.Application.Dtos;

namespace GridTrack.Application.CQRS.ReadServices;

public interface IDistrictReadService
{
    Task<GetDistrictsResponse> GetDistrictsAsync(CancellationToken ct);
    Task<GetDistrictBoundariesResponse> GetDistrictBoundariesAsync(CancellationToken ct);
    Task<DistrictContextDto> GetDistrictContextAsync(string districtId, CancellationToken ct);
}
