using GridTrack.Application.Dtos;

namespace GridTrack.Application.Interfaces;

public interface IDeliveryReadService
{
    Task<DeliveryDto?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IEnumerable<DeliveryDto>> GetByDistrictAsync(string districtId, CancellationToken ct);
}
