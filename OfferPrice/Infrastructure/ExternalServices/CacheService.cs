using Domain;
using Microsoft.Extensions.Caching.Memory;
using OfferPrice.Application.Interfaces;

namespace OfferPrice.Application.ExternalServices;

public class CacheService(IMemoryCache cache) : ICacheService
{
    private readonly IMemoryCache _cache = cache ?? throw new ArgumentNullException(nameof(cache));

    public Result<T> Get<T>(string key) where T : class
    {
        if (string.IsNullOrWhiteSpace(key))
            return Result<T>.Failure("Cache key cannot be empty");

        if (!_cache.TryGetValue(key, out T? value)) return Result<T>.Failure($"Cache entry not found for key: {key}");
        return value != null ? Result<T>.Success(value) : Result<T>.Failure($"Cache entry not found for key: {key}");
    }

    public Result Set<T>(string key, T value, TimeSpan expiration) where T : class
    {
        if (string.IsNullOrWhiteSpace(key))
            return Result.Failure("Cache key cannot be empty");
        _cache.Set(key, value, expiration);
        return Result.Success();
    }

    public bool Exists(string key)
    {
        return _cache.TryGetValue(key, out _);
    }
}