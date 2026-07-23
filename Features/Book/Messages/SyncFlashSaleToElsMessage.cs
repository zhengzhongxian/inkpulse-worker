using System;

namespace InkPulse.Worker.Features.Book.Messages
{
    public record SyncFlashSaleToElsMessage(
        Guid BookEditionId,
        double? FlashSalePrice,
        string? FlashSaleItemId
    );
}
