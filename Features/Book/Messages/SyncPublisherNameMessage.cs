using System;

namespace InkPulse.Worker.Features.Book.Messages
{
    public record SyncPublisherNameMessage(
        Guid Id,
        string Name,
        bool IsDeleted
    );
}
