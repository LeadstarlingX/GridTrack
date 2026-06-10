using FluentAssertions;
using GridTrack.Application.Abstractions.Cache;
using GridTrack.IntegrationTests.Abstractions;

namespace GridTrack.IntegrationTests.InfrastructureTests;

/// <summary>Exercises the Redis-backed <see cref="ICacheService"/> against a real container.</summary>
public class CacheServiceIntegrationTests : BaseIntegrationTest
{
    private sealed record Sample(string Name, int Value);

    [Test]
    [NotInParallel(Order = 600)]
    public async Task SetAsync_Then_GetAsync_Should_Roundtrip_Value()
    {
        var key = $"test:roundtrip:{Guid.NewGuid()}";
        var payload = new Sample("forecast", 42);

        await ResolveAsync<ICacheService, bool>(async cache =>
        {
            await cache.SetAsync(key, payload, TimeSpan.FromMinutes(1));
            return true;
        });

        var fetched = await ResolveAsync<ICacheService, Sample?>(
            cache => cache.GetAsync<Sample>(key));

        fetched.Should().NotBeNull();
        fetched!.Name.Should().Be("forecast");
        fetched.Value.Should().Be(42);
    }

    [Test]
    [NotInParallel(Order = 601)]
    public async Task GetAsync_Should_Return_Default_When_Key_Missing()
    {
        var fetched = await ResolveAsync<ICacheService, Sample?>(
            cache => cache.GetAsync<Sample>($"test:missing:{Guid.NewGuid()}"));

        fetched.Should().BeNull();
    }

    [Test]
    [NotInParallel(Order = 602)]
    public async Task RemoveAsync_Should_Delete_Key()
    {
        var key = $"test:remove:{Guid.NewGuid()}";

        await ResolveAsync<ICacheService, bool>(async cache =>
        {
            await cache.SetAsync(key, new Sample("x", 1), TimeSpan.FromMinutes(1));
            await cache.RemoveAsync(key);
            return true;
        });

        var fetched = await ResolveAsync<ICacheService, Sample?>(
            cache => cache.GetAsync<Sample>(key));

        fetched.Should().BeNull();
    }

    [Test]
    [NotInParallel(Order = 603)]
    public async Task SetAsync_Should_Overwrite_Existing_Value()
    {
        var key = $"test:overwrite:{Guid.NewGuid()}";

        await ResolveAsync<ICacheService, bool>(async cache =>
        {
            await cache.SetAsync(key, new Sample("first", 1), TimeSpan.FromMinutes(1));
            await cache.SetAsync(key, new Sample("second", 2), TimeSpan.FromMinutes(1));
            return true;
        });

        var fetched = await ResolveAsync<ICacheService, Sample?>(
            cache => cache.GetAsync<Sample>(key));

        fetched!.Name.Should().Be("second");
        fetched.Value.Should().Be(2);
    }
}
