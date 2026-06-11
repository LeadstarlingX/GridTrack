using GridTrack.Application.Abstractions.Cache;

namespace GridTrack.Application.UnitTests.CQRS.Handlers;

/// <summary>In-memory ICacheService fake; records Set calls for assertion without Redis.</summary>
internal sealed class FakeCacheService : ICacheService
{
    private readonly Dictionary<string, (object Value, TimeSpan? Expiration)> _store = new();

    public List<(string Key, object Value, TimeSpan? Expiration)> SetCalls { get; } = new();

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (_store.TryGetValue(key, out var entry) && entry.Value is T typed)
            return Task.FromResult<T?>(typed);
        return Task.FromResult<T?>(default);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        _store[key] = (value!, expiration);
        SetCalls.Add((key, value!, expiration));
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _store.Remove(key);
        return Task.CompletedTask;
    }
}
