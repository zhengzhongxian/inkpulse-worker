namespace InkPulse.Worker.Features.Order.Messages
{
    public record CancelGhnOrderMessage(
        string OrderCode,
        string GhnOrderCode
    );
}
