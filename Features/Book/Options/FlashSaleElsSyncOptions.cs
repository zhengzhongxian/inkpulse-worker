using InkPulse.Worker.Infrastructure.Constants;

namespace InkPulse.Worker.Features.Book.Options
{
    public class FlashSaleElsSyncOptions
    {
        public static string SectionName => KeyConstant.ConfigSections.FlashSaleElsSyncJob;

        public string JobName { get; set; } = "FlashSaleElsSync";
        public string CronExpression { get; set; } = "0 0/2 * * * ?";
        public bool Enabled { get; set; } = true;
    }
}
