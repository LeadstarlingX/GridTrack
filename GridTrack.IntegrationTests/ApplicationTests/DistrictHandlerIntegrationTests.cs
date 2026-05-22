using FluentAssertions;
using GridTrack.Application.Dtos;
using GridTrack.Application.UseCases.Districts;
using GridTrack.IntegrationTests.Abstractions;

namespace GridTrack.IntegrationTests.ApplicationTests;

public class DistrictHandlerIntegrationTests : BaseIntegrationTest
{
    [Test]
    [NotInParallel(Order = 600)]
    public async Task GetDistrictsQuery_Should_Return_Empty_When_No_Districts()
    {
        await ResetDatabaseAsync();

        var result = await InvokeAsync<GetDistrictsResponse>(new GetDistrictsQuery());

        result.Should().NotBeNull();
        result.Items.Should().BeEmpty();
    }

    [Test]
    [NotInParallel(Order = 601)]
    public async Task GetDistrictBoundariesQuery_Should_Return_Empty_FeatureCollection_When_No_Districts()
    {
        await ResetDatabaseAsync();

        var result = await InvokeAsync<GetDistrictBoundariesResponse>(new GetDistrictBoundariesQuery());

        result.Should().NotBeNull();
        result.Type.Should().Be("FeatureCollection");
        result.Features.Should().BeEmpty();
    }
}
