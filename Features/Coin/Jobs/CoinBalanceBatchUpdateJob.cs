using System;
using System.Threading.Tasks;
using InkPulse.Worker.Dtos.User;
using InkPulse.Worker.Infrastructure.Services.Caching.Interfaces;
using InkPulse.Worker.Infrastructure.Services.Caching.Models;
using InkPulse.Worker.Infrastructure.Constants;
using InkPulse.Worker.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace InkPulse.Worker.Features.Coin.Jobs
{
    [DisallowConcurrentExecution]
    public class CoinBalanceBatchUpdateJob : IJob
    {
        private readonly ICacheService _cacheService;
        private readonly ISectionCacheService _sectionCacheService;
        private readonly IDapperRepository _dapperRepository;
        private readonly CacheProperties _cacheProperties;
        private readonly ILogger<CoinBalanceBatchUpdateJob> _logger;

        public CoinBalanceBatchUpdateJob(
            ICacheService cacheService,
            ISectionCacheService sectionCacheService,
            IDapperRepository dapperRepository,
            IOptions<CacheProperties> cachePropertiesOptions,
            ILogger<CoinBalanceBatchUpdateJob> logger)
        {
            _cacheService = cacheService;
            _sectionCacheService = sectionCacheService;
            _dapperRepository = dapperRepository;
            _cacheProperties = cachePropertiesOptions.Value;
            _logger = logger;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            try
            {
                var cacheKey = _cacheProperties.BuildKey(KeyConstant.CacheSections.CoinPendingDeltas, "");
                var pendingDeltas = await _cacheService.HashGetAllAsync(cacheKey);

                if (pendingDeltas == null || pendingDeltas.Count == 0)
                {
                    return;
                }

                _logger.LogInformation("Processing coin balance batch update for {Count} users", pendingDeltas.Count);

                foreach (var entry in pendingDeltas)
                {
                    var userIdStr = entry.Key;
                    var deltaStr = entry.Value;

                    if (!Guid.TryParse(userIdStr, out var userId))
                    {
                        _logger.LogWarning("Invalid user ID format in pending deltas: {UserId}", userIdStr);
                        continue;
                    }

                    if (!long.TryParse(deltaStr, out var delta))
                    {
                        _logger.LogWarning("Invalid delta value in pending deltas: {Delta} for user {UserId}", deltaStr, userIdStr);
                        continue;
                    }

                    if (delta == 0)
                    {
                        // No changes, delete from hash
                        await _cacheService.HashDeleteAsync(cacheKey, userIdStr);
                        continue;
                    }

                    // 1. Update user profile database
                    var updateSql = @"
                        UPDATE user_profiles 
                        SET coin_balance = coin_balance + @delta, 
                            updated_at = NOW() 
                        WHERE user_id = @userId";

                    int affected = await _dapperRepository.ExecuteAsync(updateSql, new { delta, userId });

                    if (affected > 0)
                    {
                        _logger.LogInformation("Successfully updated coin balance for user {UserId} with delta {Delta}", userId, delta);

                        // 2. Evict User Profile cache so frontend pulls fresh data
                        await _sectionCacheService.RemoveAsync<UserProfileCacheDto>(userIdStr);
                    }
                    else
                    {
                        _logger.LogWarning("User profile not found for user ID: {UserId} when attempting to apply delta {Delta}", userId, delta);
                    }

                    // 3. Clear from Redis hash
                    await _cacheService.HashDeleteAsync(cacheKey, userIdStr);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during execution of CoinBalanceBatchUpdateJob");
            }
        }
    }
}
