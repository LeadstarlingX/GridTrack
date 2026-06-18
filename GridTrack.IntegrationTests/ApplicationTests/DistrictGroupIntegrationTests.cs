using FluentAssertions;
using GridTrack.Application.Dtos;
using GridTrack.Application.UseCases.DistrictGroups;
using GridTrack.Domain.Abstractions;
using GridTrack.Domain.DistrictGroups;
using GridTrack.Infrastructure.Hubs;
using GridTrack.IntegrationTests.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace GridTrack.IntegrationTests.ApplicationTests;

public class DistrictGroupIntegrationTests : BaseIntegrationTest
{
    [Test]
    [NotInParallel(Order = 700)]
    public async Task Create_Should_Persist_Group_And_Return_Dto()
    {
        await ResetDatabaseAsync();

        var result = await InvokeAsync<Result<DistrictGroupDto>>(
            new CreateDistrictGroupCommand(new CreateDistrictGroupRequest(
                "Damascus Central", ["mezzeh", "malki", "baramkeh"])));

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Damascus Central");
        result.Value.DistrictIds.Should().BeEquivalentTo(["mezzeh", "malki", "baramkeh"]);

        var all = await InvokeAsync<IReadOnlyList<DistrictGroupDto>>(new GetDistrictGroupsQuery());
        all.Should().HaveCount(1);
        all[0].Id.Should().Be(result.Value.Id);
    }

    [Test]
    [NotInParallel(Order = 701)]
    public async Task GetById_Should_Return_Group()
    {
        await ResetDatabaseAsync();

        var created = await InvokeAsync<Result<DistrictGroupDto>>(
            new CreateDistrictGroupCommand(new CreateDistrictGroupRequest(
                "Outer Ring", ["kafr-sousa", "daraya"])));

        var found = await InvokeAsync<Result<DistrictGroupDto>>(
            new GetDistrictGroupByIdQuery(created.Value.Id));

        found.IsSuccess.Should().BeTrue();
        found.Value.Name.Should().Be("Outer Ring");
    }

    [Test]
    [NotInParallel(Order = 702)]
    public async Task Update_Should_Modify_Name_And_Districts()
    {
        await ResetDatabaseAsync();

        var created = await InvokeAsync<Result<DistrictGroupDto>>(
            new CreateDistrictGroupCommand(new CreateDistrictGroupRequest(
                "Old Name", ["d1"])));

        var update = await InvokeAsync<Result>(
            new UpdateDistrictGroupCommand(created.Value.Id,
                new UpdateDistrictGroupRequest("New Name", ["d1", "d2"])));

        update.IsSuccess.Should().BeTrue();

        var found = await InvokeAsync<Result<DistrictGroupDto>>(
            new GetDistrictGroupByIdQuery(created.Value.Id));
        found.Value.Name.Should().Be("New Name");
        found.Value.DistrictIds.Should().BeEquivalentTo(["d1", "d2"]);
    }

    [Test]
    [NotInParallel(Order = 703)]
    public async Task Delete_Should_Remove_Group()
    {
        await ResetDatabaseAsync();

        var created = await InvokeAsync<Result<DistrictGroupDto>>(
            new CreateDistrictGroupCommand(new CreateDistrictGroupRequest(
                "To Delete", ["x1"])));

        var delete = await InvokeAsync<Result>(
            new DeleteDistrictGroupCommand(created.Value.Id));

        delete.IsSuccess.Should().BeTrue();

        var found = await InvokeAsync<Result<DistrictGroupDto>>(
            new GetDistrictGroupByIdQuery(created.Value.Id));
        found.IsFailure.Should().BeTrue();
        found.Error.Should().Be(DistrictGroupErrors.NotFound);
    }

    [Test]
    [NotInParallel(Order = 704)]
    public async Task Update_NonExistent_Should_Return_NotFound()
    {
        await ResetDatabaseAsync();

        var result = await InvokeAsync<Result>(
            new UpdateDistrictGroupCommand(Guid.NewGuid(),
                new UpdateDistrictGroupRequest("X", ["d1"])));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DistrictGroupErrors.NotFound);
    }

    [Test]
    [NotInParallel(Order = 705)]
    public async Task DistrictGroupCache_Should_Return_GroupId_For_Member_District()
    {
        await ResetDatabaseAsync();

        var created = await InvokeAsync<Result<DistrictGroupDto>>(
            new CreateDistrictGroupCommand(new CreateDistrictGroupRequest(
                "Cache Test Group", ["mezzeh", "malki"])));

        // Force cache reload after seeding.
        var cache = Factory.Services.GetRequiredService<IDistrictGroupCache>();
        cache.Invalidate();

        var ids = await cache.GetGroupIdsForDistrictAsync("mezzeh", CancellationToken.None);

        ids.Should().ContainSingle().Which.Should().Be(created.Value.Id);
    }

    [Test]
    [NotInParallel(Order = 706)]
    public async Task DistrictGroupCache_Should_Return_Empty_For_Unknown_District()
    {
        await ResetDatabaseAsync();

        var cache = Factory.Services.GetRequiredService<IDistrictGroupCache>();
        cache.Invalidate();

        var ids = await cache.GetGroupIdsForDistrictAsync("unknown-district-xyz", CancellationToken.None);

        ids.Should().BeEmpty();
    }
}
