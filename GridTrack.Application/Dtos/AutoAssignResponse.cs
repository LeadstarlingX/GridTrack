namespace GridTrack.Application.Dtos;

public sealed record AutoAssignResponse(
    bool AutoAssigned,
    Guid? AssignedDriverId,
    IReadOnlyList<DispatchCandidateDto> TopCandidates);
