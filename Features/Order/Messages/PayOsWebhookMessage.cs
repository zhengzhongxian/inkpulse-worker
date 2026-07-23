namespace InkPulse.Worker.Features.Order.Messages
{
    public record PayOsWebhookMessage(
        string OrderCode,
        string PaymentLinkId,
        int Amount,
        string Description,
        string Code,
        bool Success
    );
}
