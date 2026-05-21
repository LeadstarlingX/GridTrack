using GridTrack.Domain.Deliveries;

namespace GridTrack.Application.CQRS.Repositories;

public interface IDeliveryRepository
{
    Task<Delivery?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IEnumerable<Delivery>> GetActiveByDistrictAsync(string districtId, CancellationToken ct);
    Task AddAsync(Delivery delivery, CancellationToken ct);
    Task UpdateAsync(Delivery delivery, CancellationToken ct);
}
