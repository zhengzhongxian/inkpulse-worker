using System;
using System.Threading.Tasks;
using Elastic.Clients.Elasticsearch;
using InkPulse.Worker.Features.Book.Documents;
using InkPulse.Worker.Features.Book.Messages;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace InkPulse.Worker.Features.Book.Consumers
{
    public class SyncPublisherNameConsumer : IConsumer<SyncPublisherNameMessage>
    {
        private readonly ElasticsearchClient _elasticClient;
        private readonly ILogger<SyncPublisherNameConsumer> _logger;

        public SyncPublisherNameConsumer(ElasticsearchClient elasticClient, ILogger<SyncPublisherNameConsumer> logger)
        {
            _elasticClient = elasticClient;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<SyncPublisherNameMessage> context)
        {
            var message = context.Message;
            _logger.LogInformation("Consuming SyncPublisherNameMessage for Publisher ID: {PublisherId}, Name: {Name}, IsDeleted: {IsDeleted}", 
                message.Id, message.Name, message.IsDeleted);

            try
            {
                UpdateByQueryResponse response;

                if (message.IsDeleted)
                {
                    // If deleted, set publisher_name and publisher_id to null for all book editions matching publisher_id
                    response = await _elasticClient.UpdateByQueryAsync<BookEditionDocument>(
                        "inkpulse_books",
                        u => u
                            .Query(q => q
                                .Term(t => t
                                    .Field(f => f.PublisherId)
                                    .Value(message.Id.ToString())
                                )
                            )
                            .Script(s => s
                                .Source("ctx._source.publisher_name = null; ctx._source.publisher_id = null;")
                            ),
                        context.CancellationToken
                    );
                }
                else
                {
                    // If updated, set publisher_name to the new name for all book editions matching publisher_id
                    response = await _elasticClient.UpdateByQueryAsync<BookEditionDocument>(
                        "inkpulse_books",
                        u => u
                            .Query(q => q
                                .Term(t => t
                                    .Field(f => f.PublisherId)
                                    .Value(message.Id.ToString())
                                )
                            )
                            .Script(s => s
                                .Source("ctx._source.publisher_name = params.name")
                                .Params(p => { p.Add("name", message.Name ?? string.Empty); return p; })
                            ),
                        context.CancellationToken
                    );
                }

                if (response.IsValidResponse)
                {
                    _logger.LogInformation("Successfully updated publisher on ELS via UpdateByQuery. Updated {Count} documents.", response.Updated);
                }
                else
                {
                    _logger.LogError("Failed to update publisher on ELS via UpdateByQuery. Error: {Error}", 
                        response.ElasticsearchServerError?.Error?.Reason);
                    throw new Exception($"Elasticsearch UpdateByQuery failed: {response.ElasticsearchServerError?.Error?.Reason}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during Elasticsearch UpdateByQuery for Publisher ID: {PublisherId}", message.Id);
                throw;
            }
        }
    }
}
