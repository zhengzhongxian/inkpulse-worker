namespace InkPulse.Worker.Features.Order.Messages
{
    public record GhnStatusUpdateMessage(
        string OrderCode,   // GHN order code (e.g. Z82BS)
        string Status,      // ready_to_pick, delivering, delivered, cancel, return
        string RawPayload,  // raw JSON payload
        string Type         // switch_status
    );
}
