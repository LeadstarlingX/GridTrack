using FluentAssertions;
using GridTrack.Application.Dtos;
using GridTrack.Application.UseCases.Forecast;
using GridTrack.Domain.Abstractions;
using GridTrack.IntegrationTests.Abstractions;

namespace GridTrack.IntegrationTests.ApplicationTests;

public class ForecastHandlerIntegrationTests : BaseIntegrationTest
{
    [Test]
    [NotInParallel(Order = 400)]
    public async Task FetchDistrictForecastCommand_Should_Return_Forecast_From_Service()
    {
        await ResetDatabaseAsync();

        var command = new FetchDistrictForecastCommand(new ForecastRequest(
            DistrictId: "h3-district-1",
            ForecastWindow: DateTime.UtcNow.AddHours(24)));

        var result = await InvokeAsync<Result<ForecastDto>>(command);

        result.Should().NotBeNull();
    }
}
