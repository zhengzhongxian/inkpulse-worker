using System;
using System.Collections.Generic;

namespace InkPulse.Worker.Features.Order.Messages
{
    public record CreateGhnOrderMessage(
        string OrderCode,
        string ReceiverName,
        string RecipientPhone,
        string ToAddress,
        string ToWardCode,
        int ToDistrictId,
        string ToWardName,
        string ToDistrictName,
        string ToProvinceName,
        string PaymentMethod,
        int CodAmount,
        int TotalWeight,
        int TotalLength,
        int TotalWidth,
        int TotalHeight,
        int InsuranceValue,
        List<OrderItemInfo> Items,
        string UserEmail,
        string UserName
    );

    public record OrderItemInfo
    {
        public string Name { get; init; } = "";
        public string Code { get; init; } = "";
        public int Quantity { get; init; }
        public int Price { get; init; }
        public int Weight { get; init; }
        public int Length { get; init; }
        public int Width { get; init; }
        public int Height { get; init; }
    }
}
