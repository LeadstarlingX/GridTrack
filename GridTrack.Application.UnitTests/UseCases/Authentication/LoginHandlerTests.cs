using GridTrack.Application.Abstractions.Authentication;
using GridTrack.Application.CQRS.Repositories;
using GridTrack.Application.Errors;
using GridTrack.Application.UseCases.Authentication;
using GridTrack.Domain.Authentication;
using GridTrack.Domain.DistrictGroups;

namespace GridTrack.Application.UnitTests.UseCases.Authentication;

public class LoginHandlerTests
{
    // ── Fakes ──────────────────────────────────────────────────────────────

    private sealed class FakeUserRepository(AppUser? user) : IUserRepository
    {
        public Task<AppUser?> GetByUsernameAsync(string username, CancellationToken ct)
            => Task.FromResult(user);
        public Task AddAsync(AppUser u, CancellationToken ct) => Task.CompletedTask;
        public Task<AppUser?> GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult<AppUser?>(null);
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

    private sealed class FakePasswordHasher(bool matches = true) : IPasswordHasher
    {
        public string Hash(string password) => "hashed";
        public bool   Verify(string password, string hash) => matches;
    }

    private sealed class CapturingJwtService : ILocalJwtService
    {
        public string?                IssuedRole       { get; private set; }
        public IReadOnlyList<string>? IssuedDistrictIds { get; private set; }
        public Guid[]?                IssuedSectorIds  { get; private set; }

        public string Issue(Guid userId, string role, IReadOnlyList<string>? districtIds, Guid[]? sectorIds)
        {
            IssuedRole        = role;
            IssuedDistrictIds = districtIds;
            IssuedSectorIds   = sectorIds;
            return "fake-token";
        }
    }

    private static AppUser MakeUser(string role, Guid[]? sectorIds = null)
        => AppUser.Create(Guid.NewGuid(), "user", "hash", role, sectorIds ?? []).Value;

    private static DistrictGroup MakeGroup(Guid id, params string[] districtIds)
        => DistrictGroup.Create(id, "Test Group", districtIds).Value;

    // ── Tests ───────────────────────────────────────────────────────────────

    [Test]
    public async Task Handle_Returns_Failure_When_User_Not_Found()
    {
        var jwt     = new CapturingJwtService();
        var handler = new LoginHandler();

        var result = await handler.Handle(
            new LoginCommand(new LoginRequest("ghost", "pass")),
            new FakeUserRepository(null),
            new FakeDistrictGroupRepository(),
            new FakePasswordHasher(),
            jwt,
            CancellationToken.None);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsEqualTo(AuthErrors.InvalidCredentials);
    }

    [Test]
    public async Task Handle_Returns_Failure_When_Password_Wrong()
    {
        var user    = MakeUser("Observer");
        var jwt     = new CapturingJwtService();
        var handler = new LoginHandler();

        var result = await handler.Handle(
            new LoginCommand(new LoginRequest("user", "wrong")),
            new FakeUserRepository(user),
            new FakeDistrictGroupRepository(),
            new FakePasswordHasher(matches: false),
            jwt,
            CancellationToken.None);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsEqualTo(AuthErrors.InvalidCredentials);
    }

    [Test]
    public async Task Handle_GeneralObserver_Issues_Token_With_Null_DistrictIds()
    {
        var user    = MakeUser("GeneralObserver");
        var jwt     = new CapturingJwtService();
        var handler = new LoginHandler();

        var result = await handler.Handle(
            new LoginCommand(new LoginRequest("user", "pass")),
            new FakeUserRepository(user),
            new FakeDistrictGroupRepository(),
            new FakePasswordHasher(),
            jwt,
            CancellationToken.None);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(jwt.IssuedRole).IsEqualTo("GeneralObserver");
        await Assert.That(jwt.IssuedDistrictIds).IsNull();
        await Assert.That(jwt.IssuedSectorIds).IsNull();
    }

    [Test]
    public async Task Handle_Observer_With_No_Sectors_Issues_Empty_DistrictIds()
    {
        var user    = MakeUser("Observer", sectorIds: []);
        var jwt     = new CapturingJwtService();
        var handler = new LoginHandler();

        var result = await handler.Handle(
            new LoginCommand(new LoginRequest("user", "pass")),
            new FakeUserRepository(user),
            new FakeDistrictGroupRepository(),
            new FakePasswordHasher(),
            jwt,
            CancellationToken.None);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(jwt.IssuedDistrictIds).IsNotNull();
        await Assert.That(jwt.IssuedDistrictIds!.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Handle_Observer_Resolves_DistrictIds_From_Sectors()
    {
        var sectorId = Guid.NewGuid();
        var group    = MakeGroup(sectorId, "mezzeh", "malki");
        var user     = MakeUser("Observer", sectorIds: [sectorId]);
        var jwt      = new CapturingJwtService();
        var handler  = new LoginHandler();

        var result = await handler.Handle(
            new LoginCommand(new LoginRequest("user", "pass")),
            new FakeUserRepository(user),
            new FakeDistrictGroupRepository([group]),
            new FakePasswordHasher(),
            jwt,
            CancellationToken.None);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(jwt.IssuedDistrictIds).IsNotNull();
        await Assert.That(jwt.IssuedDistrictIds).Contains("mezzeh");
        await Assert.That(jwt.IssuedDistrictIds).Contains("malki");
        await Assert.That(jwt.IssuedSectorIds).Contains(sectorId);
    }

    [Test]
    public async Task Handle_Observer_Deduplicates_Districts_Across_Overlapping_Sectors()
    {
        var sectorA = Guid.NewGuid();
        var sectorB = Guid.NewGuid();
        var groupA  = MakeGroup(sectorA, "mezzeh", "shared");
        var groupB  = MakeGroup(sectorB, "malki",  "shared"); // "shared" appears in both
        var user    = MakeUser("Observer", sectorIds: [sectorA, sectorB]);
        var jwt     = new CapturingJwtService();
        var handler = new LoginHandler();

        var result = await handler.Handle(
            new LoginCommand(new LoginRequest("user", "pass")),
            new FakeUserRepository(user),
            new FakeDistrictGroupRepository([groupA, groupB]),
            new FakePasswordHasher(),
            jwt,
            CancellationToken.None);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(jwt.IssuedDistrictIds!.Distinct().Count())
            .IsEqualTo(jwt.IssuedDistrictIds!.Count); // no duplicates
        await Assert.That(jwt.IssuedDistrictIds).Contains("shared");
    }

    [Test]
    public async Task Handle_Observer_Ignores_Sectors_Not_In_Repository()
    {
        var knownSector   = Guid.NewGuid();
        var unknownSector = Guid.NewGuid();
        var group         = MakeGroup(knownSector, "mezzeh");
        var user          = MakeUser("Observer", sectorIds: [knownSector, unknownSector]);
        var jwt           = new CapturingJwtService();
        var handler       = new LoginHandler();

        var result = await handler.Handle(
            new LoginCommand(new LoginRequest("user", "pass")),
            new FakeUserRepository(user),
            new FakeDistrictGroupRepository([group]),
            new FakePasswordHasher(),
            jwt,
            CancellationToken.None);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(jwt.IssuedDistrictIds).Contains("mezzeh");
        await Assert.That(jwt.IssuedDistrictIds!.Count).IsEqualTo(1);
    }
}