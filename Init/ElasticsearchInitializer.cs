using System;
using System.Threading;
using System.Threading.Tasks;
using Elastic.Clients.Elasticsearch;
using InkPulse.Worker.Features.Book.Documents;
using InkPulse.Worker.Infrastructure.Constants;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace InkPulse.Worker.Init
{
    public static class ElasticsearchInitializer
    {
        public static async Task InitializeIndicesAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
        {
            using (var scope = serviceProvider.CreateScope())
            {
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<ElasticsearchClient>>();
                var elasticClient = scope.ServiceProvider.GetRequiredService<ElasticsearchClient>();
                try
                {
                    logger.LogInformation("Checking Elasticsearch indices...");
                    
                    // 1. Setup books index
                    var booksExists = await elasticClient.Indices.ExistsAsync(ElasticsearchIndexConstant.Books, cancellationToken);
                    if (!booksExists.Exists)
                    {
                        logger.LogInformation("Creating index {Index} with custom mappings...", ElasticsearchIndexConstant.Books);
                        var createBooksResponse = await elasticClient.Indices.CreateAsync(ElasticsearchIndexConstant.Books, c => c
                            .Mappings(m => m
                                .Properties<BookEditionDocument>(p => p
                                    .Keyword("sku")
                                    .DoubleNumber("price")
                                    .DoubleNumber("old_price")
                                    .IntegerNumber("stock_quantity")
                                    .IntegerNumber("edition_number")
                                    .Keyword("edition_thumbnail_url")
                                    .Keyword("file_path_pdf")
                                    .Keyword("cover_type")
                                    .IntegerNumber("page_count")
                                    .IntegerNumber("publication_year")
                                    .Keyword("dimensions")
                                    .Keyword("language")
                                    .Keyword("publisher_name")
                                    .Keyword("image_urls")
                                    .Keyword("book_id")
                                    .Text("book_title")
                                    .Keyword("book_thumbnail_url")
                                    .Text("introduce")
                                    .Text("description")
                                    .Text("author")
                                    .Keyword("badge_text")
                                    .Keyword("badge_text_color")
                                    .Keyword("badge_bg_color")
                                    .Boolean("is_active")
                                    .Boolean("is_deleted")
                                    .Keyword("category_slugs")
                                    .Keyword("publisher_id")
                                    .Keyword("author_ids")
                                    .Keyword("badge_ids")
                                    .IntegerNumber("sold_count")
                                    .DoubleNumber("flash_sale_price")
                                    .Keyword("flash_sale_item_id")
                                )
                            ), cancellationToken
                        );

                        if (createBooksResponse.IsValidResponse)
                            logger.LogInformation("Index {Index} created successfully.", ElasticsearchIndexConstant.Books);
                        else
                            logger.LogError("Failed to create index {Index}: {Error}", ElasticsearchIndexConstant.Books, createBooksResponse.ElasticsearchServerError?.Error?.Reason);
                    }
                    else
                    {
                        logger.LogInformation("Index {Index} already exists. Updating mappings for flash sale fields...", ElasticsearchIndexConstant.Books);
                        var putMappingResponse = await elasticClient.Indices.PutMappingAsync(ElasticsearchIndexConstant.Books, m => m
                            .Properties<BookEditionDocument>(p => p
                                .DoubleNumber("flash_sale_price")
                                .Keyword("flash_sale_item_id")
                            ), cancellationToken
                        );

                        if (putMappingResponse.IsValidResponse)
                            logger.LogInformation("Index {Index} mappings updated successfully with flash sale fields.", ElasticsearchIndexConstant.Books);
                        else
                            logger.LogError("Failed to update mappings for {Index}: {Error}", ElasticsearchIndexConstant.Books, putMappingResponse.ElasticsearchServerError?.Error?.Reason);
                    }

                    // 2. Setup authors index
                    var authorsExists = await elasticClient.Indices.ExistsAsync(ElasticsearchIndexConstant.Authors, cancellationToken);
                    if (!authorsExists.Exists)
                    {
                        logger.LogInformation("Creating index {Index} with custom mappings...", ElasticsearchIndexConstant.Authors);
                        var createAuthorsResponse = await elasticClient.Indices.CreateAsync(ElasticsearchIndexConstant.Authors, c => c
                            .Mappings(m => m
                                .Properties<AuthorDocument>(p => p
                                    .Keyword("id")
                                    .Text("name")
                                    .Text("biography")
                                    .Keyword("avatar_url")
                                    .Boolean("is_deleted")
                                )
                            ), cancellationToken
                        );

                        if (createAuthorsResponse.IsValidResponse)
                            logger.LogInformation("Index {Index} created successfully.", ElasticsearchIndexConstant.Authors);
                        else
                            logger.LogError("Failed to create index {Index}: {Error}", ElasticsearchIndexConstant.Authors, createAuthorsResponse.ElasticsearchServerError?.Error?.Reason);
                    }
                    else
                    {
                        logger.LogInformation("Index {Index} already exists.", ElasticsearchIndexConstant.Authors);
                    }
                }
                catch (OperationCanceledException)
                {
                    logger.LogWarning("Elasticsearch index initialization timed out or was canceled.");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An error occurred while setting up Elasticsearch indices.");
                }
            }
        }
    }
}
