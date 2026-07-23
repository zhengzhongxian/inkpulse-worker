using System;
using System.Threading;
using System.Threading.Tasks;
using InkPulse.Worker.Features.Book.Jobs;
using InkPulse.Worker.Features.Book.Options;
using InkPulse.Worker.Infrastructure.Services.Scheduling.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InkPulse.Worker.Features.Book.Services
{
    public class FlashSaleWarmUpService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly FlashSaleJobOptions _config;
        private readonly ILogger<FlashSaleWarmUpService> _logger;

        public FlashSaleWarmUpService(
            IServiceProvider serviceProvider,
            IOptions<FlashSaleJobOptions> configOptions,
            ILogger<FlashSaleWarmUpService> logger)
        {
            _serviceProvider = serviceProvider;
            _config = configOptions.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_config.Enabled)
            {
                _logger.LogInformation("[FlashSaleWarmUpService] FlashSaleWarmUpJob is disabled");
                return;
            }

            _logger.LogInformation(
                "[FlashSaleWarmUpService] Starting service. JobName={JobName}, CronExpression={CronExpression}",
                _config.JobName, _config.CronExpression);

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

            using var scope = _serviceProvider.CreateScope();
            var scheduleService = scope.ServiceProvider.GetRequiredService<IScheduleService<FlashSaleWarmUpJob>>();

            try
            {
                await scheduleService.ScheduleJobAsync(
                    _config.JobName,
                    _config.CronExpression,
                    null,
                    stoppingToken
                );

                _logger.LogInformation("[FlashSaleWarmUpService] FlashSaleWarmUpJob scheduled successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[FlashSaleWarmUpService] Error scheduling FlashSaleWarmUpJob");
            }
        }
    }
}
