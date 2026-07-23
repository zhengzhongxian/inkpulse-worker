using System;
using System.Threading.Tasks;
using Elastic.Clients.Elasticsearch;
using InkPulse.Worker.Features.Book.Documents;
using InkPulse.Worker.Infrastructure.Constants;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace InkPulse.Worker.Features.Book.Consumers
{
    public class PartialUpdateBookEditionConsumer : IConsumer<Messages.SyncBookEditionMessage>
    {
        private readonly ElasticsearchClient _elasticClient;
        private readonly ILogger<PartialUpdateBookEditionConsumer> _logger;

        public PartialUpdateBookEditionConsumer(ElasticsearchClient elasticClient, ILogger<PartialUpdateBookEditionConsumer> logger)
        {
            _elasticClient = elasticClient;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<Messages.SyncBookEditionMessage> context)
        {
            var message = context.Message;
            _logger.LogInformation("Consuming PartialUpdateBookEdition for Edition ID: {EditionId}", message.Id);

            try
            {
                double? existingFlashSalePrice = null;
                string? existingFlashSaleItemId = null;
                try
                {
                    var existingDocResponse = await _elasticClient.GetAsync<BookEditionDocument>(
                        message.Id.ToString(),
                        d => d.Index(ElasticsearchIndexConstant.Books),
                        context.CancellationToken
                    );
                    if (existingDocResponse.Found && existingDocResponse.Source != null)
                    {
                        existingFlashSalePrice = existingDocResponse.Source.FlashSalePrice;
                        existingFlashSaleItemId = existingDocResponse.Source.FlashSaleItemId;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch existing document for ID {EditionId} to preserve flash sale fields", message.Id);
                }

                var doc = new BookEditionDocument
                {
                    Id = message.Id.ToString(),
                    Isbn = message.Isbn ?? string.Empty,
                    Price = (double)message.Price,
                    OldPrice = message.OldPrice.HasValue ? (double?)message.OldPrice.Value : null,
                    FlashSalePrice = existingFlashSalePrice,
                    FlashSaleItemId = existingFlashSaleItemId,
                    StockQuantity = message.StockQuantity,
                    EditionNumber = message.EditionNumber,
                    ThumbnailUrl = message.ThumbnailUrl ?? string.Empty,
                    FilePathPdf = message.FilePathPdf ?? string.Empty,
                    CoverType = message.CoverType ?? string.Empty,
                    PageCount = message.PageCount,
                    PublicationYear = message.PublicationYear,
                    WeightGram = message.WeightGram,
                    WidthCm = message.WidthCm,
                    HeightCm = message.HeightCm,
                    LengthCm = message.LengthCm,
                    Language = message.Language ?? string.Empty,
                    PublisherName = message.PublisherName ?? string.Empty,
                    ImageUrls = message.ImageUrls ?? new System.Collections.Generic.List<string>(),
                    BookId = message.BookId.ToString(),
                    Title = message.Title ?? string.Empty,
                    BookThumbnailUrl = message.BookThumbnailUrl ?? string.Empty,
                    Introduce = message.Introduce ?? string.Empty,
                    Description = message.Description ?? string.Empty,
                    AuthorName = message.AuthorName ?? string.Empty,
                    BadgeText = message.BadgeText ?? string.Empty,
                    BadgeTextColor = message.BadgeTextColor ?? string.Empty,
                    BadgeBgColor = message.BadgeBgColor ?? string.Empty,
                    Active = message.Active,
                    Deleted = message.Deleted,
                    CategorySlugs = message.CategorySlugs ?? new System.Collections.Generic.List<string>(),
                    Badges = message.Badges ?? new System.Collections.Generic.List<Messages.BadgeInfo>()
                };

                var indexResponse = await _elasticClient.IndexAsync(
                    doc, 
                    i => i.Index(ElasticsearchIndexConstant.Books), 
                    context.CancellationToken
                );

                if (indexResponse.IsValidResponse)
                {
                    _logger.LogInformation("Successfully partial-updated BookEdition in Elasticsearch. ID: {EditionId}", message.Id);
                }
                else
                {
                    _logger.LogError("Failed to partial-update BookEdition in Elasticsearch. ID: {EditionId}. Error: {Error}", 
                        message.Id, indexResponse.ElasticsearchServerError?.Error?.Reason);
                    throw new Exception($"Elasticsearch partial update failed: {indexResponse.ElasticsearchServerError?.Error?.Reason}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during partial update BookEdition ID: {EditionId}", message.Id);
                throw;
            }
        }
    }
}