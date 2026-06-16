namespace GridTrack.Application.Interfaces;

public interface IDistrictDataService
{
    IReadOnlyList<DistrictInfo> GetAll();
    DistrictInfo? GetById(string id);
    DistrictInfo GetRandom(string? exceptId = null);
}

public sealed record DistrictInfo(
    string Id,
    string NameAr,
    double CentroidLat,
    double CentroidLng,
    double JitterRadius);
