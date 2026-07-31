using GridTrack.Application.Dtos;
using GridTrack.Application.UseCases.Authentication;
using GridTrack.Domain.Abstractions;
using GridTrack.Domain.Authentication;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Wolverine;

namespace GridTrack.Presentation.Controllers.Users;

public sealed record UpdateUserSectorsHttpRequest(Guid[] SectorIds);

[ApiController]
[Route("/api/users")]
public class UsersController(IMessageBus bus) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        if (User.FindFirstValue("role") != "GeneralObserver")
            return Forbid();

        var result = await bus.InvokeAsync<IReadOnlyList<UserDto>>(new GetUsersQuery(), ct);
        return Ok(result);
    }

    [HttpPatch("{id:guid}/sectors")]
    public async Task<IActionResult> UpdateSectors(Guid id, [FromBody] UpdateUserSectorsHttpRequest request, CancellationToken ct)
    {
        if (User.FindFirstValue("role") != "GeneralObserver")
            return Forbid();

        var result = await bus.InvokeAsync<Result<UserDto>>(
            new UpdateUserSectorsCommand(id, new UpdateUserSectorsRequest(request.SectorIds)), ct);

        if (result.IsFailure)
        {
            if (result.Error == AppUserErrors.NotFound)           return NotFound();
            if (result.Error == AppUserErrors.CannotAssignSectors) return BadRequest(new { error = result.Error.Message });
            return UnprocessableEntity(new { error = result.Error.Message });
        }

        return Ok(result.Value);
    }
}
