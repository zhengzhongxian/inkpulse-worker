using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using InkPulse.Worker.Infrastructure.Constants;
using InkPulse.Worker.Infrastructure.Persistence;
using InkPulse.Worker.Infrastructure.Services.Caching.Interfaces;
using Microsoft.Extensions.Logging;
using Quartz;

namespace InkPulse.Worker.Features.Book.Jobs
{
    [DisallowConcurrentExecution]
    public class FlashSaleWarmUpJob : IJob
    {
        private readonly ICacheService _cacheService;
        private readonly IDapperRepository _dapperRepository;
        private readonly ILogger<FlashSaleWarmUpJob> _logger;

        public FlashSaleWarmUpJob(
            ICacheService cacheService,
            IDapperRepository dapperRepository,
            ILogger<FlashSaleWarmUpJob> logger)
        {
            _cacheService = cacheService;
            _dapperRepository = dapperRepository;
            _logger = logger;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            try
            {
                var now = DateTime.UtcNow;
                _logger.LogInformation("Executing FlashSaleWarmUpJob at {Time}", now);

                // Fetch current Redis stock state
                var redisStocks = await _cacheService.HashGetAllAsync(KeyConstant.CacheSections.FlashSaleStock) 
                                 ?? new Dictionary<string, string>();

                // 1. Warm-up upcoming and active Flash Sale Items
                var upcomingLimit = now.AddMinutes(10);
                var activeSalesSql = @"
                    SELECT fsi.flash_sale_item_id, fsi.book_edition_id, fsi.discount_amount, fsi.flash_sale_stock, fsi.sold_count, fs.start_date, fs.end_date
                    FROM flash_sales fs
                    JOIN flash_sale_items fsi ON fs.flash_sale_id = fsi.flash_sale_id
                    WHERE fs.is_deleted = false AND fs.is_active = true AND fsi.is_deleted = false AND fs.start_date <= @upcomingLimit AND fs.end_date > @now";

                var activeSales = await _dapperRepository.QueryAsync<FlashSaleDbDto>(
                    activeSalesSql, 
                    new { upcomingLimit, now }
                );

                foreach (var sale in activeSales)
                {
                    var itemIdStr = sale.flash_sale_item_id.ToString();
                    if (!redisStocks.ContainsKey(itemIdStr))
                    {
                        int initialStock = Math.Max(0, sale.flash_sale_stock - sale.sold_count);
                        _logger.LogInformation("Preloading Flash Sale Item stock to Redis: ID={Id}, Stock={Stock}", sale.flash_sale_item_id, initialStock);
                        
                        // Atomically set Redis stock
                        await _cacheService.HashIncrementAsync(KeyConstant.CacheSections.FlashSaleStock, itemIdStr, initialStock);
                    }
                }

                // 2. Cleanup and sync expired Flash Sale Items
                var expiredSalesSql = @"
                    SELECT fsi.flash_sale_item_id, fsi.book_edition_id, fsi.discount_amount, fsi.flash_sale_stock, fsi.sold_count, fs.start_date, fs.end_date
                    FROM flash_sales fs
                    JOIN flash_sale_items fsi ON fs.flash_sale_id = fsi.flash_sale_id
                    WHERE fs.is_deleted = false AND fs.is_active = true AND fsi.is_deleted = false AND fs.end_date <= @now";

                var expiredSales = await _dapperRepository.QueryAsync<FlashSaleDbDto>(
                    expiredSalesSql, 
                    new { now }
                );

                foreach (var sale in expiredSales)
                {
                    var itemIdStr = sale.flash_sale_item_id.ToString();
                    int newlySold = 0;

                    if (redisStocks.TryGetValue(itemIdStr, out var remainingStockStr) && int.TryParse(remainingStockStr, out var remainingStock))
                    {
                        newlySold = Math.Max(0, sale.flash_sale_stock - sale.sold_count - remainingStock);
                    }

                    _logger.LogInformation("Syncing expired Flash Sale Item ID={Id} to DB, Newly Sold={Sold}", sale.flash_sale_item_id, newlySold);

                    // Update DB child item sold count
                    var updateItemSql = @"
                        UPDATE flash_sale_items
                        SET sold_count = sold_count + @newlySold, updated_at = NOW()
                        WHERE flash_sale_item_id = @flashSaleItemId";

                    await _dapperRepository.ExecuteAsync(updateItemSql, new { newlySold, flashSaleItemId = sale.flash_sale_item_id });

                    // Clean up Redis hash and buyer set
                    await _cacheService.HashDeleteAsync(KeyConstant.CacheSections.FlashSaleStock, itemIdStr);
                    await _cacheService.RemoveAsync($"{KeyConstant.CacheSections.FlashSaleBuyers}:{itemIdStr}");
                }

                // 3. Deactivate expired parent campaigns globally
                var deactivateCampaignsSql = @"
                    UPDATE flash_sales
                    SET is_active = false, updated_at = NOW()
                    WHERE is_deleted = false AND is_active = true AND end_date <= @now";
                
                int deactivatedCount = await _dapperRepository.ExecuteAsync(deactivateCampaignsSql, new { now });
                if (deactivatedCount > 0)
                {
                    _logger.LogInformation("Deactivated {Count} expired Flash Sale campaigns globally", deactivatedCount);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during execution of FlashSaleWarmUpJob");
            }
        }

        private class FlashSaleDbDto
        {
            public Guid flash_sale_item_id { get; set; }
            public Guid book_edition_id { get; set; }
            public decimal discount_amount { get; set; }
            public int flash_sale_stock { get; set; }
            public int sold_count { get; set; }
            public DateTime start_date { get; set; }
            public DateTime end_date { get; set; }
        }
    }
}
