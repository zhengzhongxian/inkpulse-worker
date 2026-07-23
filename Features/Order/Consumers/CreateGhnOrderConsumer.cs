using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;
using InkPulse.Worker.Features.Order.Messages;
using InkPulse.Worker.Infrastructure.Services.Email.Interfaces;
using InkPulse.Worker.Infrastructure.Helpers;
using InkPulse.Worker.Infrastructure.Services.Security.Interfaces;
using InkPulse.Worker.Infrastructure.Services.Shipping.Models;
using InkPulse.Worker.Infrastructure.Constants;
using InkPulse.Worker.Infrastructure.Persistence;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace InkPulse.Worker.Features.Order.Consumers
{
    public class CreateGhnOrderConsumer : IConsumer<CreateGhnOrderMessage>
    {
        private readonly ICryptographyService _cryptographyService;
        private readonly IConfiguration _configuration;
        private readonly IDapperRepository _dapperRepository;
        private readonly ILogger<CreateGhnOrderConsumer> _logger;
        private readonly HttpClient _httpClient;

        public CreateGhnOrderConsumer(
            ICryptographyService cryptographyService,
            IConfiguration configuration,
            IDapperRepository dapperRepository,
            ILogger<CreateGhnOrderConsumer> logger)
        {
            _cryptographyService = cryptographyService;
            _configuration = configuration;
            _dapperRepository = dapperRepository;
            _logger = logger;
            _httpClient = new HttpClient();
        }

        public async Task Consume(ConsumeContext<CreateGhnOrderMessage> context)
        {
            var message = context.Message;
            var plainPhone = _cryptographyService.DecryptAes(message.RecipientPhone);
            var plainEmail = _cryptographyService.DecryptAes(message.UserEmail);

            _logger.LogInformation("Consuming CreateGhnOrderMessage for Order Code: {OrderCode}, Phone: {Phone}, Email: {Email}", 
                message.OrderCode, plainPhone, plainEmail);

            // 1. Get GHN Settings Section
            var ghnSection = _configuration.GetSection(GhnSettings.SectionName);
            var apiToken = ghnSection["ApiToken"];
            var baseUrl = ghnSection["BaseUrl"];
            var shopIdStr = ghnSection["ShopId"] ?? "0";
            int.TryParse(shopIdStr, out var shopId);

            // 2. Call GHN Create Shipping Order API
            var ghnItems = new List<object>();
            foreach (var item in message.Items)
            {
                ghnItems.Add(new
                {
                    name = item.Name,
                    code = item.Code,
                    quantity = item.Quantity,
                    price = item.Price,
                    weight = item.Weight,
                    length = item.Length,
                    width = item.Width,
                    height = item.Height
                });
            }

            var ghnPayload = new
            {
                payment_type_id = 1, // Seller/Shop pays shipping fee (since shipping fee is already included in order total / COD amount)
                note = "Cho xem hang, khong cho thu",
                required_note = "CHOXEMHANGKHONGTHU",
                to_name = message.ReceiverName,
                to_phone = plainPhone,
                to_address = message.ToAddress,
                to_ward_code = message.ToWardCode,
                to_district_id = message.ToDistrictId,
                cod_amount = message.CodAmount,
                weight = message.TotalWeight,
                length = message.TotalLength,
                width = message.TotalWidth,
                height = message.TotalHeight,
                service_type_id = 2,
                insurance_value = message.InsuranceValue,
                items = ghnItems
            };

            var url = $"{baseUrl}/shiip/public-api/v2/shipping-order/create";
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, url);
            requestMessage.Headers.Add("Token", apiToken);
            requestMessage.Headers.Add("ShopId", shopId.ToString());
            requestMessage.Content = new StringContent(JsonHelper.Serialize(ghnPayload), Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.SendAsync(requestMessage);
                var responseBody = await response.Content.ReadAsStringAsync();
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("GHN Order Creation failed with HTTP code {StatusCode}. Response: {Response}", response.StatusCode, responseBody);
                    throw new Exception($"GHN Error code {response.StatusCode}: {responseBody}");
                }

                using var doc = JsonHelper.ParseDocument(responseBody);
                var root = doc.RootElement;
                var code = JsonHelper.GetInt(root, "code");

                if (code != 200)
                {
                    var errMsg = JsonHelper.GetString(root, "message");
                    _logger.LogError("GHN Order Creation failed with business error. Response: {Response}", responseBody);
                    throw new Exception($"GHN Business Error: {errMsg}");
                }

                var data = root.GetProperty("data");
                var ghnOrderCode = JsonHelper.GetString(data, "order_code");
                _logger.LogInformation("Successfully created GHN Shipping Order. GHN Order Code: {GhnCode}", ghnOrderCode);

                // 3. Update Order and log using Dapper inside a transaction scope
                using (var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
                {
                    var updateOrderSql = "UPDATE orders SET ghn_order_code = @ghn, order_status = 'PROCESSING', updated_at = NOW() WHERE order_code = @code";
                    await _dapperRepository.ExecuteAsync(updateOrderSql, new { ghn = ghnOrderCode, code = message.OrderCode });

                    var insertLogSql = @"
                        INSERT INTO order_logs (log_id, order_code, from_status, to_status, changed_by, admin_note, user_note, is_deleted, created_at, updated_at, version) 
                        VALUES (gen_random_uuid(), @code, 'PROCESSING', 'PROCESSING', @changedBy, 'Đã tạo vận đơn GHN', 'Đơn hàng đang được chuẩn bị và chờ đóng gói', false, NOW(), NOW(), 0)";
                    await _dapperRepository.ExecuteAsync(insertLogSql, new { changedBy = Guid.Empty, code = message.OrderCode });

                    scope.Complete();
                }

                // 4. Publish SendOrderConfirmationEmailMessage to email queue
                var emailItems = new List<OrderItemEmailInfo>();
                foreach (var item in message.Items)
                {
                    emailItems.Add(new OrderItemEmailInfo(item.Name, item.Quantity, item.Price));
                }

                var isPayOs = "PAYOS".Equals(message.PaymentMethod, StringComparison.OrdinalIgnoreCase);
                var templateName = isPayOs ? "order-packed-template.html" : "order-confirmation-template.html";
                var subject = isPayOs 
                    ? $"[InkPulse] Đơn hàng #{message.OrderCode} đã đóng gói & chờ vận chuyển" 
                    : $"[InkPulse] Xác nhận đơn hàng #{message.OrderCode}";

                var emailMessage = new SendOrderStatusEmailMessage(
                    plainEmail,
                    message.ReceiverName,
                    message.OrderCode,
                    message.PaymentMethod,
                    message.ToAddress,
                    emailItems,
                    message.CodAmount,
                    subject,
                    templateName
                );

                await context.Publish(emailMessage, context.CancellationToken);
                _logger.LogInformation("Published SendOrderStatusEmailMessage for Order Code: {OrderCode} with template {TemplateName}", message.OrderCode, templateName);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error processing order: {OrderCode}. Details: {Message}", message.OrderCode, ex.Message);
                throw;
            }
        }
    }
}
