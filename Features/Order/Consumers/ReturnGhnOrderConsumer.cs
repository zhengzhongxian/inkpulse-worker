using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using InkPulse.Worker.Features.Order.Messages;
using InkPulse.Worker.Infrastructure.Helpers;
using InkPulse.Worker.Infrastructure.Constants;
using InkPulse.Worker.Infrastructure.Services.Shipping.Models;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace InkPulse.Worker.Features.Order.Consumers
{
    public class ReturnGhnOrderConsumer : IConsumer<ReturnGhnOrderMessage>
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<ReturnGhnOrderConsumer> _logger;
        private readonly HttpClient _httpClient;

        public ReturnGhnOrderConsumer(
            IConfiguration configuration,
            ILogger<ReturnGhnOrderConsumer> logger)
        {
            _configuration = configuration;
            _logger = logger;
            _httpClient = new HttpClient();
        }

        public async Task Consume(ConsumeContext<ReturnGhnOrderMessage> context)
        {
            var message = context.Message;
            _logger.LogInformation("Consuming ReturnGhnOrderMessage for Order Code: {OrderCode}, GHN Code: {GhnCode}", 
                message.OrderCode, message.GhnOrderCode);

            // 1. Get GHN Settings Section
            var ghnSection = _configuration.GetSection(GhnSettings.SectionName);
            var ghnSettings = ghnSection.Get<GhnSettings>();
            var apiToken = ghnSettings?.ApiToken;
            var baseUrl = ghnSettings?.BaseUrl;
            var shopIdStr = ghnSettings?.ShopId ?? "0";
            int.TryParse(shopIdStr, out var shopId);

            // 2. Call GHN Return Shipping Order API
            var ghnPayload = new
            {
                order_codes = new[] { message.GhnOrderCode }
            };

            var url = $"{baseUrl}/shiip/public-api/v2/shipping-order/return";
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
                    _logger.LogError("GHN Order Return failed with HTTP code {StatusCode}. Response: {Response}", response.StatusCode, responseBody);
                    throw new Exception($"GHN Error code {response.StatusCode}: {responseBody}");
                }

                using var doc = JsonHelper.ParseDocument(responseBody);
                var root = doc.RootElement;
                var code = JsonHelper.GetInt(root, "code");

                if (code != 200)
                {
                    var errMsg = JsonHelper.GetString(root, "message");
                    _logger.LogError("GHN Order Return failed with business error. Response: {Response}", responseBody);
                    throw new Exception($"GHN Business Error: {errMsg}");
                }

                _logger.LogInformation("Successfully requested Return/Turn-back for GHN Shipping Order. Order Code: {OrderCode}, GHN Code: {GhnCode}", 
                    message.OrderCode, message.GhnOrderCode);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error requesting return for GHN order: {GhnCode}. Details: {Message}", message.GhnOrderCode, ex.Message);
                throw;
            }
        }
    }
}
