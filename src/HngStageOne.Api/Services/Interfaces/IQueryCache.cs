namespace HngStageOne.Api.Services.Interfaces;

public interface IQueryCache
{
    Task<T?> GetAsync<T>(string scope, string key, CancellationToken cancellationToken = default) where T : class;
    Task SetAsync<T>(string scope, string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default) where T : class;
    Task RemoveAsync(string scope, string key, CancellationToken cancellationToken = default);
    Task InvalidateScopeAsync(string scope, CancellationToken cancellationToken = default);
}
