using System;
using System.Threading.Tasks;
using Elastic.Clients.Elasticsearch;
using InkPulse.Worker.Features.Book.Documents;
using InkPulse.Worker.Features.Book.Messages;
using InkPulse.Worker.Infrastructure.Constants;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace InkPulse.Worker.Features.Book.Consumers
{
    public class SyncFlashSaleToElsConsumer : IConsumer<SyncFlashSaleToElsMessage>
    {
        private readonly ElasticsearchClient _elasticClient;
        private readonly ILogger<SyncFlashSaleToElsConsumer> _logger;

        public SyncFlashSaleToElsConsumer(ElasticsearchClient elasticClient, ILogger<SyncFlashSaleToElsConsumer> logger)
        {
            _elasticClient = elasticClient;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<SyncFlashSaleToElsMessage> context)
        {
            var message = context.Message;
            _logger.LogInformation("Consuming SyncFlashSaleToElsMessage for Edition ID: {BookEditionId}, FlashSaleItemId: {FlashSaleItemId}, Price: {Price}", 
                message.BookEditionId, message.FlashSaleItemId, message.FlashSalePrice);

            try
            {
                var response = await _elasticClient.UpdateAsync<BookEditionDocument, object>(
                    ElasticsearchIndexConstant.Books,
                    message.BookEditionId.ToString(),
                    u => u.Doc(new
                    {
                        flash_sale_price = message.FlashSalePrice,
                        flash_sale_item_id = message.FlashSaleItemId
                    }),
                    context.CancellationToken
                );

                if (response.IsValidResponse)
                {
                    _logger.LogInformation("Successfully updated Flash Sale fields in Elasticsearch for Edition ID: {BookEditionId}", message.BookEditionId);
                }
                else
                {
                    _logger.LogWarning("Failed to update Flash Sale fields in Elasticsearch (document might not exist yet). ID: {BookEditionId}. Error: {Error}", 
                        message.BookEditionId, response.ElasticsearchServerError?.Error?.Reason);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during Elasticsearch sync for Flash Sale on Edition ID: {BookEditionId}", message.BookEditionId);
                throw;
            }
        }
    }
}
