using System;
using System.Threading.Tasks;
using Elastic.Clients.Elasticsearch;
using InkPulse.Worker.Features.Book.Documents;
using InkPulse.Worker.Features.Book.Messages;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace InkPulse.Worker.Features.Book.Consumers
{
    public class SyncAuthorConsumer : IConsumer<SyncAuthorMessage>
    {
        private readonly ElasticsearchClient _elasticClient;
        private readonly ILogger<SyncAuthorConsumer> _logger;

        public SyncAuthorConsumer(ElasticsearchClient elasticClient, ILogger<SyncAuthorConsumer> logger)
        {
            _elasticClient = elasticClient;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<SyncAuthorMessage> context)
        {
            var message = context.Message;
            _logger.LogInformation("Consuming SyncAuthorMessage for Author ID: {AuthorId}, IsDeleted: {IsDeleted}", 
                message.Id, message.IsDeleted);

            try
            {
                if (message.IsDeleted)
                {
                    var deleteResponse = await _elasticClient.DeleteAsync<AuthorDocument>(
                        message.Id.ToString(), 
                        d => d.Index("inkpulse_authors"), 
                        context.CancellationToken
                    );

                    if (deleteResponse.IsValidResponse)
                    {
                        _logger.LogInformation("Successfully deleted Author from Elasticsearch. ID: {AuthorId}", message.Id);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to delete Author from Elasticsearch (might not exist). ID: {AuthorId}. Error: {Error}", 
                            message.Id, deleteResponse.ElasticsearchServerError?.Error?.Reason);
                    }
                }
                else
                {
                    var doc = new AuthorDocument
                    {
                        Id = message.Id.ToString(),
                        Name = message.Name ?? string.Empty,
                        Biography = message.Biography ?? string.Empty,
                        AvatarUrl = message.AvatarUrl ?? string.Empty,
                        Deleted = false
                    };

                    var indexResponse = await _elasticClient.IndexAsync(
                        doc, 
                        i => i.Index("inkpulse_authors"), 
                        context.CancellationToken
                    );

                    if (indexResponse.IsValidResponse)
                    {
                        _logger.LogInformation("Successfully indexed Author in Elasticsearch. ID: {AuthorId}", message.Id);
                    }
                    else
                    {
                        _logger.LogError("Failed to index Author in Elasticsearch. ID: {AuthorId}. Error: {Error}", 
                            message.Id, indexResponse.ElasticsearchServerError?.Error?.Reason);
                        throw new Exception($"Elasticsearch indexing failed: {indexResponse.ElasticsearchServerError?.Error?.Reason}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during Elasticsearch sync for Author ID: {AuthorId}", message.Id);
                throw;
            }
        }
    }
}
