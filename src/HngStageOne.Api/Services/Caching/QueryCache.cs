using System.Text.Json;
using HngStageOne.Api.Services.Interfaces;
using Microsoft.Extensions.Caching.Distributed;

namespace HngStageOne.Api.Services.Caching;

/// <summary>
/// Versioned cache. Each <c>scope</c> (e.g. "profiles:list") holds a monotonically increasing
/// version counter. Real keys are namespaced by version, so bumping the version makes all
/// previously-cached entries unreachable instantly without enumerating them.
///
/// Backed by <see cref="IDistributedCache"/> so swapping in-memory for Redis is a config change.
/// </summary>
public sealed class QueryCache : IQueryCache
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<QueryCache> _logger;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = null
    };

    public QueryCache(IDistributedCache cache, ILogger<QueryCache> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string scope, string key, CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            var version = await GetOrInitVersionAsync(scope, cancellationToken);
            var fullKey = BuildKey(scope, version, key);
            var bytes = await _cache.GetAsync(fullKey, cancellationToken);
            if (bytes is null || bytes.Length == 0) return null;
            return JsonSerializer.Deserialize<T>(bytes, Json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache GET failed for scope={Scope} key={Key}", scope, key);
            return null;
        }
    }

    public async Task SetAsync<T>(string scope, string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            var version = await GetOrInitVersionAsync(scope, cancellationToken);
            var fullKey = BuildKey(scope, version, key);
            var bytes = JsonSerializer.SerializeToUtf8Bytes(value, Json);
            await _cache.SetAsync(fullKey, bytes, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ttl
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache SET failed for scope={Scope} key={Key}", scope, key);
        }
    }

    public async Task RemoveAsync(string scope, string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var version = await GetOrInitVersionAsync(scope, cancellationToken);
            await _cache.RemoveAsync(BuildKey(scope, version, key), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache REMOVE failed for scope={Scope} key={Key}", scope, key);
        }
    }

    public async Task InvalidateScopeAsync(string scope, CancellationToken cancellationToken = default)
    {
        try
        {
            var version = await GetOrInitVersionAsync(scope, cancellationToken);
            var next = version + 1;
            await _cache.SetStringAsync(VersionKey(scope), next.ToString(), new DistributedCacheEntryOptions
            {
                // The version key has no TTL; it persists for the cache lifetime.
            }, cancellationToken);
            _logger.LogDebug("Cache scope {Scope} invalidated, version bumped to {Version}", scope, next);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache scope invalidation failed for {Scope}", scope);
        }
    }

    private async Task<long> GetOrInitVersionAsync(string scope, CancellationToken cancellationToken)
    {
        var raw = await _cache.GetStringAsync(VersionKey(scope), cancellationToken);
        if (raw is not null && long.TryParse(raw, out var version)) return version;
        await _cache.SetStringAsync(VersionKey(scope), "1", cancellationToken);
        return 1;
    }

    private static string VersionKey(string scope) => $"{scope}:_version";

    private static string BuildKey(string scope, long version, string key) => $"{scope}:v{version}:{key}";
}
