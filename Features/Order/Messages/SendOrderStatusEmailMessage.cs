using System.Collections.Generic;

namespace InkPulse.Worker.Features.Order.Messages
{
    public record SendOrderStatusEmailMessage(
        string Email, // Plain email
        string Name,
        string OrderCode,
        string PaymentMethod,
        string Address,
        List<OrderItemEmailInfo> Items,
        int CodAmount,
        string Subject,
        string TemplateName
    );

    public record OrderItemEmailInfo(
        string Name,
        int Quantity,
        int Price
    );
}
