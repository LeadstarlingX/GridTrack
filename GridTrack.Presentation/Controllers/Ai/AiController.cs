using GridTrack.Application.Dtos;
using GridTrack.Application.Errors;
using GridTrack.Application.UseCases.Ai;
using GridTrack.Domain.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

// ReSharper disable RouteTemplates.MethodMissingRouteParameters

namespace GridTrack.Presentation.Controllers.Ai;

[ApiController]
[Route("/api/ai")]
public class AiController(IMessageBus bus) : ControllerBase
{
    [HttpGet("delivery/{id}/recommendation")]
    public async Task<IActionResult> GetDeliveryRecommendation(string id, CancellationToken ct)
    {
        if (!Guid.TryParse(id, out var deliveryId))
            return BadRequest();

        var result = await bus.InvokeAsync<Result<DeliveryRecommendationResponse>>(
            new GetDeliveryRecommendationQuery(deliveryId), ct);

        if (result.IsFailure)
            return result.Error == ApplicationErrors.DeliveryNotFound
                ? NotFound(new { error = result.Error.Message })
                : Problem(result.Error.Message);

        return Ok(result.Value);
    }

    [HttpGet("district-summary/{districtId}")]
    public async Task<IActionResult> GetDistrictSummary(string districtId, CancellationToken ct)
    {
        var result = await bus.InvokeAsync<DistrictSummaryResponse?>(
            new GetDistrictSummaryQuery(districtId), ct);

        return result is null ? NotFound() : Ok(result);
    }
}
