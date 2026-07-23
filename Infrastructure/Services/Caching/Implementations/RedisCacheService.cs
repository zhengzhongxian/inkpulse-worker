using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InkPulse.Worker.Infrastructure.Services.Caching.Interfaces;
using InkPulse.Worker.Infrastructure.Helpers;
using StackExchange.Redis;

namespace InkPulse.Worker.Infrastructure.Services.Caching.Implementations
{
    public class RedisCacheService : ICacheService
    {
        private readonly IConnectionMultiplexer _redis;

        public RedisCacheService(IConnectionMultiplexer redis)
        {
            _redis = redis;
        }

        private IDatabase Database => _redis.GetDatabase();

        public async Task SetAsync<T>(string key, T value, TimeSpan expiry)
        {
            var json = JsonHelper.Serialize(value);
            await Database.StringSetAsync(key, json, expiry);
        }

        public async Task<T?> GetAsync<T>(string key)
        {
            var value = await Database.StringGetAsync(key);
            if (!value.HasValue) return default;
            return JsonHelper.Deserialize<T>(value!);
        }

        public async Task RemoveAsync(string key)
        {
            await Database.KeyDeleteAsync(key);
        }

        public async Task<long> HashIncrementAsync(string key, string field, long delta)
        {
            return await Database.HashIncrementAsync(key, field, delta);
        }

        public async Task<Dictionary<string, string>> HashGetAllAsync(string key)
        {
            var entries = await Database.HashGetAllAsync(key);
            return entries.ToDictionary(
                x => x.Name.ToString(),
                x => x.Value.ToString()
            );
        }

        public async Task<bool> HashDeleteAsync(string key, string field)
        {
            return await Database.HashDeleteAsync(key, field);
        }
    }
}
