using InkPulse.Worker.Infrastructure.Constants;

namespace InkPulse.Worker.Features.Book.Options
{
    public class FlashSaleJobOptions
    {
        public static string SectionName => KeyConstant.ConfigSections.FlashSaleJob;

        public string JobName { get; set; } = "FlashSaleWarmUp";
        public string CronExpression { get; set; } = "0 0/1 * * * ?";
        public bool Enabled { get; set; } = true;
    }
}
