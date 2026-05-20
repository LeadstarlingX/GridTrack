namespace GridTrack.Presentation.Controllers.Forcast;

public class ForecastResponse (
    string DistrictId,
    int ForecastedDemand,
    string Horizon,
    int DriverRecommendation,
    double StaffingRatio,
    DateTime UpdatedAt);