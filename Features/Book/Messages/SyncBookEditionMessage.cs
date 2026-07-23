using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace InkPulse.Worker.Features.Book.Messages
{
    public record SyncBookEditionMessage(
        Guid Id,
        Guid BookId,
        string Title,
        string Introduce,
        string Description,
        string BookThumbnailUrl,
        string Isbn,
        decimal Price,
        decimal? OldPrice,
        int StockQuantity,
        int EditionNumber,
        string ThumbnailUrl,
        string FilePathPdf,
        string CoverType,
        int PageCount,
        int PublicationYear,
        int WeightGram,
        int WidthCm,
        int HeightCm,
        int LengthCm,
        string Language,
        string PublisherName,
        string AuthorName,
        string BadgeText,
        string BadgeTextColor,
        string BadgeBgColor,
        bool Active,
        bool Deleted,
        List<string> CategorySlugs,
        List<string> ImageUrls,
        List<BadgeInfo> Badges,
        Guid? PublisherId,
        List<Guid> AuthorIds,
        List<Guid> BadgeIds,
        int SoldCount
    );

    public record BadgeInfo(
        [property: JsonPropertyName("text")] string Text, 
        [property: JsonPropertyName("textColor")] string TextColor, 
        [property: JsonPropertyName("bgColor")] string BgColor,
        [property: JsonPropertyName("shape")] string Shape
    );
}

