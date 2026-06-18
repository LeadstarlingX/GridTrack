namespace GridTrack.Presentation.Controllers.DistrictGroups;

public sealed record CreateDistrictGroupHttpRequest(string Name, string[] DistrictIds);
public sealed record UpdateDistrictGroupHttpRequest(string Name, string[] DistrictIds);
