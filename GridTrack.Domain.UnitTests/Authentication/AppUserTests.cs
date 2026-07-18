using GridTrack.Domain.Authentication;

namespace GridTrack.Domain.UnitTests.Authentication;

public class AppUserTests
{
    [Test]
    public async Task Create_Should_Succeed_With_Valid_Observer()
    {
        var result = AppUser.Create(Guid.NewGuid(), "ahmad", "hash", "Observer", []);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value.Role).IsEqualTo("Observer");
        await Assert.That(result.Value.IsGeneralObserver).IsFalse();
    }

    [Test]
    public async Task Create_Should_Succeed_With_GeneralObserver()
    {
        var result = AppUser.Create(Guid.NewGuid(), "  admin  ", "hash", "GeneralObserver", []);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value.Username).IsEqualTo("admin"); // trimmed
        await Assert.That(result.Value.IsGeneralObserver).IsTrue();
    }

    [Test]
    public async Task Create_Should_Fail_When_UserId_Is_Empty()
    {
        var result = AppUser.Create(Guid.Empty, "ahmad", "hash", "Observer", []);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsEqualTo(AppUserErrors.InvalidId);
    }

    [Test]
    public async Task Create_Should_Fail_When_Username_Is_Whitespace()
    {
        var result = AppUser.Create(Guid.NewGuid(), "   ", "hash", "Observer", []);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsEqualTo(AppUserErrors.InvalidUsername);
    }

    [Test]
    public async Task Create_Should_Fail_When_PasswordHash_Is_Empty()
    {
        var result = AppUser.Create(Guid.NewGuid(), "ahmad", "", "Observer", []);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsEqualTo(AppUserErrors.InvalidPasswordHash);
    }

    [Test]
    [Arguments("Admin")]
    [Arguments("admin")]
    [Arguments("")]
    [Arguments("superuser")]
    public async Task Create_Should_Fail_When_Role_Is_Invalid(string role)
    {
        var result = AppUser.Create(Guid.NewGuid(), "ahmad", "hash", role, []);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsEqualTo(AppUserErrors.InvalidRole);
    }

    [Test]
    public async Task Create_Should_Store_SectorIds()
    {
        var sectorIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var result    = AppUser.Create(Guid.NewGuid(), "ahmad", "hash", "Observer", sectorIds);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(result.Value.SectorIds).IsEquivalentTo(sectorIds);
    }
}