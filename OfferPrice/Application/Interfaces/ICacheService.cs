using Domain;

namespace OfferPrice.Application.Interfaces;

public interface ICacheService
{
    Result<T> Get<T>(string key) where T : class;
    Result Set<T>(string key, T value, TimeSpan expiration) where T : class?;
    bool Exists(string key);
}