using GridTrack.Application.Dtos;
using GridTrack.Application.UseCases.DistrictGroups;
using GridTrack.Domain.Abstractions;
using GridTrack.Domain.DistrictGroups;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace GridTrack.Presentation.Controllers.DistrictGroups;

[ApiController]
[Route("/api/district-groups")]
public class DistrictGroupsController(IMessageBus bus) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await bus.InvokeAsync<IReadOnlyList<DistrictGroupDto>>(new GetDistrictGroupsQuery(), ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await bus.InvokeAsync<Result<DistrictGroupDto>>(new GetDistrictGroupByIdQuery(id), ct);
        return result.IsFailure ? NotFound() : Ok(result.Value);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDistrictGroupHttpRequest request, CancellationToken ct)
    {
        var result = await bus.InvokeAsync<Result<DistrictGroupDto>>(
            new CreateDistrictGroupCommand(new CreateDistrictGroupRequest(request.Name, request.DistrictIds)), ct);

        if (result.IsFailure)
            return UnprocessableEntity(new { error = result.Error.Message });

        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateDistrictGroupHttpRequest request, CancellationToken ct)
    {
        var result = await bus.InvokeAsync<Result>(
            new UpdateDistrictGroupCommand(id, new UpdateDistrictGroupRequest(request.Name, request.DistrictIds)), ct);

        if (result.IsFailure)
            return result.Error == DistrictGroupErrors.NotFound
                ? NotFound()
                : UnprocessableEntity(new { error = result.Error.Message });

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await bus.InvokeAsync<Result>(new DeleteDistrictGroupCommand(id), ct);
        return result.IsFailure ? NotFound() : NoContent();
    }
}
