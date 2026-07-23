using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using InkPulse.Worker.Features.Order.Messages;
using InkPulse.Worker.Infrastructure.Services.Email.Interfaces;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace InkPulse.Worker.Features.Order.Consumers
{
    public class SendOrderStatusEmailConsumer : IConsumer<SendOrderStatusEmailMessage>
    {
        private readonly IGmailApiService _gmailApiService;
        private readonly ILogger<SendOrderStatusEmailConsumer> _logger;

        public SendOrderStatusEmailConsumer(
            IGmailApiService gmailApiService,
            ILogger<SendOrderStatusEmailConsumer> logger)
        {
            _gmailApiService = gmailApiService;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<SendOrderStatusEmailMessage> context)
        {
            var message = context.Message;
            _logger.LogInformation("Consuming SendOrderStatusEmailMessage. OrderCode: {OrderCode}, Email: {Email}, Template: {TemplateName}", 
                message.OrderCode, message.Email, message.TemplateName);

            var sb = new StringBuilder();
            long subtotalSum = 0;
            foreach (var item in message.Items)
            {
                long subtotal = (long)item.Quantity * item.Price;
                subtotalSum += subtotal;

                sb.Append("<tr>");
                sb.Append($"<td style=\"padding: 12px; border-bottom: 1px solid #EBEBEB; color: #5A5A5A; font-size: 14px;\">{item.Name}</td>");
                sb.Append($"<td style=\"padding: 12px; border-bottom: 1px solid #EBEBEB; text-align: center; color: #5A5A5A; font-size: 14px;\">{item.Quantity}</td>");
                sb.Append($"<td style=\"padding: 12px; border-bottom: 1px solid #EBEBEB; text-align: right; color: #5A5A5A; font-size: 14px;\">{FormatVnd(item.Price)}</td>");
                sb.Append("</tr>");
            }

            sb.Append("<tr>");
            sb.Append("<td colspan=\"2\" style=\"padding: 16px 12px 12px 12px; text-align: left; font-weight: 700; color: #1C1C1C; font-size: 14px;\">Tổng cộng</td>");
            sb.Append($"<td style=\"padding: 16px 12px 12px 12px; text-align: right; font-weight: 700; color: #F66398; font-size: 16px;\">{FormatVnd(message.CodAmount)}</td>");
            sb.Append("</tr>");

            var placeholders = new Dictionary<string, string>
            {
                ["{Name}"] = string.IsNullOrEmpty(message.Name) ? "Khách hàng" : message.Name,
                ["{OrderCode}"] = message.OrderCode,
                ["{PaymentMethod}"] = message.PaymentMethod,
                ["{Address}"] = message.Address,
                ["{ItemsTable}"] = sb.ToString()
            };

            await _gmailApiService.SendTemplateEmailViaGmailApiAsync(
                message.Email,
                message.Subject,
                message.TemplateName,
                placeholders,
                context.CancellationToken
            );

            _logger.LogInformation("Successfully sent order status email ({TemplateName}) for Order Code: {OrderCode}", 
                message.TemplateName, message.OrderCode);
        }

        private string FormatVnd(int amount)
        {
            return amount.ToString("N0", new System.Globalization.CultureInfo("vi-VN")) + "đ";
        }
    }
}
