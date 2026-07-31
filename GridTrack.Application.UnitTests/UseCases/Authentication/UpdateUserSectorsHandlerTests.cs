using GridTrack.Application.CQRS.Repositories;
using GridTrack.Application.UseCases.Authentication;
using GridTrack.Domain.Abstractions;
using GridTrack.Domain.Authentication;
using GridTrack.Domain.DistrictGroups;

namespace GridTrack.Application.UnitTests.UseCases.Authentication;

public class UpdateUserSectorsHandlerTests
{
    // ── Fakes ──────────────────────────────────────────────────────────────

    private sealed class FakeUserRepository(AppUser? user) : IUserRepository
    {
        public Task<AppUser?> GetByIdAsync(Guid id, CancellationToken ct)     => Task.FromResult(user);
        public Task<AppUser?> GetByUsernameAsync(string u, CancellationToken ct) => Task.FromResult<AppUser?>(null);
        public Task AddAsync(AppUser u, CancellationToken ct)                  => Task.CompletedTask;
        public Task<IReadOnlyList<AppUser>> GetAllAsync(CancellationToken ct)  => Task.FromResult<IReadOnlyList<AppUser>>([]);
    }

    private sealed class FakeDistrictGroupRepository(IReadOnlyList<DistrictGroup>? groups = null)
        : IDistrictGroupRepository
    {
        private readonly IReadOnlyList<DistrictGroup> _groups = groups ?? [];
        public Task<IReadOnlyList<DistrictGroup>> GetAllAsync(CancellationToken ct) => Task.FromResult(_groups);
        public Task AddAsync(DistrictGroup g, CancellationToken ct)    => Task.CompletedTask;
        public Task RemoveAsync(DistrictGroup g, CancellationToken ct) => Task.CompletedTask;
        public Task<DistrictGroup?> GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult<DistrictGroup?>(null);
    }

    private static AppUser MakeObserver(Guid[]? sectors = null)
        => AppUser.Create(Guid.NewGuid(), "observer", "hash", "Observer", sectors ?? []).Value;

    private static AppUser MakeAdmin()
        => AppUser.Create(Guid.NewGuid(), "admin", "hash", "GeneralObserver", []).Value;

    private static DistrictGroup MakeGroup(Guid id)
        => DistrictGroup.Create(id, "Group", ["d1"]).Value;

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct) => Task.FromResult(0);
    }

    // ── Tests ───────────────────────────────────────────────────────────────

    [Test]
    public async Task Handle_Returns_NotFound_When_User_Missing()
    {
        var handler = new UpdateUserSectorsHandler();
        var result = await handler.Handle(
            new UpdateUserSectorsCommand(Guid.NewGuid(), new UpdateUserSectorsRequest([])),
            new FakeUserRepository(null),
            new FakeDistrictGroupRepository(),
            new FakeUnitOfWork(),
            CancellationToken.None);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsEqualTo(AppUserErrors.NotFound);
    }

    [Test]
    public async Task Handle_Returns_Error_When_Target_Is_GeneralObserver()
    {
        var handler = new UpdateUserSectorsHandler();
        var result = await handler.Handle(
            new UpdateUserSectorsCommand(Guid.NewGuid(), new UpdateUserSectorsRequest([])),
            new FakeUserRepository(MakeAdmin()),
            new FakeDistrictGroupRepository(),
            new FakeUnitOfWork(),
            CancellationToken.None);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsEqualTo(AppUserErrors.CannotAssignSectors);
    }

    [Test]
    public async Task Handle_Returns_UnprocessableEntity_Error_For_Unknown_SectorIds()
    {
        var handler   = new UpdateUserSectorsHandler();
        var unknownId = Guid.NewGuid();

        var result = await handler.Handle(
            new UpdateUserSectorsCommand(Guid.NewGuid(), new UpdateUserSectorsRequest([unknownId])),
            new FakeUserRepository(MakeObserver()),
            new FakeDistrictGroupRepository(),  // empty — no known groups
            new FakeUnitOfWork(),
            CancellationToken.None);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error.Code).IsEqualTo("AppUser.UnknownSectorIds");
    }

    [Test]
    public async Task Handle_Succeeds_With_Valid_SectorIds_And_Returns_Updated_Dto()
    {
        var sectorId = Guid.NewGuid();
        var group    = MakeGroup(sectorId);
        var user     = MakeObserver();
        var handler  = new UpdateUserSectorsHandler();

        var result = await handler.Handle(
            new UpdateUserSectorsCommand(user.UserId, new UpdateUserSectorsRequest([sectorId])),
            new FakeUserRepository(user),
            new FakeDistrictGroupRepository([group]),
            new FakeUnitOfWork(),
            CancellationToken.None);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value.SectorIds).Contains(sectorId);
        await Assert.That(result.Value.UserId).IsEqualTo(user.UserId);
    }

    [Test]
    public async Task Handle_Succeeds_With_Empty_SectorIds_Revoking_Access()
    {
        var user    = MakeObserver([Guid.NewGuid()]);
        var handler = new UpdateUserSectorsHandler();

        var result = await handler.Handle(
            new UpdateUserSectorsCommand(user.UserId, new UpdateUserSectorsRequest([])),
            new FakeUserRepository(user),
            new FakeDistrictGroupRepository(),
            new FakeUnitOfWork(),
            CancellationToken.None);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value.SectorIds).IsEmpty();
    }
}
