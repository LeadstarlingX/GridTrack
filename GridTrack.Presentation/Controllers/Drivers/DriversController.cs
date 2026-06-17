using GridTrack.Application.Dtos;
using GridTrack.Application.Errors;
using GridTrack.Application.UseCases.Common;
using GridTrack.Application.UseCases.Drivers;
using GridTrack.Domain.Abstractions;
using Microsoft.AspNetCore.Mvc;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using Wolverine;

namespace GridTrack.Presentation.Controllers.Drivers;

[ApiController]
[Route("/api/drivers")]
public class DriversController(IMessageBus bus) : ControllerBase
{
    private static readonly GeometryFactory GeoFactory =
        NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

    // ── Queries ───────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> GetDrivers([FromQuery] GetDriversRequest request, CancellationToken ct)
    {
        var result = await bus.InvokeAsync<GetDriversResponse>(
            new GetDriversQuery(request.Cursor, request.DistrictId, request.Status, request.PageSize ?? 8), ct);
        return Ok(result);
    }

    [HttpGet("nearest")]
    public async Task<IActionResult> GetNearestDrivers(
        [FromQuery] double lat, [FromQuery] double lng,
        [FromQuery] int count = 5, CancellationToken ct = default)
    {
        var location = GeoFactory.CreatePoint(new Coordinate(lng, lat));
        var result = await bus.InvokeAsync<Result<IEnumerable<DriverDto>>>(
            new GetNearestDriversQuery(new NearestDriversRequest(location, count)), ct);
        return Ok(result.Value.Select(DriverSummaryResponse.From));
    }

    [HttpGet("by-district/{districtId}")]
    public async Task<IActionResult> GetDriversByDistrict(string districtId, CancellationToken ct)
    {
        var result = await bus.InvokeAsync<Result<IEnumerable<DriverDto>>>(
            new GetDriversByDistrictQuery(new DistrictFilterRequest(districtId)), ct);
        return Ok(result.Value.Select(DriverSummaryResponse.From));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetDriverDetail(string id, CancellationToken ct)
    {
        if (!Guid.TryParse(id, out var driverId))
            return BadRequest();
        var result = await bus.InvokeAsync<DriverDetailResponse?>(new GetDriverDetailQuery(driverId), ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("{id}/stats")]
    public async Task<IActionResult> GetDriverStats(string id, CancellationToken ct)
    {
        if (!Guid.TryParse(id, out var driverId))
            return BadRequest();
        var result = await bus.InvokeAsync<DriverStatsResponse?>(new GetDriverStatsQuery(driverId), ct);
        return result is null ? NotFound() : Ok(result);
    }

    // ── Commands ──────────────────────────────────────────────────────────

    [HttpPost]
    public async Task<IActionResult> CreateDriver([FromBody] CreateDriverHttpRequest request, CancellationToken ct)
    {
        var driverId = Guid.NewGuid();
        var location = GeoFactory.CreatePoint(new Coordinate(request.Lng, request.Lat));
        var result = await bus.InvokeAsync<Result<DriverDto>>(
            new CreateDriverCommand(new CreateDriverRequest(
                driverId, location, H3Resolution: 9, request.DistrictId,
                request.Name, request.ShortName, request.IsActive)),
            ct);

        if (result.IsFailure)
            return UnprocessableEntity(new { error = result.Error.Message });

        return CreatedAtAction(nameof(GetDriverDetail), new { id = result.Value.DriverId },
            DriverSummaryResponse.From(result.Value));
    }

    [HttpPatch("{id}/availability")]
    public async Task<IActionResult> UpdateDriverAvailability(
        [FromRoute] string id,
        [FromBody] UpdateDriverAvailabilityRequest request,
        CancellationToken ct)
    {
        if (!Guid.TryParse(id, out var driverId))
            return BadRequest();
        var isActive = request.Status.Equals("available", StringComparison.OrdinalIgnoreCase);
        var result = await bus.InvokeAsync<Result<DriverAvailabilityResponse>>(
            new ToggleDriverAvailabilityCommand(driverId, isActive), ct);
        if (result.IsFailure)
            return result.Error == ApplicationErrors.DriverNotFound
                ? NotFound()
                : Conflict(new { error = result.Error.Message });
        return Ok(result.Value);
    }
}
