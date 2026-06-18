using System.Buffers;
using System.Collections.Concurrent;
using System.Text.Json;
using GridTrack.Application.Abstractions.Cache;
using Microsoft.Extensions.Caching.Distributed;

namespace GridTrack.Infrastructure.Caching;

internal sealed class CacheService(IDistributedCache cache) : ICacheService
{
    private readonly IDistributedCache _cache = cache;

    // Single-flight: when the cache expires, every concurrent request for the same key
    // would otherwise miss and run the (expensive) factory against Postgres at once — a
    // cache stampede that exhausts the connection pool. This map lets the FIRST request
    // run the factory while the rest await the SAME Task, so N concurrent misses collapse
    // into one DB query. This is request coalescing, not the serializing lock we reverted:
    // unrelated keys never block each other, and callers observe their own cancellation
    // via Task.WaitAsync without cancelling the shared computation others depend on.
    private readonly ConcurrentDictionary<string, Lazy<Task<object?>>> _inFlight = new();

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        byte[]? bytes = await _cache.GetAsync(key, cancellationToken);

        return bytes is null ? default : Deserialize<T>(bytes);
    }

    public Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default)
    {
        byte[] bytes = Serialize(value);

        return _cache.SetAsync(key, bytes, CacheOptions.Create(expiration), cancellationToken);
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default) =>
        _cache.RemoveAsync(key, cancellationToken);

    public async Task<T> GetOrSetAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan expiration,
        CancellationToken cancellationToken = default)
    {
        var cached = await GetAsync<T>(key, cancellationToken);
        if (cached is not null) return cached;

        // Coalesce concurrent misses for this key into a single factory call.
        var lazy = _inFlight.GetOrAdd(
            key,
            k => new Lazy<Task<object?>>(() => LoadAndCacheAsync(k, factory, expiration)));

        try
        {
            // WaitAsync lets this caller bail on its own token without aborting the
            // shared task that other waiters still depend on.
            var result = await lazy.Value.WaitAsync(cancellationToken);
            return (T)result!;
        }
        finally
        {
            // Drop the entry once it has produced its value so the next miss recomputes.
            // Remove-if-same-instance avoids evicting a fresher in-flight task.
            if (lazy.Value.IsCompleted)
                _inFlight.TryRemove(new KeyValuePair<string, Lazy<Task<object?>>>(key, lazy));
        }
    }

    // Runs the factory once for the whole coalesced group, then writes the result to Redis.
    // Uses CancellationToken.None deliberately: this shared work must not be cancelled by
    // whichever individual caller happens to give up first.
    private async Task<object?> LoadAndCacheAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan expiration)
    {
        var value = await factory(CancellationToken.None);
        if (value is not null)
            await SetAsync(key, value, expiration, CancellationToken.None);
        return value;
    }

    private static T Deserialize<T>(byte[] bytes)
    {
        return JsonSerializer.Deserialize<T>(bytes)!;
    }

    private static byte[] Serialize<T>(T value)
    {
        var buffer = new ArrayBufferWriter<byte>();

        using var writer = new Utf8JsonWriter(buffer);

        JsonSerializer.Serialize(writer, value);

        return buffer.WrittenSpan.ToArray();
    }
}