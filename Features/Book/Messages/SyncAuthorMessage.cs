using System;

namespace InkPulse.Worker.Features.Book.Messages
{
    public record SyncAuthorMessage(
        Guid Id,
        string Name,
        string Biography,
        string AvatarUrl,
        bool IsDeleted
    );
}
