using System;
using System.Threading.Tasks;
using Elastic.Clients.Elasticsearch;
using InkPulse.Worker.Features.Book.Documents;
using InkPulse.Worker.Features.Book.Messages;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace InkPulse.Worker.Features.Book.Consumers
{
    public class SyncCategorySlugConsumer : IConsumer<SyncCategorySlugMessage>
    {
        private readonly ElasticsearchClient _elasticClient;
        private readonly ILogger<SyncCategorySlugConsumer> _logger;

        public SyncCategorySlugConsumer(ElasticsearchClient elasticClient, ILogger<SyncCategorySlugConsumer> logger)
        {
            _elasticClient = elasticClient;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<SyncCategorySlugMessage> context)
        {
            var message = context.Message;
            _logger.LogInformation("Consuming SyncCategorySlugMessage for Category ID: {CategoryId}, OldSlug: {OldSlug}, NewSlug: {NewSlug}, IsDeleted: {IsDeleted}", 
                message.Id, message.OldSlug, message.NewSlug, message.IsDeleted);

            if (string.IsNullOrEmpty(message.OldSlug))
            {
                _logger.LogWarning("Skipping SyncCategorySlugMessage because OldSlug is empty.");
                return;
            }

            try
            {
                UpdateByQueryResponse response;

                if (message.IsDeleted)
                {
                    // If deleted, remove oldSlug from category_slugs array
                    response = await _elasticClient.UpdateByQueryAsync<BookEditionDocument>(
                        "inkpulse_books",
                        u => u
                            .Query(q => q
                                .Term(t => t
                                    .Field(f => f.CategorySlugs)
                                    .Value(message.OldSlug)
                                )
                            )
                            .Script(s => s
                                .Source("if (ctx._source.category_slugs != null) { int idx = ctx._source.category_slugs.indexOf(params.old_slug); if (idx >= 0) { ctx._source.category_slugs.remove(idx); } }")
                                .Params(p => { p.Add("old_slug", message.OldSlug); return p; })
                            ),
                        context.CancellationToken
                    );
                }
                else
                {
                    // If updated, set oldSlug to newSlug inside category_slugs array
                    if (string.IsNullOrEmpty(message.NewSlug))
                    {
                        _logger.LogWarning("Skipping slug rename update because NewSlug is empty.");
                        return;
                    }

                    response = await _elasticClient.UpdateByQueryAsync<BookEditionDocument>(
                        "inkpulse_books",
                        u => u
                            .Query(q => q
                                .Term(t => t
                                    .Field(f => f.CategorySlugs)
                                    .Value(message.OldSlug)
                                )
                            )
                            .Script(s => s
                                .Source("if (ctx._source.category_slugs != null) { int idx = ctx._source.category_slugs.indexOf(params.old_slug); if (idx >= 0) { ctx._source.category_slugs.set(idx, params.new_slug); } }")
                                .Params(p => { p.Add("old_slug", message.OldSlug); p.Add("new_slug", message.NewSlug); return p; })
                            ),
                        context.CancellationToken
                    );
                }

                if (response.IsValidResponse)
                {
                    _logger.LogInformation("Successfully updated category slugs on ELS via UpdateByQuery. Updated {Count} documents.", response.Updated);
                }
                else
                {
                    _logger.LogError("Failed to update category slugs on ELS via UpdateByQuery. Error: {Error}", 
                        response.ElasticsearchServerError?.Error?.Reason);
                    throw new Exception($"Elasticsearch UpdateByQuery failed: {response.ElasticsearchServerError?.Error?.Reason}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during Elasticsearch UpdateByQuery for Category ID: {CategoryId}", message.Id);
                throw;
            }
        }
    }
}
