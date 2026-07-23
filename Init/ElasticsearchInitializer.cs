using System;
using System.Threading.Tasks;
using Elastic.Clients.Elasticsearch;
using InkPulse.Worker.Features.Book.Documents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace InkPulse.Worker.Init
{
    public static class ElasticsearchInitializer
    {
        public static async Task InitializeIndicesAsync(IServiceProvider serviceProvider)
        {
            using (var scope = serviceProvider.CreateScope())
            {
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<ElasticsearchClient>>();
                var elasticClient = scope.ServiceProvider.GetRequiredService<ElasticsearchClient>();
                try
                {
                    logger.LogInformation("Checking Elasticsearch indices...");
                    
                    // 1. Setup inkpulse_books index
                    var booksExists = await elasticClient.Indices.ExistsAsync("inkpulse_books");
                    if (!booksExists.Exists)
                    {
                        logger.LogInformation("Creating index inkpulse_books with custom mappings...");
                        var createBooksResponse = await elasticClient.Indices.CreateAsync("inkpulse_books", c => c
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
                            )
                        );

                        if (createBooksResponse.IsValidResponse)
                            logger.LogInformation("Index inkpulse_books created successfully.");
                        else
                            logger.LogError("Failed to create index inkpulse_books: {Error}", createBooksResponse.ElasticsearchServerError?.Error?.Reason);
                    }
                    else
                    {
                        logger.LogInformation("Index inkpulse_books already exists. Updating mappings for flash sale fields...");
                        var putMappingResponse = await elasticClient.Indices.PutMappingAsync("inkpulse_books", m => m
                            .Properties<BookEditionDocument>(p => p
                                .DoubleNumber("flash_sale_price")
                                .Keyword("flash_sale_item_id")
                            )
                        );

                        if (putMappingResponse.IsValidResponse)
                            logger.LogInformation("Index inkpulse_books mappings updated successfully with flash sale fields.");
                        else
                            logger.LogError("Failed to update mappings for inkpulse_books: {Error}", putMappingResponse.ElasticsearchServerError?.Error?.Reason);
                    }

                    // 2. Setup inkpulse_authors index
                    var authorsExists = await elasticClient.Indices.ExistsAsync("inkpulse_authors");
                    if (!authorsExists.Exists)
                    {
                        logger.LogInformation("Creating index inkpulse_authors with custom mappings...");
                        var createAuthorsResponse = await elasticClient.Indices.CreateAsync("inkpulse_authors", c => c
                            .Mappings(m => m
                                .Properties<AuthorDocument>(p => p
                                    .Keyword("id")
                                    .Text("name")
                                    .Text("biography")
                                    .Keyword("avatar_url")
                                    .Boolean("is_deleted")
                                )
                            )
                        );

                        if (createAuthorsResponse.IsValidResponse)
                            logger.LogInformation("Index inkpulse_authors created successfully.");
                        else
                            logger.LogError("Failed to create index inkpulse_authors: {Error}", createAuthorsResponse.ElasticsearchServerError?.Error?.Reason);
                    }
                    else
                    {
                        logger.LogInformation("Index inkpulse_authors already exists.");
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An error occurred while setting up Elasticsearch indices.");
                }
            }
        }
    }
}
