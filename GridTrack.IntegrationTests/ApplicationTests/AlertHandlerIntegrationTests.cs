using FluentAssertions;
using GridTrack.Application.Dtos;
using GridTrack.Application.UseCases.Alerts;
using GridTrack.Domain.Abstractions;
using GridTrack.IntegrationTests.Abstractions;

namespace GridTrack.IntegrationTests.ApplicationTests;

public class AlertHandlerIntegrationTests : BaseIntegrationTest
{
    [Test]
    [NotInParallel(Order = 300)]
    public async Task GetEtaAnomalyAlertsQuery_Should_Return_Anomalies_For_Districts()
    {
        await ResetDatabaseAsync();

        var query = new GetEtaAnomalyAlertsQuery(new AnomalyFilterRequest(new[] { "h3-district-1", "h3-district-2" }));
        var result = await InvokeAsync<Result<IEnumerable<AnomalyAlertDto>>>(query);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
    }
}
