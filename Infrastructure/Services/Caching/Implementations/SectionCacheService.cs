using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading.Tasks;
using InkPulse.Worker.Infrastructure.Services.Caching.Interfaces;
using InkPulse.Worker.Infrastructure.Services.Caching.Models;
using Microsoft.Extensions.Options;

namespace InkPulse.Worker.Infrastructure.Services.Caching.Implementations
{
    public class SectionCacheService : ISectionCacheService
    {
        private readonly ICacheService _cacheService;
        private readonly CacheProperties _cacheProperties;
        private readonly ConcurrentDictionary<Type, CacheProperties.SectionConfig> _configCache = new();

        public SectionCacheService(ICacheService cacheService, IOptions<CacheProperties> cachePropertiesOptions)
        {
            _cacheService = cacheService;
            _cacheProperties = cachePropertiesOptions.Value;
        }

        public async Task SetAsync(ICacheable value)
        {
            var config = GetSectionConfig(value.GetType());
            var finalKey = config.Key + value.CacheId();
            await _cacheService.SetAsync(finalKey, value, TimeSpan.FromMinutes(config.Ttl));
        }

        public async Task<T?> GetAsync<T>(string identifier) where T : class, ICacheable
        {
            var config = GetSectionConfig(typeof(T));
            var finalKey = config.Key + identifier;
            return await _cacheService.GetAsync<T>(finalKey);
        }

        public async Task RemoveAsync<T>(string identifier) where T : class, ICacheable
        {
            var config = GetSectionConfig(typeof(T));
            var finalKey = config.Key + identifier;
            await _cacheService.RemoveAsync(finalKey);
        }

        private CacheProperties.SectionConfig GetSectionConfig(Type type)
        {
            return _configCache.GetOrAdd(type, t =>
            {
                var annotation = t.GetCustomAttribute<CacheSectionAttribute>();
                if (annotation == null)
                {
                    throw new ArgumentException(
                        $"Class {t.Name} is missing CacheSection attribute. " +
                        "Add [CacheSection(\"section-name\")] to the class.");
                }

                if (!_cacheProperties.Sections.TryGetValue(annotation.Value, out var config))
                {
                    throw new ArgumentException(
                        $"Cache section '{annotation.Value}' is not configured. " +
                        "Add it to Cache:Sections in appsettings.json.");
                }

                return config;
            });
        }
    }
}
