using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Elastic.Clients.Elasticsearch;
using InkPulse.Worker.Features.Book.Documents;
using InkPulse.Worker.Features.Book.Messages;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace InkPulse.Worker.Features.Book.Consumers
{
    public class SyncBookEditionConsumer : IConsumer<SyncBookEditionMessage>
    {
        private readonly ElasticsearchClient _elasticClient;
        private readonly ILogger<SyncBookEditionConsumer> _logger;

        public SyncBookEditionConsumer(ElasticsearchClient elasticClient, ILogger<SyncBookEditionConsumer> logger)
        {
            _elasticClient = elasticClient;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<SyncBookEditionMessage> context)
        {
            var message = context.Message;
            _logger.LogInformation("Consuming SyncBookEditionMessage for Edition ID: {EditionId}, IsDeleted: {Deleted}", 
                message.Id, message.Deleted);

            try
            {
                if (message.Deleted)
                {
                    var deleteResponse = await _elasticClient.DeleteAsync<BookEditionDocument>(
                        message.Id.ToString(), 
                        d => d.Index("inkpulse_books"), 
                        context.CancellationToken
                    );

                    if (deleteResponse.IsValidResponse)
                    {
                        _logger.LogInformation("Successfully deleted BookEdition from Elasticsearch. ID: {EditionId}", message.Id);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to delete BookEdition from Elasticsearch (might not exist). ID: {EditionId}. Error: {Error}", 
                            message.Id, deleteResponse.ElasticsearchServerError?.Error?.Reason);
                    }
                }
                else
                {
                    double? existingFlashSalePrice = null;
                    string? existingFlashSaleItemId = null;
                    try
                    {
                        var existingDocResponse = await _elasticClient.GetAsync<BookEditionDocument>(
                            message.Id.ToString(),
                            d => d.Index("inkpulse_books"),
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
                        ImageUrls = message.ImageUrls ?? new List<string>(),
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
                        CategorySlugs = message.CategorySlugs ?? new List<string>(),
                        Badges = message.Badges ?? new List<BadgeInfo>(),
                        PublisherId = message.PublisherId?.ToString(),
                        AuthorIds = message.AuthorIds != null ? message.AuthorIds.ConvertAll(id => id.ToString()) : new List<string>(),
                        BadgeIds = message.BadgeIds != null ? message.BadgeIds.ConvertAll(id => id.ToString()) : new List<string>(),
                        SoldCount = message.SoldCount
                    };

                    var indexResponse = await _elasticClient.IndexAsync(
                        doc, 
                        i => i.Index("inkpulse_books"), 
                        context.CancellationToken
                    );

                    if (indexResponse.IsValidResponse)
                    {
                        _logger.LogInformation("Successfully indexed BookEdition in Elasticsearch. ID: {EditionId}", message.Id);
                    }
                    else
                    {
                        _logger.LogError("Failed to index BookEdition in Elasticsearch. ID: {EditionId}. Error: {Error}", 
                            message.Id, indexResponse.ElasticsearchServerError?.Error?.Reason);
                        throw new Exception($"Elasticsearch indexing failed: {indexResponse.ElasticsearchServerError?.Error?.Reason}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during Elasticsearch sync for BookEdition ID: {EditionId}", message.Id);
                throw;
            }
        }
    }
}
