namespace InkPulse.Worker.Features.Order.Messages
{
    public record ReturnGhnOrderMessage(
        string OrderCode,
        string GhnOrderCode
    );
}
