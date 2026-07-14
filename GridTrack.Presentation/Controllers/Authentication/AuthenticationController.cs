using GridTrack.Application.UseCases.Authentication;
using GridTrack.Domain.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace GridTrack.Presentation.Controllers.Authentication;

[ApiController]
[AllowAnonymous]
[Route("/api/auth")]
public sealed class AuthenticationController(IMessageBus bus) : ControllerBase
{
    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromBody] LoginHttpRequest request,
        CancellationToken ct)
    {
        var result = await bus.InvokeAsync<Result<LoginResponse>>(
            new LoginCommand(new LoginRequest(request.Username, request.Password)), ct);

        if (result.IsFailure)
            return Unauthorized(new { error = result.Error.Code, message = result.Error.Message });

        return Ok(result.Value);
    }
}