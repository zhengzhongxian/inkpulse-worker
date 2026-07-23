using InkPulse.Worker.Infrastructure.Constants;

namespace InkPulse.Worker.Infrastructure.Services.Shipping.Models
{
    public class GhnSettings
    {
        public static string SectionName => KeyConstant.ConfigSections.Ghn;
        public string ApiToken { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = string.Empty;
        public string ShopId { get; set; } = string.Empty;
    }
}
