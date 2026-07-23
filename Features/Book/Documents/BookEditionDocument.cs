using System.Collections.Generic;
using System.Text.Json.Serialization;
using InkPulse.Worker.Features.Book.Messages;

namespace InkPulse.Worker.Features.Book.Documents
{
    public class BookEditionDocument
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("sku")]
        public string Isbn { get; set; } = string.Empty;

        [JsonPropertyName("price")]
        public double Price { get; set; }

        [JsonPropertyName("old_price")]
        public double? OldPrice { get; set; }

        [JsonPropertyName("flash_sale_price")]
        public double? FlashSalePrice { get; set; }

        [JsonPropertyName("flash_sale_item_id")]
        public string? FlashSaleItemId { get; set; }

        [JsonPropertyName("stock_quantity")]
        public int StockQuantity { get; set; }

        [JsonPropertyName("edition_number")]
        public int EditionNumber { get; set; }

        [JsonPropertyName("edition_thumbnail_url")]
        public string ThumbnailUrl { get; set; } = string.Empty;

        [JsonPropertyName("file_path_pdf")]
        public string FilePathPdf { get; set; } = string.Empty;

        [JsonPropertyName("cover_type")]
        public string CoverType { get; set; } = string.Empty;

        [JsonPropertyName("page_count")]
        public int? PageCount { get; set; }

        [JsonPropertyName("publication_year")]
        public int? PublicationYear { get; set; }

        [JsonPropertyName("weight_gram")]
        public int WeightGram { get; set; }

        [JsonPropertyName("width_cm")]
        public int WidthCm { get; set; }

        [JsonPropertyName("height_cm")]
        public int HeightCm { get; set; }

        [JsonPropertyName("length_cm")]
        public int LengthCm { get; set; }

        [JsonPropertyName("language")]
        public string Language { get; set; } = string.Empty;

        [JsonPropertyName("publisher_name")]
        public string PublisherName { get; set; } = string.Empty;

        [JsonPropertyName("image_urls")]
        public List<string> ImageUrls { get; set; } = new();

        [JsonPropertyName("book_id")]
        public string BookId { get; set; } = string.Empty;

        [JsonPropertyName("book_title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("book_thumbnail_url")]
        public string BookThumbnailUrl { get; set; } = string.Empty;

        [JsonPropertyName("introduce")]
        public string Introduce { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("author")]
        public string AuthorName { get; set; } = string.Empty;

        [JsonPropertyName("badge_text")]
        public string BadgeText { get; set; } = string.Empty;

        [JsonPropertyName("badge_text_color")]
        public string BadgeTextColor { get; set; } = string.Empty;

        [JsonPropertyName("badge_bg_color")]
        public string BadgeBgColor { get; set; } = string.Empty;

        [JsonPropertyName("is_active")]
        public bool Active { get; set; }

        [JsonPropertyName("is_deleted")]
        public bool Deleted { get; set; }

        [JsonPropertyName("category_slugs")]
        public List<string> CategorySlugs { get; set; } = new();

        [JsonPropertyName("badges")]
        public List<BadgeInfo> Badges { get; set; } = new();

        [JsonPropertyName("publisher_id")]
        public string? PublisherId { get; set; }

        [JsonPropertyName("author_ids")]
        public List<string> AuthorIds { get; set; } = new();

        [JsonPropertyName("badge_ids")]
        public List<string> BadgeIds { get; set; } = new();

        [JsonPropertyName("sold_count")]
        public int SoldCount { get; set; }
    }
}
