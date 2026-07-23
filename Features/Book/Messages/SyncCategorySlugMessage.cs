using System;

namespace InkPulse.Worker.Features.Book.Messages
{
    public record SyncCategorySlugMessage(
        Guid Id,
        string OldSlug,
        string NewSlug,
        bool IsDeleted
    );
}
