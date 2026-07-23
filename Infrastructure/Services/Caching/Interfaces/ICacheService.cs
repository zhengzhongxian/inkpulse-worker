using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InkPulse.Worker.Infrastructure.Services.Caching.Interfaces
{
    public interface ICacheService
    {
        Task SetAsync<T>(string key, T value, TimeSpan expiry);
        Task<T?> GetAsync<T>(string key);
        Task RemoveAsync(string key);
        Task<long> HashIncrementAsync(string key, string field, long delta);
        Task<Dictionary<string, string>> HashGetAllAsync(string key);
        Task<bool> HashDeleteAsync(string key, string field);
    }
}
