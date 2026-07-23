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
    public class FlashSaleElsSyncService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly FlashSaleElsSyncOptions _config;
        private readonly ILogger<FlashSaleElsSyncService> _logger;

        public FlashSaleElsSyncService(
            IServiceProvider serviceProvider,
            IOptions<FlashSaleElsSyncOptions> configOptions,
            ILogger<FlashSaleElsSyncService> logger)
        {
            _serviceProvider = serviceProvider;
            _config = configOptions.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_config.Enabled)
            {
                _logger.LogInformation("[FlashSaleElsSyncService] FlashSaleElsSyncJob is disabled");
                return;
            }

            _logger.LogInformation(
                "[FlashSaleElsSyncService] Starting service. JobName={JobName}, CronExpression={CronExpression}",
                _config.JobName, _config.CronExpression);

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

            using var scope = _serviceProvider.CreateScope();
            var scheduleService = scope.ServiceProvider.GetRequiredService<IScheduleService<FlashSaleElsSyncJob>>();

            try
            {
                await scheduleService.ScheduleJobAsync(
                    _config.JobName,
                    _config.CronExpression,
                    null,
                    stoppingToken
                );

                _logger.LogInformation("[FlashSaleElsSyncService] FlashSaleElsSyncJob scheduled successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[FlashSaleElsSyncService] Error scheduling FlashSaleElsSyncJob");
            }
        }
    }
}
