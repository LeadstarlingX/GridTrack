using System.Security.Claims;
using GridTrack.Application.Abstractions.Authentication;
using Microsoft.AspNetCore.Http;

namespace GridTrack.Infrastructure.Authentication;

internal sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? User => accessor.HttpContext?.User;

    public Guid UserId =>
        Guid.TryParse(User?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;

    public string Role => User?.FindFirstValue("role") ?? string.Empty;

    // When there is no HTTP context (background jobs, Wolverine consumers) treat as
    // GeneralObserver so background processing never gets silently filtered.
    public bool IsGeneralObserver =>
        accessor.HttpContext is null ||
        string.IsNullOrEmpty(Role) ||
        Role == "GeneralObserver";

    public IReadOnlyList<string>? AllowedDistrictIds =>
        IsGeneralObserver
            ? null
            : User?.FindAll("districtId").Select(c => c.Value).ToList() ?? [];

    public IReadOnlyList<Guid>? AllowedSectorIds =>
        IsGeneralObserver
            ? null
            : User?.FindAll("sectorId")
                .Select(c => Guid.TryParse(c.Value, out var g) ? g : Guid.Empty)
                .Where(g => g != Guid.Empty)
                .ToList();
}