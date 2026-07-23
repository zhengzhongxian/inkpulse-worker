using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Elastic.Clients.Elasticsearch;
using InkPulse.Worker.Features.Book.Documents;
using InkPulse.Worker.Infrastructure.Constants;
using InkPulse.Worker.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using Quartz;

namespace InkPulse.Worker.Features.Book.Jobs
{
    [DisallowConcurrentExecution]
    public class FlashSaleElsSyncJob : IJob
    {
        private readonly ElasticsearchClient _elasticClient;
        private readonly IDapperRepository _dapperRepository;
        private readonly ILogger<FlashSaleElsSyncJob> _logger;

        public FlashSaleElsSyncJob(
            ElasticsearchClient elasticClient,
            IDapperRepository dapperRepository,
            ILogger<FlashSaleElsSyncJob> logger)
        {
            _elasticClient = elasticClient;
            _dapperRepository = dapperRepository;
            _logger = logger;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            try
            {
                var now = DateTime.UtcNow;
                _logger.LogInformation("Executing FlashSaleElsSyncJob at {Time}", now);

                // 1. Get currently active flash sale items from database
                var activeSalesSql = @"
                    SELECT fsi.book_edition_id, fsi.flash_sale_item_id, fsi.discount_amount, be.price
                    FROM flash_sale_items fsi
                    JOIN flash_sales fs ON fs.flash_sale_id = fsi.flash_sale_id
                    JOIN book_editions be ON be.id = fsi.book_edition_id
                    WHERE fs.is_deleted = false AND fs.is_active = true AND fsi.is_deleted = false
                      AND fs.start_date <= @now AND fs.end_date > @now";

                var activeSales = (await _dapperRepository.QueryAsync<FlashSaleItemDbDto>(activeSalesSql, new { now })).ToList();
                var activeSalesMap = activeSales.ToDictionary(item => item.book_edition_id, item => item);

                // 2. Fetch all books from Elasticsearch that currently have flash sale fields set
                var esActiveDocs = new List<BookEditionDocument>();
                try
                {
                    var esResponse = await _elasticClient.SearchAsync<BookEditionDocument>(s => s
                        .Index(ElasticsearchIndexConstant.Books)
                        .Query(q => q.Exists(e => e.Field(f => f.FlashSaleItemId)))
                        .Size(1000),
                        context.CancellationToken
                    );

                    if (esResponse.IsValidResponse && esResponse.Documents != null)
                    {
                        esActiveDocs.AddRange(esResponse.Documents);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to fetch active flash sale documents from Elasticsearch");
                }

                var esActiveMap = esActiveDocs.ToDictionary(doc => Guid.Parse(doc.Id), doc => doc);

                var updates = new List<EsUpdateDto>();

                // 3. Compare and build update tasks
                // 3a. Check which DB active sales need setting or updating in ES
                foreach (var dbItem in activeSales)
                {
                    double expectedPrice = (double)(dbItem.price - dbItem.discount_amount);
                    if (expectedPrice < 0) expectedPrice = 0;

                    if (!esActiveMap.TryGetValue(dbItem.book_edition_id, out var esDoc) || 
                        esDoc.FlashSaleItemId != dbItem.flash_sale_item_id.ToString() || 
                        esDoc.FlashSalePrice != expectedPrice)
                    {
                        updates.Add(new EsUpdateDto(
                            dbItem.book_edition_id.ToString(),
                            expectedPrice,
                            dbItem.flash_sale_item_id.ToString()
                        ));
                    }
                }

                // 3b. Check which ES active documents are no longer active in DB
                foreach (var esDoc in esActiveDocs)
                {
                    var editionId = Guid.Parse(esDoc.Id);
                    if (!activeSalesMap.ContainsKey(editionId))
                    {
                        updates.Add(new EsUpdateDto(
                            esDoc.Id,
                            null,
                            null
                        ));
                    }
                }

                // 4. Perform updates in Elasticsearch
                if (updates.Any())
                {
                    _logger.LogInformation("Syncing {Count} book editions to Elasticsearch for Flash Sale state changes...", updates.Count);

                    var tasks = updates.Select(async update =>
                    {
                        try
                        {
                            var updateResponse = await _elasticClient.UpdateAsync<BookEditionDocument, object>(
                                ElasticsearchIndexConstant.Books,
                                update.Id,
                                u => u.Doc(new
                                {
                                    flash_sale_price = update.Price,
                                    flash_sale_item_id = update.ItemId
                                }),
                                context.CancellationToken
                            );

                            if (updateResponse.IsValidResponse)
                            {
                                _logger.LogDebug("Successfully synced Flash Sale state for BookEdition ID: {Id}", update.Id);
                            }
                            else
                            {
                                _logger.LogWarning("Failed to sync Flash Sale state in ES for ID: {Id}. Error: {Error}", 
                                    update.Id, updateResponse.ElasticsearchServerError?.Error?.Reason);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Exception during ES Update of Flash Sale state for ID: {Id}", update.Id);
                        }
                    });

                    await Task.WhenAll(tasks);
                    _logger.LogInformation("Elasticsearch Flash Sale state sync completed.");
                }
                else
                {
                    _logger.LogInformation("Elasticsearch Flash Sale states are already fully in sync.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during execution of FlashSaleElsSyncJob");
            }
        }

        private class FlashSaleItemDbDto
        {
            public Guid book_edition_id { get; set; }
            public Guid flash_sale_item_id { get; set; }
            public decimal discount_amount { get; set; }
            public decimal price { get; set; }
        }

        private record EsUpdateDto(string Id, double? Price, string? ItemId);
    }
}
